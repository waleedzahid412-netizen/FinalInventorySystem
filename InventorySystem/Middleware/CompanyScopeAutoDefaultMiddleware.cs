using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Middleware
{
    /// <summary>
    /// Ensures authenticated users without a valid company scope cookie receive a default allowed company.
    /// </summary>
    public sealed class CompanyScopeAutoDefaultMiddleware
    {
        private readonly RequestDelegate _next;

        public CompanyScopeAutoDefaultMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICompanyScopeCookieService cookieService)
        {
            if (context.User.Identity?.IsAuthenticated == true
                && CurrentUserHelper.TryGetUserId(context.User, out var userId))
            {
                await cookieService.TryEnsureDefaultCompanyAsync(context, userId, context.RequestAborted);
            }

            await _next(context);
        }
    }
}
