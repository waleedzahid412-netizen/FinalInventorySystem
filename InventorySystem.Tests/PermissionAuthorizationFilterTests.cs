using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using InventorySystem.Authorization;
using InventorySystem.Constants;
using InventorySystem.Filters;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;
using Xunit;

namespace InventorySystem.Tests
{
    public class PermissionAuthorizationFilterTests
    {
        [Fact]
        public async Task OnAuthorizationAsync_UnmappedPage_DeniesAccess_ForAjaxRequest()
        {
            var permissionService = new FakePermissionService { PageToResolve = null };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(PermissionTestController).GetMethod(nameof(PermissionTestController.UnmappedAction))!;
            var context = CreateAuthorizationContext(
                method,
                controllerName: "Test",
                roleName: "User",
                userId: 42,
                isAjax: true);

            await filter.OnAuthorizationAsync(context);

            Assert.True(permissionService.ResolvePageCalled);
            var json = Assert.IsType<JsonResult>(context.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        }

        [Fact]
        public async Task OnAuthorizationAsync_MappedPageWithPermission_AllowsAccess()
        {
            var permissionService = new FakePermissionService
            {
                PageToResolve = new ApplicationPage { PageKey = PageKeys.SalesInvoices, ControllerName = "Sales", DefaultActionName = "Index" },
                HasPermission = true
            };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(PermissionTestController).GetMethod(nameof(PermissionTestController.MappedAction))!;
            var context = CreateAuthorizationContext(
                method,
                controllerName: "Sales",
                roleName: "User",
                userId: 42);

            await filter.OnAuthorizationAsync(context);

            Assert.Equal(PageKeys.SalesInvoices, permissionService.LastPageKey);
            Assert.Equal(PermissionAction.View, permissionService.LastAction);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task OnAuthorizationAsync_MappedPageWithoutPermission_DeniesAccess()
        {
            var permissionService = new FakePermissionService
            {
                PageToResolve = new ApplicationPage { PageKey = PageKeys.SalesInvoices, ControllerName = "Sales", DefaultActionName = "Index" },
                HasPermission = false
            };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(PermissionTestController).GetMethod(nameof(PermissionTestController.MappedAction))!;
            var context = CreateAuthorizationContext(
                method,
                controllerName: "Sales",
                roleName: "User",
                userId: 42,
                isAjax: true);

            await filter.OnAuthorizationAsync(context);

            var json = Assert.IsType<JsonResult>(context.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        }

        [Fact]
        public async Task OnAuthorizationAsync_SkipPermissionCheck_AllowsEvenWhenUnmapped()
        {
            var permissionService = new FakePermissionService { PageToResolve = null };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(SkipPermissionTestController).GetMethod(nameof(SkipPermissionTestController.Index))!;
            var context = CreateAuthorizationContext(
                method,
                controllerType: typeof(SkipPermissionTestController),
                controllerName: "SkipPermissionTest",
                roleName: "User",
                userId: 42);

            await filter.OnAuthorizationAsync(context);

            Assert.False(permissionService.ResolvePageCalled);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task OnAuthorizationAsync_AdminRole_AllowsEvenWhenUnmapped()
        {
            var permissionService = new FakePermissionService { PageToResolve = null, HasPermission = false };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(PermissionTestController).GetMethod(nameof(PermissionTestController.UnmappedAction))!;
            var context = CreateAuthorizationContext(
                method,
                controllerName: "Test",
                roleName: RoleNames.Admin,
                userId: 42);

            await filter.OnAuthorizationAsync(context);

            Assert.False(permissionService.ResolvePageCalled);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task OnAuthorizationAsync_RequirePermission_UsesExplicitPageKeyWithoutResolvePage()
        {
            var permissionService = new FakePermissionService { PageToResolve = null, HasPermission = true };
            var filter = new PermissionAuthorizationFilter(permissionService);
            var method = typeof(PermissionTestController).GetMethod(nameof(PermissionTestController.ExplicitPermissionAction))!;
            var context = CreateAuthorizationContext(
                method,
                controllerName: "Test",
                roleName: "User",
                userId: 42);

            await filter.OnAuthorizationAsync(context);

            Assert.False(permissionService.ResolvePageCalled);
            Assert.Equal(PageKeys.SalesInvoices, permissionService.LastPageKey);
            Assert.Equal(PermissionAction.View, permissionService.LastAction);
            Assert.Null(context.Result);
        }

        private static AuthorizationFilterContext CreateAuthorizationContext(
            MethodInfo method,
            string controllerName,
            string roleName,
            int userId,
            bool isAjax = false,
            Type? controllerType = null)
        {
            controllerType ??= method.DeclaringType!;
            var httpContext = new DefaultHttpContext();
            if (isAjax)
            {
                httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, roleName)
            };
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerName,
                ActionName = method.Name,
                MethodInfo = method,
                ControllerTypeInfo = controllerType.GetTypeInfo()
            };

            var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        private sealed class FakePermissionService : IPermissionService
        {
            public ApplicationPage? PageToResolve { get; set; }
            public bool HasPermission { get; set; } = true;
            public bool ResolvePageCalled { get; private set; }
            public string? LastPageKey { get; private set; }
            public PermissionAction LastAction { get; private set; }

            public Task<bool> HasPermissionAsync(int userId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default)
            {
                LastPageKey = pageKey;
                LastAction = action;
                return Task.FromResult(HasPermission);
            }

            public Task<bool> HasPermissionAsync(string? roleName, int roleId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default)
            {
                LastPageKey = pageKey;
                LastAction = action;
                return Task.FromResult(HasPermission);
            }

            public Task<ApplicationPage?> ResolvePageAsync(string controllerName, string actionName, CancellationToken cancellationToken = default)
            {
                ResolvePageCalled = true;
                return Task.FromResult(PageToResolve);
            }

            public PermissionAction MapMvcActionToPermission(string actionName)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    return PermissionAction.View;
                }

                var action = actionName.Trim();
                if (action.Equals("Create", StringComparison.OrdinalIgnoreCase))
                {
                    return PermissionAction.Add;
                }

                if (action.Equals("Edit", StringComparison.OrdinalIgnoreCase))
                {
                    return PermissionAction.Edit;
                }

                if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase) ||
                    action.Equals("SoftDelete", StringComparison.OrdinalIgnoreCase))
                {
                    return PermissionAction.Delete;
                }

                return PermissionAction.View;
            }

            public void InvalidateRoleCache(int roleId)
            {
            }
        }
    }

    [SkipPermissionCheck]
    public sealed class SkipPermissionTestController : Controller
    {
        public IActionResult Index() => Ok();
    }

    public sealed class PermissionTestController : Controller
    {
        public IActionResult UnmappedAction() => Ok();

        [RequirePermission(PageKeys.SalesInvoices, PermissionAction.View)]
        public IActionResult ExplicitPermissionAction() => Ok();

        public IActionResult MappedAction() => Ok();
    }
}
