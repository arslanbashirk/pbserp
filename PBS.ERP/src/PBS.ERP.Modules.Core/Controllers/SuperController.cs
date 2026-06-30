// PBS.ERP.Modules.Core.Controllers/SuperController.cs

using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure;
using PBS.ERP.Shared.Models;
using System.Data;
using System.Data.Common;
using System.Security.Claims;

namespace PBS.ERP.Modules.Core.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = Constants.Identity_Application_Scheme,Roles = "Root,Super,Admin")]
    public class SuperController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuperController(ApplicationDbContext context)
        {
            _context = context;
        }

        
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            await using var con = await GetOpenConnectionAsync();

            var tables = (await con.QueryAsync<Entity>(
                $@"
                SELECT *
                FROM {Constants.EntityTable}
                WHERE ISNULL(IsDeleted, 0) = 0
                ORDER BY CreatedTime DESC"))
                .ToList();

            return View(tables);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(r => r.Value)
                .ToList();

            bool isRootOrSuper =
                roles.Contains("Root", StringComparer.OrdinalIgnoreCase) ||
                roles.Contains("Super", StringComparer.OrdinalIgnoreCase) ||
                roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

            string sql;
            object? param = null;

            if (isRootOrSuper)
            {
                sql = @"
                SELECT [Short], [Name]
                FROM SYS_TABLE_TYPE
                WHERE IsDeleted = 0
                ORDER BY [Name]";
                            }
                            else
                            {
                                sql = $@"
                SELECT 
                    st.Short AS Short,
                    st.Name AS Name
                FROM SYS_TABLE_TYPE st
                JOIN {Constants.RoleTable} r
                    ON st.Short = r.Name
                    AND r.IsDeleted = 0
                JOIN {Constants.UserRoleTable} ur
                    ON r.Id = ur.RoleId
                    AND ur.IsDeleted = 0
                WHERE st.IsDeleted = 0
                AND ur.UserId = @UserId
                ORDER BY st.Name";

                param = new { UserId = userId };
            }

            await using var connection = await GetOpenConnectionAsync();

            var data = (await connection.QueryAsync<TableTypeDto>(
                sql,
                param))
                .ToList();

            return View(data);
        }

        [HttpGet("Alter")]
        public async Task<IActionResult> Alter(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Table UID is required.");

            var name = await _context.Entities
                .Where(m => m.UID == id && m.IsDeleted != true)
                .Select(m => m.Name)
                .FirstOrDefaultAsync();

            var type = await _context.Entities
                .Where(m => m.Name == "SYS_TABLE_TYPE" && m.IsDeleted != true)
                .Select(m => m.UID)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(name))
                return NotFound("Table metadata not found.");

            ViewBag.Table = name;
            ViewBag.UID = id;
            ViewBag.Type = type;

            return View();
        }


        [HttpGet("Order")]
        public async Task<IActionResult> Order(string? id)
        {
            var name = await _context.Entities
               .Where(m => m.UID == id)
               .Select(m => m.Name)
               .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(name))
                return NotFound("Table metadata not found.");

            ViewBag.Table = name;
            ViewBag.UID = id;

            return View();
        }


        [HttpGet("Unique")]
        public async Task<IActionResult> Unique(string? id)
        {
            var name = await _context.Entities
               .Where(m => m.UID == id)
               .Select(m => m.Name)
               .FirstOrDefaultAsync();

            var fields = await _context.Fields
               .Where(m => m.Entity == id)
               .ToListAsync();

            if (string.IsNullOrWhiteSpace(name))
                return NotFound("Table metadata not found.");

            ViewBag.Table = name;
            ViewBag.UID = id;

            return View(fields);
        }



        [HttpGet("Fields")]
        public async Task<IActionResult> Fields(string? id)
        {
            var name = await _context.Entities
               .Where(m => m.UID == id)
               .Select(m => m.Name)
               .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(name))
                return NotFound("Table metadata not found.");

            ViewBag.Table = name;
            ViewBag.UID = id;

            return View();
        }
        private async Task<DbConnection> GetOpenConnectionAsync()
        {
            var con = _context.Database.GetDbConnection();

            if (con.State != ConnectionState.Open)
                await con.OpenAsync();

            return con;
        }
    }

    public class TableTypeDto
    {
        public string Short { get; set; }
        public string Name { get; set; }
    }
}