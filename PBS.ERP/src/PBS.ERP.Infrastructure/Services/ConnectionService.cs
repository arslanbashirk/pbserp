using Microsoft.Extensions.Configuration;
using PBS.ERP.Shared.Models;
using System.Data.Common;

namespace PBS.ERP.Infrastructure.Services
{
    public interface IConnectionService
    {
        List<string> GetConnectionNames();
        List<DatabaseInfo> GetAllDatabases();
        DatabaseInfo ParseConnectionString(string name, string connectionString);
    }

    public class ConnectionService : IConnectionService
    {
        private readonly IConfiguration _configuration;

        public ConnectionService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<string> GetConnectionNames()
        {
            return _configuration
                .GetSection("ConnectionStrings")
                .GetChildren()
                .Select(x => x.Key)
                .ToList();
        }
        public List<DatabaseInfo> GetAllDatabases()
        {
            var result = new List<DatabaseInfo>();

            var connectionStrings = _configuration
                .GetSection("ConnectionStrings")
                .GetChildren();

            foreach (var conn in connectionStrings)
            {
                var info = ParseConnectionString(conn.Key, conn.Value);
                result.Add(info);
            }

            return result;
        }


        public DatabaseInfo ParseConnectionString(string name, string connectionString)
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            string server = null;
            string database = null;
            string port = null;
            string schema = null;
            string dbType = DetectDatabaseType(builder);

            if (builder.ContainsKey("Server"))
                server = builder["Server"]?.ToString();

            if (builder.ContainsKey("Data Source"))
                server = builder["Data Source"]?.ToString();

            if (builder.ContainsKey("Host"))
                server = builder["Host"]?.ToString();

            if (builder.ContainsKey("Port"))
                port = builder["Port"]?.ToString();

            if (builder.ContainsKey("Database"))
                database = builder["Database"]?.ToString();

            if (builder.ContainsKey("Initial Catalog"))
                database = builder["Initial Catalog"]?.ToString();

            if (builder.ContainsKey("Search Path"))
                schema = builder["Search Path"]?.ToString();
            else
                schema = builder.ContainsKey("Search Path")
                ? builder["Search Path"]?.ToString()
                : dbType == "PostgreSQL" ? "public" : "dbo";

            return new DatabaseInfo
            {
                Name = name,
                DatabaseType = dbType,
                Server = server,
                Port = port,
                DatabaseName = database,
                Schema = schema,
                ConnectionString = connectionString
            };
        }

        private string DetectDatabaseType(DbConnectionStringBuilder builder)
        {
            var keys = builder.Keys.Cast<string>().Select(k => k.ToLower());

            if (keys.Contains("initial catalog") || keys.Contains("data source"))
                return "SQL Server";

            if (keys.Contains("host"))
                return "PostgreSQL";

            if (keys.Contains("server") && keys.Contains("uid") && keys.Contains("port"))
                return "MySQL";

            if (keys.Contains("service name"))
                return "Oracle";

            return "Unknown";
        }
    }
}