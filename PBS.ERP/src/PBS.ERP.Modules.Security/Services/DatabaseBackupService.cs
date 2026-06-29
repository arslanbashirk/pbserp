using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PBS.ERP.Modules.Security.Services
{
    public class DatabaseBackupService
    {
        private readonly string _connectionString;
        private readonly string _backupFolder;

        public DatabaseBackupService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MasterDb")
                ?? throw new Exception("MasterDb connection string is missing.");

            _backupFolder = configuration["BackupSettings:BackupFolder"]
                ?? throw new Exception("BackupSettings:BackupFolder is missing.");
        }

        public async Task<List<string>> GetAllowedDatabasesAsync()
        {
            const string sql = @"
            SELECT DISTINCT [Database]
            FROM ERPCORE.dbo.Entity
            WHERE (IsDeleted = 0 OR IsDeleted IS NULL)
              AND [Database] IS NOT NULL
              AND LTRIM(RTRIM([Database])) <> ''
            ORDER BY [Database];";

            var databases = new List<string>();

            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            await using var cmd = new SqlCommand(sql, con);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                databases.Add(reader.GetString(0));
            }

            return databases;
        }

        private async Task<bool> IsAllowedDatabaseAsync(SqlConnection con, string databaseName)
        {
            const string sql = @"
            SELECT COUNT(1)
            FROM ERPCORE.dbo.Entity
            WHERE (IsDeleted = 0 OR IsDeleted IS NULL)
              AND [Database] = @DatabaseName;";

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@DatabaseName", databaseName);

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<string> BackupDatabaseAsync(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new Exception("Database name is required.");

            Directory.CreateDirectory(_backupFolder);

            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            if (!await IsAllowedDatabaseAsync(con, databaseName))
                throw new Exception("Selected database is not allowed for backup.");

            string safeDbName = databaseName.Replace("]", "]]");

            string fileName = $"{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            string backupPath = Path.Combine(_backupFolder, fileName);

            string sql = $@"
                BACKUP DATABASE [{safeDbName}]
                TO DISK = @BackupPath
                WITH INIT,
                     NAME = @BackupName,
                     STATS = 10;";

            await using var cmd = new SqlCommand(sql, con);
            cmd.CommandTimeout = 0;
            cmd.Parameters.AddWithValue("@BackupPath", backupPath);
            cmd.Parameters.AddWithValue("@BackupName", $"{databaseName} Full Backup");

            await cmd.ExecuteNonQueryAsync();

            return fileName;
        }

        public List<string> GetBackupFiles()
        {
            var folder = _backupFolder?.Trim();

            if (string.IsNullOrWhiteSpace(folder))
                throw new Exception("BackupFolder setting is empty.");

            if (!Directory.Exists(folder))
                throw new Exception($"Backup folder does not exist: {folder}");

            var files = Directory.GetFiles(folder, "*.bak", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .OrderByDescending(x => x)
                .ToList();

            return files;
        }

        public string GetBackupFullPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception("File name is required.");

            fileName = Path.GetFileName(fileName);

            string fullPath = Path.Combine(_backupFolder, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Backup file not found.");

            return fullPath;
        }
    }
}