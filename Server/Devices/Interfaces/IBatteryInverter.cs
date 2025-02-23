using HomeEnergy.Devices.Interfaces;
using UnitsNet;

namespace HomeEnergyManager.ApiService.Devices.Interfaces
{
    public interface IBatteryInverter : IDevice
    {
        IInverterBattery[] GetBatteries();

        bool AllowChargingBatteriesFromGrid { get; }
        Task SetAllowChargingBatteriesFromGrid(bool allow);

        bool GridPeakShavingEnabled { get;}
        Task SetGridPeakShavingEnabled(bool enabled);
        Power GridPeakShavingPower { get; }
        Task SetGridPeakShavingPower(Power power);

        Power GridExportLimit { get; }
        Task SetGridExportLimit(Power power);

    }
}
