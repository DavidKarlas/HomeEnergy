using HomeEnergy.Database;
using HomeEnergy.Devices.Implementations;
using HomeEnergy.Devices.Interfaces;
using HomeEnergyManager.ApiService.Devices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HomeEnergy.Services
{
    public class DevicesManager
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Dictionary<string, IDeviceFactory> _deviceFactories;
        private readonly List<IDevice> _devices;

        public DevicesManager(IServiceScopeFactory scopeFactory, IEnumerable<IDeviceFactory> deviceFactories)
        {
            _scopeFactory = scopeFactory;
            _deviceFactories = deviceFactories.ToDictionary(d => d.FactoryId);
            using var scope = scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            dbContext.Database.EnsureCreated();
            _devices = dbContext.Devices.ToArray().Select(dbDevice => {
                try
                {
                    if (!_deviceFactories.TryGetValue(dbDevice.DeviceFactoryId, out var factory))
                    {
                        return new FailedToCreateDevice($"Device factory '{dbDevice.DeviceFactoryId}' not found.");
                    }
                    var device = factory.CreateNewDevice(dbDevice.Name, dbDevice.SettingsJson);
                    device.Id = dbDevice.Id;
                    return device;
                }
                catch (Exception ex)
                {
                    return new FailedToCreateDevice($"Failed to load '{dbDevice.DeviceFactoryId}'." + Environment.NewLine + ex.ToString());
                }
            }).ToList();
        }

        public IEnumerable<IInverterBattery> GetBatteries()
        {
            return _devices.OfType<IBatteryInverter>().SelectMany(bi =>bi.GetBatteries());
        }

        public async Task AddDevice(IDevice device)
        {
            var dbDevice = new DbDevice {
                Name = device.Name,
                DeviceFactoryId = device.DeviceFactory.FactoryId,
                SettingsJson = device.SettingsJson
            };
            using var scope = _scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            dbContext.Devices.Add(dbDevice);
            await dbContext.SaveChangesAsync();
            device.Id = dbDevice.Id;
            _devices.Add(device);
        }
        public async Task RemoveDevice(int deviceId)
        {
            using var scope = _scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var dbDevice = dbContext.Devices.Find(deviceId);
            if (dbDevice == null)
            {
                throw new ArgumentException("Device not found");
            }
            dbContext.Devices.Remove(dbDevice);
            await dbContext.SaveChangesAsync();
            _devices.RemoveAll(device => device.Id == deviceId);
        }

        public IEnumerable<IDevice> GetDevices()
        {
            return _devices;
        }

        public IReadOnlyDictionary<string, IDeviceFactory> GetAllDeviceFactories() => _deviceFactories;
    }
}