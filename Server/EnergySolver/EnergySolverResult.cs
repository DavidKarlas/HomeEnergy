public class EnergySolverResult
{
    public List<TimePeriodResult> TimePeriods { get; } = new();
    public double Profit { get; set; }
}

public class TimePeriodResult
{
    public DateTime Time { get; set; }
    public double GridImportPower { get; set; }
    public double GridExportPower { get; set; }
    public double HeatPumpUsagePower { get; set; }
    public double HouseConsumption { get; set; }
    public List<BatteryResult> Batteries { get; set; }
    public List<EvChargeResult> ElectricVehicles { get; set; }
    public double PredictedSolar { get; internal set; }
    public double DayAheadPrice { get; set; }
}

public class BatteryResult
{
    public double ChargePower { get; set; }
    public double DischargePower { get; set; }
    public double StateOfChargeKWh { get; set; }
    public double StateOfChargePercentage { get; set; }
}

public class EvChargeResult
{
    public string Name { get; set; }
    public double ChargePower { get; set; }
    public double StateOfChargeKWh { get; set; }
    public double StateOfChargePercentage { get; set; }
}
