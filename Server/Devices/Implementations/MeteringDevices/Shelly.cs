using HomeEnergy.Components.Devices.Shelly;
using HomeEnergy.Devices.Implementations.Inverters;
using HomeEnergy.Devices.Interfaces;
using HomeEnergyManager.ApiService.Devices.Interfaces;
using System.ComponentModel;
using System.Text.Json;
using UnitsNet;

namespace HomeEnergy.Devices.Implementations.MeteringDevices
{
    public class ShellyFactory : IDeviceFactory
    {
        public string FactoryId => nameof(ShellyFactory);

        public string Name => "Shelly";

        public string Description => "";

        Type IDeviceFactory.SettingsRazorComponent => typeof(Settings);

        public IDevice CreateNewDevice(string name, string jsonSettings)
        {
            return new Shelly(this, name, jsonSettings);
        }
    }

    public class ShellySettings
    {
        public string IpAddress { get; set; }
    }

    public class Shelly : IMeteringDevice
    {
        Meter meter;
        ShellySettings settings;
        public Shelly(ShellyFactory shellyFactory, string name, string jsonSettings)
        {
            DeviceFactory = shellyFactory;
            Name = name;
            SettingsJson = jsonSettings;
            meter = new Meter(1, "Shelly", MeterType.HVAC);
            AllMeters = [meter];
            FetchingLoop();
        }

        class ShellyResponse
        {
            public double total_power { get; set; }
        }

        private async void FetchingLoop()
        {
            var httpClient = new HttpClient();
            while (true)
            {
                try
                {
                    var response = await httpClient.GetFromJsonAsync<ShellyResponse>($"http://{settings.IpAddress}/status");
                    meter.CurrentPower = Power.FromWatts((int)response.total_power);
                }
                catch
                {
                    //TODO: Log error
                }
                await Task.Delay(1000);
            }
        }

        public IMeter[] AllMeters { get; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string SettingsJson
        {
            get
            {
                return JsonSerializer.Serialize(settings);
            }
            set
            {
                settings = JsonSerializer.Deserialize<ShellySettings>(value);
            }
        }
        public IDeviceFactory DeviceFactory { get; }
        public IEnumerable<string> LogMessages => throw new NotImplementedException();
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
