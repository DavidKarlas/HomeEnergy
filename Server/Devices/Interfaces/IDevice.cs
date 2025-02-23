using HomeEnergy.Devices.Interfaces;
using System.ComponentModel;

namespace HomeEnergyManager.ApiService.Devices.Interfaces
{
    public interface IDevice : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SettingsJson { get; set; }
        public IDeviceFactory DeviceFactory { get; }
        public IEnumerable<string> LogMessages { get; }
    }
}
