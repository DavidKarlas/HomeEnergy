using HomeEnergyManager.ApiService.Devices.Interfaces;
using PropertyChanged.SourceGenerator;
using System.ComponentModel;
using UnitsNet;

namespace HomeEnergy.Devices.Interfaces
{
    public enum MeterType
    {
        Grid,
        Solar,
        EvCharger,
        HVAC,
        Other,
        House,
        Battery,
    }

    public interface IMeter : INotifyPropertyChanged
    {
        int Id { get; }
        string Name { get; }
        Power CurrentPower { get; }
        Energy ImportEnergy { get; }
        Energy ExportEnergy { get; }
        MeterType PreferredMeterType { get; }
    }

    public partial class Meter : IMeter
    {
        public int Id { get; }
        public string Name { get; }
        public MeterType PreferredMeterType { get; }
        [Notify]
        Power currentPower;
        [Notify]
        Energy importEnergy;
        [Notify]
        Energy exportEnergy;

        public Meter(int id, string name, MeterType preferredMeterType)
        {
            Id = id;
            Name = name;
            PreferredMeterType = preferredMeterType;
        }
    }

    public interface IMeteringDevice : IDevice
    {
        IMeter[] AllMeters { get; }
    }
}
