using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using InventorySystem.Configuration;
using InventorySystem.Helpers;

namespace InventorySystem.Filters
{
    /// <summary>
    /// Converts <see cref="MissingUserIdentityException"/> into 401 JSON for AJAX
    /// or a login redirect for browser navigation (same contract as JWT OnChallenge).
    /// </summary>
    public sealed class MissingUserIdentityExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not MissingUserIdentityException)
            {
                return;
            }

            context.ExceptionHandled = true;
            context.Result = CreateUnauthorizedResult(context.HttpContext.Request);
        }

        public static IActionResult CreateUnauthorizedResult(HttpRequest request)
        {
            if (!JwtCookieHelper.IsBrowserNavigation(request))
            {
                return new JsonResult(new { success = false, message = "Your session has expired. Please sign in again." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            return new RedirectResult(JwtCookieHelper.BuildLoginRedirect(request, authenticateFailure: null));
        }
    }
}
