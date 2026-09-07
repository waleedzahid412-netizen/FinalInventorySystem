using System;
using System.Security.Claims;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Resolves the authenticated user ID from JWT/cookie claims.
    /// Never substitutes a default user (e.g. admin ID 1) — missing identity must fail closed.
    /// </summary>
    public static class CurrentUserHelper
    {
        public static bool TryGetUserId(ClaimsPrincipal? user, out int userId)
        {
            userId = 0;
            var claim = user?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var parsed) || parsed <= 0)
            {
                return false;
            }

            userId = parsed;
            return true;
        }

        public static int GetRequiredUserId(ClaimsPrincipal? user)
        {
            if (TryGetUserId(user, out var userId))
            {
                return userId;
            }

            throw new MissingUserIdentityException();
        }
    }

    /// <summary>
    /// Thrown when a mutating action requires an authenticated user ID and the claim is missing or invalid.
    /// Mapped to HTTP 401 by <see cref="InventorySystem.Filters.MissingUserIdentityExceptionFilter"/>.
    /// </summary>
    public sealed class MissingUserIdentityException : InvalidOperationException
    {
        public const string DefaultMessage =
            "The request has no authenticated user identity. Transaction attribution was refused.";

        public MissingUserIdentityException()
            : base(DefaultMessage)
        {
        }
    }
}
