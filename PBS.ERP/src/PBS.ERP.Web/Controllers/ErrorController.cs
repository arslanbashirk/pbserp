using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PBS.ERP.Controllers
{
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode:int}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            Response.StatusCode = statusCode;

            switch (statusCode)
            {
                case 404:
                    return View("NotFound");

                case 403:
                    return RedirectToAction("AccessDenied", "Account");

                default:
                    ViewBag.StatusCode = statusCode;
                    return View("Error");
            }
        }
    }
}