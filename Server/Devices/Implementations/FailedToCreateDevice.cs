using HomeEnergy.Devices.Interfaces;
using HomeEnergyManager.ApiService.Devices.Interfaces;
using System.ComponentModel;

namespace HomeEnergy.Devices.Implementations
{
    public class FailedToCreateDevice : IDevice
    {
        public FailedToCreateDevice(string message)
        {
            LogMessages = [message];
        }
        public string Name { get; set; } = "Failed to create device";

        public IEnumerable<string> LogMessages { get; }
        public int Id { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string ImplementationType => throw new NotImplementedException();

        public string SettingsJson { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDeviceFactory DeviceFactory => throw new NotImplementedException();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
