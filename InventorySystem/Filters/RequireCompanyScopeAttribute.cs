using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Filters
{
    /// <summary>
    /// Ensures ambient company scope is resolved before hard-scoped actions run.
    /// Auto-defaults to the user's first allowed company when possible; otherwise redirects home.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireCompanyScopeAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;
            var scope = services.GetRequiredService<ICompanyContext>();
            var cookieService = services.GetRequiredService<ICompanyScopeCookieService>();

            if (!await scope.TryResolveAsync(context.HttpContext.RequestAborted) || !scope.HasResolvedScope)
            {
                if (CurrentUserHelper.TryGetUserId(context.HttpContext.User, out var userId)
                    && await cookieService.TryEnsureDefaultCompanyAsync(
                        context.HttpContext,
                        userId,
                        context.HttpContext.RequestAborted)
                    && scope.HasResolvedScope)
                {
                    await next();
                    return;
                }

                if (context.Controller is Controller controller)
                {
                    controller.TempData["ToastError"] =
                        "Select a company from the navbar before opening this screen.";
                }

                context.Result = new RedirectToActionResult("Index", "Home", null);
                return;
            }

            await next();
        }
    }
}
