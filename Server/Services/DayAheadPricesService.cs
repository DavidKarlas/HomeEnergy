using System.Text.Json;

namespace HomeEnergy.Services
{
    public class DayAheadPricesService
    {
        private readonly Entso_E_ApiClient _entso_E_ApiClient;
        private readonly ILogger<DayAheadPricesService> _logger;
        private Dictionary<DateTime, double> _prices;

        public DayAheadPricesService(Entso_E_ApiClient entso_E_ApiClient, ILogger<DayAheadPricesService> logger)
        {
            _entso_E_ApiClient = entso_E_ApiClient;
            _logger = logger;
            ScheduleDailyRefresh();
        }

        private void ScheduleDailyRefresh()
        {
            var now = DateTime.Now;
            var nextRunTime = new DateTime(now.Year, now.Month, now.Day, 13, 10, 0);
            if (now > nextRunTime)
            {
                nextRunTime = nextRunTime.AddDays(1);
            }
            var initialDelay = nextRunTime - now;
            Task.Delay(initialDelay).ContinueWith(async _ => {
                await RefreshCacheAsync();
                ScheduleDailyRefresh();
            });
            _ = RefreshCacheAsync();
        }

        private async Task RefreshCacheAsync()
        {
            try
            {
                await FetchDayAheadPricesFromApiAsync();
                _logger.LogInformation("Day ahead prices cache refreshed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing day ahead prices cache.");
            }
        }

        private async Task FetchDayAheadPricesFromApiAsync()
        {
            if(File.Exists("prices.json") && File.GetLastWriteTimeUtc("prices.json").AddHours(1) > DateTime.UtcNow)
            {
                var json = File.ReadAllText("prices.json");
                _prices = JsonSerializer.Deserialize<Dictionary<DateTime, double>>(json);
                return;
            }

            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(2);
            var countryCode = "10YSI-ELES-----O";

            _prices = await _entso_E_ApiClient.QueryDayAheadPricesAsync(countryCode, start, end);
            File.WriteAllText("prices.json", JsonSerializer.Serialize(_prices));
        }
        public Dictionary<DateTime, double> GetPrices()
        {
            return _prices;
        }
    }
}
