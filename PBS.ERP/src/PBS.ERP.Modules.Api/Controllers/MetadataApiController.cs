using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;

namespace PBS.ERP.Modules.Api.Controllers;

[ApiController]
[Route("api/metadata")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public sealed class MetadataApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionService _connectionService;
    private readonly ILogger<MetadataApiController> _logger;

    public MetadataApiController(
        ApplicationDbContext context,
        IConnectionService connectionService,
        ILogger<MetadataApiController> logger)
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
    // GET: /api/metadata/get?table={entityUid}
    // Existing JS compatible:
    // /Api/Metadata/Get?table=...
    // =========================================================

    [HttpGet("get")]
    public async Task<IActionResult> Get([FromQuery] string table)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return Ok(new
            {
                Success = false,
                Message = "Table is required.",
            });
        }

        try
        {
            await using var con = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT *
                FROM Field
                WHERE Entity = @table
                  AND ISNULL(IsDeleted, 0) = 0
                ORDER BY ISNULL(SectionNumber, 9),
                         ISNULL(SortOrder, Id);";

            var data = (await con.QueryAsync<Field>(
                sql,
                new { table }
            )).ToList();

            return Ok(new
            {
                Success = true,
                Message = "Successful",
                Data = data,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metadata for table {Table}.", table);

            return Ok(new
            {
                Success = false,
                ex.Message,
            });
        }
    }

    // =========================================================
    // GET: /api/metadata/connections
    // =========================================================

    [HttpGet("connections")]
    public IActionResult GetConnections()
    {
        try
        {
            var connectionNames = _connectionService.GetConnectionNames();

            return Ok(new
            {
                error = false,
                message = "Successful",
                data = connectionNames
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connection names.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Success = false,
                Message = "Error: " + ex.Message,
            });
        }
    }

    // =========================================================
    // GET: /api/metadata/tables
    // GET: /api/metadata/tables?database=ERPCORE
    // GET: /api/metadata/tables?uids=uid1&uids=uid2
    // =========================================================

    [HttpGet("tables")]
    public async Task<IActionResult> GetAllTables(
        [FromQuery] List<string>? uids = null,
        [FromQuery] string? database = null)
    {
        try
        {
            await using var con = await GetOpenConnectionAsync();

            var sql = @"
                SELECT UID, [Database], [Schema], [Name]
                FROM Entity
                WHERE ISNULL(IsDeleted, 0) = 0";

            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(database))
            {
                sql += " AND [Database] = @Database";
                parameters.Add("@Database", database);
            }

            if (uids != null && uids.Count > 0)
            {
                sql += " AND UID IN @Uids";
                parameters.Add("@Uids", uids);
            }

            sql += " ORDER BY [Database], [Name];";

            var tables = (await con.QueryAsync<DatabaseTables>(
                sql,
                parameters
            )).ToList();

            if (IsPrivilegedUser())
            {
                tables.Add(Constants.systemEntity);
            }

            return Ok(new
            {
                Success = true,
                Message = "Successful",
                Data = tables
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metadata tables.");

            return Ok(new
            {
                Success = false,
                Message = "Error: " + ex.Message,
            });
        }
    }

    // =========================================================
    // GET: /api/metadata/columns?table={entityUid}
    // =========================================================

    [HttpGet("columns")]
    public async Task<IActionResult> GetTableColumns([FromQuery] string table)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return Ok(new
            {
                Success = false,
                Message = "Table is required.",
            });
        }

        if (Constants.systemEntity.UID.Equals(table, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                Sucess = true,
                Message = "Successful",
                Data = Constants.systemFields
            });
        }

        try
        {
            await using var con = await GetOpenConnectionAsync();

            var entity = await con.QueryFirstOrDefaultAsync<DatabaseTables>(
                @"
                SELECT UID, [Database], [Schema], [Name]
                FROM Entity
                WHERE ISNULL(IsDeleted, 0) = 0
                  AND UID = @table;",
                new { table });

            if (entity == null)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "Table not found.",
                });
            }

            var columns = (await con.QueryAsync<string>(
                @"
                SELECT ColumnName
                FROM Field
                WHERE ISNULL(IsDeleted, 0) = 0
                  AND Entity = @table
                ORDER BY ISNULL(SectionNumber, 9),
                         ISNULL(SortOrder, Id);",
                new { table }
            )).ToList();

            return Ok(new
            {
                Success = true,
                Message = "Successful",
                Data = columns
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load columns for table {Table}.", table);

            return Ok(new
            {
                Sucess = false,
                Message = "Error: " + ex.Message,
            });
        }
    }

    private bool IsPrivilegedUser()
    {
        return User.Identity?.IsAuthenticated == true &&
               (
                   User.IsInRole("Admin") ||
                   User.IsInRole("Super") ||
                   User.IsInRole("Root")
               );
    }
}