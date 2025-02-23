using Google.OrTools.LinearSolver;

internal static class EnergySolver
{
    public static EnergySolverResult? Solve(SolverParameters solverParams)
    {
        // Switched to CBC for mixed-integer programming support
        var solver = Solver.CreateSolver("CBC_MIXED_INTEGER_PROGRAMMING");
        var evSoc = new Variable[solverParams.EVParams.Length][];
        var evCharge = new Variable[solverParams.EVParams.Length][];
        var evChargingIndicator = new Variable[solverParams.EVParams.Length][];

        for (var i = 0; i < solverParams.EVParams.Length; i++)
        {
            evSoc[i] = new Variable[solverParams.TimePeriods];
            evCharge[i] = new Variable[solverParams.TimePeriods];
            evChargingIndicator[i] = new Variable[solverParams.TimePeriods];
        }

        // Decision variables
        var batteryCharge = new Variable[solverParams.BatteryParams.Length][];
        var batteryDischarge = new Variable[solverParams.BatteryParams.Length][];
        var batterySoc = new Variable[solverParams.BatteryParams.Length][];
        var batteryChargingIndicator = new Variable[solverParams.BatteryParams.Length][];
        var batteryDischargingIndicator = new Variable[solverParams.BatteryParams.Length][];
        for (var i = 0; i < solverParams.BatteryParams.Length; i++)
        {
            batteryCharge[i] = new Variable[solverParams.TimePeriods];
            batteryDischarge[i] = new Variable[solverParams.TimePeriods];
            batterySoc[i] = new Variable[solverParams.TimePeriods];
            batteryChargingIndicator[i] = new Variable[solverParams.TimePeriods];
            batteryDischargingIndicator[i] = new Variable[solverParams.TimePeriods];
        }
        var gridImport = new Variable[solverParams.TimePeriods];
        var gridExport = new Variable[solverParams.TimePeriods];
        var importExportIndicator = new Variable[solverParams.TimePeriods];
        var heatPumpUsage = new Variable[solverParams.TimePeriods];

        for (var t = 0; t < solverParams.TimePeriods; t++)
        {
            for (var i = 0; i < solverParams.BatteryParams.Length; i++)
            {
                batteryCharge[i][t] = solver.MakeNumVar(0, solverParams.BatteryParams[i].MaxChargePower, $"battery_charge_{i}_{t}");
                batteryDischarge[i][t] = solver.MakeNumVar(0, solverParams.BatteryParams[i].MaxDischargePower, $"battery_discharge_{i}_{t}");
                batterySoc[i][t] = solver.MakeNumVar(solverParams.BatteryParams[i].MinimalCapacityAllowed, solverParams.BatteryParams[i].Capacity, $"soc_{i}_{t}");
                batteryChargingIndicator[i][t] = solver.MakeIntVar(0, 1, $"batteryChargingIndicator_{i}_{t}");
                batteryDischargingIndicator[i][t] = solver.MakeIntVar(0, 1, $"batteryDischargingIndicator_{i}_{t}");
            }
            for (var i = 0; i < solverParams.EVParams.Length; i++)
            {
                evCharge[i][t] = solver.MakeNumVar(0, solverParams.EVParams[i].MaxChargePower, $"evCharge_{i}_{t}");
                evSoc[i][t] = solver.MakeNumVar(0, solverParams.EVParams[i].Capacity, $"evSoc_{i}_{t}");
                evChargingIndicator[i][t] = solver.MakeIntVar(0, 1, $"evChargingIndicator_{i}_{t}");

                // Enforce minimal charge when charging
                solver.Add(evCharge[i][t] >= 4.2 * evChargingIndicator[i][t]);
                solver.Add(evCharge[i][t] <= solverParams.EVParams[i].MaxChargePower * evChargingIndicator[i][t]);
            }
            heatPumpUsage[t] = solver.MakeBoolVar($"heatPumpUsage_{t}");
            gridImport[t] = solver.MakeNumVar(0, solverParams.MaxGridImport[t], $"gridImport_{t}");
            gridExport[t] = solver.MakeNumVar(0, solverParams.MaxGridExport[t], $"gridExport_{t}");
            importExportIndicator[t] = solver.MakeIntVar(0, 1, $"importExportIndicator_{t}");
        }

        // Ensure heat pump runs for at least X hours
        var heatPumpTotalUsage = new LinearExpr();
        for (var t = 0; t < solverParams.TimePeriods; t++)
        {
            heatPumpTotalUsage += heatPumpUsage[t];
        }
        solver.Add(heatPumpTotalUsage >= solverParams.HeatPumpParams.RunHours * 4);
        if (solverParams.HeatPumpParams.TimePeriodsWhenHeatpumpMustRun != null)
        {
            foreach (var period in solverParams.HeatPumpParams.TimePeriodsWhenHeatpumpMustRun)
            {
                solver.Add(heatPumpUsage[period] == 1);
            }
        }

        // Constraints
        for (var t = 0; t < solverParams.TimePeriods; t++)
        {
            for (var i = 0; i < solverParams.BatteryParams.Length; i++)
            {
                if (t == 0)
                {
                    solver.Add(batterySoc[i][t] == solverParams.BatteryParams[i].InitialSoC + ((batteryCharge[i][t] / 4) * solverParams.BatteryParams[i].ChargeEfficiency - (batteryDischarge[i][t] / 4) / solverParams.BatteryParams[i].DischargeEfficiency));
                }
                else
                {
                    solver.Add(batterySoc[i][t] == batterySoc[i][t - 1] + ((batteryCharge[i][t] / 4) * solverParams.BatteryParams[i].ChargeEfficiency - (batteryDischarge[i][t] / 4) / solverParams.BatteryParams[i].DischargeEfficiency));
                }
            }
            for (var i = 0; i < solverParams.EVParams.Length; i++)
            {
                if (t == 0)
                {
                    solver.Add(evSoc[i][t] == solverParams.EVParams[i].InitialSoC + (evCharge[i][t] / 4));
                }
                else
                {
                    solver.Add(evSoc[i][t] == evSoc[i][t - 1] + (evCharge[i][t] / 4));
                }
            }

            // Grid constraints
            var totalEvCharge = new LinearExpr();
            for (var i = 0; i < solverParams.EVParams.Length; i++)
            {
                totalEvCharge += evCharge[i][t];
            }
            var totalBatteryCharge = new LinearExpr();
            var totalBatteryDischarge = new LinearExpr();
            for (var i = 0; i < solverParams.BatteryParams.Length; i++)
            {
                totalBatteryCharge += batteryCharge[i][t];
                totalBatteryDischarge += batteryDischarge[i][t];
            }
            solver.Add(gridImport[t] + totalBatteryDischarge + solverParams.SolarProduction[t] == gridExport[t] + solverParams.HouseConsumption[t] + totalBatteryCharge + totalEvCharge + heatPumpUsage[t] * solverParams.HeatPumpParams.Power);
            solver.Add(gridImport[t] <= solverParams.MaxGridImport[t] * importExportIndicator[t]);
            solver.Add(gridExport[t] <= solverParams.MaxGridExport[t] * (1 - importExportIndicator[t]));
            for (var i = 0; i < solverParams.BatteryParams.Length; i++)
            {
                solver.Add(batteryDischargingIndicator[i][t] + batteryChargingIndicator[i][t] <= 1);
                solver.Add(batteryCharge[i][t] <= solverParams.BatteryParams[i].MaxChargePower * batteryChargingIndicator[i][t]);
                solver.Add(batteryDischarge[i][t] <= solverParams.BatteryParams[i].MaxDischargePower * (1 - batteryDischargingIndicator[i][t]));
            }
        }

        // Ensure each EV reaches target charge by specified hour
        for (var i = 0; i < solverParams.EVParams.Length; i++)
        {
            solver.Add(evSoc[i][solverParams.EVParams[i].HourWhenCarMustBeAtTargetSoC] >= solverParams.EVParams[i].TargetSoC);
        }

        // Objective: Maximize profit
        var objective = solver.Objective();
        for (var t = 0; t < solverParams.TimePeriods; t++)
        {
            objective.SetCoefficient(gridImport[t], -solverParams.SpotPrices[t] - solverParams.NetworkImportFee);
            objective.SetCoefficient(gridExport[t], solverParams.SpotPrices[t] - solverParams.NetworkExportFee);
        }
        objective.SetMaximization();
        solver.EnableOutput();
        // Solve the problem
        var resultStatus = solver.Solve();
        if (resultStatus == Solver.ResultStatus.OPTIMAL)
        {
            var result = new EnergySolverResult { Profit = objective.Value() };

            for (var t = 0; t < solverParams.TimePeriods; t++)
            {
                result.TimePeriods.Add(new TimePeriodResult {
                    Time = solverParams.StartTime.AddMinutes(t * 15).ToLocalTime(),
                    PredictedSolar = solverParams.SolarProduction[t],
                    DayAheadPrice = solverParams.SpotPrices[t],
                    GridExportPower = gridExport[t].SolutionValue(),
                    GridImportPower = gridImport[t].SolutionValue(),
                    HeatPumpUsagePower = heatPumpUsage[t].SolutionValue() * solverParams.HeatPumpParams.Power,
                    HouseConsumption = solverParams.HouseConsumption[t],
                    Batteries = Enumerable.Range(0, solverParams.BatteryParams.Length).Select(i => new BatteryResult {
                        ChargePower = batteryCharge[i][t].SolutionValue(),
                        DischargePower = batteryDischarge[i][t].SolutionValue(),
                        StateOfChargePercentage = batterySoc[i][t].SolutionValue() / solverParams.BatteryParams[i].Capacity,
                        StateOfChargeKWh = batterySoc[i][t].SolutionValue(),
                    }).ToList(),
                    ElectricVehicles = Enumerable.Range(0, solverParams.EVParams.Length).Select(i => new EvChargeResult {
                        Name = solverParams.EVParams[i].Name,
                        ChargePower = evCharge[i][t].SolutionValue(),
                        StateOfChargeKWh = evSoc[i][t].SolutionValue(),
                        StateOfChargePercentage = evSoc[i][t].SolutionValue() / solverParams.EVParams[i].Capacity
                    }).ToList()
                });
            }

            return result;
        }
        else
        {
            return null;
        }
    }
}
