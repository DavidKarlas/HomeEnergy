using HomeEnergy.Database;
using UnitsNet;

namespace HomeEnergy.Services
{
    public class EnergyForecastService
    {
        private readonly SolarProductionForecast _solarProductionForecast;
        private readonly DayAheadPricesService _dayAheadPricesService;
        private readonly DevicesManager _devicesManager;

        public EnergyForecastService(SolarProductionForecast solarProductionForecast, DayAheadPricesService dayAheadPricesService, DevicesManager devicesManager)
        {
            _solarProductionForecast = solarProductionForecast;
            _dayAheadPricesService = dayAheadPricesService;
            _devicesManager = devicesManager;
        }

        public async Task<EnergySolverResult> GetEnergyForecastAsync()
        {
            var timeNow = DateTime.UtcNow;
            var beginningOfThisQuarter = new DateTime(timeNow.Year, timeNow.Month, timeNow.Day, timeNow.Hour, timeNow.Minute - (timeNow.Minute % 15), 0);
            var dayAheadPrices = _dayAheadPricesService.GetPrices()
                .Where(p => p.Key >= beginningOfThisQuarter)
                .OrderBy(p => p.Key)
                .ToArray();
            var times = dayAheadPrices.Select(d => d.Key).ToArray();
            var timesIndexLookup = times.Index().ToDictionary(t => t.Item, t => t.Index);
            var timeSlots = times.Length;

            var solarProduction = _solarProductionForecast.GetForecast();
            var batteries = _devicesManager.GetBatteries();
            var solverParameters = new SolverParameters {
                StartTime = beginningOfThisQuarter,
                TimePeriods = timeSlots,
                BatteryParams = batteries.Select(b => new BatteryParameters {
                    Capacity = b.TotalEnergyCapacity.KilowattHours,
                    ChargeEfficiency = 0.9,
                    DischargeEfficiency = 0.9,
                    MaxChargePower = 5,
                    MaxDischargePower = 5,
                    InitialSoC = b.StateOfChargeEnergy.KilowattHours,
                    MinimalCapacityAllowed = 2 //b.MinimalAllowedStateOfCharge.KilowattHours
                }).ToArray(),
                SpotPrices = dayAheadPrices.Select(d => d.Value / 1000).ToArray(),
                SolarProduction = times.Select(t => solarProduction[t].Kilowatts).ToArray(),
                EVParams = [new EVParameters() {
                    Capacity= 58,
                    HourWhenCarMustBeAtTargetSoC=times.Length-1,
                    MaxChargePower=11,
                    MinChargePower=4.2,
                    TargetSoC=58*0.8,
                    InitialSoC=58*0.8,
                    Name="ID.3"
                }],
                HeatPumpParams = new HeatPumpParameters() {
                    Power = 1,
                    RunHours = 4,
                    TimePeriodsWhenHeatpumpMustRun = timesIndexLookup.Where(t => t.Key.Hour == 13).Select(t => t.Value).ToArray()
                },
                HouseConsumption = times.Select(t => 0.5).ToArray(),
                MaxGridExport = times.Select(t => 13.0).ToArray(),
                MaxGridImport = times.Select(t => 4.6).ToArray(),
                NetworkExportFee = 0.012,
                NetworkImportFee = 0.035
            };

            var forecastResult = EnergySolver.Solve(solverParameters);

            return forecastResult;
        }
    }
}
