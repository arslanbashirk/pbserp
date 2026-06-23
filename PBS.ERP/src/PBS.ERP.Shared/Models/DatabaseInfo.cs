namespace PBS.ERP.Shared.Models
{
    public class DatabaseInfo
    {
        public string Name { get; set; }
        public string DatabaseType { get; set; }
        public string Server { get; set; }
        public string Port { get; set; }
        public string DatabaseName { get; set; }
        public string Schema { get; set; }
        public string ConnectionString { get; set; }
    }
}
