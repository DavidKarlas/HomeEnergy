using System.Text.Json;
using UnitsNet;

namespace HomeEnergy.Services
{
    public class SolarProductionForecast
    {
        private const string url = "https://api.solcast.com.au/rooftop_sites/333f-e1e5-d6d2-6f7f/forecasts?format=json";
        private readonly HttpClient _httpClient;
        private Dictionary<DateTime, Power> _cachedForecast;

        public SolarProductionForecast()
        {
            _httpClient = new();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "tet6dW3lZ68M44PKMjdqb_RXQOAaquA2");
            _ = UpdateForecastPeriodically();
        }

        public Dictionary<DateTime, Power> GetForecast()
        {
            return _cachedForecast;
        }

        private async Task UpdateForecastPeriodically()
        {
            while (true)
            {
                try
                {
                    string json = null;
                    //Todo change to 1 hour
                    if (File.Exists("forecast.json") && File.GetLastWriteTimeUtc("forecast.json").AddHours(2) > DateTime.UtcNow)
                    {
                        json = File.ReadAllText("forecast.json");
                    }
                    else
                    {
                        json = await _httpClient.GetStringAsync(url);
                        File.WriteAllText("forecast.json", json);
                    }
                    var result = JsonSerializer.Deserialize<ForecastResponse>(json);
                    var newDictionary = new Dictionary<DateTime, Power>();
                    Forecast? previous = null;
                    foreach (var forecast in result.forecasts)
                    {
                        if (forecast.period != "PT30M")
                            throw new NotImplementedException(forecast.period);
                        if (previous == null)
                            newDictionary.Add(forecast.period_end.AddMinutes(-30), Power.FromKilowatts(forecast.pv_estimate));
                        else
                            newDictionary.Add(forecast.period_end.AddMinutes(-30), Power.FromKilowatts((forecast.pv_estimate + previous.pv_estimate) / 2));
                        newDictionary.Add(forecast.period_end.AddMinutes(-15), Power.FromKilowatts(forecast.pv_estimate));
                        previous = forecast;
                    }
                    _cachedForecast = newDictionary;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating forecast: {ex.Message}");
                }
                await Task.Delay(TimeSpan.FromMinutes(60));
            }
        }

        public class ForecastResponse
        {
            public Forecast[] forecasts { get; set; }
        }

        public class Forecast
        {
            public float pv_estimate { get; set; }
            public float pv_estimate10 { get; set; }
            public float pv_estimate90 { get; set; }
            public DateTime period_end { get; set; }
            public string period { get; set; }
        }
    }
}
