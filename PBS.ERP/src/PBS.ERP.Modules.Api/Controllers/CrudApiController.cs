using ClosedXML.Excel;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace PBS.ERP.Modules.Api.Controllers;

/// <summary>
/// JWT-secured metadata-driven CRUD API.
/// This controller is for API/mobile/external clients.
/// MVC/Razor view actions must remain in the MVC CrudController.
/// </summary>
[ApiController]
[Route("api/crud")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public sealed class CrudApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CrudApiController> _logger;
    private readonly IDbInterface _dbService;
    private readonly ISuperInterface _tableService;
    public CrudApiController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IDbInterface dbService,
        ISuperInterface tableService,
        ILogger<CrudApiController> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // =========================================================
    // Common infrastructure
    // =========================================================

    private async Task<DbConnection> GetConnectionAsync()
    {
        var con = _context.Database.GetDbConnection();

        if (string.IsNullOrWhiteSpace(con.ConnectionString))
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("DefaultConnection is missing.");

            con.ConnectionString = connectionString;
        }

        if (con.State != ConnectionState.Open)
            await con.OpenAsync(HttpContext.RequestAborted);

        return con;
    }

    private string CurrentUserName()
    {
        return User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("uid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }

    private string? CurrentUserUid()
    {
        return User.FindFirstValue("uid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private bool IsPrivilegedUser()
    {
        return User.IsInRole("Root") ||
               User.IsInRole("Super") ||
               User.IsInRole("Admin");
    }

    private static ApiResponse OkResponse(object? data = null, string message = "Success.")
    {
        return new ApiResponse(true, message, data, null);
    }

    private static ApiResponse FailResponse(string message = "Failed.", object? errors = null)
    {
        return new ApiResponse(false, message, null, errors);
    }

    private async Task<bool> IsAllowedTableAsync(DbConnection con, string? table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return false;

        var count = await con.QueryFirstOrDefaultAsync<int>(
            @"SELECT COUNT(1)
              FROM " + Constants.EntityTable + @"
              WHERE UID = @table AND IsDeleted = 0",
            new { table });

        return count > 0;
    }

    private async Task<Entity?> ResolveEntityAsync(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return null;

        return await _context.Entities
            .AsNoTracking()
            .Where(e => e.UID == table && !e.IsDeleted)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
    }

    private static string FullTableName(string database, string schema, string table)
    {
        return $"{Q(database)}.{Q(schema)}.{Q(table)}";
    }

    private static string BuildFullTableName(Entity entity)
    {
        return FullTableName(entity.Database, entity.Schema, entity.Name);
    }

    private static bool IsSurveyTable(Entity? entity)
    {
        if (entity == null)
            return false;

        return string.Equals(entity.Database, "ERPCORE", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.Schema, "dbo", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.Name, "Survey", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFormTable(Entity? entity)
    {
        if (entity == null)
            return false;

        return string.Equals(entity.Database, "ERPCORE", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.Schema, "dbo", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.Name, "Form", StringComparison.OrdinalIgnoreCase);
    }
    private async Task<List<Field>> GetFieldsAsync(DbConnection con, string table)
    {
        var fields = await con.QueryAsync<Field>(
            @"SELECT *
              FROM Field
              WHERE Entity = @table AND IsDeleted = 0
              ORDER BY ISNULL(SectionNumber, 100), ISNULL(SortOrder, Id)",
            new { table });

        return fields.ToList();
    }

    private static IFormCollection ToFormCollection(IDictionary<string, object?> data)
    {
        var formData = data.ToDictionary(
            x => x.Key,
            x => new StringValues(ConvertToStringValue(x.Value)));

        return new FormCollection(formData);
    }

    private static string ConvertToStringValue(object? value)
    {
        if (value == null)
            return string.Empty;

        // When bound from System.Text.Json, object values may be JsonElement.
        // ToString() still returns useful scalar values for this metadata-driven scenario.
        return value.ToString() ?? string.Empty;
    }

    private static DynamicParameters ToParameters(IDictionary<string, object?> data)
    {
        var parameters = new DynamicParameters();

        foreach (var item in data)
            parameters.Add(item.Key, item.Value);

        return parameters;
    }

    private static void NormalizeNulls(IDictionary<string, object?> data)
    {
        foreach (var key in data.Keys.ToList())
        {
            if (data[key] == DBNull.Value)
                data[key] = null;

            if (data[key] is string s && string.IsNullOrWhiteSpace(s))
                data[key] = null;
        }
    }

    // =========================================================
    // Validation / data building
    // =========================================================

    private async Task<List<string>> ValidateFormAsync(
        List<Field> fields,
        IFormCollection form,
        bool isInsert,
        DbConnection con,
        DbTransaction? tx = null)
    {
        var errors = new List<string>();

        foreach (var f in fields)
        {
            if (string.IsNullOrWhiteSpace(f.ColumnName))
                continue;

            if (isInsert && f.AllowInsert != true)
                continue;

            if (!isInsert && f.AllowUpdate != true)
                continue;

            var hasValue = form.ContainsKey(f.ColumnName);

            // PATCH/UPDATE: skip untouched fields.
            if (!isInsert && !hasValue)
                continue;

            var values = hasValue
                ? form[f.ColumnName]
                : StringValues.Empty;

            var firstValue = values.FirstOrDefault()?.Trim();
            var isEmpty = string.IsNullOrWhiteSpace(firstValue);

            if (f.IsRequired == true && isEmpty)
            {
                errors.Add($"{f.DisplayLabel ?? f.ColumnName} is required");
                continue;
            }

            if (isEmpty)
                continue;

            if (f.MinLength.HasValue && firstValue!.Length < f.MinLength.Value)
                errors.Add($"{f.DisplayLabel ?? f.ColumnName} is too short");

            if (f.MaxLength.HasValue && firstValue!.Length > f.MaxLength.Value)
                errors.Add($"{f.DisplayLabel ?? f.ColumnName} is too long");

            if (!string.IsNullOrWhiteSpace(f.RegexPattern))
            {
                try
                {
                    if (!Regex.IsMatch(firstValue!, f.RegexPattern))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} format is invalid");
                }
                catch (ArgumentException)
                {
                    errors.Add($"{f.DisplayLabel ?? f.ColumnName} has invalid regex metadata");
                }
            }

            switch (f.SqlType?.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(firstValue, out _))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} must be numeric");
                    break;

                case "bigint":
                    if (!long.TryParse(firstValue, out _))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} must be bigint");
                    break;

                case "decimal":
                case "numeric":
                case "money":
                    if (!decimal.TryParse(firstValue, out _))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} must be decimal");
                    break;

                case "datetime":
                case "datetime2":
                case "date":
                    if (!DateTime.TryParse(firstValue, out _))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} invalid date/time");
                    break;

                case "uniqueidentifier":
                    if (!Guid.TryParse(firstValue, out _))
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} invalid GUID");
                    break;
            }

            if (f.IsForeignKey == true)
            {
                if (string.IsNullOrWhiteSpace(f.DropdownSourceTable))
                {
                    errors.Add($"{f.DisplayLabel ?? f.ColumnName} has invalid FK source");
                    continue;
                }

                if (string.Equals(f.DropdownSourceTable, Constants.systemEntity.UID, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(f.DropdownValueColumn))
                {
                    errors.Add($"{f.DisplayLabel ?? f.ColumnName} has invalid FK value column");
                    continue;
                }

                var source = await con.ExecuteScalarAsync<string>(
                    @"SELECT '[' + [Database] + '].[' + [Schema] + '].[' + [Name] + ']'
                      FROM Entity
                      WHERE UID = @uid AND IsDeleted = 0",
                    new { uid = f.DropdownSourceTable },
                    tx);

                if (string.IsNullOrWhiteSpace(source))
                {
                    errors.Add($"{f.DisplayLabel ?? f.ColumnName} references invalid source");
                    continue;
                }

                var checkValues =
                    IsCheckboxLike(f.InputType)
                        ? values
                            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            .Select(v => v.Trim().Trim(','))
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                        : new List<string> { firstValue! };

                foreach (var val in checkValues)
                {
                    var exists = await con.ExecuteScalarAsync<int>(
                        $@"SELECT COUNT(1)
                           FROM {source}
                           WHERE {Q(f.DropdownValueColumn)} = @v",
                        new { v = val },
                        tx);

                    if (exists == 0)
                        errors.Add($"{f.DisplayLabel ?? f.ColumnName} contains invalid value ({val})");
                }
            }
        }

        return errors;
    }

    private static Dictionary<string, object?> BuildData(
        List<Field> fields,
        IFormCollection form,
        bool isInsert)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fields)
        {
            if (string.IsNullOrWhiteSpace(f.ColumnName))
                continue;

            if ((isInsert ? f.AllowInsert : f.AllowUpdate) != true)
                continue;

            var key = f.ColumnName;
            var inputType = f.InputType?.ToLowerInvariant();

            object? value = null;

            if (form.ContainsKey(key))
            {
                if (inputType == "checkbox")
                {
                    var values = form[key]
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        .Select(x => x.Trim().Trim(','))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    value = values.Any()
                        ? "," + string.Join(",", values) + ","
                        : null;
                }
                else
                {
                    var raw = form[key].FirstOrDefault();
                    value = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                }
            }

            data[key] = value;
        }

        return data;
    }

    // =========================================================
    // Identifier / SQL helpers
    // =========================================================

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string UnquoteIdentifier(string identifier)
    {
        identifier = identifier.Trim();

        if (identifier.StartsWith("[") && identifier.EndsWith("]") && identifier.Length >= 2)
            return identifier.Substring(1, identifier.Length - 2);

        return identifier;
    }

    private static string Q(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("SQL identifier cannot be empty.", nameof(identifier));

        identifier = UnquoteIdentifier(identifier);

        return "[" + identifier.Replace("]", "]]") + "]";
    }

    

    private static bool IsKeyColumn(string? columnName)
    {
        return string.Equals(columnName, "ID", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(columnName, "UID", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCheckboxLike(string? inputType)
    {
        return string.Equals(inputType, "checkbox", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSelectKeyword(string text)
    {
        return Regex.IsMatch(
            text,
            @"\bselect\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsSimpleIdentifier(string value)
    {
        value = value.Trim();

        if (value.StartsWith("[") && value.EndsWith("]") && value.Length >= 2)
            return true;

        return Regex.IsMatch(
            value,
            @"^[A-Za-z_][A-Za-z0-9_@$#]*$",
            RegexOptions.CultureInvariant);
    }

    private static List<string> SplitTopLevelCommas(string input)
    {
        var result = new List<string>();
        var sb = new StringBuilder();

        var depth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracket = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (inSingleQuote)
            {
                sb.Append(ch);

                if (ch == '\'')
                {
                    if (i + 1 < input.Length && input[i + 1] == '\'')
                    {
                        sb.Append(input[i + 1]);
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }

                continue;
            }

            if (inDoubleQuote)
            {
                sb.Append(ch);

                if (ch == '"')
                    inDoubleQuote = false;

                continue;
            }

            if (inBracket)
            {
                sb.Append(ch);

                if (ch == ']')
                {
                    if (i + 1 < input.Length && input[i + 1] == ']')
                    {
                        sb.Append(input[i + 1]);
                        i++;
                    }
                    else
                    {
                        inBracket = false;
                    }
                }

                continue;
            }

            switch (ch)
            {
                case '\'':
                    inSingleQuote = true;
                    sb.Append(ch);
                    break;

                case '"':
                    inDoubleQuote = true;
                    sb.Append(ch);
                    break;

                case '[':
                    inBracket = true;
                    sb.Append(ch);
                    break;

                case '(':
                    depth++;
                    sb.Append(ch);
                    break;

                case ')':
                    if (depth > 0)
                        depth--;

                    sb.Append(ch);
                    break;

                case ',' when depth == 0:
                    var part = sb.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(part))
                        result.Add(part);

                    sb.Clear();
                    break;

                default:
                    sb.Append(ch);
                    break;
            }
        }

        var lastPart = sb.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(lastPart))
            result.Add(lastPart);

        return result;
    }

    private static string BuildSqlTextPart(string textPart, string lookupAlias)
    {
        var expr = textPart.Trim();
        expr = expr.Replace("{alias}", lookupAlias);

        if (IsSimpleIdentifier(expr))
            return $"CONVERT(NVARCHAR(4000), {lookupAlias}.{Q(expr)})";

        if (ContainsSelectKeyword(expr) && !expr.TrimStart().StartsWith("("))
            return $"CONVERT(NVARCHAR(4000), ({expr}))";

        return $"CONVERT(NVARCHAR(4000), {expr})";
    }

    private static string BuildDropdownTextExpression(string? textCol, string alias)
    {
        if (string.IsNullOrWhiteSpace(textCol))
            return "''";

        var columns = SplitSqlCsv(textCol)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (columns.Count == 0)
            return "''";

        string CastIfNeeded(string col)
        {
            col = NormalizeAlias(col, alias).Trim();

            bool isExpression =
                col.Contains(" ") ||
                col.Contains("(") ||
                col.Contains(")") ||
                col.Contains("+") ||
                col.Contains("'") ||
                col.Contains(".") ||
                col.Contains("CASE", StringComparison.OrdinalIgnoreCase) ||
                col.Contains("CONCAT", StringComparison.OrdinalIgnoreCase) ||
                col.Contains("SELECT", StringComparison.OrdinalIgnoreCase);

            if (isExpression)
                return $"CONVERT(NVARCHAR(4000), ({col}))";

            return $"CONVERT(NVARCHAR(4000), {alias}.{Q(col)})";
        }

        if (columns.Count == 1)
        {
            return CastIfNeeded(columns[0]);
        }

        if (columns.Count == 2)
        {
            return $"CONCAT({CastIfNeeded(columns[0])}, ' (', {CastIfNeeded(columns[1])}, ')')";
        }

        var parts = columns.Select(CastIfNeeded);

        return $"CONCAT({string.Join(", ' - ', ", parts)})";
    }
    private static string BuildDropdownOrderBy(
    string? dropdownOrderBy,
    string lookupAlias,
    string fallbackColumn)
    {
        if (string.IsNullOrWhiteSpace(dropdownOrderBy))
            return "";

        // Do not allow full ORDER BY keyword in metadata
        if (Regex.IsMatch(dropdownOrderBy, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase))
            throw new InvalidOperationException($"Do not include ORDER BY in DropdownOrderBy: {dropdownOrderBy}");

        var parts = dropdownOrderBy
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var orderParts = new List<string>();

        foreach (var part in parts)
        {
            var tokens = Regex.Split(part.Trim(), @"\s+")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (tokens.Length < 1 || tokens.Length > 2)
                throw new InvalidOperationException($"Invalid DropdownOrderBy: {dropdownOrderBy}");

            var columnExpression = tokens[0].Trim();

            // Allow:
            // Code
            // [Code]
            // {alias}.Code
            // {alias}.[Code]
            columnExpression = columnExpression
                .Replace("{alias}.", "", StringComparison.OrdinalIgnoreCase)
                .Replace("{alias}", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (columnExpression.StartsWith("."))
                columnExpression = columnExpression[1..];

            var column = columnExpression.Trim('[', ']');

            if (!Regex.IsMatch(column, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new InvalidOperationException($"Invalid column in DropdownOrderBy: {column}");

            var direction = "ASC";

            if (tokens.Length == 2)
            {
                direction = tokens[1].ToUpperInvariant();

                if (direction != "ASC" && direction != "DESC")
                    throw new InvalidOperationException($"Invalid sort direction in DropdownOrderBy: {dropdownOrderBy}");
            }

            orderParts.Add($"{lookupAlias}.{Q(column)} {direction}");
        }

        return orderParts.Any()
            ? "ORDER BY " + string.Join(", ", orderParts)
            : "";
    }
    private static string BuildOrderPart(string item, string alias)
    {
        item = item.Trim();

        if (string.IsNullOrWhiteSpace(item))
            return string.Empty;

        string direction = string.Empty;

        if (item.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
        {
            direction = " DESC";
            item = item.Substring(0, item.Length - 5).Trim();
        }
        else if (item.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
        {
            direction = " ASC";
            item = item.Substring(0, item.Length - 4).Trim();
        }

        item = NormalizeAlias(item, alias).Trim();

        if (IsSimpleIdentifier(item))
            return $"{alias}.{Q(item)}{direction}";

        if (item.StartsWith("[") && item.EndsWith("]"))
            return $"{alias}.{item}{direction}";

        return $"{item}{direction}";
    }

    private static string NormalizeAlias(string value, string alias)
    {
        return value
            .Replace("{alias}.", alias + ".", StringComparison.OrdinalIgnoreCase)
            .Replace("{alias}", alias, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitSqlCsv(string input)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(input))
            return result;

        var current = new StringBuilder();
        var parentheses = 0;
        var inSingleQuote = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (ch == '\'')
            {
                inSingleQuote = !inSingleQuote;
                current.Append(ch);
                continue;
            }

            if (!inSingleQuote)
            {
                if (ch == '(')
                    parentheses++;

                if (ch == ')' && parentheses > 0)
                    parentheses--;

                if (ch == ',' && parentheses == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
    private static bool IsRealLookupField(Field field)
    {
        return field.IsForeignKey == true &&
               HasText(field.DropdownSourceTable) &&
               HasText(field.DropdownTextColumn) &&
               HasText(field.DropdownValueColumn);
    }

    private static bool LooksUnsafeSqlFragment(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var badPatterns = new[]
        {
            ";", "--", "/*", "*/",
            " xp_", " sp_",
            " exec ", " execute ",
            " insert ", " update ", " delete ",
            " drop ", " alter ", " create ",
            " truncate ", " merge ",
            " union ", " openrowset ", " opendatasource "
        };

        var padded = " " + sql.ToLowerInvariant() + " ";

        return badPatterns.Any(p => padded.Contains(p));
    }

    private static string BuildTrustedFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return string.Empty;

        if (LooksUnsafeSqlFragment(filter))
            throw new InvalidOperationException("Unsafe SQL filter fragment was rejected.");

        return filter.Replace("{alias}", "T1");
    }

    // =========================================================
    // Simple metadata endpoints
    // =========================================================

    [HttpGet("name")]
    public async Task<IActionResult> Name([FromQuery] string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return Ok(FailResponse("Table name is required."));

        var uid = await _context.Entities
            .AsNoTracking()
            .Where(e => e.Name == table && e.Database == "ERPCORE" && !e.IsDeleted)
            .Select(e => e.UID)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(uid))
            return NotFound(FailResponse("Table not found."));

        return Ok(OkResponse(new { uid }));
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata(string table)
    {
        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Table metadata not found."));

        var fields = await GetFieldsAsync(con, table);

        return Ok(OkResponse(new
        {
            entity = new
            {
                entity.UID,
                entity.Database,
                entity.Schema,
                entity.Name,
                entity.Description
            },
            fields
        }));
    }

    // =========================================================
    // Record endpoints
    // =========================================================

    [HttpGet("record")]
    public async Task<IActionResult> GetRecord(string table,string id)
    {
        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);

        var record = await con.QuerySingleOrDefaultAsync(
            $@"SELECT *
               FROM {tableName}
               WHERE UID = @id AND IsDeleted = 0",
            new { id });

        if (record == null)
            return NotFound(FailResponse("Record not found."));

        return Ok(OkResponse(record));
    }

    /// <summary>
    /// Optimized listing endpoint based on your List2.
    /// Uses OUTER APPLY TOP(1) to prevent row multiplication from lookup joins.
    /// This is the recommended list endpoint for metadata-driven grids.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> List2(
        string table,
        [FromQuery] string? filter,
        [FromQuery] bool all = true)
    {
        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var allMetadata = await GetFieldsAsync(con, table);

        var metadata = allMetadata
            .Where(f => all || f.ShowInList == true || IsKeyColumn(f.ColumnName))
            .OrderBy(f => f.SectionNumber ?? 9)
            .ThenBy(f => f.SortOrder ?? f.ID)
            .ToList();

        if (!metadata.Any())
            return NotFound(FailResponse("No metadata found for table."));

        var requiredEntityUids = metadata
            .Where(IsRealLookupField)
            .Select(f => f.DropdownSourceTable)
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Select(uid => uid!)
            .Append(table)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entityRows = await _context.Entities
            .AsNoTracking()
            .Where(e => requiredEntityUids.Contains(e.UID))
            .Select(e => new
            {
                e.UID,
                e.Database,
                e.Schema,
                e.Name
            })
            .ToListAsync(HttpContext.RequestAborted);

        var entityNames = entityRows.ToDictionary(
            e => e.UID,
            e => FullTableName(e.Database, e.Schema, e.Name),
            StringComparer.OrdinalIgnoreCase);

        if (!entityNames.TryGetValue(table, out var name))
            return NotFound(FailResponse("Table metadata not found."));

        var selectColumns = new List<string>();
        var joins = new List<string>();
        var sr = 1;

        foreach (var field in metadata)
        {
            sr++;

            var columnName = field.ColumnName;

            if (string.IsNullOrWhiteSpace(columnName))
                continue;

            if (IsRealLookupField(field))
            {
                var dropdownSourceTable = field.DropdownSourceTable;
                var dropdownValueColumn = field.DropdownValueColumn;
                var dropdownTextColumn = field.DropdownTextColumn;
                var dropdownWhere = field.DropdownWhere;
                var dropdownOrderBy = field.DropdownOrderBy;
                var inputType = field.InputType;

                if (IsCheckboxLike(inputType))
                {
                    selectColumns.Add($"T1.{Q(columnName)}");
                    continue;
                }

                if (string.Equals(
                        dropdownSourceTable,
                        Constants.systemEntity.UID,
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectColumns.Add($"T1.{Q(columnName)}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dropdownSourceTable) ||
                    string.IsNullOrWhiteSpace(dropdownValueColumn) ||
                    string.IsNullOrWhiteSpace(dropdownTextColumn) ||
                    !entityNames.TryGetValue(dropdownSourceTable, out var source))
                {
                    selectColumns.Add($"T1.{Q(columnName)}");
                    continue;
                }

                var applyAlias = $"T{sr}";
                var lookupAlias = $"S{sr}";

                var joinWhere = $"{lookupAlias}.{Q(dropdownValueColumn)} = T1.{Q(columnName)}";

                if (!string.IsNullOrWhiteSpace(dropdownWhere))
                {
                    if (LooksUnsafeSqlFragment(dropdownWhere))
                        return Ok(FailResponse($"Unsafe DropdownWhere metadata rejected for {columnName}."));

                    joinWhere += " AND (" + NormalizeAlias(dropdownWhere, lookupAlias) + ")";
                }

                var textExpression = BuildDropdownTextExpression(dropdownTextColumn, lookupAlias);
                var orderBy = BuildDropdownOrderBy(dropdownOrderBy, lookupAlias, dropdownValueColumn);

                joins.Add($@"
                    OUTER APPLY (
                        SELECT TOP (1)
                            {textExpression} AS {Q(columnName)}
                        FROM {source} AS {lookupAlias}
                        WHERE {joinWhere}
                        {orderBy}
                    ) AS {applyAlias}");

                selectColumns.Add($"{applyAlias}.{Q(columnName)} AS {Q(columnName)}");
            }
            else
            {
                selectColumns.Add($"T1.{Q(columnName)}");
            }
        }

        if (!selectColumns.Any())
            return Ok(FailResponse("No selectable columns found for table."));

        var whereClause = "T1.[IsDeleted] = @IsDeleted";

        try
        {
            var trustedFilter = BuildTrustedFilter(filter);

            if (!string.IsNullOrWhiteSpace(trustedFilter))
                whereClause += " AND (" + trustedFilter + ")";
        }
        catch (Exception ex)
        {
            return Ok(FailResponse(ex.Message));
        }

        var sql = $@"
            SELECT {string.Join(", ", selectColumns)}
            FROM {name} AS T1
            {string.Join("\n", joins)}
            WHERE {whereClause}";

        try
        {
            var data = await con.QueryAsync(sql, new { IsDeleted = false });
            return Ok(OkResponse(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List failed for table {Table}. SQL={Sql}", table, sql);
            return Ok(FailResponse("List failed.", new { ex.Message, sql }));
        }
    }

    [HttpPost("insert")]
    public async Task<IActionResult> Insert(string table, [FromBody] CrudSaveRequest request)
    {
        if (request?.Fields == null || request.Fields.Count == 0)
            return Ok(FailResponse("No fields supplied."));

        return await InsertInternalAsync(table, request.Fields);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromQuery] string table, [FromBody] CrudSaveRequest request)
    {
        if (request?.Fields == null || request.Fields.Count == 0)
            return Ok(FailResponse("No fields supplied."));

        if (string.IsNullOrWhiteSpace(request.ID))
            return await InsertInternalAsync(table, request.Fields);

        return await UpdateInternalAsync(table, request.ID, request.Fields);
    }

    [HttpPut("update")]
    public async Task<IActionResult> Update([FromQuery] string table, [FromQuery] string uid, [FromBody] CrudSaveRequest request)
    {
        if (request?.Fields == null || request.Fields.Count == 0)
            return Ok(FailResponse("No fields supplied."));

        return await UpdateInternalAsync(table, uid, request.Fields);
    }

    private async Task<IActionResult> InsertInternalAsync(
    string table,
    Dictionary<string, object?> fieldsInput)
    {
        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);

        var fields = (await GetFieldsAsync(con, table))
            .Where(f => f.AllowInsert == true)
            .ToList();

        var form = ToFormCollection(fieldsInput);

        var errors = await ValidateFormAsync(fields, form, true, con);

        if (errors.Any())
            return Ok(FailResponse("Validation error.", errors));

        var data = BuildData(fields, form, true);
        NormalizeNulls(data);

        if (!data.Any())
            return Ok(FailResponse("No valid data to insert."));

        var uid = Guid.NewGuid().ToString();
        var now = DateTime.Now;
        var user = CurrentUserName();

        data["UID"] = uid;
        data["CreatedTime"] = now;
        data["CreatedBy"] = user;
        data["IsDeleted"] = false;
        data["IsActive"] = true;

        var input = new Dictionary<string, object?>(
            fieldsInput,
            StringComparer.OrdinalIgnoreCase
        );

        if (input.TryGetValue("IsActive", out var isActiveValue))
        {
            data["IsActive"] = ConvertToBoolValue(isActiveValue);
        }
        else
        {
            data["IsActive"] = true;
        }

        if (input.TryGetValue("Remarks", out var remarksValue))
        {
            data["Remarks"] = ConvertToStringValue(remarksValue);
        }

        SurveyInsert? surveyDatabaseWork = null;
        FormInsert? formTableWork = null;

        try
        {
            //STEP 1 — Survey Database handling
            surveyDatabaseWork = await CreateSurveyDatabase(entity,form,user);

            if (surveyDatabaseWork.FailureResult != null)
                return surveyDatabaseWork.FailureResult;

            //STEP 2 — Form physical table handling
            formTableWork = await CreateSurveyTable(entity,con,form,uid,user);

            if (formTableWork.FailureResult != null)
                return formTableWork.FailureResult;

            //STEP 3 — Insert metadata row
            var sql = $@"
            INSERT INTO {tableName}
            ({string.Join(",", data.Keys.Select(k => Q(k)))})
            VALUES
            ({string.Join(",", data.Keys.Select(k => "@" + k))})";

            await con.ExecuteAsync(sql, ToParameters(data));

            return Ok(OkResponse(new { uid},"Inserted successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insert failed for table {Table}.", table);

            if (formTableWork != null)
            {
                await CleanupSurveyTable(formTableWork,user);
            }

            if (surveyDatabaseWork != null)
            {
                await CleanupSurveyDatabase(surveyDatabaseWork,user);
            }

            return Ok(FailResponse("Insert failed.",new{ ex.Message}));
        }
    }

    private async Task<IActionResult> UpdateInternalAsync(
        string table,
        string uid,
        Dictionary<string, object?> fieldsInput)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return Ok(FailResponse("UID is required."));

        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return Ok(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);
        var form = ToFormCollection(fieldsInput);

        var fields = (await GetFieldsAsync(con, table))
            .Where(f =>
                f.AllowUpdate == true &&
                !string.IsNullOrWhiteSpace(f.ColumnName) &&
                form.Keys.Any(k => k.Equals(f.ColumnName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var errors = await ValidateFormAsync(fields, form, false, con);

        if (errors.Any())
            return Ok(FailResponse("Validation error.", errors));

        var data = BuildData(fields, form, false);
        NormalizeNulls(data);

        data.Remove("Id");
        data.Remove("ID");
        data.Remove("UID");

        var user = CurrentUserName();
        var now = DateTime.Now;

        data["ModifiedTime"] = now;
        data["ModifiedBy"] = user;

        if (fieldsInput.ContainsKey("Remarks"))
            data["Remarks"] = ConvertToStringValue(fieldsInput["Remarks"]);

        if (!data.Any())
            return Ok(FailResponse("Nothing to update."));

        var surveyDatabaseWork = await PrepareSurveyDatabase(entity,uid,fieldsInput);

        if (surveyDatabaseWork.FailureResult != null)
            return surveyDatabaseWork.FailureResult;

        var formTableWork = await PrepareFormTableRename(entity,con,uid,fieldsInput);

        if (formTableWork.FailureResult != null)
            return formTableWork.FailureResult;

        var setClause = string.Join(",", data.Keys.Select(k => $"{Q(k)} = @{k}"));
        data["UID"] = uid;

        var sql = $@"
        UPDATE {tableName}
        SET {setClause}
        WHERE UID = @UID AND IsDeleted = 0";

        try
        {
            var affected = await con.ExecuteAsync(sql, ToParameters(data));

            if (affected == 0)
                return NotFound(FailResponse("Record not found."));

            var renameFailureResult = await RenameSurveyDatabase(
                surveyDatabaseWork,
                tableName,
                con,
                uid,
                user);

            if (renameFailureResult != null)
                return renameFailureResult;

            var formRenameFailureResult = await RenameFormTableAfterUpdate(
                formTableWork,
                tableName,
                con,
                uid,
                user);

            if (formRenameFailureResult != null)
                return formRenameFailureResult;

            return Ok(OkResponse(null, "Updated successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed for table {Table}, UID {UID}.", table, uid);
            return Ok(FailResponse("Update failed.", new { ex.Message }));
        }
    }

    [HttpPost("bulksave")]
    public async Task<IActionResult> SaveBulk(string table, [FromBody] CrudBulkSaveRequest request)
    {
        if (request?.Fields == null || request.Fields.Count == 0)
            return Ok(FailResponse("No rows found."));

        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);

        var insertFields = (await GetFieldsAsync(con, table))
            .Where(f => f.AllowInsert == true)
            .ToList();

        var updateFields = (await GetFieldsAsync(con, table))
            .Where(f => f.AllowUpdate == true)
            .ToList();

        var now = DateTime.Now;
        var currentUser = CurrentUserName();

        var preparedInserts = new List<Dictionary<string, object?>>();
        var preparedUpdates = new List<(string UID, Dictionary<string, object?> Data)>();
        var rowErrors = new List<object>();

        for (var i = 0; i < request.Fields.Count; i++)
        {
            var row = request.Fields[i];

            var uid = row.ContainsKey("UID")
                ? row["UID"]?.ToString()
                : null;

            var isUpdate = !string.IsNullOrWhiteSpace(uid);
            var form = ToFormCollection(row);

            if (isUpdate)
            {
                var errors = await ValidateFormAsync(updateFields, form, false, con);

                if (errors.Any())
                {
                    rowErrors.Add(new { row = i + 1, mode = "Update", errors });
                    continue;
                }

                var data = BuildData(updateFields, form, false);
                NormalizeNulls(data);

                data.Remove("Id");
                data.Remove("ID");
                data.Remove("UID");

                data["ModifiedTime"] = now;
                data["ModifiedBy"] = currentUser;

                if (!data.Any())
                {
                    rowErrors.Add(new { row = i + 1, message = "Nothing to update." });
                    continue;
                }

                preparedUpdates.Add((uid!, data));
            }
            else
            {
                var errors = await ValidateFormAsync(insertFields, form, true, con);

                if (errors.Any())
                {
                    rowErrors.Add(new { row = i + 1, mode = "Insert", errors });
                    continue;
                }

                var data = BuildData(insertFields, form, true);
                NormalizeNulls(data);

                data.Remove("Id");
                data.Remove("ID");

                data["UID"] = Guid.NewGuid().ToString();
                data["CreatedTime"] = now;
                data["CreatedBy"] = currentUser;
                data["IsActive"] = true;
                data["IsDeleted"] = false;

                preparedInserts.Add(data);
            }
        }

        if (rowErrors.Any())
        {
            return Ok(FailResponse(
                "Bulk save failed. No rows were saved.",
                rowErrors));
        }

        await using var transaction = await con.BeginTransactionAsync(HttpContext.RequestAborted);

        try
        {
            var insertedRows = 0;
            var updatedRows = 0;

            foreach (var data in preparedInserts)
            {
                var sql = $@"
                    INSERT INTO {tableName}
                    ({string.Join(",", data.Keys.Select(k => Q(k)))})
                    VALUES
                    ({string.Join(",", data.Keys.Select(k => "@" + k))})";

                insertedRows += await con.ExecuteAsync(sql, ToParameters(data), transaction);
            }

            foreach (var row in preparedUpdates)
            {
                var data = row.Data;
                data["UID"] = row.UID;

                var setClause = string.Join(",",
                    data.Keys
                        .Where(k => !string.Equals(k, "UID", StringComparison.OrdinalIgnoreCase))
                        .Select(k => $"{Q(k)} = @{k}"));

                var sql = $@"
                    UPDATE {tableName}
                    SET {setClause}
                    WHERE UID = @UID AND IsDeleted = 0";

                var affected = await con.ExecuteAsync(sql, ToParameters(data), transaction);

                if (affected == 0)
                    throw new InvalidOperationException($"No matching record found for UID: {row.UID}");

                updatedRows += affected;
            }

            await transaction.CommitAsync(HttpContext.RequestAborted);

            return Ok(OkResponse(new
            {
                totalRows = request.Fields.Count,
                insertedRows,
                updatedRows
            }, "Bulk save completed successfully."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(ex, "Bulk save failed for table {Table}.", table);

            return Ok(FailResponse(
                "Bulk save failed. No rows were saved.",
                new { ex.Message }));
        }
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete(
    [FromQuery] string table,
    [FromQuery] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Ok(FailResponse("UID is required."));

        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid Entity. Request Blocked"));

        var tableName = BuildFullTableName(entity);
        var user = CurrentUserName();

        var formTableDropWork = await PrepareFormTableDrop(
            entity,
            tableName,
            con,
            id);

        if (formTableDropWork.FailureResult != null)
            return formTableDropWork.FailureResult;

        try
        {
            var affected = await con.ExecuteAsync(
                $@"
                UPDATE {tableName}
                SET IsDeleted = 1,
                    DeletedTime = @now,
                    DeletedBy = @user
                WHERE UID = @id
                AND IsDeleted = 0",
                new
                {
                    id,
                    now = DateTime.Now,
                    user
                });

            if (affected == 0)
                return NotFound(FailResponse("Deletion Failed, Record not found."));

            var dropFailureResult = await DropFormTableAfterDelete(
                formTableDropWork,
                user);

            if (dropFailureResult != null)
                return dropFailureResult;

            return Ok(OkResponse(null, "Deleted successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Delete failed for table {Table}, UID {UID}.",
                table,
                id);

            return Ok(FailResponse(
                "Delete failed.",
                new
                {
                    ex.Message
                }));
        }
    }

    // =========================================================
    // File import / upload
    // =========================================================

    [HttpPost("{table}/import")]
    [RequestSizeLimit(100_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(string table, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Ok(FailResponse("No file uploaded."));

        return await ImportOrUploadInternalAsync(table, file, mergeAttributes: null);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(string table, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Ok(FailResponse("No file uploaded."));

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            attributes = Request.Form
                .Where(q => q.Key.StartsWith("fields[", StringComparison.OrdinalIgnoreCase))
                .GroupBy(q => q.Key.Replace("fields[", "").Replace("]", ""))
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
        }

        return await ImportOrUploadInternalAsync(table, file, attributes);
    }

    private async Task<IActionResult> ImportOrUploadInternalAsync(
        string table,
        IFormFile file,
        Dictionary<string, string>? mergeAttributes)
    {
        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);

        var fields = (await GetFieldsAsync(con, table))
            .Where(f => f.AllowInsert == true)
            .ToList();

        if (!fields.Any())
            return Ok(FailResponse("No insertable fields found."));

        var rows = await ReadRowsFromFileAsync(file);

        if (rows.Count == 0)
            return Ok(FailResponse("No rows found in uploaded file."));

        var successCount = 0;
        var errorRows = new List<object>();

        await using var transaction = await con.BeginTransactionAsync(HttpContext.RequestAborted);

        try
        {
            foreach (var row in rows)
            {
                var merged = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase);

                if (mergeAttributes != null)
                {
                    foreach (var a in mergeAttributes)
                    {
                        if (!merged.ContainsKey(a.Key) || string.IsNullOrWhiteSpace(merged[a.Key]))
                            merged[a.Key] = a.Value;
                    }
                }

                var formDict = merged.ToDictionary(
                    k => k.Key,
                    v => new StringValues(v.Value),
                    StringComparer.OrdinalIgnoreCase);

                var form = new FormCollection(formDict);

                var errors = await ValidateFormAsync(fields, form, true, con, transaction);

                if (errors.Any())
                {
                    errorRows.Add(new { row, errors });

                    await transaction.RollbackAsync(HttpContext.RequestAborted);

                    return Ok(FailResponse(
                        "Validation failed. Transaction rolled back.",
                        errorRows));
                }

                var data = BuildData(fields, form, true);
                NormalizeNulls(data);

                if (!data.Any())
                    continue;

                data["UID"] = Guid.NewGuid().ToString();
                data["CreatedTime"] = DateTime.Now;
                data["CreatedBy"] = CurrentUserName();
                data["Remarks"] = row.ContainsKey("Remarks") && !string.IsNullOrWhiteSpace(row["Remarks"])
                    ? row["Remarks"]
                    : null;
                data["IsDeleted"] = false;
                data["IsActive"] = true;

                var sql = $@"
                    INSERT INTO {tableName}
                    ({string.Join(",", data.Keys.Select(k => Q(k)))})
                    VALUES
                    ({string.Join(",", data.Keys.Select(k => "@" + k))})";

                await con.ExecuteAsync(sql, ToParameters(data), transaction);

                successCount++;
            }

            await transaction.CommitAsync(HttpContext.RequestAborted);

            return Ok(OkResponse(new
            {
                insertedRows = successCount
            }, $"Inserted rows: {successCount}"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(ex, "Import/upload failed for table {Table}.", table);

            return Ok(FailResponse(
                "Import failed. Transaction rolled back.",
                new { ex.Message }));
        }
    }

    private static async Task<List<Dictionary<string, string>>> ReadRowsFromFileAsync(IFormFile file)
    {
        var rows = new List<Dictionary<string, string>>();

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension == ".csv")
        {
            using var reader = new StreamReader(stream);

            var headerLine = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(headerLine))
                return rows;

            var headers = headerLine
                .Split(',')
                .Select(h => h.Trim())
                .ToList();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = line.Split(',');
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < headers.Count; i++)
                    dict[headers[i]] = i < values.Length ? values[i].Trim() : string.Empty;

                rows.Add(dict);
            }

            return rows;
        }

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        var headerRow = worksheet.Row(1);
        var headersExcel = headerRow
            .Cells()
            .Select(c => c.Value.ToString().Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headersExcel.Count; i++)
                dict[headersExcel[i]] = row.Cell(i + 1).Value.ToString();

            rows.Add(dict);
        }

        return rows;
    }

    // =========================================================
    // Clean / aggregate
    // =========================================================

    [HttpPost("clean")]
    public async Task<IActionResult> Clean([FromBody] CleanRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Id))
            return Ok(FailResponse("Grid/file facility ID is required."));

        if (request.Fields == null || request.Fields.Count == 0)
            return Ok(FailResponse("No fields provided for deletion."));

        await using var con = await GetConnectionAsync();

        var grid = await con.QueryFirstOrDefaultAsync<FileUploadGridDto>(
            $@"SELECT UID, Title, TableReference, ByColumns, ForColumns
               FROM ERPCORE.dbo.{Constants.FileUploadFacility}
               WHERE UID = @Id AND IsDeleted = 0",
            new { request.Id });

        if (grid == null)
            return NotFound(FailResponse("Unable to refer table."));

        if (!await IsAllowedTableAsync(con, grid.TableReference))
            return Forbid();

        var entity = await ResolveEntityAsync(grid.TableReference);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);
        var metadata = await GetFieldsAsync(con, grid.TableReference);
        var allowedColumns = metadata
            .Where(f => !string.IsNullOrWhiteSpace(f.ColumnName))
            .Select(f => f.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var where = new List<string>();
        var parameters = new DynamicParameters();

        var i = 0;

        foreach (var f in request.Fields)
        {
            if (!allowedColumns.Contains(f.Key))
                return Ok(FailResponse($"Invalid column: {f.Key}"));

            var p = $"p{i++}";
            where.Add($"{Q(f.Key)} = @{p}");
            parameters.Add(p, f.Value);
        }

        if (!IsPrivilegedUser())
        {
            where.Add("(CreatedBy = @user OR ModifiedBy = @user)");
            parameters.Add("user", CurrentUserName());
        }

        where.Add("[IsDeleted] = 0");

        var whereClause = string.Join(" AND ", where);

        var totalRows = await con.ExecuteScalarAsync<int>(
            $@"SELECT COUNT(1) FROM {tableName} WHERE {whereClause}",
            parameters);

        if (totalRows == 0)
            return NotFound(FailResponse("No matching records found."));

        parameters.Add("now", DateTime.Now);
        parameters.Add("deletedBy", CurrentUserName());

        var affected = await con.ExecuteAsync(
            $@"
            UPDATE {tableName}
            SET IsDeleted = 1,
                DeletedTime = @now,
                DeletedBy = @deletedBy
            WHERE {whereClause}",
            parameters);

        return Ok(OkResponse(new { affected }, "Record(s) deleted successfully."));
    }

    [HttpGet("aggregate")]
    public async Task<IActionResult> Aggregate(
        [FromQuery] string table,
        [FromQuery] string column,
        [FromQuery] string function,
        [FromQuery] string? where,
        [FromQuery] string? group,
        [FromQuery] string? having,
        [FromQuery] string? order,
        [FromQuery] bool checkRights = true)
    {
        if (string.IsNullOrWhiteSpace(function))
            return Ok(FailResponse("Aggregate function is required."));

        await using var con = await GetConnectionAsync();

        if (!await IsAllowedTableAsync(con, table))
            return Forbid();

        var entity = await ResolveEntityAsync(table);

        if (entity == null)
            return NotFound(FailResponse("Invalid table mapping."));

        var tableName = BuildFullTableName(entity);
        var fields = await GetFieldsAsync(con, table);
        var allowedColumns = fields
            .Where(f => !string.IsNullOrWhiteSpace(f.ColumnName))
            .Select(f => f.ColumnName)
            .Append("UID")
            .Append("ID")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedFunctions = new[] { "COUNT", "COUNT_DISTINCT", "SUM", "AVG", "MIN", "MAX" };
        function = function.ToUpperInvariant();

        if (!allowedFunctions.Contains(function))
            return Ok(FailResponse("Invalid aggregate function."));

        if (string.IsNullOrWhiteSpace(column))
            return Ok(FailResponse("Column is required."));

        var selectClause = BuildAggregateSelectClause(column, function, allowedColumns);

        if (selectClause == null)
            return Ok(FailResponse("Invalid aggregate column."));

        var whereClause = "T1.[IsDeleted] = @IsDeleted";

        if (checkRights && !IsPrivilegedUser())
        {
            whereClause += " AND (T1.[CreatedBy] = @CurrentUser OR T1.[ModifiedBy] = @CurrentUser)";
        }

        try
        {
            var trustedWhere = BuildTrustedFilter(where);

            if (!string.IsNullOrWhiteSpace(trustedWhere))
                whereClause += " AND (" + trustedWhere + ")";

            if (!string.IsNullOrWhiteSpace(having) && LooksUnsafeSqlFragment(having))
                return Ok(FailResponse("Unsafe HAVING fragment was rejected."));

            if (!string.IsNullOrWhiteSpace(order) && LooksUnsafeSqlFragment(order))
                return Ok(FailResponse("Unsafe ORDER BY fragment was rejected."));
        }
        catch (Exception ex)
        {
            return Ok(FailResponse(ex.Message));
        }

        var groupClause = string.Empty;

        if (!string.IsNullOrWhiteSpace(group))
        {
            var groupColumns = group
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();

            if (groupColumns.Any(g => !allowedColumns.Contains(UnquoteIdentifier(g))))
                return Ok(FailResponse("Invalid group column."));

            var quotedGroupColumns = groupColumns.Select(g => "T1." + Q(g)).ToList();

            selectClause = string.Join(", ", quotedGroupColumns) + ", " + selectClause;
            groupClause = " GROUP BY " + string.Join(", ", quotedGroupColumns);
        }

        var sql = $@"
            SELECT {selectClause}
            FROM {tableName} AS T1
            WHERE {whereClause}
            {groupClause}";

        if (!string.IsNullOrWhiteSpace(having))
            sql += " HAVING " + having.Replace("{alias}", "T1");

        if (!string.IsNullOrWhiteSpace(order))
            sql += " ORDER BY " + order.Replace("{alias}", "T1");

        try
        {
            var data = await con.QueryAsync(sql, new
            {
                IsDeleted = false,
                CurrentUser = CurrentUserName()
            });

            return Ok(OkResponse(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregate failed for table {Table}.", table);
            return Ok(FailResponse("Aggregate failed.", new { ex.Message }));
        }
    }

    private static bool ConvertToBoolValue(object? value)
    {
        if (value == null)
            return false;

        if (value is bool b)
            return b;

        if (value is int i)
            return i == 1;

        if (value is long l)
            return l == 1;

        if (value is string s)
        {
            s = s.Trim();

            if (bool.TryParse(s, out var boolResult))
                return boolResult;

            if (int.TryParse(s, out var intResult))
                return intResult == 1;

            return s.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || s.Equals("on", StringComparison.OrdinalIgnoreCase)
                || s.Equals("active", StringComparison.OrdinalIgnoreCase);
        }

        if (value is System.Text.Json.JsonElement json)
        {
            if (json.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;

            if (json.ValueKind == System.Text.Json.JsonValueKind.False)
                return false;

            if (json.ValueKind == System.Text.Json.JsonValueKind.Number &&
                json.TryGetInt32(out var jsonInt))
                return jsonInt == 1;

            if (json.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = json.GetString();

                if (bool.TryParse(str, out var jsonBool))
                    return jsonBool;

                if (int.TryParse(str, out var jsonStringInt))
                    return jsonStringInt == 1;
            }
        }

        return false;
    }
    private static string? BuildAggregateSelectClause(
        string column,
        string function,
        HashSet<string> allowedColumns)
    {
        column = column.Trim();

        if (function == "COUNT" && column == "*")
            return "COUNT(*) AS [Value]";

        var rawColumn = column.Replace("{alias}.", "").Replace("T1.", "").Trim();

        if (!allowedColumns.Contains(UnquoteIdentifier(rawColumn)))
            return null;

        var qCol = "T1." + Q(rawColumn);

        return function switch
        {
            "COUNT_DISTINCT" => $"COUNT(DISTINCT {qCol}) AS [Value]",
            "COUNT" => $"COUNT({qCol}) AS [Value]",
            "SUM" => $"SUM({qCol}) AS [Value]",
            "AVG" => $"AVG({qCol}) AS [Value]",
            "MIN" => $"MIN({qCol}) AS [Value]",
            "MAX" => $"MAX({qCol}) AS [Value]",
            _ => null
        };
    }


    private async Task<string?> GetSurveyDatabaseNameBySectionAsync(
    DbConnection con,
    string sectionUid)
    {
        if (string.IsNullOrWhiteSpace(sectionUid))
            return null;

        const string sql = @"
        SELECT TOP 1 s.DatabaseName
        FROM [ERPCORE].[dbo].[Section] sec
        INNER JOIN [ERPCORE].[dbo].[Questionnaire] q
            ON q.UID = sec.Questionnaire
            AND ISNULL(q.IsDeleted, 0) = 0
        INNER JOIN [ERPCORE].[dbo].[Survey] s
            ON s.UID = q.Survey
            AND ISNULL(s.IsDeleted, 0) = 0
        WHERE sec.UID = @SectionUid
        AND ISNULL(sec.IsDeleted, 0) = 0;";

        return await con.ExecuteScalarAsync<string?>(
            sql,
            new
            {
                SectionUid = sectionUid
            });
    }

    private async Task<FormInsert> CreateSurveyTable(
    Entity entity,
    DbConnection con,
    IFormCollection form,
    string insertedUid,
    string user)
    {
        var work = new FormInsert
        {
            IsFormTable = IsFormTable(entity),
            FormUid = insertedUid
        };

        if (!work.IsFormTable)
            return work;

        var sectionUid = form["Section"].FirstOrDefault();
        var heading = form["Heading"].FirstOrDefault();
        var surveyTableName = form["TableName"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(sectionUid))
        {
            work.FailureResult = Ok(FailResponse("Section is required to create survey form table."));
            return work;
        }

        if (string.IsNullOrWhiteSpace(surveyTableName))
        {
            work.FailureResult = Ok(FailResponse("TableName is required to create survey form table."));
            return work;
        }

        work.TableName = surveyTableName.Trim();

        work.DatabaseName = await GetSurveyDatabaseNameBySectionAsync(con,sectionUid);

        if (string.IsNullOrWhiteSpace(work.DatabaseName))
        {
            work.FailureResult = Ok(FailResponse("Survey database was not found from selected Section."));
            return work;
        }

        var request = new TableCreateRequest
        {
            UID = insertedUid,
            Table = work.TableName,
            Database = work.DatabaseName,
            TableType = "SUR",
            TableDescription = string.IsNullOrWhiteSpace(heading)
                ? work.TableName
                : heading.Trim()
        };

        work.CreateResult = await _tableService.CreateTableAsync(request, user);

        if (!work.CreateResult.Success)
        {
            work.FailureResult = Ok(FailResponse(
                "Survey form table creation failed.",
                new
                {
                    work.CreateResult.Message
                }));
        }

        return work;
    }

    private async Task<FormUpdate> PrepareFormTableRename(
    Entity entity,
    DbConnection con,
    string uid,
    Dictionary<string, object?> fieldsInput)
    {
        var work = new FormUpdate();

        if (!IsFormTable(entity))
            return work;

        var tableNameInput = fieldsInput.FirstOrDefault(x => string.Equals(x.Key, "TableName", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(tableNameInput.Key))
            return work;

        work.NewTableName = ConvertToStringValue(tableNameInput.Value)?.Trim();

        if (string.IsNullOrWhiteSpace(work.NewTableName))
        {
            work.FailureResult = Ok(FailResponse("Form TableName is required."));
            return work;
        }

        work.OldTableName = await _dbService.GetSurveyTableNameAsync(uid);

        if (string.IsNullOrWhiteSpace(work.OldTableName))
        {
            work.FailureResult = Ok(FailResponse("Existing Form TableName was not found."));
            return work;
        }

        work.OldTableName = work.OldTableName.Trim();

        if (!string.Equals(
                work.OldTableName,
                work.NewTableName,
                StringComparison.OrdinalIgnoreCase))
        {
            work.ShouldRename = true;
        }

        return work;
    }

    private async Task<IActionResult?> RenameFormTableAfterUpdate(
        FormUpdate work,
        string tableName,
        DbConnection con,
        string uid,
        string user)
    {
        if (!work.ShouldRename ||
            string.IsNullOrWhiteSpace(work.OldTableName) ||
            string.IsNullOrWhiteSpace(work.NewTableName))
        {
            return null;
        }

        var renameResult = await _tableService.RenameTableAsync(
            new AlterTableName
            {
                Old = uid,
                New = work.NewTableName
            },
            user);

        if (renameResult.Success)
            return null;

        await RestoreFormTable(
            tableName,
            con,
            uid,
            work.OldTableName,
            user);

        return Ok(FailResponse(
            "Form physical table rename failed.",
            new
            {
                renameResult.Message
            }));
    }

    private async Task<FormDelete> PrepareFormTableDrop(
    Entity entity,
    string tableName,
    DbConnection con,
    string uid)
    {
        var work = new FormDelete
        {
            FormUid = uid
        };

        if (!IsFormTable(entity))
            return work;

        work.TableName = await _dbService.GetSurveyTableNameAsync(uid);

        if (string.IsNullOrWhiteSpace(work.TableName))
        {
            work.FailureResult = Ok(FailResponse("Existing Form TableName was not found."));
            return work;
        }

        var entityExists = await con.ExecuteScalarAsync<int>(
            $@"
            SELECT COUNT(1)
            FROM {Constants.EntityTable}
            WHERE UID = @UID
            AND ISNULL(IsDeleted, 0) = 0",
            new
            {
                UID = uid
            });

        if (entityExists == 0)
        {
            // Old form record or no physical table was created.
            // Normal delete can continue.
            return work;
        }

        work.ShouldDrop = true;
        work.TableName = work.TableName.Trim();

        return work;
    }

    private async Task<IActionResult?> DropFormTableAfterDelete(
    FormDelete work,
    string user)
    {
        if (!work.ShouldDrop || string.IsNullOrWhiteSpace(work.FormUid))
            return null;

        var dropResult = await _tableService.DropTableAsync(
            new DropTableRequest
            {
                Table = work.FormUid
            },
            user);

        if (dropResult.Success)
            return null;

        return Ok(FailResponse(
            "Form physical table drop failed.",
            new
            {
                dropResult.Message
            }));
    }

    private async Task RestoreFormTable(
    string tableName,
    DbConnection con,
    string uid,
    string oldTableName,
    string user)
    {
        try
        {
            var restoreSql = $@"
            UPDATE {tableName}
            SET {Q("TableName")} = @OldTableName,
                {Q("ModifiedTime")} = @ModifiedTime,
                {Q("ModifiedBy")} = @ModifiedBy
            WHERE UID = @UID
            AND IsDeleted = 0";

            await con.ExecuteAsync(
                restoreSql,
                new
                {
                    OldTableName = oldTableName,
                    ModifiedTime = DateTime.Now,
                    ModifiedBy = user,
                    UID = uid
                });
        }
        catch (Exception restoreEx)
        {
            _logger.LogError(
                restoreEx,
                "Failed to restore Form TableName after physical table rename failure. UID {UID}.",
                uid);
        }
    }

    private async Task CleanupSurveyTable(FormInsert work,string user)
    {
        if (!work.IsFormTable ||
            work.CreateResult?.Success != true ||
            string.IsNullOrWhiteSpace(work.FormUid))
        {
            return;
        }

        var request = new DropTableRequest
        {
            Table = work.FormUid,
        };

        var cleanupResult = await _tableService.DropTableAsync(request, user);

        if (!cleanupResult.Success)
        {
            _logger.LogError(
                "Survey form table cleanup failed after Form insert failure. Form UID: {FormUid}. Table: {Table}. Error: {Error}",
                work.FormUid,
                work.TableName,
                cleanupResult.Message);
        }
    }

    private async Task<SurveyInsert> CreateSurveyDatabase(
    Entity entity,
    IFormCollection form,
    string user)
    {
        var work = new SurveyInsert
        {
            IsSurveyTable = IsSurveyTable(entity)
        };

        if (!work.IsSurveyTable)
            return work;

        work.DatabaseName = form["DatabaseName"].FirstOrDefault();

        work.CreateResult = await _dbService.CreateDatabaseAsync(
            work.DatabaseName,
            user);

        if (!work.CreateResult.Success)
        {
            work.FailureResult = Ok(FailResponse(
                "Database creation failed.",
                new
                {
                    work.CreateResult.Message
                }));
        }

        return work;
    }

    private async Task CleanupSurveyDatabase(
        SurveyInsert work,
        string user)
    {
        if (!work.IsSurveyTable ||
            work.CreateResult?.Success != true ||
            string.IsNullOrWhiteSpace(work.DatabaseName))
        {
            return;
        }

        var cleanupResult = await _dbService.DropDatabaseAsync(
            work.DatabaseName,
            user);

        if (!cleanupResult.Success)
        {
            _logger.LogError(
                "Survey database cleanup failed after insert failure. Database: {Database}. Error: {Error}",
                work.DatabaseName,
                cleanupResult.Message);
        }
    }

    private async Task<SurveyUpdate> PrepareSurveyDatabase(
        Entity entity,
        string uid,
        Dictionary<string, object?> fieldsInput)
    {
        var work = new SurveyUpdate();

        if (!IsSurveyTable(entity))
            return work;

        var databaseNameInput = fieldsInput.FirstOrDefault(x =>
            string.Equals(x.Key, "DatabaseName", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(databaseNameInput.Key))
            return work;

        work.ShouldCheckRename = true;
        work.NewDatabaseName = ConvertToStringValue(databaseNameInput.Value)?.Trim();

        if (string.IsNullOrWhiteSpace(work.NewDatabaseName))
        {
            work.FailureResult = Ok(FailResponse("Survey DatabaseName is required."));
            return work;
        }

        work.OldDatabaseName = await _dbService.GetSurveyDatabaseNameAsync(uid);

        if (string.IsNullOrWhiteSpace(work.OldDatabaseName))
        {
            work.FailureResult = Ok(FailResponse("Existing survey database name was not found."));
            return work;
        }

        work.OldDatabaseName = work.OldDatabaseName.Trim();

        return work;
    }

    private async Task<IActionResult?> RenameSurveyDatabase(
        SurveyUpdate work,
        string tableName,
        DbConnection con,
        string uid,
        string user)
    {
        if (!work.ShouldCheckRename ||
            string.IsNullOrWhiteSpace(work.OldDatabaseName) ||
            string.IsNullOrWhiteSpace(work.NewDatabaseName) ||
            string.Equals(work.OldDatabaseName, work.NewDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var renameResult = await _dbService.RenameDatabaseAsync(
            work.OldDatabaseName,
            work.NewDatabaseName,
            user);

        if (renameResult.Success)
            return null;

        await RestoreSurveyDatabase(
            tableName,
            con,
            uid,
            work.OldDatabaseName,
            user);

        return Ok(FailResponse(
            "Database rename failed.",
            new
            {
                renameResult.Message
            }));
    }

    private async Task RestoreSurveyDatabase(
        string tableName,
        DbConnection con,
        string uid,
        string oldDatabaseName,
        string user)
    {
        try
        {
            var restoreSql = $@"
        UPDATE {tableName}
        SET {Q("DatabaseName")} = @OldDatabaseName,
            {Q("ModifiedTime")} = @ModifiedTime,
            {Q("ModifiedBy")} = @ModifiedBy
        WHERE UID = @UID AND IsDeleted = 0";

            await con.ExecuteAsync(
                restoreSql,
                new
                {
                    OldDatabaseName = oldDatabaseName,
                    ModifiedTime = DateTime.Now,
                    ModifiedBy = user,
                    UID = uid
                });
        }
        catch (Exception restoreEx)
        {
            _logger.LogError(
                restoreEx,
                "Failed to restore Survey DatabaseName after database rename failure. UID {UID}.",
                uid);
        }


    }
    private sealed class SurveyInsert
    {
        public bool IsSurveyTable { get; set; }
        public string? DatabaseName { get; set; }
        public ServiceResult? CreateResult { get; set; }
        public IActionResult? FailureResult { get; set; }
    }

    private sealed class SurveyUpdate
    {
        public bool ShouldCheckRename { get; set; }
        public string? OldDatabaseName { get; set; }
        public string? NewDatabaseName { get; set; }
        public IActionResult? FailureResult { get; set; }
    }

    private sealed class FormInsert
    {
        public bool IsFormTable { get; set; }
        public string? FormUid { get; set; }
        public string? TableName { get; set; }
        public string? DatabaseName { get; set; }
        public ServiceResult? CreateResult { get; set; }
        public IActionResult? FailureResult { get; set; }
    }

    private sealed class FormUpdate
    {
        public bool ShouldRename { get; set; }
        public string? OldTableName { get; set; }
        public string? NewTableName { get; set; }
        public IActionResult? FailureResult { get; set; }
    }

    private sealed class FormDelete
    {
        public bool ShouldDrop { get; set; }
        public string? FormUid { get; set; }
        public string? TableName { get; set; }
        public IActionResult? FailureResult { get; set; }
    }
}
