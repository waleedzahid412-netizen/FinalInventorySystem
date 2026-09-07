using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using Xunit;

namespace InventorySystem.Tests
{
    public class MissingUserIdentityExceptionFilterTests
    {
        [Fact]
        public void OnException_Returns401Json_ForAjaxRequests()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            var context = CreateExceptionContext(httpContext);
            var filter = new MissingUserIdentityExceptionFilter();

            filter.OnException(context);

            Assert.True(context.ExceptionHandled);
            var json = Assert.IsType<JsonResult>(context.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, json.StatusCode);
        }

        [Fact]
        public void OnException_RedirectsToLogin_ForBrowserNavigation()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Sec-Fetch-Dest"] = "document";
            httpContext.Request.Path = "/Sales/Create";
            var context = CreateExceptionContext(httpContext);
            var filter = new MissingUserIdentityExceptionFilter();

            filter.OnException(context);

            Assert.True(context.ExceptionHandled);
            var redirect = Assert.IsType<RedirectResult>(context.Result);
            Assert.Contains("/Auth/Login", redirect.Url);
        }

        [Fact]
        public void OnException_IgnoresOtherExceptions()
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ExceptionContext(actionContext, new List<IFilterMetadata>())
            {
                Exception = new InvalidOperationException("other")
            };
            var filter = new MissingUserIdentityExceptionFilter();

            filter.OnException(context);

            Assert.False(context.ExceptionHandled);
            Assert.Null(context.Result);
        }

        private static ExceptionContext CreateExceptionContext(HttpContext httpContext)
        {
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ExceptionContext(actionContext, new List<IFilterMetadata>())
            {
                Exception = new MissingUserIdentityException()
            };
        }
    }
}
