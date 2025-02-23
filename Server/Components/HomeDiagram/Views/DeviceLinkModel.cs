using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;
using PropertyChanged.SourceGenerator;
using System.ComponentModel;

namespace HomeEnergy.Components.HomeDiagram.Views
{
    public partial class DeviceLinkModel : LinkModel, INotifyPropertyChanged
    {
        public DeviceLinkModel(string id, Anchor source, Anchor target) : base(id, source, target)
        {
            var targetDevice = (DeviceNodeModel)target.Model;
            PropertyChanged += DeviceLinkModel_PropertyChanged;
            targetDevice.Changed += (model) => {
                var watts = targetDevice.TotalPower.Watts;
                if (watts == 0)
                {
                    CssClasses = "neutral-device-link";
                    Color = "gray";
                }
                else if (watts > 0)
                {
                    CssClasses = "positive-device-link";
                    Color = "green";
                }
                else
                {
                    CssClasses = "negative-device-link";
                    Color = "red";
                }
            };
        }

        private void DeviceLinkModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        [Notify]
        private string cssClasses;
    }
}
