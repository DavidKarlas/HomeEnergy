using Blazor.Diagrams.Core.Models;
using HomeEnergy.Devices.Interfaces;
using UnitsNet;

namespace HomeEnergy.Components.HomeDiagram.Views
{
    public class DeviceNodeModel : NodeModel
    {
        private IMeter[] meters;

        public IMeter[] Meters
        {
            get => meters;
            set
            {
                if (meters != null)
                {
                    foreach (var meter in meters)
                    {
                        meter.PropertyChanged -= Meter_PropertyChanged;
                    }
                }
                else
                {
                    meters = value;
                    foreach (var meter in value)
                    {
                        meter.PropertyChanged += Meter_PropertyChanged;
                    }
                }
            }
        }

        public string? Icon { get; set; }
        public Power TotalPower { get => Meters == null ? Power.Zero : Power.FromWatts(Meters.Sum(m => m.PreferredMeterType == MeterType.Battery ? -m.CurrentPower.Watts : m.CurrentPower.Watts)); }

        public DeviceNodeModel(Blazor.Diagrams.Core.Geometry.Point? position = null)
            : base(position: position)
        {
        }

        private void Meter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.Refresh();
        }
    }
}
