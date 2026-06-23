using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PBS.ERP.Modules.Core.Services
{
    public interface IDatabaseService
    {
        Task EnsureDatabaseExistsAsync(string databaseName);

        string GetDefaultConnectionString();
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly string _defaultConnection;

        public DatabaseService(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _defaultConnection = configuration.GetConnectionString("DefaultConnection")?.Trim();

            if (string.IsNullOrWhiteSpace(_defaultConnection))
                throw new InvalidOperationException(
                    "DefaultConnection is missing or empty in appsettings.json");
        }

        public string GetDefaultConnectionString() => _defaultConnection;

        public async Task EnsureDatabaseExistsAsync(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));

            // Build connection to 'master' database
            var builder = new SqlConnectionStringBuilder(_defaultConnection)
            {
                InitialCatalog = "master"
            };

            await using var con = new SqlConnection(builder.ConnectionString);

            try
            {
                await con.OpenAsync();

                var sql = $@"
                IF DB_ID(N'{databaseName}') IS NULL
                BEGIN
                    CREATE DATABASE [{databaseName}]
                END";

                await con.ExecuteAsync(sql, new { db = databaseName });
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}