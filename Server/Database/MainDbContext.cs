using HomeEnergy.Devices.Implementations.Inverters;
using HomeEnergy.Devices.Implementations.MeteringDevices;
using Microsoft.EntityFrameworkCore;

namespace HomeEnergy.Database
{
    public class MainDbContext : DbContext
    {
        public DbSet<DbDevice> Devices { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            //optionsBuilder.UseSqlite($"Data Source={appData}/HomeEnergy/main.db");
            optionsBuilder
                .UseInMemoryDatabase("HomeEnergy")
                .UseSeeding((db, b) => {
                    db.Set<DbDevice>().AddRange([
                        new DbDevice { Id = 1, Name = "Deye main", DeviceFactoryId = nameof(DeyeInverterFactory), SettingsJson = """
                        {
                            "IpAddress": "192.168.88.2",
                            "Port": 1503
                        }
                        """ }
                    ]);
                    db.Set<DbDevice>().AddRange([
                       new DbDevice { Id = 2, Name = "Shelly", DeviceFactoryId = nameof(ShellyFactory), SettingsJson = """
                        {
                            "IpAddress": "192.168.88.85"
                        }
                        """ }
                   ]);
                    db.SaveChanges();
                });
        }
    }
}
