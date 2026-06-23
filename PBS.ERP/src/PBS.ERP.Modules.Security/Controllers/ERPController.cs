using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBS.ERP.Modules.Security.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ERPController : Controller
    {
        protected string CurrentUser => User?.Identity?.Name ?? "system";
    }
}