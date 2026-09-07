using Microsoft.AspNetCore.Mvc;
using InventorySystem.Helpers;

namespace InventorySystem.Controllers
{
    /// <summary>
    /// Shared MVC base that resolves the authenticated user ID without falling back to a real account.
    /// </summary>
    public abstract class InventoryController : Controller
    {
        /// <summary>
        /// Returns the authenticated user ID from the NameIdentifier claim.
        /// Throws <see cref="MissingUserIdentityException"/> (HTTP 401) when the claim is missing or invalid.
        /// </summary>
        protected int GetCurrentUserId() => CurrentUserHelper.GetRequiredUserId(User);
    }
}
