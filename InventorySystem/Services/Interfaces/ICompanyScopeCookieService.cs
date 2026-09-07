using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace InventorySystem.Services.Interfaces
{
    public interface ICompanyScopeCookieService
    {
        void SetScopeCookie(HttpResponse response, HttpRequest request, string value);

        /// <summary>
        /// First allowed company for the user (ordered by name). Admin/unrestricted users get the first active company globally.
        /// </summary>
        Task<int?> GetDefaultCompanyIdForUserAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// When the request has no valid scope cookie, sets the default company cookie and seeds the current request scope.
        /// </summary>
        Task<bool> TryEnsureDefaultCompanyAsync(HttpContext httpContext, int userId, CancellationToken cancellationToken = default);
    }
}
