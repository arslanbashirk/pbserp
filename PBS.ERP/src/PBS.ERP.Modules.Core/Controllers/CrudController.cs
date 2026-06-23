using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Modules.Core.Services;
using PBS.ERP.Shared;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;

namespace PBS.ERP.Modules.Core.Controllers;

/// <summary>
/// MVC/Razor controller only.
/// Keep JWT away from view actions. MVC users authenticate through Identity cookie.
/// All API/data endpoints are in PBS.ERP.Modules.Api.Controllers.CrudApiController.
/// </summary>
[Authorize(AuthenticationSchemes = Constants.Identity_Application_Scheme)]
[Route("Crud")]
public class CrudController : Controller
{
    protected readonly ApplicationDbContext _context;
    protected readonly IConfiguration _configuration;
    protected readonly IDatabaseService _databaseService;

    public CrudController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IDatabaseService databaseService,
        ISuperInterface tableService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    protected async Task<DbConnection> GetConnectionAsync()
    {
        var con = _context.Database.GetDbConnection();

        if (string.IsNullOrWhiteSpace(con.ConnectionString))
        {
            con.ConnectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        if (con.State != ConnectionState.Open)
            await con.OpenAsync();

        return con;
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

    [HttpGet("Table")]
    public virtual async Task<IActionResult> Table(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return BadRequest("Table UID is required.");

        var entity = await _context.Entities
            .Where(e => e.UID == table && !e.IsDeleted)
            .FirstOrDefaultAsync();

        if (entity == null)
            return NotFound("Table/entity not found.");

        ViewBag.Table = entity.Name;
        ViewBag.UID = entity.UID;
        ViewBag.Path = "crud";

        var preFilledData = Request.Query
            .Where(q => !string.Equals(q.Key, "table", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                q => q.Key,
                q => (object)q.Value.ToString()
            );

        ViewBag.PreFilledData = preFilledData;

        return View(entity);
    }

    [HttpGet("Grid")]
    public async Task<IActionResult> Grid(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Grid/File facility ID is required.");

        await using var con = await GetConnectionAsync();

        var grid = await con.QueryFirstOrDefaultAsync<FileUploadGrid>(
            $@"SELECT UID, TableReference, ByColumns
               FROM ERPCORE.dbo.{Constants.FileUploadFacility}
               WHERE UID = @Id AND IsDeleted = 0",
            new { Id = id });

        if (grid == null)
            return View("Grid", new List<Dictionary<string, object>>());

        var entity = await _context.Entities
            .Where(m => m.UID == grid.TableReference)
            .FirstOrDefaultAsync();

        if (entity == null)
            return View("Grid", new List<Dictionary<string, object>>());

        var columns = !string.IsNullOrWhiteSpace(grid.ByColumns)
            ? grid.ByColumns
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToArray()
            : Array.Empty<string>();

        var tableName = $"[{entity.Database}].[{entity.Schema}].[{entity.Name}]";

        ViewBag.Table = id;
        ViewBag.Name = $"{entity.Name} ({entity.Description})";

        if (!await IsAllowedTableAsync(con, grid.TableReference))
            return View("Grid", new List<Dictionary<string, object>>());

        var metadata = (await GetFieldsAsync(con, grid.TableReference))
            .Where(f => columns.Contains(f.ColumnName))
            .ToList();

        if (!metadata.Any())
            return View("Grid", new List<Dictionary<string, object>>());

        var cteList = new List<string>();
        var cteNames = new List<string>();
        var joins = new List<string>();
        var selectColumns = new List<string>();

        var cteIndex = 1;
        var aliasCounter = 2;

        var fkGroups = metadata
            .Where(f => f.IsForeignKey == true && !string.IsNullOrWhiteSpace(f.DropdownSourceTable))
            .GroupBy(f => f.DropdownSourceTable)
            .ToList();

        foreach (var group in fkGroups)
        {
            var firstField = group.First();

            var sourceTable = await _context.Entities
                .Where(e => e.UID == group.Key)
                .Select(e => $"[{e.Database}].[{e.Schema}].[{e.Name}]")
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(sourceTable))
                continue;

            var alias = $"T{aliasCounter++}";
            var cteName = $"CTE{cteIndex++}";

            var wh = "";

            if (!string.IsNullOrWhiteSpace(firstField.DropdownWhere))
                wh = "AND " + firstField.DropdownWhere.Replace("{alias}.", "");

            cteList.Add($@"
                {cteName} AS (
                    SELECT DISTINCT
                        CAST([{firstField.DropdownValueColumn}] AS VARCHAR(50)) AS [{firstField.ColumnName}]
                    FROM {sourceTable}
                    WHERE IsDeleted = 0 {wh}
                )");

            cteNames.Add(cteName);

            foreach (var field in group)
            {
                var joinCondition =
                    $@"CAST(Cartesian.[{field.ColumnName}] AS VARCHAR(50)) = CAST({alias}.[{field.DropdownValueColumn}] AS VARCHAR(50))";

                joins.Add($"LEFT JOIN {sourceTable} {alias} ON {joinCondition}");

                var textCols = field.DropdownTextColumn
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();

                string CastCol(string col) => $"CAST({alias}.[{col}] AS VARCHAR(100))";

                var textExpression = textCols.Count switch
                {
                    1 => CastCol(textCols[0]),
                    2 => $"{CastCol(textCols[0])} + ' (' + {CastCol(textCols[1])} + ')'",
                    _ => $"{CastCol(textCols[0])} + ' (' + {string.Join(" + ' ' + ", textCols.Skip(1).Select(CastCol))} + ')'"
                };

                selectColumns.Add($"{textExpression} AS [{field.ColumnName}_Text]");
            }
        }

        foreach (var field in metadata.Where(f => f.IsForeignKey != true))
        {
            var cteName = $"CTE{cteIndex++}";

            cteList.Add($@"
                {cteName} AS (
                    SELECT DISTINCT
                        CAST([{field.ColumnName}] AS VARCHAR(50)) AS [{field.ColumnName}]
                    FROM {tableName}
                    WHERE [{field.ColumnName}] IS NOT NULL
                      AND IsDeleted = 0
                )");

            cteNames.Add(cteName);
        }

        if (!cteNames.Any())
            return View("Grid", new List<Dictionary<string, object>>());

        var cartesianSelect = string.Join(" CROSS JOIN ", cteNames);

        var sql = $@"
            ;WITH {string.Join(",\n", cteList)}
            , Cartesian AS (
                SELECT * FROM {cartesianSelect}
            )
            SELECT
                Cartesian.*,
                ISNULL(Agg.TotalRows, 0) AS TotalRows,
                Agg.CreatedTime,
                Agg.CreatedBy
                {(selectColumns.Any() ? "," : "")}
                {string.Join(",\n", selectColumns)}
            FROM Cartesian
            OUTER APPLY (
                SELECT
                    COUNT(1) AS TotalRows,
                    MAX(T.CreatedTime) AS CreatedTime,
                    MAX(T.CreatedBy) AS CreatedBy
                FROM {tableName} T
                WHERE T.IsDeleted = 0
                  AND {string.Join(" AND ", metadata.Select(f =>
                        $"CAST(T.[{f.ColumnName}] AS VARCHAR(50)) = CAST(Cartesian.[{f.ColumnName}] AS VARCHAR(50))"
                    ))}
            ) Agg
            {string.Join("\n", joins)}";

        try
        {
            var data = await con.QueryAsync(sql);

            var model = data
                .Select(r => (IDictionary<string, object>)r)
                .Select(d => new Dictionary<string, object>(d))
                .ToList();

            return View("Grid", model);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View("Grid", new List<Dictionary<string, object>>());
        }
    }

    [HttpPost("File")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> File()
    {
        var model = new FileTableView
        {
            fields = new Dictionary<string, string>(),
            Reference = new Dictionary<string, string>(),
            Entity = new Entity(),
            Grid = new FileUploadGrid()
        };

        if (!Request.HasFormContentType)
            return View(model);

        var form = Request.Form;

        model.fields = form
            .Where(q => q.Key != "id" && !q.Key.EndsWith("_Text"))
            .ToDictionary(
                q => q.Key,
                q => q.Value.ToString()
            );

        model.Reference = form
            .Where(q => q.Key.EndsWith("_Text"))
            .ToDictionary(
                q => q.Key,
                q => q.Value.ToString()
            );

        var id = form["id"].ToString();

        if (string.IsNullOrWhiteSpace(id))
            return View(model);

        await using var con = await GetConnectionAsync();

        var grid = await con.QueryFirstOrDefaultAsync<FileUploadGrid>(
            $@"SELECT UID, Title, TableReference, ByColumns, ForColumns,
                      ColumnTypes, LockedColumns, FormatTable, FormatColumns, FormatFilter
               FROM ERPCORE.dbo.{Constants.FileUploadFacility}
               WHERE UID = @Id AND IsDeleted = 0",
            new { Id = id });

        if (grid == null)
            return View(model);

        model.Title = grid.Title;
        model.Columns = grid.ForColumns;
        model.Grid = grid;

        var entity = await _context.Entities
            .Where(m => m.UID == grid.TableReference)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            ViewBag.Error = "Entity not found for TableReference: " + grid.TableReference;
            model.Entity = new Entity();
        }
        else
        {
            model.Entity = entity;
        }

        model.FormatTable = grid.FormatTable;
        model.FormatColumns = grid.FormatColumns;
        model.FormatFilter = grid.FormatFilter;

        return View(model);
    }
}
