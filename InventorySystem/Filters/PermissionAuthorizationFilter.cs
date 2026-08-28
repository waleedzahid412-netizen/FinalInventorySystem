using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using InventorySystem.Authorization;
using InventorySystem.Constants;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Filters
{
    public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private const string PermissionDeniedMessage = "You do not have permission to perform this action.";
        private readonly IPermissionService _permissionService;

        public PermissionAuthorizationFilter(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return;
            }

            if (context.ActionDescriptor.EndpointMetadata.Any(m => m is AllowAnonymousAttribute))
            {
                return;
            }

            if (HasSkipPermission(descriptor))
            {
                return;
            }

            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var roleName = context.HttpContext.User.FindFirstValue(ClaimTypes.Role);
            if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            string pageKey;
            PermissionAction permissionAction;

            var explicitAttr = descriptor.MethodInfo.GetCustomAttribute<RequirePermissionAttribute>(inherit: true)
                ?? descriptor.ControllerTypeInfo.GetCustomAttribute<RequirePermissionAttribute>(inherit: true);

            if (explicitAttr != null)
            {
                pageKey = explicitAttr.PageKey;
                permissionAction = explicitAttr.Action;
            }
            else
            {
                var page = await _permissionService.ResolvePageAsync(descriptor.ControllerName, descriptor.ActionName, context.HttpContext.RequestAborted);
                if (page == null)
                {
                    return;
                }

                pageKey = page.PageKey;
                permissionAction = _permissionService.MapMvcActionToPermission(descriptor.ActionName);
            }

            var hasPermission = await _permissionService.HasPermissionAsync(userId, pageKey, permissionAction, context.HttpContext.RequestAborted);
            if (!hasPermission)
            {
                DenyAccess(context);
            }
        }

        private static void DenyAccess(AuthorizationFilterContext context)
        {
            if (WantsJsonResponse(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new { success = false, message = PermissionDeniedMessage })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            var referer = context.HttpContext.Request.Headers.Referer.ToString();
            var redirectUrl = !string.IsNullOrWhiteSpace(referer) ? referer : "/";

            var tempDataFactory = context.HttpContext.RequestServices.GetService(typeof(ITempDataDictionaryFactory)) as ITempDataDictionaryFactory;
            if (tempDataFactory != null)
            {
                var tempData = tempDataFactory.GetTempData(context.HttpContext);
                tempData["ToastError"] = PermissionDeniedMessage;
            }

            context.Result = new RedirectResult(redirectUrl);
        }

        private static bool WantsJsonResponse(HttpRequest request)
        {
            if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            var accept = request.Headers.Accept.ToString();
            return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSkipPermission(ControllerActionDescriptor descriptor)
        {
            if (descriptor.ControllerName.Equals("Auth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (descriptor.ControllerName.Equals("CompanyScope", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (descriptor.ControllerName.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                (descriptor.ActionName.Equals("Privacy", StringComparison.OrdinalIgnoreCase) ||
                 descriptor.ActionName.Equals("Error", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return descriptor.MethodInfo.GetCustomAttribute<SkipPermissionCheckAttribute>(inherit: true) != null
                || descriptor.ControllerTypeInfo.GetCustomAttribute<SkipPermissionCheckAttribute>(inherit: true) != null;
        }
    }
}
