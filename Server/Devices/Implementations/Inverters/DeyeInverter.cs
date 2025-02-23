using HomeEnergy.Utils;
using HomeEnergyManager.ApiService.Devices.Interfaces;
using System.Text.Json;
using UnitsNet;
using PropertyChanged.SourceGenerator;
using HomeEnergy.Devices.Interfaces;
using HomeEnergy.Components.Devices.DeyeInverter;
using System.Diagnostics;

namespace HomeEnergy.Devices.Implementations.Inverters
{
    public class DeyeInverterFactory : IDeviceFactory
    {
        public string FactoryId => nameof(DeyeInverterFactory);

        public string Name => "Deye Inverter";

        public string Description => "";

        Type IDeviceFactory.SettingsRazorComponent => typeof(Settings);

        public IDevice CreateNewDevice(string name, string jsonSettings)
        {
            return new DeyeInverter(this, name, jsonSettings);
        }
    }

    public partial class DeyeInverter : IBatteryInverter, IMeteringDevice
    {
        private DeyeInverterSettings settings;
        private ModbusClient modbusClient;
        private byte slaveId = 1;

        private readonly Meter gridMeter = new Meter(1, "Grid", MeterType.Grid);
        private readonly Meter loadMeter = new Meter(2, "Load", MeterType.House);
        private readonly Meter microInverterMeter = new Meter(3, "MicroInverter", MeterType.Solar);
        private readonly Meter battery1Meter = new Meter(3, "Battery 1", MeterType.Battery);
        private readonly Meter battery2Meter = new Meter(4, "Battery 2", MeterType.Battery);

        public DeyeInverter(DeyeInverterFactory factory, string name, string settingsJson)
        {
            settings = JsonSerializer.Deserialize<DeyeInverterSettings>(settingsJson)!;
            modbusClient = new ModbusClient(settings.IpAddress, settings.Port);
            Name = name;
            SettingsJson = settingsJson;
            DeviceFactory = factory;
            AllMeters = [gridMeter, loadMeter, microInverterMeter, battery1Meter, battery2Meter];
            _batteries = [new(1, modbusClient, slaveId), new(2, modbusClient, slaveId)];
            Task.Run(UpdateValues);
        }

        private async void UpdateValues()
        {
            Stopwatch? totalValuesCaptureTimer = null;
            while (true)
            {
                try
                {
                    var batterySegment = await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.Battery_1_Temperature, DeyeRegisters.Battery_2_AH_Capacity - DeyeRegisters.Battery_1_Temperature + 1);

                    _batteries[0].StateOfChargePercentage = batterySegment[DeyeRegisters.Battery_1_SocPercentage - DeyeRegisters.Battery_1_Temperature] / 100.0;
                    _batteries[0].Voltage = ElectricPotential.FromVolts(batterySegment[DeyeRegisters.Battery_1_Voltage - DeyeRegisters.Battery_1_Temperature] / 10.0);
                    _batteries[0].Current = ElectricCurrent.FromAmperes((short)batterySegment[DeyeRegisters.Battery_1_Current - DeyeRegisters.Battery_1_Temperature] / 100.0);
                    _batteries[0].Power = Power.FromWatts((int)(_batteries[0].Voltage * _batteries[0].Current).Watts);
                    battery1Meter.CurrentPower = _batteries[0].Power;

                    _batteries[1].StateOfChargePercentage = batterySegment[DeyeRegisters.Battery_2_SocPercentage - DeyeRegisters.Battery_1_Temperature] / 100.0;
                    _batteries[1].Voltage = ElectricPotential.FromVolts(batterySegment[DeyeRegisters.Battery_2_Voltage - DeyeRegisters.Battery_1_Temperature] / 100.0);
                    _batteries[1].Current = ElectricCurrent.FromAmperes((short)batterySegment[DeyeRegisters.Battery_2_Current - DeyeRegisters.Battery_1_Temperature] / 100.0);
                    _batteries[1].Power = Power.FromWatts((int)(_batteries[1].Voltage * _batteries[1].Current).Watts);
                    battery2Meter.CurrentPower = _batteries[1].Power;

                    gridMeter.CurrentPower = Power.FromWatts((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPower))[0]);
                    microInverterMeter.CurrentPower = Power.FromWatts((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GenPortTotalPower))[0]);
                    loadMeter.CurrentPower = Power.FromWatts((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.TotalLoadPower))[0]);

                    if (totalValuesCaptureTimer == null || totalValuesCaptureTimer.Elapsed > TimeSpan.FromMinutes(5))
                    {
                        totalValuesCaptureTimer = Stopwatch.StartNew();
                        gridMeter.ImportEnergy = Energy.FromWattHours((await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPower))[0]);
                        gridMeter.ExportEnergy = Energy.FromWattHours((await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPower))[0]);

                        _batteries[0].DischargingLimit = ElectricCurrent.FromAmperes((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.Battery_1_DischargeCurrentLimit))[0]) * _batteries[0].Voltage;
                        _batteries[0].ChargingLimit = ElectricCurrent.FromAmperes((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.Battery_1_ChargeCurrentLimit))[0]) * _batteries[0].Voltage;
                        _batteries[1].DischargingLimit = ElectricCurrent.FromAmperes((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.Battery_2_DischargeCurrentLimit))[0]) * _batteries[1].Voltage;
                        _batteries[1].ChargingLimit = ElectricCurrent.FromAmperes((short)(await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.Battery_2_ChargeCurrentLimit))[0]) * _batteries[1].Voltage;
                    }
                }
                catch (Exception ex)
                {
                    //TODO: Log error
                    Console.WriteLine(ex.ToString());
                    await Task.Delay(15_000);
                }
                await Task.Delay(1_000);
            }
        }

        public async Task SetGridPeakShavingEnabled(bool enable)
        {
            var flags = (await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingControlFlags))[0];
            var peakShavingEnabled = (flags & 0b110000) == 0b110000;
            if (peakShavingEnabled && !enable)
            {
                await modbusClient.WriteRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingControlFlags, [(ushort)(flags & 0b1111111111101111)]);
            }
            else if (!peakShavingEnabled && enable)
            {
                await modbusClient.WriteRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingControlFlags, [(ushort)(flags | 0b110000)]);
            }
            GridPeakShavingEnabled = (await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingControlFlags))[0] == 0b110000;
        }

        public async Task SetGridPeakShavingPower(Power power)
        {
            await modbusClient.WriteRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingPower, [(ushort)(power.Watts / 10)]);
            GridPeakShavingPower = Power.FromWatts((await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridPeakShavingPower))[0] * 10);
        }

        public async Task SetGridExportLimit(Power power)
        {
            await modbusClient.WriteRegisters(slaveId, (ushort)DeyeRegisters.GridExportLimit, [(ushort)(power.Watts / 10)]);
            GridExportLimit = Power.FromWatts((await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.GridExportLimit))[0] * 10);
        }

        public IMeter[] AllMeters { get; }

        public int Id { get; set; }

        public string Name { get; set; }

        public string ImplementationType => typeof(DeyeInverter).FullName!;

        public string SettingsJson { get; set; }

        public IEnumerable<string> LogMessages => throw new NotImplementedException();

        public IDeviceFactory DeviceFactory { get; }

        private DeyeInverterBattery[] _batteries;

        public IInverterBattery[] GetBatteries()
        {
            return _batteries.Where(b => b.Voltage.Volts > 0).ToArray();
        }

        public async Task SetAllowChargingBatteriesFromGrid(bool allow)
        {
            await modbusClient.WriteRegisters(slaveId, (ushort)DeyeRegisters.BatteryChargeFromGridLimit, [allow ? (ushort)1000 : (ushort)0]);
            AllowChargingBatteriesFromGrid = (await modbusClient.ReadRegisters(slaveId, (ushort)DeyeRegisters.BatteryChargeFromGridLimit))[0] > 0;
        }

        [Notify(set: Setter.Private)]
        private bool allowChargingBatteriesFromGrid;
        [Notify(set: Setter.Private)]
        private bool gridPeakShavingEnabled;
        [Notify(set: Setter.Private)]
        private Power gridPeakShavingPower;
        [Notify(set: Setter.Private)]
        private Power gridExportLimit;

    }

    public class DeyeInverterSettings
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }

    enum DeyeRegisters
    {
        BatteryChargeLimit = 108,
        BatteryDischargeLimit = 109,
        BatteryChargeFromGridLimit = 128,
        GridExportLimit = 143,
        GridPeakShavingControlFlags = 178,
        GridPeakShavingPower = 191,
        Battery_1_ChargeCurrentLimit = 212,
        Battery_1_DischargeCurrentLimit = 213,
        Battery_2_ChargeCurrentLimit = 243,
        Battery_2_DischargeCurrentLimit = 244,
        Battery_1_Temperature = 586,
        Battery_1_Voltage = 587,
        Battery_1_SocPercentage = 588,
        Battery_2_SocPercentage = 589,
        Battery_1_Power = 590,
        Battery_1_Current = 591,
        Battery_1_AH_Capacity = 592,
        Battery_2_Voltage = 593,
        Battery_2_Current = 594,
        Battery_2_Power = 595,
        Battery_2_Temperature = 596,
        Battery_2_AH_Capacity = 597,// assumption
        GridPower = 619,
        TotalLoadPower = 653,
        GenPortTotalPower = 667
    }


    public partial class DeyeInverterBattery : IInverterBattery
    {
        private readonly int batteryId;
        private readonly ModbusClient modbusClient;
        private readonly byte slaveId;

        public DeyeInverterBattery(int batteryId, ModbusClient modbusClient, byte slaveId)
        {
            this.batteryId = batteryId;
            this.modbusClient = modbusClient;
            this.slaveId = slaveId;
        }

        public async Task SetDischargingLimit(Power power)
        {
            var addr = (ushort)batteryId == 1 ? (ushort)DeyeRegisters.Battery_1_DischargeCurrentLimit : (ushort)DeyeRegisters.Battery_2_DischargeCurrentLimit;
            var amps = power / Voltage;
            await modbusClient.WriteRegisters(slaveId, addr, [(ushort)amps.Amperes]);
            DischargingLimit = Voltage * ElectricCurrent.FromAmperes((await modbusClient.ReadRegisters(slaveId, addr))[0]);
        }

        public async Task SetChargingLimit(Power power)
        {
            var addr = (ushort)batteryId == 1 ? (ushort)DeyeRegisters.Battery_1_ChargeCurrentLimit : (ushort)DeyeRegisters.Battery_2_ChargeCurrentLimit;
            var amps = power / Voltage;
            await modbusClient.WriteRegisters(slaveId, addr, [(ushort)amps.Amperes]);
            ChargingLimit = Voltage * ElectricCurrent.FromAmperes((await modbusClient.ReadRegisters(slaveId, addr))[0]);
        }

        public Energy TotalEnergyCapacity => Energy.FromKilowattHours(25.5);
        [Notify]
        private double stateOfChargePercentage;
        [Notify]
        private Energy minimalAllowedStateOfCharge = Energy.FromKilowattHours(10);
        [Notify]
        private ElectricPotential voltage;
        [Notify]
        private ElectricCurrent current;
        [Notify]
        private Power power;
        [Notify]
        private Power dischargingLimit;
        [Notify]
        private Power chargingLimit;
    }
}
