using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private const string RolePermCachePrefix = "role-perms:";
        private const string PageCatalogCacheKey = "permission-page-catalog";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public PermissionService(ApplicationDbContext context, IMemoryCache memoryCache)
        {
            _context = context;
            _cache = memoryCache;
        }

        public async Task<bool> HasPermissionAsync(int userId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserID == userId && u.IsActive, cancellationToken);

            if (user?.Role == null)
            {
                return false;
            }

            return await HasPermissionAsync(user.Role.RoleName, user.RoleID, pageKey, action, cancellationToken);
        }

        public async Task<bool> HasPermissionAsync(string? roleName, int roleId, string pageKey, PermissionAction action, CancellationToken cancellationToken = default)
        {
            if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var permissions = await GetRolePermissionsAsync(roleId, cancellationToken);
            if (!permissions.TryGetValue(pageKey, out var flags))
            {
                return false;
            }

            return action switch
            {
                PermissionAction.View => flags.CanView,
                PermissionAction.Add => flags.CanAdd,
                PermissionAction.Edit => flags.CanEdit,
                PermissionAction.Delete => flags.CanDelete,
                _ => false
            };
        }

        public async Task<ApplicationPage?> ResolvePageAsync(string controllerName, string actionName, CancellationToken cancellationToken = default)
        {
            var catalog = await GetPageCatalogAsync(cancellationToken);
            var normalizedController = controllerName.Trim();
            var normalizedAction = actionName.Trim();

            var exact = catalog.FirstOrDefault(p =>
                string.Equals(p.ControllerName, normalizedController, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.DefaultActionName, normalizedAction, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                return exact;
            }

            if (!normalizedAction.Equals("Index", StringComparison.OrdinalIgnoreCase))
            {
                var indexFallback = catalog.FirstOrDefault(p =>
                    string.Equals(p.ControllerName, normalizedController, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.DefaultActionName, "Index", StringComparison.OrdinalIgnoreCase));

                if (indexFallback != null)
                {
                    return indexFallback;
                }
            }

            return catalog.FirstOrDefault(p =>
                string.Equals(p.ControllerName, normalizedController, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(p.DefaultActionName));
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
            _cache.Remove(RolePermCachePrefix + roleId);
        }

        private async Task<Dictionary<string, (bool CanView, bool CanAdd, bool CanEdit, bool CanDelete)>> GetRolePermissionsAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            var cacheKey = RolePermCachePrefix + roleId;
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, (bool, bool, bool, bool)>? cached) && cached != null)
            {
                return cached;
            }

            var rows = await _context.RolePermissions
                .AsNoTracking()
                .Where(rp => rp.RoleID == roleId)
                .Include(rp => rp.Page)
                .ToListAsync(cancellationToken);

            var map = rows.ToDictionary(
                rp => rp.Page.PageKey,
                rp => (rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete));

            _cache.Set(cacheKey, map, CacheDuration);
            return map;
        }

        private async Task<List<ApplicationPage>> GetPageCatalogAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(PageCatalogCacheKey, out List<ApplicationPage>? cached) && cached != null)
            {
                return cached;
            }

            var pages = await _context.ApplicationPages
                .AsNoTracking()
                .Where(p => p.IsActive)
                .ToListAsync(cancellationToken);

            _cache.Set(PageCatalogCacheKey, pages, CacheDuration);
            return pages;
        }
    }
}
