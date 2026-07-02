using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBS.ERP.Modules.GitHub.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public sealed class GitHubController : Controller
{
    [HttpGet]
    public IActionResult Dashboard()
    {
        return View();
    }
}