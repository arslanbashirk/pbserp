using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace PBS.ERP.Infrastructure.Services
{
    public class TableService : ISuperInterface
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public TableService(
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _configuration = configuration;
        }

        private string BuildCreateTableSql(TableCreateRequest request)
        {
            var schema = string.IsNullOrWhiteSpace(request.Schema) ? "dbo" : request.Schema;

            var fullTableName = $"[{request.Database}].[{schema}].[{request.Table}]";

            var sb = new StringBuilder();

            sb.Append($"CREATE TABLE {fullTableName} (");

            foreach (var col in request.Columns)
            {
                var type = MyData.NormalizeSqlType(col.SqlType);

                sb.Append($"[{col.ColumnName}] {type}");

                if (type == "decimal")
                    sb.Append($"({col.MaxLength},{col.DecimalPlaces})");
                else if (type.Contains("char") && col.MaxLength.HasValue)
                    sb.Append($"({col.MaxLength})");

                sb.Append(col.IsRequired == true ? " NOT NULL" : " NULL");

                AppendDefault(sb, col, type);

                sb.Append(", ");
            }

            sb.Length -= 2;
            sb.Append(")");

            return sb.ToString();
        }
        private void AppendDefault(StringBuilder sb, Field col, string type)
        {
            var safeExpressions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "getdate()",
                "newid()",
                "sysdatetime()",
                "current_timestamp"
            };

            if (!string.IsNullOrWhiteSpace(col.DefaultExpression))
            {
                if (!safeExpressions.Contains(col.DefaultExpression))
                    throw new Exception($"Unsafe default expression: {col.ColumnName}");

                sb.Append($" DEFAULT {col.DefaultExpression.ToUpper()}");
            }
            else if (col.DefaultValue != null)
            {
                bool isNumeric = type.Contains("int") || type.Contains("decimal") || type.Contains("float") || type.Contains("bit");
                bool isDate = type.Contains("date");

                if (isNumeric)
                {
                    if (!decimal.TryParse(col.DefaultValue, out _))
                        throw new Exception($"Invalid numeric default: {col.ColumnName}");

                    sb.Append($" DEFAULT {col.DefaultValue}");
                }
                else if (isDate && col.DefaultValue.Equals("getdate()", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(" DEFAULT GETDATE()");
                }
                else
                {
                    sb.Append($" DEFAULT '{col.DefaultValue.Replace("'", "''")}'");
                }
            }
        }

        private string BuildColumnSql(Field field)
        {
            var type =
                MyData.NormalizeSqlType(field.SqlType);

            var sb = new StringBuilder();

            sb.Append($"[{field.ColumnName}] {type}");

            if (type == "decimal")
            {
                sb.Append(
                    $"({field.MaxLength ?? 18},{field.DecimalPlaces ?? 2})");
            }
            else if (
                type.Contains("char") &&
                !type.Contains("(max)") &&
                field.MaxLength.HasValue)
            {
                sb.Append($"({field.MaxLength})");
            }
            else if (
                type == "varbinary" &&
                field.MaxLength.HasValue)
            {
                sb.Append($"({field.MaxLength})");
            }

            sb.Append(
                field.IsRequired == true
                    ? " NOT NULL"
                    : " NULL");

            AppendDefault(
                sb,
                field,
                type);

            return sb.ToString();
        }

        private async Task<HashSet<string>> GetExistingColumnsAsync(
            DbConnection con,
            DbTransaction tx,
            string database,
            string schema,
            string table)
        {
            var sql = $@"
        SELECT COLUMN_NAME
        FROM [{database}].INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @schema
        AND TABLE_NAME = @table";

            var cols = await con.QueryAsync<string>(
                sql,
                new { schema, table },
                transaction: tx);

            return cols.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private Field MapFieldMetadata(Field col, string tableUid, string user)
        {
            return new Field
            {
                UID = Guid.NewGuid().ToString(),
                Entity = tableUid,
                ColumnName = col.ColumnName,
                SqlType = MyData.NormalizeSqlType(col.SqlType),
                InputType = col.InputType,
                DisplayLabel = col.DisplayLabel,
                Placeholder = col.Placeholder,
                Tooltip = col.Tooltip,
                SectionNumber = col.SectionNumber,
                SectionName = col.SectionName,
                SortOrder = col.SortOrder,
                IsRequired = col.IsRequired,
                IsReadonly = col.IsReadonly,
                IsComputed = col.IsComputed,
                ComputedExpression = col.ComputedExpression,
                DefaultValue = col.DefaultValue,
                DefaultExpression = col.DefaultExpression,
                MaxLength = col.MaxLength,
                MinLength = col.MinLength,
                MinValue = col.MinValue,
                MaxValue = col.MaxValue,
                DecimalPlaces = col.DecimalPlaces,
                RegexPattern = col.RegexPattern,
                IsForeignKey = col.IsForeignKey,
                DropdownSourceTable = col.DropdownSourceTable,
                DropdownValueColumn = col.DropdownValueColumn,
                DropdownTextColumn = col.DropdownTextColumn,
                DropdownWhere = col.DropdownWhere,
                DropdownOrderBy = col.DropdownOrderBy,
                IsMultiSelect = col.IsMultiSelect ?? false,
                IncludeBlankOption = true,
                AllowInsert = col.AllowInsert ?? true,
                AllowUpdate = col.AllowUpdate ?? true,
                AllowDelete = col.AllowDelete ?? true,
                ShowInList = col.ShowInList ?? true,
                ShowInMobile = col.ShowInMobile ?? true,
                IsSearchable = col.IsSearchable ?? true,
                IsSortable = col.IsSortable ?? true,
                Exportable = col.Exportable ?? true,
                Importable = col.Importable ?? true,
                Width = col.Width ?? 2,
                CreatedBy = user ?? "system",
                CreatedTime = DateTime.Now,
                IsDeleted = false
            };
        }

        public async Task<ServiceResult> RenameTableAsync(AlterTableName request, string user)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Old) || string.IsNullOrWhiteSpace(request.New))
                return new ServiceResult { Success =false, Message = "Invalid request" };

            var entity = await _context.Entities
                .Where(m => m.UID == request.Old)
                .Select(m => new { m.UID, m.Name, m.Schema, m.Database })
                .FirstOrDefaultAsync();

            if (entity == null)
                return new ServiceResult{ Success = false, Message = "Entity not found" };

            var newTable = request.New;

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database) ||
                !IsValidSqlIdentifier(newTable))
                return new ServiceResult{ Success = false, Message = "Invalid table name" };

            var fullOldName = $"[{entity.Schema}].[{entity.Name}]";

            await using var con = await GetOpenConnectionAsync();
            await using var tx = await con.BeginTransactionAsync();

            try
            {

                var renameSql = $@"
                    EXEC [{entity.Database}].sys.sp_rename
                    N'{fullOldName}',
                    N'{newTable}',
                    N'OBJECT';
                    ";

                await con.ExecuteAsync(renameSql, transaction: tx);


                await con.ExecuteAsync(
                    @"UPDATE " + Constants.EntityTable + " SET Name = @newName, ModifiedBy=@user, ModifiedTime=@now WHERE UID = @uid",
                    new
                    {
                        newName = newTable,
                        user = string.IsNullOrWhiteSpace(user) ? "system" : user,
                        now = DateTime.Now,
                        uid = request.Old
                    },
                    tx
                );


                await con.ExecuteAsync(
                    @"UPDATE " + Constants.FieldTable + " SET DropdownSourceTable = @newName WHERE DropdownSourceTable = @oldName",
                    new
                    {
                        newName = newTable,
                        oldName = entity.Name
                    },
                    tx
                );

                await tx.CommitAsync();

                return new ServiceResult { Success = true, Message = "Table renamed successfully" };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ServiceResult { Success = false, Message = "Error renaming table: " + ex.Message };
                
            }
        }
        public async Task<ServiceResult> CreateTableAsync(TableCreateRequest request, string user)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Table))
                return new ServiceResult { Success = false, Message = "Invalid request: Table name is required" };

            var schema = string.IsNullOrWhiteSpace(request.Schema) ? "dbo" : request.Schema;

            if (!IsValidSqlIdentifier(request.Table) || !IsValidSqlIdentifier(schema))
                return new ServiceResult { Success = false, Message = "Invalid request: Invalid table/schema name" };

            if (!string.IsNullOrWhiteSpace(request.UID))
            {
                var exs = await _context.Entities.AnyAsync(e => e.UID == request.UID);
                if (exs)
                    return new ServiceResult { Success = false, Message = "Invalid request: Table UID already exists" };
            }

            string? connectionKey = null;
            string baseConnectionString;

            var baseConn = await GetOpenConnectionAsync();
            var sql = $@"SELECT TOP 1 [Connection] FROM [ERPCORE].[dbo].[{Constants.TableType}]
             WHERE IsDeleted = 0 AND [Short] = @Short";
            connectionKey = (await baseConn.QueryAsync<string>(
                sql,
                new { Short = request.TableType }
            )).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(connectionKey))
            {
                baseConnectionString = _configuration.GetConnectionString(connectionKey);
                if (string.IsNullOrWhiteSpace(baseConnectionString))
                    return new ServiceResult { Success = false, Message = $"Connection '{connectionKey}' not found." };
            }
            else
            {
                connectionKey = "DefaultConnection";
                baseConnectionString = _configuration.GetConnectionString(connectionKey);
            }

            string targetDatabase;
            var builder = new SqlConnectionStringBuilder(baseConnectionString);
            if (!string.IsNullOrWhiteSpace(request.Database))
            {
                if (!IsValidSqlIdentifier(request.Database))
                    return new ServiceResult { Success = false, Message = "Invalid database name" };

                targetDatabase = request.Database;
                // Override catalog if needed
                //builder.InitialCatalog = targetDatabase;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                    return new ServiceResult { Success = false, Message = "Database is not specified in request or connection string." };

                targetDatabase = builder.InitialCatalog;
            }

            request.Database = targetDatabase;

            await using var conn = new SqlConnection(baseConnectionString);
            await conn.OpenAsync();


            var dbExists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sys.databases WHERE name = @db",
                new { db = request.Database });

            if (dbExists == 0)
                return new ServiceResult { Success = false, Message = $"Database '{request.Database}' does not exist" };


            if (request.Columns == null)
                request.Columns = new List<Field>();

            foreach (var def in DEFAULTS.Columns)
            {
                if (!request.Columns.Any(c => c.ColumnName == def.ColumnName))
                    request.Columns.Add(def);
            }

            foreach (var col in request.Columns)
            {
                if (!IsValidSqlIdentifier(col.ColumnName))
                    return new ServiceResult { Success = false, Message = $"Invalid column: {col.ColumnName}" };

                var type = MyData.NormalizeSqlType(col.SqlType);

                if (!MyData.AllowedSqlTypes.Contains(type))
                    return new ServiceResult { Success = false, Message = $"Unsupported type: {col.SqlType}" };
            }

            var existsSql = $@"
            SELECT COUNT(1)
            FROM [{request.Database}].INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @Table
            AND TABLE_SCHEMA = @Schema";

            var exists = await conn.ExecuteScalarAsync<int>(existsSql, new
            {
                Table = request.Table,
                Schema = schema
            });

            if (exists > 0)
                return new ServiceResult { Success = false, Message = "Table already exists in target database" };

            var createSql = BuildCreateTableSql(request);
            bool tableCreated = false;
            try
            {
                await conn.ExecuteAsync(createSql);
                tableCreated = true;
            }
            catch (Exception ex)
            {
                return new ServiceResult { Success = false, Message = $"Table creation failed: {ex.Message}" };
            }
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var tableUid = request.UID ?? Guid.NewGuid().ToString();

                var entity = new Entity
                {
                    UID = tableUid,
                    Connection = connectionKey,
                    Server = conn.DataSource,
                    Database = request.Database,
                    Schema = schema,
                    Name = request.Table,
                    Description = request.TableDescription ?? request.Table,
                    Type = string.IsNullOrWhiteSpace(request.TableType) ? "SYS" : request.TableType,
                    CreatedTime = DateTime.Now,
                    CreatedBy = user ?? "system"
                };

                _context.Entities.Add(entity);

                foreach (var col in request.Columns)
                {
                    _context.Fields.Add(MapFieldMetadata(col, tableUid, user));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "Table created successfully",
                    Table = request.Table,
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // rollback physical table
                if (tableCreated)
                {
                    try
                    {
                        await conn.ExecuteAsync(
                            $"DROP TABLE [{request.Database}].[{schema}].[{request.Table}]");
                    }
                    catch { }
                }

                return new ServiceResult { Success = false, Message = $"Metadata save failed: {ex.Message}" };
            }
        }

        public async Task<ServiceResult> ImportColumnsAsync(
            ImportColumnsRequest request,
            string user)
        {
            if (request == null)
                return Fail("Request is null");

            if (string.IsNullOrWhiteSpace(request.Table))
                return Fail("Table is required");

            if (request.Columns == null || request.Columns.Count == 0)
                return Fail("No columns supplied");

            var entity = await _context.Entities
                .FirstOrDefaultAsync(x => x.UID == request.Table);

            if (entity == null)
                return Fail("Entity not found");

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database))
                return Fail("Invalid entity identifiers");

            await using var conn = await GetOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // 🔥 IMPORTANT: attach SAME connection + transaction to EF
            _context.Database.UseTransaction((SqlTransaction)tx);

            try
            {
                var fields = new List<Field>();
                var addParts = new List<string>();

                int sortOrder =
                    await _context.Fields
                        .Where(x => x.Entity == request.Table)
                        .MaxAsync(x => (int?)x.SortOrder) ?? 0;

                var existingColumns = await GetExistingColumnsAsync(
                    conn,
                    tx,
                    entity.Database,
                    entity.Schema,
                    entity.Name);

                foreach (var item in request.Columns)
                {
                    if (string.IsNullOrWhiteSpace(item.ColumnName))
                        continue;

                    if (existingColumns.Contains(item.ColumnName))
                        return Fail($"Column already exists: {item.ColumnName}");

                    var field = MetadataImportBuilder.Build(
                        request.Table,
                        item,
                        user,
                        ++sortOrder);

                    fields.Add(field);
                    addParts.Add(BuildColumnSql(field));
                }

                if (!addParts.Any())
                    return Fail("Nothing to import");

                var sql = $@"
                ALTER TABLE [{entity.Database}].[{entity.Schema}].[{entity.Name}]
                ADD {string.Join(",", addParts)}";

                // 🔥 STEP 1: schema change (Dapper)
                await conn.ExecuteAsync(sql, transaction: tx);

                // 🔥 STEP 2: metadata (EF using SAME transaction)
                _context.Fields.AddRange(fields);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = $"{fields.Count} columns imported"
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await tx.RollbackAsync();
                }
                catch
                {
                    // ignore rollback failure safely
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private ServiceResult Fail(string msg)
        {
            return new ServiceResult
            {
                Success = false,
                Message = msg
            };
        }


        private void SafeRollback(DbTransaction tx)
        {
            try
            {
                if (tx != null && tx.Connection != null)
                    tx.Rollback();
            }
            catch
            {
                // ignore rollback failures safely
            }
        }


        private ServiceResult RollbackFail(DbTransaction tx, string msg)
        {
            SafeRollback(tx);

            return new ServiceResult
            {
                Success = false,
                Message = msg
            };
        }

        public async Task<ServiceResult> AddColumnAsync(AddColumnRequest request, string user)
        {
            if (request == null)
                return Fail("Invalid request");

            if (string.IsNullOrWhiteSpace(request.Table))
                return Fail("Table name is required.");

            var entity = await _context.Entities
                .Where(m => m.UID == request.Table)
                .Select(m => new
                {
                    m.UID,
                    m.Name,
                    m.Schema,
                    m.Database
                })
                .FirstOrDefaultAsync();

            if (entity == null)
                return Fail("Entity not found");

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database))
                return Fail("Invalid table identifier");

            var col = request.Meta;

            if (col == null)
                return Fail("Column metadata required");

            if (string.IsNullOrWhiteSpace(col.ColumnName))
                return Fail("Column name is required");

            col.ColumnName = col.ColumnName.Trim();

            if (!IsValidSqlIdentifier(col.ColumnName))
                return Fail($"Invalid column name: {col.ColumnName}");

            if (string.IsNullOrWhiteSpace(col.SqlType))
                return Fail("SqlType is required");

            var normalizedType = MyData.NormalizeSqlType(col.SqlType);

            if (!MyData.AllowedSqlTypes.Contains(normalizedType))
                return Fail($"Unsupported SQL type: {col.SqlType}");

            if (normalizedType == "decimal" &&
                (!col.MaxLength.HasValue || !col.DecimalPlaces.HasValue))
                return Fail("Decimal requires precision and scale");

            if ((normalizedType.Contains("char") || normalizedType == "varbinary") &&
                !normalizedType.Contains("(max)") &&
                !col.MaxLength.HasValue)
                return Fail($"{col.ColumnName} requires MaxLength");

            col.SqlType = normalizedType;

            // Default values for newly-added metadata
            col.SectionNumber ??= 0;
            col.SortOrder ??= 0;
            col.IsRequired ??= false;
            col.IsReadonly ??= false;
            col.IsComputed ??= false;
            col.IsForeignKey ??= false;
            col.IsMultiSelect ??= false;
            col.AllowInsert ??= true;
            col.AllowUpdate ??= true;
            col.AllowDelete ??= true;
            col.ShowInList ??= true;
            col.ShowInMobile ??= true;
            col.IsSearchable ??= true;
            col.IsSortable ??= true;
            col.Exportable ??= true;
            col.Importable ??= true;
            col.Width ??= 2;
            col.IsDeleted ??= false;

            var currentUser = string.IsNullOrWhiteSpace(user) ? "system" : user;

            var dbConnection = _context.Database.GetDbConnection();

            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dbTransaction = transaction.GetDbTransaction();

                var existingColumns = await GetExistingColumnsAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name);

                if (existingColumns.Contains(col.ColumnName))
                {
                    await transaction.RollbackAsync();
                    return Fail($"Column already exists: {col.ColumnName}");
                }

                var columnSql = BuildColumnSql(col);

                var alterSql = $@"
                ALTER TABLE [{entity.Database}].[{entity.Schema}].[{entity.Name}]
                ADD {columnSql};";

                await dbConnection.ExecuteAsync(
                    alterSql,
                    transaction: dbTransaction);

                var metadata = MapFieldMetadata(
                    col,
                    request.Table,
                    currentUser);

                metadata.ModifiedBy = currentUser;
                metadata.ModifiedTime = DateTime.Now;
                metadata.IsDeleted = col.IsDeleted ?? false;

                _context.Fields.Add(metadata);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "Column added securely",
                    Column = col.ColumnName
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore rollback failure safely
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = $"Error adding column. Operation rolled back. {ex.Message}"
                };
            }
        }

        public async Task<ServiceResult> AlterColumnAsync(AlterTableRequest request,string user)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Table) ||
                string.IsNullOrWhiteSpace(request.Column) ||
                request.Meta == null)
            {
                return Fail("Invalid request data");
            }

            var entity = await _context.Entities
                .Where(m => m.UID == request.Table)
                .Select(m => new
                {
                    m.UID,
                    m.Name,
                    m.Schema,
                    m.Database
                })
                .FirstOrDefaultAsync();

            if (entity == null)
                return Fail("Entity not found");

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database))
            {
                return Fail("Invalid table/schema/database name");
            }

            var oldColumn = request.Column.Trim();
            var col = request.Meta;

            if (string.IsNullOrWhiteSpace(col.ColumnName))
                return Fail("New column name is required");

            var newColumn = col.ColumnName.Trim();

            if (!IsValidSqlIdentifier(oldColumn) ||
                !IsValidSqlIdentifier(newColumn))
            {
                return Fail("Invalid column name");
            }

            if (string.IsNullOrWhiteSpace(col.SqlType))
                return Fail("SqlType is required");

            var normalizedType = MyData.NormalizeSqlType(col.SqlType);

            if (!MyData.AllowedSqlTypes.Contains(normalizedType))
                return Fail("Unsupported SQL type");

            if (normalizedType.Contains("identity"))
                return Fail("Identity column cannot be altered");

            if (normalizedType == "decimal" &&
                (!col.MaxLength.HasValue || !col.DecimalPlaces.HasValue))
            {
                return Fail("Decimal requires precision and scale");
            }

            if ((normalizedType.Contains("char") || normalizedType == "varbinary") &&
                !normalizedType.Contains("(max)") &&
                !col.MaxLength.HasValue)
            {
                return Fail($"{newColumn} requires MaxLength");
            }

            col.ColumnName = newColumn;
            col.SqlType = normalizedType;

            bool renamed = !string.Equals(
                oldColumn,
                newColumn,
                StringComparison.OrdinalIgnoreCase);

            var dbConnection = _context.Database.GetDbConnection();

            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dbTransaction = transaction.GetDbTransaction();

                var existingColumns = await GetExistingColumnsAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name);

                if (!existingColumns.Contains(oldColumn))
                {
                    await transaction.RollbackAsync();
                    return Fail($"Column does not exist: {oldColumn}");
                }

                if (renamed && existingColumns.Contains(newColumn))
                {
                    await transaction.RollbackAsync();
                    return Fail($"Column already exists: {newColumn}");
                }

                var fullTableName = $"[{entity.Database}].[{entity.Schema}].[{entity.Name}]";

                /*
                ================================
                STEP 1 — Drop old default constraint
                ================================
                */
                await DropDefaultConstraintAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name,
                    oldColumn);

                /*
                ================================
                STEP 2 — Alter physical column
                ================================
                */
                var alterColumnSql = $@"
                ALTER TABLE {fullTableName}
                ALTER COLUMN {BuildAlterColumnSql(oldColumn, col)};";

                await dbConnection.ExecuteAsync(
                    alterColumnSql,
                    transaction: dbTransaction);

                /*
                ================================
                STEP 3 — Rename physical column, if changed
                ================================
                */
                if (renamed)
                {
                    var renameSql = $@"
                    EXEC [{entity.Database}].sys.sp_rename
                        @ObjectName,
                        @NewColumnName,
                        'COLUMN';";

                    await dbConnection.ExecuteAsync(
                        renameSql,
                        new
                        {
                            ObjectName = $"[{entity.Schema}].[{entity.Name}].[{oldColumn}]",
                            NewColumnName = newColumn
                        },
                        transaction: dbTransaction);
                }

                /*
                ================================
                STEP 4 — Add new default constraint
                ================================
                */
                if (!string.IsNullOrWhiteSpace(col.DefaultExpression) ||
                    col.DefaultValue != null)
                {
                    var defaultSqlValue = BuildDefaultSqlValue(col, normalizedType);
                    var constraintName = BuildDefaultConstraintName(entity.Name, newColumn);

                    var addDefaultSql = $@"
                    ALTER TABLE {fullTableName}
                    ADD CONSTRAINT [{constraintName}]
                    DEFAULT {defaultSqlValue}
                    FOR [{newColumn}];";

                    await dbConnection.ExecuteAsync(
                        addDefaultSql,
                        transaction: dbTransaction);
                }

                /*
                ================================
                STEP 5 — Update metadata
                ================================
                */
                var metadata = await _context.Fields.FirstOrDefaultAsync(f =>
                    f.Entity == request.Table &&
                    f.ColumnName == oldColumn &&
                    (f.IsDeleted == false || f.IsDeleted == null));

                if (metadata == null)
                {
                    await transaction.RollbackAsync();
                    return Fail("Column metadata not found");
                }

                ApplyFieldMetadataUpdate(
                    metadata,
                    col,
                    newColumn,
                    normalizedType,
                    user);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "Column altered securely",
                    Column = newColumn
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore rollback failure safely
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<ServiceResult> DropTableAsync(DropTableRequest request,string user)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Table))
                return Fail("Invalid request");

            var entity = await _context.Entities
                .Where(m => m.UID == request.Table)
                .Select(m => new
                {
                    m.UID,
                    m.Name,
                    m.Schema,
                    m.Database
                })
                .FirstOrDefaultAsync();

            if (entity == null)
                return Fail("Entity not found");

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database))
            {
                return Fail("Invalid table/schema/database name");
            }

            var dbConnection = _context.Database.GetDbConnection();

            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dbTransaction = transaction.GetDbTransaction();

                var fullTableName = $"[{entity.Database}].[{entity.Schema}].[{entity.Name}]";

                /*
                ================================
                STEP 1 — Prevent drop if referenced
                ================================
                */

                var refCount = await dbConnection.ExecuteScalarAsync<int>(
                    $@"
                    SELECT COUNT(1)
                    FROM {Constants.FieldTable}
                    WHERE ISNULL(IsDeleted, 0) = 0
                    AND (
                        DropdownSourceTable = @TableUid
                        OR DropdownSourceTable = @TableName
                    );",
                    new
                    {
                        TableUid = request.Table,
                        TableName = entity.Name
                    },
                    transaction: dbTransaction);

                if (refCount > 0)
                {
                    await transaction.RollbackAsync();

                    return Fail("Cannot drop table because it is referenced.");
                }

                /*
                ================================
                STEP 2 — Soft delete metadata
                ================================
                */

                await dbConnection.ExecuteAsync(
                    $@"
                    UPDATE {Constants.FieldTable}
                    SET IsDeleted = 1,
                        DeletedBy = @User,
                        DeletedTime = @Now
                    WHERE Entity = @TableUid;",
                    new
                    {
                        TableUid = request.Table,
                        User = string.IsNullOrWhiteSpace(user) ? "system" : user,
                        Now = DateTime.Now
                    },
                    transaction: dbTransaction);

                await dbConnection.ExecuteAsync(
                    $@"
                    UPDATE {Constants.EntityTable}
                    SET IsDeleted = 1,
                    DeletedBy = @User,
                    DeletedTime = @Now
                    WHERE UID = @TableUid;",
                    new
                    {
                        TableUid = request.Table,
                        User = string.IsNullOrWhiteSpace(user) ? "system" : user,
                        Now = DateTime.Now
                    },
                    transaction: dbTransaction);

                /*
                ================================
                STEP 3 — Backup physical table
                ================================
                */

                var backupTableName =
                    $"Zeleted_{entity.Name}_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (!IsValidSqlIdentifier(backupTableName))
                    return Fail("Generated backup table name is invalid");

                var backupFullPath =
                    $"[{entity.Database}].[{entity.Schema}].[{backupTableName}]";

                var backupSql = $@"
                SELECT *
                INTO {backupFullPath}
                FROM {fullTableName};";

                await dbConnection.ExecuteAsync(
                    backupSql,
                    transaction: dbTransaction);

                /*
                ================================
                STEP 4 — Drop physical table
                ================================
                */

                var dropSql = $@"DROP TABLE {fullTableName};";

                await dbConnection.ExecuteAsync(
                    dropSql,
                    transaction: dbTransaction);

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "Table dropped successfully",
                    Table = entity.Name
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore rollback failure safely
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<ServiceResult> DropColumnAsync(DropColumnRequest request,string user)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Table) ||
                string.IsNullOrWhiteSpace(request.Column))
            {
                return Fail("Table and Column names are required");
            }

            var entity = await _context.Entities
                .Where(m => m.UID == request.Table)
                .Select(m => new
                {
                    m.UID,
                    m.Name,
                    m.Schema,
                    m.Database
                })
                .FirstOrDefaultAsync();

            if (entity == null)
                return Fail("Entity not found");

            var column = request.Column.Trim();

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database) ||
                !IsValidSqlIdentifier(column))
            {
                return Fail("Invalid identifier");
            }

            var protectedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UID",
                "Id",
                "CreatedBy",
                "CreatedTime",
                "ModifiedBy",
                "ModifiedTime",
                "DeletedBy",
                "DeletedTime",
                "IsDeleted",
                "RowVersion"
            };

            if (protectedColumns.Contains(column))
                return Fail($"System column '{column}' cannot be dropped.");

            var dbConnection = _context.Database.GetDbConnection();

            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dbTransaction = transaction.GetDbTransaction();

                var fullTableName =
                    $"[{entity.Database}].[{entity.Schema}].[{entity.Name}]";

                /*
                ================================
                STEP 1 — Confirm physical column exists
                ================================
                */

                var existingColumns = await GetExistingColumnsAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name);

                if (!existingColumns.Contains(column))
                {
                    await transaction.RollbackAsync();
                    return Fail($"Column does not exist: {column}");
                }

                /*
                ================================
                STEP 2 — Prevent drop if referenced in dropdown metadata
                ================================
                */

                var refCount = await dbConnection.ExecuteScalarAsync<int>(
                    $@"
                    SELECT COUNT(1)
                    FROM {Constants.FieldTable}
                    WHERE ISNULL(IsDeleted, 0) = 0
                    AND (
                            DropdownSourceTable = @TableUid
                            OR DropdownSourceTable = @TableName
                        )
                    AND (
                            DropdownValueColumn = @Column
                            OR DropdownTextColumn = @Column
                        );",
                    new
                    {
                        TableUid = request.Table,
                        TableName = entity.Name,
                        Column = column
                    },
                    transaction: dbTransaction);

                if (refCount > 0)
                {
                    await transaction.RollbackAsync();

                    return Fail(
                        $"Cannot drop column '{column}' because it is referenced in dropdown metadata.");
                }

                /*
                ================================
                STEP 3 — Drop default constraint if exists
                ================================
                */

                await DropDefaultConstraintAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name,
                    column);

                /*
                ================================
                STEP 4 — Drop physical column
                ================================
                */

                var dropColumnSql = $@"
                ALTER TABLE {fullTableName}
                DROP COLUMN [{column}];";

                await dbConnection.ExecuteAsync(
                    dropColumnSql,
                    transaction: dbTransaction);

                /*
                ================================
                STEP 5 — Soft-delete metadata
                ================================
                */

                var currentUser = string.IsNullOrWhiteSpace(user) ? "system" : user;
                var now = DateTime.Now;

                await dbConnection.ExecuteAsync(
                    $@"
                    UPDATE {Constants.FieldTable}
                    SET IsDeleted = 1,
                        DeletedBy = @CurrentUser,
                        DeletedTime = @Now
                    WHERE Entity = @TableUid
                    AND ColumnName = @Column;",
                    new
                    {
                        CurrentUser = currentUser,
                        Now = now,
                        TableUid = request.Table,
                        Column = column
                    },
                    transaction: dbTransaction);

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = $"Column '{column}' dropped successfully",
                    Column = column
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore rollback failure safely
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = "Error dropping column: " +
                              (ex.InnerException?.Message ?? ex.Message)
                };
            }
        }

        public async Task<ServiceResult> LayoutFieldAsync(FieldSortRequest model,string user)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Table))
                return Fail("Invalid request.");

            if (model.Fields == null || !model.Fields.Any())
                return Fail("No fields received.");

            var fieldUids = model.Fields
                .Where(x => !string.IsNullOrWhiteSpace(x.UID))
                .Select(x => x.UID)
                .Distinct()
                .ToList();

            if (!fieldUids.Any())
                return Fail("No valid field UID received.");

            var dbFields = await _context.Fields
                .Where(x =>
                    x.Entity == model.Table &&
                    fieldUids.Contains(x.UID) &&
                    x.IsDeleted != true)
                .ToListAsync();

            if (!dbFields.Any())
                return Fail("No matching fields found.");

            var currentUser = string.IsNullOrWhiteSpace(user) ? "system" : user;
            var now = DateTime.Now;

            foreach (var item in model.Fields)
            {
                if (string.IsNullOrWhiteSpace(item.UID))
                    continue;

                var field = dbFields.FirstOrDefault(x => x.UID == item.UID);

                if (field == null)
                    continue;

                field.SectionNumber = item.SectionNumber;
                field.SectionName = item.SectionName;
                field.SortOrder = item.SortOrder;
                field.Width = item.Width;
                field.ModifiedTime = now;
                field.ModifiedBy = currentUser;
            }

            await _context.SaveChangesAsync();

            return new ServiceResult
            {
                Success = true,
                Message = "Field sequence saved successfully.",
                Updated = dbFields.Count
            };
        }

        public async Task<ServiceResult> SetUniqueKeyAsync(UniqueRequest request, string user)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Table) ||
                string.IsNullOrWhiteSpace(request.UniqueGroup) ||
                request.Fields == null ||
                !request.Fields.Any())
            {
                return Fail("Table, UniqueGroup and Fields are required");
            }

            /*
            ================================
            STEP 1 — Load Entity (Table Info)
            ================================
            */

            var entity = await _context.Entities
                .Where(m => m.UID == request.Table)
                .Select(m => new
                {
                    m.UID,
                    m.Name,
                    m.Schema,
                    m.Database
                })
                .FirstOrDefaultAsync();

            if (entity == null)
                return Fail("Entity not found");

            /*
            ================================
            STEP 2 — Validate Identifiers
            ================================
            */

            if (!IsValidSqlIdentifier(entity.Name) ||
                !IsValidSqlIdentifier(entity.Schema) ||
                !IsValidSqlIdentifier(entity.Database))
            {
                return Fail("Invalid entity identifier");
            }

            var cleanFields = request.Fields
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            foreach (var f in cleanFields)
            {
                if (!IsValidSqlIdentifier(f))
                    return Fail($"Invalid column identifier: {f}");
            }

            /*
            ================================
            STEP 3 — Open Connection + Transaction
            ================================
            */

            var dbConnection = _context.Database.GetDbConnection();

            if (dbConnection.State != ConnectionState.Open)
                await dbConnection.OpenAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var dbTransaction = transaction.GetDbTransaction();

            try
            {
                var fullTableName =
                    $"[{entity.Database}].[{entity.Schema}].[{entity.Name}]";

                /*
                ================================
                STEP 4 — Confirm Columns Exist
                ================================
                */

                var existingColumns = await GetExistingColumnsAsync(
                    dbConnection,
                    dbTransaction,
                    entity.Database,
                    entity.Schema,
                    entity.Name);

                var missing = cleanFields
                    .Where(f => !existingColumns.Contains(f))
                    .ToList();

                if (missing.Any())
                {
                    await transaction.RollbackAsync();
                    return Fail($"Missing columns: {string.Join(", ", missing)}");
                }

                /*
                ================================
                STEP 5 — Check Existing Duplicate Data
                ================================
                */

                var columnListSql = string.Join(", ", cleanFields.Select(c => $"[{c}]"));

                var duplicateCheckSql = $@"
                SELECT COUNT(*) 
                FROM (
                    SELECT {columnListSql}, COUNT(*) AS Cnt
                    FROM {fullTableName}
                    GROUP BY {columnListSql}
                    HAVING COUNT(*) > 1
                ) AS X";

                var duplicates = await dbConnection.ExecuteScalarAsync<int>(
                    duplicateCheckSql,
                    transaction: dbTransaction);

                if (duplicates > 0)
                {
                    await transaction.RollbackAsync();
                    return Fail("Cannot create unique constraint. Duplicate data exists.");
                }

                /*
                ================================
                STEP 6 — Drop Existing Constraint (if exists)
                ================================
                */

                var constraintName =
                    $"UX_{entity.Name}_{request.UniqueGroup}"
                    .Replace(" ", "_");

                var dropSql = $@"
                IF EXISTS (
                    SELECT * FROM sys.objects
                    WHERE type = 'UQ'
                    AND name = '{constraintName}'
                )
                BEGIN
                    ALTER TABLE {fullTableName}
                    DROP CONSTRAINT [{constraintName}]
                END";

                await dbConnection.ExecuteAsync(
                    dropSql,
                    transaction: dbTransaction);

                /*
                ================================
                STEP 7 — Create Unique Constraint
                ================================
                */

                var createSql = $@"
                ALTER TABLE {fullTableName}
                ADD CONSTRAINT [{constraintName}]
                UNIQUE ({columnListSql});";

                await dbConnection.ExecuteAsync(
                    createSql,
                    transaction: dbTransaction);

                /*
                ================================
                STEP 8 — Update Metadata (Fields table)
                ================================
                */

                await dbConnection.ExecuteAsync(
                    $@"
                    UPDATE {Constants.FieldTable}
                    SET UniqueGroup = @UniqueGroup,
                        ModifiedBy = @User,
                        ModifiedTime = @Now
                    WHERE Entity = @TableUid
                    AND ColumnName IN @Columns;",
                    new
                    {
                        UniqueGroup = request.UniqueGroup,
                        User = string.IsNullOrWhiteSpace(user) ? "system" : user,
                        Now = DateTime.Now,
                        TableUid = request.Table,
                        Columns = cleanFields
                    },
                    transaction: dbTransaction);

                /*
                ================================
                STEP 9 — Commit
                ================================
                */

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = $"Unique constraint '{request.UniqueGroup}' applied successfully",
                    Column = string.Join(",", cleanFields)
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // safe ignore
                }

                return new ServiceResult
                {
                    Success = false,
                    Message = "Error applying unique constraint: " +
                              (ex.InnerException?.Message ?? ex.Message)
                };
            }
        }

        public async Task<List<ServiceResult>> BulkUploadColumnsAsync(IFormFile file,string tableUid,string user)
        {
            var results = new List<ServiceResult>();

            if (file == null || file.Length == 0)
            {
                results.Add(new ServiceResult
                {
                    Success = false,
                    Message = "Excel file is required"
                });

                return results;
            }

            if (string.IsNullOrWhiteSpace(tableUid))
            {
                results.Add(new ServiceResult
                {
                    Success = false,
                    Message = "Table UID is required"
                });

                return results;
            }

            var entityExists = await _context.Entities
                .AnyAsync(x => x.UID == tableUid && x.IsDeleted != true);

            if (!entityExists)
            {
                results.Add(new ServiceResult
                {
                    Success = false,
                    Message = "Entity not found"
                });

                return results;
            }

            var columns = new List<AddColumnRequest>();

            try
            {
                using var stream = new MemoryStream();

                await file.CopyToAsync(stream);

                stream.Position = 0;

                using var package = new ExcelPackage(stream);

                var sheet = package.Workbook.Worksheets.FirstOrDefault();

                if (sheet == null || sheet.Dimension == null)
                {
                    results.Add(new ServiceResult
                    {
                        Success = false,
                        Message = "Excel sheet is empty"
                    });

                    return results;
                }

                var rowCount = sheet.Dimension.Rows;

                /*
                Expected Excel columns:

                1  ColumnName
                2  SqlType
                3  MaxLength
                4  DecimalPlaces
                5  IsRequired
                6  DefaultValue
                7  DefaultExpression
                8  InputType
                9  DisplayLabel
                */

                for (int row = 2; row <= rowCount; row++)
                {
                    var columnName = CleanExcelText(sheet.Cells[row, 1].Text);
                    var sqlType = CleanExcelText(sheet.Cells[row, 2].Text);

                    // Skip fully empty rows
                    if (string.IsNullOrWhiteSpace(columnName) &&
                        string.IsNullOrWhiteSpace(sqlType))
                    {
                        continue;
                    }

                    var request = new AddColumnRequest
                    {
                        Table = tableUid,
                        Meta = new Field
                        {
                            ColumnName = columnName,
                            SqlType = sqlType,
                            MaxLength = TryParseInt(sheet.Cells[row, 3].Text),
                            DecimalPlaces = TryParseInt(sheet.Cells[row, 4].Text),
                            IsRequired = TryParseBool(sheet.Cells[row, 5].Text),
                            DefaultValue = CleanExcelText(sheet.Cells[row, 6].Text),
                            DefaultExpression = CleanExcelText(sheet.Cells[row, 7].Text),
                            InputType = CleanExcelText(sheet.Cells[row, 8].Text),
                            DisplayLabel = CleanExcelText(sheet.Cells[row, 9].Text),

                            // Safe defaults
                            AllowInsert = true,
                            AllowUpdate = true,
                            AllowDelete = true,
                            ShowInList = true,
                            ShowInMobile = true,
                            IsSearchable = true,
                            IsSortable = true,
                            Exportable = true,
                            Importable = true,
                            IncludeBlankOption = true,
                            Width = 2,
                            SectionNumber = 1,
                            SortOrder = row - 2,
                            IsDeleted = false
                        }
                    };

                    columns.Add(request);
                }
            }
            catch (Exception ex)
            {
                results.Add(new ServiceResult
                {
                    Success = false,
                    Message = "Error reading Excel file: " + ex.Message
                });

                return results;
            }

            if (!columns.Any())
            {
                results.Add(new ServiceResult
                {
                    Success = false,
                    Message = "No valid columns found in Excel file"
                });

                return results;
            }

            foreach (var columnRequest in columns)
            {
                var columnName = columnRequest.Meta?.ColumnName;

                try
                {
                    var result = await AddColumnAsync(columnRequest, user);

                    results.Add(new ServiceResult
                    {
                        Column = columnName,
                        Success = result.Success,
                        Message = result.Message
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new ServiceResult
                    {
                        Column = columnName,
                        Success = false,
                        Message = ex.InnerException?.Message ?? ex.Message
                    });
                }
            }

            return results;
        }
        private string BuildAlterColumnSql(string columnName, Field field)
        {
            var type = MyData.NormalizeSqlType(field.SqlType);

            var sb = new StringBuilder();

            sb.Append($"[{columnName}] {type}");

            if (type == "decimal")
            {
                sb.Append($"({field.MaxLength},{field.DecimalPlaces})");
            }
            else if (
                type.Contains("char") &&
                !type.Contains("(max)") &&
                field.MaxLength.HasValue)
            {
                sb.Append($"({field.MaxLength})");
            }
            else if (
                type == "varbinary" &&
                field.MaxLength.HasValue)
            {
                sb.Append($"({field.MaxLength})");
            }

            sb.Append(field.IsRequired == true ? " NOT NULL" : " NULL");

            return sb.ToString();
        }

        private string BuildDefaultSqlValue(Field col, string normalizedType)
        {
            var safeExpressions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "getdate()",
                "newid()",
                "sysdatetime()",
                "current_timestamp"
            };

            if (!string.IsNullOrWhiteSpace(col.DefaultExpression))
            {
                if (!safeExpressions.Contains(col.DefaultExpression.Trim()))
                    throw new Exception($"Unsafe default expression in {col.ColumnName}");

                return col.DefaultExpression.Trim().ToUpper();
            }

            if (col.DefaultValue == null)
                throw new Exception($"Default value is empty for {col.ColumnName}");

            bool isNumeric =
                normalizedType.Contains("int") ||
                normalizedType.Contains("decimal") ||
                normalizedType.Contains("float") ||
                normalizedType.Contains("bit");

            bool isDate = normalizedType.Contains("date");

            if (isNumeric)
            {
                if (!decimal.TryParse(col.DefaultValue, out _))
                    throw new Exception($"Invalid numeric default for {col.ColumnName}");

                return col.DefaultValue;
            }

            if (isDate &&
                col.DefaultValue.Equals("getdate()", StringComparison.OrdinalIgnoreCase))
            {
                return "GETDATE()";
            }

            return $"'{col.DefaultValue.Replace("'", "''")}'";
        }

        private string BuildDefaultConstraintName(string tableName, string columnName)
        {
            var name = $"DF_{tableName}_{columnName}";

            if (name.Length <= 120)
                return name;

            return name.Substring(0, 120);
        }

        private async Task DropDefaultConstraintAsync(
            DbConnection con,
            DbTransaction tx,
            string database,
            string schema,
            string table,
            string column)
        {
            var fullTableName = $"[{database}].[{schema}].[{table}]";

            var sql = $@"
                DECLARE @constraintName SYSNAME;
                DECLARE @sql NVARCHAR(MAX);

                SELECT @constraintName = dc.name
                FROM [{database}].sys.default_constraints dc
                INNER JOIN [{database}].sys.columns c
                    ON c.default_object_id = dc.object_id
                INNER JOIN [{database}].sys.tables t
                    ON t.object_id = c.object_id
                INNER JOIN [{database}].sys.schemas s
                    ON s.schema_id = t.schema_id
                WHERE s.name = @SchemaName
                AND t.name = @TableName
                AND c.name = @ColumnName;

                IF @constraintName IS NOT NULL
                BEGIN
                    SET @sql = N'ALTER TABLE {fullTableName} DROP CONSTRAINT [' 
                        + REPLACE(@constraintName, ']', ']]') 
                        + N']';

                    EXEC(@sql);
                END;
                ";

            await con.ExecuteAsync(
                sql,
                new
                {
                    SchemaName = schema,
                    TableName = table,
                    ColumnName = column
                },
                transaction: tx);
        }

        private void ApplyFieldMetadataUpdate(
            Field metadata,
            Field col,
            string newColumn,
            string normalizedType,
            string user)
        {
            metadata.ColumnName = newColumn;
            metadata.SqlType = normalizedType;
            metadata.InputType = col.InputType;
            metadata.DisplayLabel = col.DisplayLabel;
            metadata.Placeholder = col.Placeholder;
            metadata.Tooltip = col.Tooltip;
            metadata.SectionNumber = col.SectionNumber;
            metadata.SectionName = col.SectionName;
            metadata.SortOrder = col.SortOrder;
            metadata.IsRequired = col.IsRequired;
            metadata.IsReadonly = col.IsReadonly;
            metadata.IsComputed = col.IsComputed;
            metadata.ComputedExpression = col.ComputedExpression;
            metadata.DefaultValue = col.DefaultValue;
            metadata.DefaultExpression = col.DefaultExpression;
            metadata.MaxLength = col.MaxLength;
            metadata.MinLength = col.MinLength;
            metadata.MinValue = col.MinValue;
            metadata.MaxValue = col.MaxValue;
            metadata.DecimalPlaces = col.DecimalPlaces;
            metadata.RegexPattern = col.RegexPattern;
            metadata.IsForeignKey = col.IsForeignKey;
            metadata.DropdownSourceTable = col.DropdownSourceTable;
            metadata.DropdownValueColumn = col.DropdownValueColumn;
            metadata.DropdownTextColumn = col.DropdownTextColumn;
            metadata.DropdownWhere = col.DropdownWhere;
            metadata.DropdownOrderBy = col.DropdownOrderBy;

            metadata.Exportable = col.Exportable ?? metadata.Exportable;
            metadata.Importable = col.Importable ?? metadata.Importable;
            metadata.Width = col.Width ?? metadata.Width;
            metadata.IsSearchable = col.IsSearchable ?? metadata.IsSearchable;
            metadata.IsSortable = col.IsSortable ?? metadata.IsSortable;
            metadata.IsMultiSelect = col.IsMultiSelect ?? metadata.IsMultiSelect;
            metadata.AllowInsert = col.AllowInsert ?? metadata.AllowInsert;
            metadata.AllowUpdate = col.AllowUpdate ?? metadata.AllowUpdate;
            metadata.AllowDelete = col.AllowDelete ?? metadata.AllowDelete;
            metadata.ShowInList = col.ShowInList ?? metadata.ShowInList;
            metadata.ShowInMobile = col.ShowInMobile ?? metadata.ShowInMobile;

            metadata.ModifiedTime = DateTime.Now;
            metadata.ModifiedBy = string.IsNullOrWhiteSpace(user) ? "system" : user;
            metadata.IsDeleted = col.IsDeleted ?? metadata.IsDeleted;
        }

        private int? TryParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return int.TryParse(value.Trim(), out var result)
                ? result
                : null;
        }

        private bool? TryParseBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            if (bool.TryParse(value, out var boolResult))
                return boolResult;

            if (value == "1")
                return true;

            if (value == "0")
                return false;

            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase))
                return false;

            return null;
        }

        private string? CleanExcelText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        public static bool IsValidSqlIdentifier(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        public async Task<DbConnection> GetOpenConnectionAsync()
        {
            var con = _context.Database.GetDbConnection();

            if (con.State != ConnectionState.Open)
                await con.OpenAsync();

            return con;
        }

        private async Task<bool> IsErpCoreSurveyTableAsync(string table)
        {
            if (string.IsNullOrWhiteSpace(table))
                return false;

            return await _context.Entities.AnyAsync(x =>
                x.UID == table &&
                x.IsDeleted != true &&
                x.Database == "ERPCORE" &&
                x.Schema == "dbo" &&
                x.Name == "Survey");
        }

    }
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string? Table { get; set; }
        public string? Column { get; set; }
        public int? Updated { get; set; }
    }
}
