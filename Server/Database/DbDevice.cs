namespace HomeEnergy.Database
{
    public class DbDevice
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string DeviceFactoryId { get; set; }

        public string SettingsJson { get; set; }
    }
}
