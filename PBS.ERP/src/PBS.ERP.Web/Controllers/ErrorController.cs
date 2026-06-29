using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PBS.ERP.Web.Models;
using System.Diagnostics;

namespace PBS.ERP.Controllers
{
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : Controller
    {
        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            var originalPath = exceptionFeature?.Path;
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            if (IsApiRequest(originalPath))
            {
                Response.StatusCode = 500;

                return Json(new
                {
                    Message = "An unexpected server error occurred.",
                    Success = false,
                    Data = (object?)null,
                    Errors = new
                    {
                        RequestId = requestId,
                        Path = originalPath
                    }
                });
            }

            Response.StatusCode = 500;

            var model = new ErrorPageViewModel
            {
                StatusCode = 500,
                Title = "Server Error",
                Message = "Something went wrong while processing your request.",
                Description = "Please try again. If the problem continues, contact the system administrator and provide the request ID shown below.",
                OriginalPath = originalPath,
                RequestId = requestId
            };

            return View("Error", model);
        }

        [Route("Error/{statusCode:int}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            Response.StatusCode = statusCode;

            var statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            var originalPath = statusCodeFeature?.OriginalPath;
            var originalQueryString = statusCodeFeature?.OriginalQueryString;
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            if (IsApiRequest(originalPath))
            {
                return Json(new
                {
                    Message = GetMessage(statusCode),
                    Success = false,
                    Data = (object?)null,
                    Errors = new
                    {
                        StatusCode = statusCode,
                        Path = originalPath,
                        QueryString = originalQueryString,
                        RequestId = requestId
                    }
                });
            }

            var model = new ErrorPageViewModel
            {
                StatusCode = statusCode,
                Title = GetTitle(statusCode),
                Message = GetMessage(statusCode),
                Description = GetDescription(statusCode),
                OriginalPath = string.IsNullOrWhiteSpace(originalQueryString)
                    ? originalPath
                    : originalPath + originalQueryString,
                RequestId = requestId
            };

            return View("Error", model);
        }

        private static bool IsApiRequest(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                   && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTitle(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request",
                401 => "Login Required",
                403 => "Access Denied",
                404 => "Page Not Found",
                405 => "Method Not Allowed",
                408 => "Request Timeout",
                500 => "Server Error",
                503 => "Service Unavailable",
                _ => "Request Error"
            };
        }

        private static string GetMessage(int statusCode)
        {
            return statusCode switch
            {
                400 => "The request could not be understood by the server.",
                401 => "You need to login before accessing this page.",
                403 => "You do not have permission to access this page.",
                404 => "The page or resource you requested could not be found.",
                405 => "This action is not allowed for the requested resource.",
                408 => "The request took too long to complete.",
                500 => "An unexpected server error occurred.",
                503 => "The service is temporarily unavailable.",
                _ => "The request could not be completed."
            };
        }

        private static string GetDescription(int statusCode)
        {
            return statusCode switch
            {
                400 => "Please check the request and try again.",
                401 => "Your session may have expired. Please login again.",
                403 => "Your account is authenticated, but you are not authorized to access this section.",
                404 => "The link may be incorrect, outdated, or the page may have been moved.",
                405 => "The page exists, but the requested HTTP method is not supported.",
                408 => "Please refresh the page or try again later.",
                500 => "Please try again. If the problem continues, contact the system administrator.",
                503 => "Please try again after some time.",
                _ => "Please try again or return to the home page."
            };
        }
    }
}