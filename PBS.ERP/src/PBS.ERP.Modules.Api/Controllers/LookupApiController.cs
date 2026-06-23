using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace PBS.ERP.Modules.Api.Controllers;

[ApiController]
[Route("api/lookup")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public sealed class LookupApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionService _connectionService;
    private readonly ILogger<LookupApiController> _logger;

    public LookupApiController(
        ApplicationDbContext context,
        IConnectionService connectionService,
        ILogger<LookupApiController> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task<DbConnection> GetOpenConnectionAsync()
    {
        var con = _context.Database.GetDbConnection();

        if (con.State != ConnectionState.Open)
            await con.OpenAsync(HttpContext.RequestAborted);

        return con;
    }

    // =========================================================
    // GET: /api/lookup/get
    // Compatible with existing JS:
    // /Api/Lookup/Get?table=...&valueCol=...&textCol=...&where=...&order=...
    // =========================================================

    [HttpGet("get")]
    public async Task<IActionResult> Get(
    [FromQuery] string table,
    [FromQuery] string valueCol,
    [FromQuery] string? textCol = null,
    [FromQuery] string? where = null,
    [FromQuery] string? order = null)
    {
        if (string.IsNullOrWhiteSpace(table))
            return Ok(new { Success = false, Message = "Table is required." });

        if (string.IsNullOrWhiteSpace(valueCol))
            return Ok(new { Success = false, Message = "Value column is required." });

        // =====================================================
        // SYSTEM ENTITY LOOKUP
        // Keeps your old functionality exactly:
        // Connection, Database, IP, Schema
        // =====================================================
        if (string.Equals(table, Constants.systemEntity.UID, StringComparison.OrdinalIgnoreCase))
        {
            var connections = _connectionService.GetAllDatabases();

            List<LookupItem> result;

            if (valueCol.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                result = connections
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => new LookupItem
                    {
                        value = c.Name,
                        text = c.Name
                    })
                    .DistinctBy(x => x.value)
                    .ToList();
            }
            else if (valueCol.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                result = connections
                    .Where(c => !string.IsNullOrWhiteSpace(c.DatabaseName))
                    .Select(c => new LookupItem
                    {
                        value = c.DatabaseName,
                        text = c.DatabaseName
                    })
                    .DistinctBy(x => x.value)
                    .ToList();
            }
            else if (valueCol.Equals("IP", StringComparison.OrdinalIgnoreCase))
            {
                result = connections
                    .Where(c => !string.IsNullOrWhiteSpace(c.Server))
                    .Select(c => new LookupItem
                    {
                        value = c.Server,
                        text = c.Server
                    })
                    .DistinctBy(x => x.value)
                    .ToList();
            }
            else if (valueCol.Equals("Schema", StringComparison.OrdinalIgnoreCase))
            {
                result = connections
                    .Where(c => !string.IsNullOrWhiteSpace(c.Schema))
                    .Select(c => new LookupItem
                    {
                        value = c.Schema,
                        text = c.Schema
                    })
                    .DistinctBy(x => x.value)
                    .ToList();
            }
            else
            {
                return Ok(new
                {
                    Success = false,
                    Message = "Invalid valueCol for system entity."
                });
            }

            return Ok(new { Success = true, Message = "Successfull", Data = result });
        }

        // =====================================================
        // NORMAL ENTITY LOOKUP
        // =====================================================
        if (!IsSimpleIdentifier(valueCol))
            return Ok(new { Success = false, Message = "Invalid value column." });

        if (LooksUnsafeSqlFragment(textCol) ||
            LooksUnsafeSqlFragment(where) ||
            LooksUnsafeSqlFragment(order))
        {
            return Ok(new
            {
                Success = false,
                Message = "Unsafe SQL fragment rejected."
            });
        }

        await using var con = await GetOpenConnectionAsync();

        var entity = await _context.Entities
            .Where(m => m.UID == table)
            .Select(m => new
            {
                m.Database,
                m.Schema,
                m.Name
            })
            .FirstOrDefaultAsync();

        if (entity == null ||
            string.IsNullOrWhiteSpace(entity.Database) ||
            string.IsNullOrWhiteSpace(entity.Schema) ||
            string.IsNullOrWhiteSpace(entity.Name))
        {
            return Ok(new { Success = false, Message = "Invalid table." });
        }

        var sourceTable =
            $"{Q(entity.Database)}.{Q(entity.Schema)}.{Q(entity.Name)}";

        string sql = string.Empty;

        try
        {
            textCol = string.IsNullOrWhiteSpace(textCol)
                ? valueCol
                : textCol;

            var textExpression = BuildLookupTextExpression(textCol, "T1");

            var whereClause = BuildLookupWhereClause(where);
            var orderClause = BuildLookupOrderClause(order);

            sql = $@"
                SELECT DISTINCT
                    CONVERT(NVARCHAR(4000), T1.{Q(valueCol)}) AS [value],
                    {textExpression} AS [text]
                FROM {sourceTable} AS T1
                {whereClause}
                {orderClause};";

            var data = (await con.QueryAsync<LookupItem>(sql)).ToList();

            return Ok(new {Success = true,Message ="Succesfull",Data=data});
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lookup get failed. Table={Table}, ValueCol={ValueCol}, SQL={Sql}",
                table,
                valueCol,
                sql);

            return StatusCode(500, new
            {
                Success = false,
                ex.Message
            });
        }
    }

    // =========================================================
    // GET: /api/lookup/options
    // Recommended endpoint when you know Entity UID + Field name.
    // It reads dropdown metadata from Field table.
    // =========================================================

    [HttpGet("options")]
    public async Task<IActionResult> Options(
        [FromQuery] string table,
        [FromQuery] string field)
    {
        if (string.IsNullOrWhiteSpace(table))
            return Ok(new { Success = false, Message = "Table is required." });

        if (string.IsNullOrWhiteSpace(field))
            return Ok(new { Success = false, Message = "Field is required." });

        var column = await _context.Fields
            .AsNoTracking()
            .Where(m =>
                m.Entity == table &&
                m.ColumnName == field &&
                m.IsDeleted == false)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (column == null)
            return Ok(new { Success = false, Message = "Invalid field metadata." });

        if (string.IsNullOrWhiteSpace(column.DropdownValueColumn))
            return Ok(new { Success = false, Message = "Dropdown value column is missing." });

        var sourceTableUid = !string.IsNullOrWhiteSpace(column.DropdownSourceTable)
            ? column.DropdownSourceTable
            : table;

        if (string.Equals(sourceTableUid, Constants.systemEntity.UID, StringComparison.OrdinalIgnoreCase))
        {
            var systemResult = GetSystemEntityLookup(column.DropdownValueColumn);

            if (systemResult == null)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "Invalid dropdown value column for system entity."
                });
            }

            return Ok(new { Success = true, Message = "Successful", Data = systemResult });
        }

        if (!IsSimpleIdentifier(column.DropdownValueColumn))
            return Ok(new { Success = false, Message = "Invalid dropdown value column." });

        if (LooksUnsafeSqlFragment(column.DropdownTextColumn) ||
            LooksUnsafeSqlFragment(column.DropdownWhere) ||
            LooksUnsafeSqlFragment(column.DropdownOrderBy))
        {
            return Ok(new
            {
                Success = false,
                Message = "Unsafe dropdown metadata rejected."
            });
        }

        var sourceTable = await ResolveEntityTableNameAsync(sourceTableUid);

        if (string.IsNullOrWhiteSpace(sourceTable))
            return Ok(new { Success = false, Message = "Invalid dropdown source table." });

        try
        {
            var valueCol = column.DropdownValueColumn;
            var textCol = string.IsNullOrWhiteSpace(column.DropdownTextColumn)
                ? valueCol
                : column.DropdownTextColumn;

            var textExpression = BuildTextExpression(textCol, "T1");
            var whereClause = BuildWhereClause(column.DropdownWhere);
            var orderClause = BuildOrderClause(column.DropdownOrderBy, valueCol);

            await using var con = await GetOpenConnectionAsync();

            var sql = $@"
                SELECT DISTINCT
                    CONVERT(NVARCHAR(4000), T1.{Q(valueCol)}) AS [value],
                    {textExpression} AS [text]
                FROM {sourceTable} AS T1
                {whereClause}
                {orderClause};";

            var data = (await con.QueryAsync<LookupItem>(sql)).ToList();

            return Ok(new { Success = true, Message = "Successfull", Data = data });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lookup options failed. Table={Table}, Field={Field}",
                table,
                field);

            return StatusCode(500, new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // =========================================================
    // System entity lookup
    // =========================================================

    private List<LookupItem>? GetSystemEntityLookup(string valueCol)
    {
        var connections = _connectionService.GetAllDatabases();

        if (valueCol.Equals("Connection", StringComparison.OrdinalIgnoreCase))
        {
            return connections
                .Select(c => new LookupItem
                {
                    value = c.Name,
                    text = c.Name
                })
                .ToList();
        }

        if (valueCol.Equals("Database", StringComparison.OrdinalIgnoreCase))
        {
            return connections
                .Where(c => !string.IsNullOrWhiteSpace(c.DatabaseName))
                .Select(c => new LookupItem
                {
                    value = c.DatabaseName,
                    text = c.DatabaseName
                })
                .DistinctBy(x => x.value)
                .ToList();
        }

        if (valueCol.Equals("IP", StringComparison.OrdinalIgnoreCase))
        {
            return connections
                .Where(c => !string.IsNullOrWhiteSpace(c.Server))
                .Select(c => new LookupItem
                {
                    value = c.Server,
                    text = c.Server
                })
                .DistinctBy(x => x.value)
                .ToList();
        }

        if (valueCol.Equals("Schema", StringComparison.OrdinalIgnoreCase))
        {
            return connections
                .Where(c => !string.IsNullOrWhiteSpace(c.Schema))
                .Select(c => new LookupItem
                {
                    value = c.Schema,
                    text = c.Schema
                })
                .DistinctBy(x => x.value)
                .ToList();
        }

        return null;
    }

    // =========================================================
    // Entity/table helpers
    // =========================================================

    private async Task<string?> ResolveEntityTableNameAsync(string entityUid)
    {
        return await _context.Entities
            .AsNoTracking()
            .Where(m => m.UID == entityUid && m.IsDeleted == false)
            .Select(m => FullTableName(m.Database, m.Schema, m.Name))
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
    }

    private static string FullTableName(string database, string schema, string table)
    {
        return $"{Q(database)}.{Q(schema)}.{Q(table)}";
    }




    // =========================================================
    // SQL expression helpers
    // =========================================================

    private static string BuildWhereClause(string? where)
    {
        if (string.IsNullOrWhiteSpace(where))
            return "WHERE T1.[IsDeleted] = 0";

        where = where.Replace("{alias}.", "T1.");
        where = where.Replace("{alias}", "T1");

        return $"WHERE ({where}) AND T1.[IsDeleted] = 0";
    }

    private static string BuildOrderClause(string? order, string valueCol)
    {
        if (string.IsNullOrWhiteSpace(order))
            return $"ORDER BY T1.{Q(valueCol)}";

        order = order.Replace("{alias}.", "T1.");
        order = order.Replace("{alias}", "T1");

        return $"ORDER BY {order}";
    }

    private static string BuildTextExpression(string textCol, string alias)
    {
        var columns = SplitTopLevelCommas(textCol)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (columns.Count == 0)
            return "NULL";

        var parts = columns
            .Select(c => BuildSqlTextPart(c, alias))
            .ToList();

        if (parts.Count == 1)
            return parts[0];

        if (parts.Count == 2)
            return $"CONCAT({parts[0]}, N' (', {parts[1]}, N')')";

        return $"CONCAT({string.Join(", N' - ', ", parts)})";
    }

    private static string BuildSqlTextPart(string textPart, string alias)
    {
        var expr = textPart.Trim();

        expr = expr.Replace("{alias}.", alias + ".");
        expr = expr.Replace("{alias}", alias);

        if (IsSimpleIdentifier(expr))
            return $"CONVERT(NVARCHAR(4000), {alias}.{Q(expr)})";

        if (ContainsSelectKeyword(expr) && !expr.TrimStart().StartsWith("("))
            return $"CONVERT(NVARCHAR(4000), ({expr}))";

        return $"CONVERT(NVARCHAR(4000), {expr})";
    }

    private static bool ContainsSelectKeyword(string text)
    {
        return Regex.IsMatch(
            text,
            @"\bselect\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

    private static string Q(string name)
    {
        return "[" + name.Replace("]", "]]") + "]";
    }

    private static bool IsSimpleIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && System.Text.RegularExpressions.Regex.IsMatch(
                value,
                @"^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private static bool LooksUnsafeSqlFragment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var s = value.Trim();

        return System.Text.RegularExpressions.Regex.IsMatch(
            s,
            @"(;|--|/\*|\*/|\b(exec|execute|drop|insert|update|delete|alter|create|truncate|merge|grant|revoke|xp_)\b)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string NormalizeLookupSqlFragment(string fragment)
    {
        return fragment
            .Replace("{alias}.", "T1.", StringComparison.OrdinalIgnoreCase)
            .Replace("{alias}", "T1", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLookupWhereClause(string? where)
    {
        if (string.IsNullOrWhiteSpace(where))
            return "WHERE T1.IsDeleted = 0";

        var safeWhere = NormalizeLookupSqlFragment(where.Trim());

        if (safeWhere.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
            safeWhere = safeWhere.Substring(6).Trim();

        return $"WHERE ({safeWhere}) AND T1.IsDeleted = 0";
    }

    private static string BuildLookupOrderClause(string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return string.Empty;

        var safeOrder = NormalizeLookupSqlFragment(order.Trim());

        if (safeOrder.StartsWith("ORDER BY ", StringComparison.OrdinalIgnoreCase))
            safeOrder = safeOrder.Substring(9).Trim();

        return $"ORDER BY {safeOrder}";
    }

    private static string BuildLookupTextExpression(string textCol, string alias)
    {
        var columns = textCol
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (columns.Count == 0)
            return "''";

        string CastIfNeeded(string col)
        {
            col = NormalizeLookupSqlFragment(col.Trim());

            bool isExpression =
                col.Contains(" ") ||
                col.Contains("(") ||
                col.Contains(")") ||
                col.Contains("+") ||
                col.Contains("'") ||
                col.Contains(".") ||
                col.Contains("select", StringComparison.OrdinalIgnoreCase);

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
}

public sealed class LookupItem
{
    public string value { get; set; } = "";
    public string text { get; set; } = "";
}