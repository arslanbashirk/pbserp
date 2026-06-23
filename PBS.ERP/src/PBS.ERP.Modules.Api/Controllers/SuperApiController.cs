using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Models;
using System.Security.Claims;

namespace PBS.ERP.Modules.Api.Controllers
{
    [ApiController]
    [Route("api/super")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,Roles = "Root,Super,Admin")]
    [Produces("application/json")]
    public sealed class SuperApiController : ControllerBase
    {
        private readonly ISuperInterface _tableService;
        private readonly ILogger<SuperApiController> _logger;

        public SuperApiController(
            ISuperInterface tableService,
            ILogger<SuperApiController> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        [HttpPost("table/create")]
        public async Task<IActionResult> CreateTable(
            [FromBody] TableCreateRequest request)
        {
            var user = GetCurrentUser();
            var result = await _tableService.CreateTableAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("column/import")]
        public async Task<IActionResult> ImportColumns(
            [FromBody] ImportColumnsRequest request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.ImportColumnsAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("column/add")]
        public async Task<IActionResult> AddColumn(
            [FromBody] AddColumnRequest request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.AddColumnAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("column/alter")]
        public async Task<IActionResult> AlterColumn(
            [FromBody] AlterTableRequest request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.AlterColumnAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("table/drop")]
        public async Task<IActionResult> DropTable(
            [FromBody] DropTableRequest request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.DropTableAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("table/rename")]
        public async Task<IActionResult> RenameTable([FromBody] AlterTableName request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.RenameTableAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("column/drop")]
        public async Task<IActionResult> DropColumn(
            [FromBody] DropColumnRequest request)
        {
            var user = GetCurrentUser();

            var result = await _tableService.DropColumnAsync(
                request,
                user);

            return Ok(result);
        }

        [HttpPost("column/sort")]
        public async Task<IActionResult> SaveSequence(
            [FromBody] FieldSortRequest model)
        {
            var user = GetCurrentUser();

            var result = await _tableService.LayoutFieldAsync(
                model,
                user);

            return Ok(result);
        }

        [HttpPost("column/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUploadColumns(
            [FromForm] IFormFile file,
            [FromQuery] string tableUid)
        {
            var user = GetCurrentUser();

            var results = await _tableService.BulkUploadColumnsAsync(
                file,
                tableUid,
                user);

            return Ok(results);
        }

        private string GetCurrentUser()
        {
            return User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("uid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
        }
    }
}