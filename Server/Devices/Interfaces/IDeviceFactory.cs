using HomeEnergyManager.ApiService.Devices.Interfaces;

namespace HomeEnergy.Devices.Interfaces
{
    public interface IDeviceFactory
    {
        string Name { get; }
        string Description { get; }
        string FactoryId { get; }
        Type SettingsRazorComponent { get; }
        IDevice CreateNewDevice(string name, string jsonSettings);
    }
}
