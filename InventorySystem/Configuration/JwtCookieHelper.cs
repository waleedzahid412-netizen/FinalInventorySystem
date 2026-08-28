using System;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace InventorySystem.Configuration
{
    public static class JwtCookieHelper
    {
        public const string CookieName = "jwt_token";
        public const string LoginPath = "/Auth/Login";

        public static CookieOptions CreateCookieOptions(DateTimeOffset expires) => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/"
        };

        public static CookieOptions DeleteCookieOptions() => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };

        public static void DeleteToken(HttpResponse response)
        {
            response.Cookies.Delete(CookieName, DeleteCookieOptions());
        }

        public static bool WantsJsonResponse(HttpRequest request)
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

        /// <summary>
        /// True for a full page load (link click / form GET). False for fetch/XHR so those
        /// receive 401 JSON instead of a login HTML redirect that breaks JSON parsers.
        /// </summary>
        public static bool IsBrowserNavigation(HttpRequest request)
        {
            if (WantsJsonResponse(request))
            {
                return false;
            }

            var dest = request.Headers["Sec-Fetch-Dest"].ToString();
            if (string.Equals(dest, "document", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var mode = request.Headers["Sec-Fetch-Mode"].ToString();
            if (string.Equals(mode, "navigate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept = request.Headers.Accept.ToString();
            return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTokenExpired(Exception? exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is SecurityTokenExpiredException)
                {
                    return true;
                }
            }

            return false;
        }

        public static string BuildLoginRedirect(HttpRequest request, Exception? authenticateFailure)
        {
            var expired = IsTokenExpired(authenticateFailure);
            var login = expired ? $"{LoginPath}?expired=1" : LoginPath;

            var returnUrl = $"{request.Path}{request.QueryString}";
            if (string.IsNullOrEmpty(returnUrl)
                || returnUrl == "/"
                || returnUrl.StartsWith("/Auth", StringComparison.OrdinalIgnoreCase))
            {
                return login;
            }

            var separator = login.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{login}{separator}returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
    }
}
