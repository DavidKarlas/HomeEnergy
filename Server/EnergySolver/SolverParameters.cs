
internal class SolverParameters
{
    public int TimePeriods { get; set; } // Number of time periods (e.g., 24 hours)
    public double[] SpotPrices { get; set; } // Spot prices for each time period
    public double[] MaxGridImport { get; set; } // Maximum grid import for each time period
    public double[] MaxGridExport { get; set; } // Maximum grid export for each time period
    public double NetworkImportFee { get; set; } // Cost per kW when charging from grid
    public double NetworkExportFee { get; set; } // Cost per kW when discharging to grid
    public double[] SolarProduction { get; set; } // Solar production for each time period
    public double[] HouseConsumption { get; set; } // House consumption for each time period
    public BatteryParameters[] BatteryParams { get; set; } // Battery parameters
    public EVParameters[] EVParams { get; set; } // Electric vehicle parameters
    public HeatPumpParameters HeatPumpParams { get; set; } // Heat pump parameters
    public DateTime StartTime { get; internal set; }
}

internal class EVParameters
{
    public double Capacity { get; set; } // kWh
    public double InitialSoC { get; set; } // kWh
    public double TargetSoC { get; set; } // kWh by hour 13
    public int HourWhenCarMustBeAtTargetSoC { get; set; }
    public double MaxChargePower { get; set; } // kW
    public string Name { get; set; }
    public double MinChargePower { get; internal set; }
}

internal class HeatPumpParameters
{
    public double Power { get; set; } // kW
    public int RunHours { get; set; } // Must run at least this many hours per day
    public int[]? TimePeriodsWhenHeatpumpMustRun { get; set; }
}

internal class BatteryParameters
{
    public double MinimalCapacityAllowed { get; set; } // kWh
    public double Capacity { get; set; } // kWh
    public double MaxChargePower { get; set; } // kW
    public double MaxDischargePower { get; set; } // kW
    public double InitialSoC { get; set; } // kWh
    public double ChargeEfficiency { get; set; } // Charging efficiency
    public double DischargeEfficiency { get; set; } // Discharging efficiency
}
