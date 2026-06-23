using Dapper;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace PBS.ERP.Infrastructure.Services
{
    public sealed class DbService : IDbInterface
    {
        private readonly ApplicationDbContext _context;

        public DbService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> CreateDatabaseAsync(
            string? databaseName,
            string user)
        {
            databaseName = databaseName?.Trim();

            if (string.IsNullOrWhiteSpace(databaseName))
                return Fail("Database name is required.");

            if (!IsValidSqlIdentifier(databaseName))
                return Fail("Invalid database name. Use letters, numbers and underscore only.");

            if (IsProtectedDatabase(databaseName))
                return Fail($"Database '{databaseName}' is protected.");

            var conn = await GetOpenConnectionAsync();

            try
            {
                if (await DatabaseExistsAsync(conn, databaseName))
                    return Fail($"Database '{databaseName}' already exists.");

                var sql = $@"
                CREATE DATABASE {QuoteSqlName(databaseName)};";

                await conn.ExecuteAsync(sql);

                return Success($"Database '{databaseName}' created successfully.", databaseName);
            }
            catch (Exception ex)
            {
                return Fail("Error creating database: " + GetErrorMessage(ex));
            }
        }

        public async Task<ServiceResult> RenameDatabaseAsync(
            string? oldDatabaseName,
            string? newDatabaseName,
            string user)
        {
            oldDatabaseName = oldDatabaseName?.Trim();
            newDatabaseName = newDatabaseName?.Trim();

            if (string.IsNullOrWhiteSpace(oldDatabaseName))
                return Fail("Old database name is required.");

            if (string.IsNullOrWhiteSpace(newDatabaseName))
                return Fail("New database name is required.");

            if (!IsValidSqlIdentifier(oldDatabaseName) ||
                !IsValidSqlIdentifier(newDatabaseName))
                return Fail("Invalid database name. Use letters, numbers and underscore only.");

            if (IsProtectedDatabase(oldDatabaseName) || IsProtectedDatabase(newDatabaseName))
                return Fail("Protected database cannot be renamed.");

            if (string.Equals(oldDatabaseName, newDatabaseName, StringComparison.OrdinalIgnoreCase))
                return Success("Database name was not changed.", oldDatabaseName);

            var conn = await GetOpenConnectionAsync();

            try
            {
                if (!await DatabaseExistsAsync(conn, oldDatabaseName))
                    return Fail($"Existing database '{oldDatabaseName}' does not exist.");

                if (await DatabaseExistsAsync(conn, newDatabaseName))
                    return Fail($"New database '{newDatabaseName}' already exists.");

                var renameSql = $@"
                ALTER DATABASE {QuoteSqlName(oldDatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                ALTER DATABASE {QuoteSqlName(oldDatabaseName)} MODIFY NAME = {QuoteSqlName(newDatabaseName)};
                ALTER DATABASE {QuoteSqlName(newDatabaseName)} SET MULTI_USER;";

                await conn.ExecuteAsync(renameSql);

                await UpdateEntityDatabaseNameAsync(
                    conn,
                    oldDatabaseName,
                    newDatabaseName,
                    user);

                return Success(
                    $"Database renamed from '{oldDatabaseName}' to '{newDatabaseName}'.",
                    newDatabaseName);
            }
            catch (Exception ex)
            {
                await TryRestoreDatabaseNameAsync(
                    conn,
                    oldDatabaseName,
                    newDatabaseName);

                return Fail("Error renaming database: " + GetErrorMessage(ex));
            }
        }

        public async Task<ServiceResult> DropDatabaseAsync(
            string? databaseName,
            string user)
        {
            databaseName = databaseName?.Trim();

            if (string.IsNullOrWhiteSpace(databaseName))
                return Fail("Database name is required.");

            if (!IsValidSqlIdentifier(databaseName))
                return Fail("Invalid database name.");

            if (IsProtectedDatabase(databaseName))
                return Fail($"Database '{databaseName}' is protected and cannot be dropped.");

            var conn = await GetOpenConnectionAsync();

            try
            {
                if (!await DatabaseExistsAsync(conn, databaseName))
                    return Success($"Database '{databaseName}' does not exist. No action required.", databaseName);

                var sql = $@"
ALTER DATABASE {QuoteSqlName(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE {QuoteSqlName(databaseName)};";

                await conn.ExecuteAsync(sql);

                return Success($"Database '{databaseName}' dropped successfully.", databaseName);
            }
            catch (Exception ex)
            {
                return Fail("Error dropping database: " + GetErrorMessage(ex));
            }
        }

        public async Task<string?> GetSurveyDatabaseNameAsync(string surveyUid)
        {
            if (string.IsNullOrWhiteSpace(surveyUid))
                return null;

            var conn = await GetOpenConnectionAsync();

            return await conn.ExecuteScalarAsync<string?>(
                @"
                SELECT DatabaseName
                FROM [ERPCORE].[dbo].[Survey]
                WHERE UID = @UID
                AND ISNULL(IsDeleted, 0) = 0;",
                new
                {
                    UID = surveyUid
                });
        }

        public async Task<string?> GetSurveyTableNameAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return null;

            var conn = await GetOpenConnectionAsync();

            return await conn.ExecuteScalarAsync<string?>(
                @"
                SELECT TableName
                FROM [ERPCORE].[dbo].[Form]
                WHERE UID = @UID
                AND ISNULL(IsDeleted, 0) = 0;",
                new
                {
                    UID = uid
                });
        }

        private async Task UpdateEntityDatabaseNameAsync(
            DbConnection conn,
            string oldDatabaseName,
            string newDatabaseName,
            string user)
        {
            await conn.ExecuteAsync(
                $@"
                UPDATE {Constants.EntityTable}
                SET [Database] = @NewDatabase,
                    ModifiedBy = @User,
                    ModifiedTime = @Now
                WHERE [Database] = @OldDatabase
                AND ISNULL(IsDeleted, 0) = 0;",
                new
                {
                    OldDatabase = oldDatabaseName,
                    NewDatabase = newDatabaseName,
                    User = NormalizeUser(user),
                    Now = DateTime.Now
                });
        }

        private async Task TryRestoreDatabaseNameAsync(
            DbConnection conn,
            string oldDatabaseName,
            string newDatabaseName)
        {
            try
            {
                var newExists = await DatabaseExistsAsync(conn, newDatabaseName);
                var oldExists = await DatabaseExistsAsync(conn, oldDatabaseName);

                if (newExists && !oldExists)
                {
                    var rollbackSql = $@"
                    ALTER DATABASE {QuoteSqlName(newDatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    ALTER DATABASE {QuoteSqlName(newDatabaseName)} MODIFY NAME = {QuoteSqlName(oldDatabaseName)};
                    ALTER DATABASE {QuoteSqlName(oldDatabaseName)} SET MULTI_USER;";

                    await conn.ExecuteAsync(rollbackSql);
                }
            }
            catch
            {
                // Ignore rollback failure. Original error will be returned.
            }
        }

        private async Task<bool> DatabaseExistsAsync(
            DbConnection conn,
            string databaseName)
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN DB_ID(@DatabaseName) IS NULL THEN 0 ELSE 1 END;",
                new
                {
                    DatabaseName = databaseName
                });

            return exists == 1;
        }

        private async Task<DbConnection> GetOpenConnectionAsync()
        {
            var con = _context.Database.GetDbConnection();

            if (con.State != ConnectionState.Open)
                await con.OpenAsync();

            return con;
        }

        private static bool IsValidSqlIdentifier(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        private static string QuoteSqlName(string name)
        {
            return "[" + name.Replace("]", "]]") + "]";
        }

        private static bool IsProtectedDatabase(string databaseName)
        {
            var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "master",
                "model",
                "msdb",
                "tempdb",
                "ERPCORE",
                "ERPFRM",
                "ERPHR",
                "ERPTMS"
            };

            return protectedNames.Contains(databaseName);
        }

        private static string NormalizeUser(string user)
        {
            return string.IsNullOrWhiteSpace(user)
                ? "system"
                : user;
        }

        private static string GetErrorMessage(Exception ex)
        {
            return ex.InnerException?.Message ?? ex.Message;
        }

        private static ServiceResult Fail(string message)
        {
            return new ServiceResult
            {
                Success = false,
                Message = message
            };
        }

        private static ServiceResult Success(string message, string? databaseName = null)
        {
            return new ServiceResult
            {
                Success = true,
                Message = message,
                Table = databaseName
            };
        }
    }
}