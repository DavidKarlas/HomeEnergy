using UnitsNet;

namespace HomeEnergy.Devices.Interfaces
{
    public interface IInverterBattery
    {
        Energy TotalEnergyCapacity { get; }
        Energy StateOfChargeEnergy { get => TotalEnergyCapacity * StateOfChargePercentage; }
        double StateOfChargePercentage { get; }
        Energy MinimalAllowedStateOfCharge { get; set; }
        ElectricPotential Voltage { get; }
        ElectricCurrent Current { get; }
        Power Power { get; }
        Power DischargingLimit { get; }
        Task SetDischargingLimit(Power power);
        Power ChargingLimit { get; }
        Task SetChargingLimit(Power power);
    }
}
