using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

public class Entso_E_ApiClient
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient = new();
    private readonly ILogger<Entso_E_ApiClient> _logger;
    private const string Url = "https://web-api.tp.entsoe.eu/api";
    private const string DateTimeFormat = "yyyyMMddHH00";

    public Entso_E_ApiClient(ILogger<Entso_E_ApiClient> logger)
    {
        _apiKey = "f59ac7f4-baeb-493b-ab30-f8b9fa8cdf37";
        _logger = logger;
    }

    private async Task<HttpResponseMessage> BaseRequestAsync(Dictionary<string, string> parameters, DateTime start, DateTime end)
    {
        var baseParams = new Dictionary<string, string>
        {
            { "securityToken", _apiKey },
            { "periodStart", start.ToLocalTime().ToString(DateTimeFormat) },
            { "periodEnd", end.ToLocalTime().ToString(DateTimeFormat) }
        };

        foreach (var param in baseParams)
        {
            parameters[param.Key] = param.Value;
        }

        _logger.LogDebug($"Performing request to {Url} with params {string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");
        var response = await _httpClient.GetAsync($"{Url}?{string.Join("&", parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");
        response.EnsureSuccessStatusCode();

        return response;
    }

    private static XElement RemoveAllNamespaces(XElement xmlDocument)
    {
        if (!xmlDocument.HasElements)
        {
            XElement xElement = new XElement(xmlDocument.Name.LocalName);
            xElement.Value = xmlDocument.Value;

            foreach (XAttribute attribute in xmlDocument.Attributes())
                xElement.Add(attribute);

            return xElement;
        }
        return new XElement(xmlDocument.Name.LocalName, xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
    }

    public async Task<Dictionary<DateTime, double>> QueryDayAheadPricesAsync(string countryCode, DateTime start, DateTime end)
    {
        var area = Area.FromCode(countryCode);
        var parameters = new Dictionary<string, string>
        {
            { "documentType", "A44" },
            { "in_Domain", area.Code },
            { "out_Domain", area.Code }
        };

        var response = await BaseRequestAsync(parameters, start, end);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                var series = ParsePriceDocument(content);
                return series.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse response content: {await response.Content.ReadAsStringAsync()}");
                throw;
            }
        }
        else
        {
            _logger.LogError($"Failed to retrieve data: {response.StatusCode}");
            return null;
        }
    }

    private Dictionary<DateTime, double> ParsePriceDocument(string document)
    {
        var root = RemoveAllNamespaces(XDocument.Parse(document).Root);
        var series = new Dictionary<DateTime, double>();

        foreach (var timeseries in root.Descendants("TimeSeries"))
        {
            foreach (var period in timeseries.Descendants("Period"))
            {
                var resolution = period.Element("resolution").Value;

                if (resolution == "PT60M" || resolution == "PT1H")
                {
                    resolution = "PT60M";
                }
                else if (resolution != "PT15M")
                {
                    continue;
                }

                var responseStart = period.Element("timeInterval").Element("start").Value;
                var startTime = DateTime.ParseExact(responseStart, "yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime();
                startTime = startTime.AddMinutes(-startTime.Minute); // ensure we start from the whole hour

                var responseEnd = period.Element("timeInterval").Element("end").Value;
                var endTime = DateTime.ParseExact(responseEnd, "yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime();

                _logger.LogDebug($"Period found is from {startTime} till {endTime} with resolution {resolution}");

                if (series.ContainsKey(startTime))
                {
                    _logger.LogDebug("We found a duplicate period in the response, possibly with another resolution. We skip this period");
                    continue;
                }

                if (resolution == "PT60M")
                {
                    series = series.Concat(ProcessPT60MPoints(period, startTime)).ToDictionary(kv => kv.Key, kv => kv.Value);
                }
                else
                {
                    series = series.Concat(ProcessPT15MPoints(period, startTime)).ToDictionary(kv => kv.Key, kv => kv.Value);
                }

                var currentTime = startTime;
                var lastPrice = series[currentTime];

                while (currentTime < endTime)
                {
                    if (series.ContainsKey(currentTime))
                    {
                        lastPrice = series[currentTime];
                    }
                    else
                    {
                        _logger.LogDebug($"Extending the price {lastPrice} of the previous hour to {currentTime}");
                        series[currentTime] = lastPrice;
                    }

                    currentTime = currentTime.AddMinutes(15);
                }
            }
        }

        return series;
    }

    private Dictionary<DateTime, double> ProcessPT60MPoints(XElement period, DateTime startTime)
    {
        var data = new Dictionary<DateTime, double>();

        foreach (var point in period.Descendants("Point"))
        {
            var position = int.Parse(point.Element("position").Value);
            var price = double.Parse(point.Element("price.amount").Value, CultureInfo.InvariantCulture);
            var time = startTime.AddHours(position - 1);
            data[time] = price;
            data[time.AddMinutes(15)] = price;
            data[time.AddMinutes(30)] = price;
            data[time.AddMinutes(45)] = price;
        }

        return data;
    }

    private Dictionary<DateTime, double> ProcessPT15MPoints(XElement period, DateTime startTime)
    {
        var positions = new Dictionary<int, double>();

        foreach (var point in period.Descendants("Point"))
        {
            var position = int.Parse(point.Element("position").Value);
            var price = double.Parse(point.Element("price.amount").Value, CultureInfo.InvariantCulture);
            positions[position] = price;
        }

        var data = new Dictionary<DateTime, double>();
        var lastHour = (positions.Keys.Max() + 3) / 4;
        var lastPrice = 0.0;

        for (var hour = 0; hour < lastHour; hour++)
        {
            var sumPrices = 0.0;
            for (var idx = hour * 4 + 1; idx <= hour * 4 + 4; idx++)
            {
                lastPrice = positions.ContainsKey(idx) ? positions[idx] : lastPrice;
                sumPrices += lastPrice;
            }

            var time = startTime.AddHours(hour);
            data[time] = Math.Round(sumPrices / 4, 2);
        }

        return data;
    }
}

public class Area
{
    public string Code { get; }
    public string Meaning { get; }
    public string TimeZone { get; }

    private Area(string code, string meaning, string timeZone)
    {
        Code = code;
        Meaning = meaning;
        TimeZone = timeZone;
    }

    public static Area FromCode(string code)
    {
        if (AreasDictionary.TryGetValue(code, out var area))
        {
            return area;
        }
        throw new ArgumentException($"Invalid area code: {code}");
    }

    static Area()
    {
        AreasDictionary = Areas.ToDictionary(a => a.Code);
    }

    public static readonly Dictionary<string, Area> AreasDictionary;

    public static readonly List<Area> Areas = new List<Area> {
        new Area("10YDE-VE-------2", "50Hertz CA, DE(50HzT) BZA", "Europe/Berlin"),
        new Area("10YAL-KESH-----5", "Albania, OST BZ / CA / MBA", "Europe/Tirane"),
        new Area("10YDE-RWENET---I", "Amprion CA", "Europe/Berlin"),
        new Area("10YAT-APG------L", "Austria, APG BZ / CA / MBA", "Europe/Vienna"),
        new Area("10Y1001A1001A51S", "Belarus BZ / CA / MBA", "Europe/Minsk"),
        new Area("10YBE----------2", "Belgium, Elia BZ / CA / MBA", "Europe/Brussels"),
        new Area("10YBA-JPCC-----D", "Bosnia Herzegovina, NOS BiH BZ / CA / MBA", "Europe/Sarajevo"),
        new Area("10YCA-BULGARIA-R", "Bulgaria, ESO BZ / CA / MBA", "Europe/Sofia"),
        new Area("10YDOM-CZ-DE-SKK", "BZ CZ+DE+SK BZ / BZA", "Europe/Prague"),
        new Area("10YHR-HEP------M", "Croatia, HOPS BZ / CA / MBA", "Europe/Zagreb"),
        new Area("10YDOM-REGION-1V", "CWE Region", "Europe/Brussels"),
        new Area("10YCY-1001A0003J", "Cyprus, Cyprus TSO BZ / CA / MBA", "Asia/Nicosia"),
        new Area("10YCZ-CEPS-----N", "Czech Republic, CEPS BZ / CA/ MBA", "Europe/Prague"),
        new Area("10Y1001A1001A63L", "DE-AT-LU BZ", "Europe/Berlin"),
        new Area("10Y1001A1001A82H", "DE-LU BZ / MBA", "Europe/Berlin"),
        new Area("10Y1001A1001A65H", "Denmark", "Europe/Copenhagen"),
        new Area("10YDK-1--------W", "DK1 BZ / MBA", "Europe/Copenhagen"),
        new Area("46Y000000000007M", "DK1 NO1 BZ", "Europe/Copenhagen"),
        new Area("10YDK-2--------M", "DK2 BZ / MBA", "Europe/Copenhagen"),
        new Area("10Y1001A1001A796", "Denmark, Energinet CA", "Europe/Copenhagen"),
        new Area("10Y1001A1001A39I", "Estonia, Elering BZ / CA / MBA", "Europe/Tallinn"),
        new Area("10YFI-1--------U", "Finland, Fingrid BZ / CA / MBA", "Europe/Helsinki"),
        new Area("10YMK-MEPSO----8", "Former Yugoslav Republic of Macedonia, MEPSO BZ / CA / MBA", "Europe/Skopje"),
        new Area("10YFR-RTE------C", "France, RTE BZ / CA / MBA", "Europe/Paris"),
        new Area("10Y1001A1001A83F", "Germany", "Europe/Berlin"),
        new Area("10YGR-HTSO-----Y", "Greece, IPTO BZ / CA/ MBA", "Europe/Athens"),
        new Area("10YHU-MAVIR----U", "Hungary, MAVIR CA / BZ / MBA", "Europe/Budapest"),
        new Area("IS", "Iceland", "Atlantic/Reykjavik"),
        new Area("10Y1001A1001A59C", "Ireland (SEM) BZ / MBA", "Europe/Dublin"),
        new Area("10YIE-1001A00010", "Ireland, EirGrid CA", "Europe/Dublin"),
        new Area("10YIT-GRTN-----B", "Italy, IT CA / MBA", "Europe/Rome"),
        new Area("10Y1001A1001A885", "Italy_Saco_AC", "Europe/Rome"),
        new Area("10Y1001C--00096J", "IT-Calabria BZ", "Europe/Rome"),
        new Area("10Y1001A1001A893", "Italy_Saco_DC", "Europe/Rome"),
        new Area("10Y1001A1001A699", "IT-Brindisi BZ", "Europe/Rome"),
        new Area("10Y1001A1001A70O", "IT-Centre-North BZ", "Europe/Rome"),
        new Area("10Y1001A1001A71M", "IT-Centre-South BZ", "Europe/Rome"),
        new Area("10Y1001A1001A72K", "IT-Foggia BZ", "Europe/Rome"),
        new Area("10Y1001A1001A66F", "IT-GR BZ", "Europe/Rome"),
        new Area("10Y1001A1001A84D", "IT-MACROZONE NORTH MBA", "Europe/Rome"),
        new Area("10Y1001A1001A85B", "IT-MACROZONE SOUTH MBA", "Europe/Rome"),
        new Area("10Y1001A1001A877", "IT-Malta BZ", "Europe/Rome"),
        new Area("10Y1001A1001A73I", "IT-North BZ", "Europe/Rome"),
        new Area("10Y1001A1001A80L", "IT-North-AT BZ", "Europe/Rome"),
        new Area("10Y1001A1001A68B", "IT-North-CH BZ", "Europe/Rome"),
        new Area("10Y1001A1001A81J", "IT-North-FR BZ", "Europe/Rome"),
        new Area("10Y1001A1001A67D", "IT-North-SI BZ", "Europe/Rome"),
        new Area("10Y1001A1001A76C", "IT-Priolo BZ", "Europe/Rome"),
        new Area("10Y1001A1001A77A", "IT-Rossano BZ", "Europe/Rome"),
        new Area("10Y1001A1001A74G", "IT-Sardinia BZ", "Europe/Rome"),
        new Area("10Y1001A1001A75E", "IT-Sicily BZ", "Europe/Rome"),
        new Area("10Y1001A1001A788", "IT-South BZ", "Europe/Rome"),
        new Area("10Y1001A1001A50U", "Kaliningrad BZ / CA / MBA", "Europe/Kaliningrad"),
        new Area("10YLV-1001A00074", "Latvia, AST BZ / CA / MBA", "Europe/Riga"),
        new Area("10YLT-1001A0008Q", "Lithuania, Litgrid BZ / CA / MBA", "Europe/Vilnius"),
        new Area("10YLU-CEGEDEL-NQ", "Luxembourg, CREOS CA", "Europe/Luxembourg"),
        new Area("10Y1001A1001A93C", "Malta, Malta BZ / CA / MBA", "Europe/Malta"),
        new Area("10YCS-CG-TSO---S", "Montenegro, CGES BZ / CA / MBA", "Europe/Podgorica"),
        new Area("10YGB----------A", "National Grid BZ / CA/ MBA", "Europe/London"),
        new Area("10Y1001A1001B012", "Georgia", "Asia/Tbilisi"),
        new Area("10Y1001C--00098F", "GB(IFA) BZN", "Europe/London"),
        new Area("17Y0000009369493", "GB(IFA2) BZ", "Europe/London"),
        new Area("11Y0-0000-0265-K", "GB(ElecLink) BZN", "Europe/London"),
        new Area("10Y1001A1001A92E", "United Kingdom", "Europe/London"),
        new Area("10YNL----------L", "Netherlands, TenneT NL BZ / CA/ MBA", "Europe/Amsterdam"),
        new Area("10YNO-1--------2", "NO1 BZ / MBA", "Europe/Oslo"),
        new Area("10Y1001A1001A64J", "NO1 A BZ", "Europe/Oslo"),
        new Area("10YNO-2--------T", "NO2 BZ / MBA", "Europe/Oslo"),
        new Area("50Y0JVU59B4JWQCU", "NO2 NSL BZ / MBA", "Europe/Oslo"),
        new Area("10Y1001C--001219", "NO2 A BZ", "Europe/Oslo"),
        new Area("10YNO-3--------J", "NO3 BZ / MBA", "Europe/Oslo"),
        new Area("10YNO-4--------9", "NO4 BZ / MBA", "Europe/Oslo"),
        new Area("10Y1001A1001A48H", "NO5 BZ / MBA", "Europe/Oslo"),
        new Area("10YNO-0--------C", "Norway, Norway MBA, Stattnet CA", "Europe/Oslo"),
        new Area("10YDOM-1001A082L", "PL-CZ BZA / CA", "Europe/Warsaw"),
        new Area("10YPL-AREA-----S", "Poland, PSE SA BZ / BZA / CA / MBA", "Europe/Warsaw"),
        new Area("10YPT-REN------W", "Portugal, REN BZ / CA / MBA", "Europe/Lisbon"),
        new Area("10Y1001A1001A990", "Republic of Moldova, Moldelectica BZ/CA/MBA", "Europe/Chisinau"),
        new Area("10YRO-TEL------P", "Romania, Transelectrica BZ / CA/ MBA", "Europe/Bucharest"),
        new Area("10Y1001A1001A49F", "Russia BZ / CA / MBA", "Europe/Moscow"),
        new Area("10Y1001A1001A44P", "SE1 BZ / MBA", "Europe/Stockholm"),
        new Area("10Y1001A1001A45N", "SE2 BZ / MBA", "Europe/Stockholm"),
        new Area("10Y1001A1001A46L", "SE3 BZ / MBA", "Europe/Stockholm"),
        new Area("10Y1001A1001A47J", "SE4 BZ / MBA", "Europe/Stockholm"),
        new Area("10YCS-SERBIATSOV", "Serbia, EMS BZ / CA / MBA", "Europe/Belgrade"),
        new Area("10YSK-SEPS-----K", "Slovakia, SEPS BZ / CA / MBA", "Europe/Bratislava"),
        new Area("10YSI-ELES-----O", "Slovenia, ELES BZ / CA / MBA", "Europe/Ljubljana"),
        new Area("10Y1001A1001A016", "Northern Ireland, SONI CA", "Europe/Belfast"),
        new Area("10YES-REE------0", "Spain, REE BZ / CA / MBA", "Europe/Madrid"),
        new Area("10YSE-1--------K", "Sweden, Sweden MBA, SvK CA", "Europe/Stockholm"),
        new Area("10YCH-SWISSGRIDZ", "Switzerland, Swissgrid BZ / CA / MBA", "Europe/Zurich"),
        new Area("10YDE-EON------1", "TenneT GER CA", "Europe/Berlin"),
        new Area("10YDE-ENBW-----N", "TransnetBW CA", "Europe/Berlin"),
        new Area("10YTR-TEIAS----W", "Turkey BZ / CA / MBA", "Europe/Istanbul"),
        new Area("10Y1001C--00003F", "Ukraine, Ukraine BZ, MBA", "Europe/Kiev"),
        new Area("10Y1001A1001A869", "Ukraine-DobTPP CTA", "Europe/Kiev"),
        new Area("10YUA-WEPS-----0", "Ukraine BEI CTA", "Europe/Kiev"),
        new Area("10Y1001C--000182", "Ukraine IPS CTA", "Europe/Kiev"),
        new Area("10Y1001C--00100H", "Kosovo/ XK CA / XK BZN", "Europe/Rome"),
        new Area("10Y1001C--00002H", "Amprion LU CA", "Europe/Berlin")
    };
}