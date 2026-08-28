using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Filters
{
    /// <summary>
    /// Redirects to the company picker when the ambient company scope cannot be resolved
    /// (neither a specific company nor All Companies). Apply to hard-scoped controllers/actions
    /// (Products, Categories, Bookers, Sales, Purchases, Promotions, Discounts).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireCompanyScopeAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var scope = context.HttpContext.RequestServices.GetRequiredService<ICompanyContext>();
            if (!await scope.TryResolveAsync(context.HttpContext.RequestAborted) || !scope.HasResolvedScope)
            {
                var path = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult(
                    "Select",
                    "CompanyScope",
                    new { returnUrl = string.IsNullOrEmpty(path) ? "/" : path });
                return;
            }

            await next();
        }
    }
}
