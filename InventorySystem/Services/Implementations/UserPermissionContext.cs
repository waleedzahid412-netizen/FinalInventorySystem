using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.Constants;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class UserPermissionContext : IUserPermissionContext
    {
        private readonly IPermissionService _permissionService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _loaded;
        private bool _isAdmin;
        private int _userId;
        private Dictionary<string, (bool CanView, bool CanAdd, bool CanEdit, bool CanDelete)> _permissions = new();

        public UserPermissionContext(IPermissionService permissionService, IHttpContextAccessor httpContextAccessor)
        {
            _permissionService = permissionService;
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsAdmin => _isAdmin;

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            _isAdmin = user.IsInRole(RoleNames.Admin);
            if (_isAdmin)
            {
                return;
            }

            var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idClaim, out _userId))
            {
                return;
            }

            foreach (var pageKey in GetAllPageKeys())
            {
                var canView = await _permissionService.HasPermissionAsync(_userId, pageKey, PermissionAction.View, cancellationToken);
                var canAdd = await _permissionService.HasPermissionAsync(_userId, pageKey, PermissionAction.Add, cancellationToken);
                var canEdit = await _permissionService.HasPermissionAsync(_userId, pageKey, PermissionAction.Edit, cancellationToken);
                var canDelete = await _permissionService.HasPermissionAsync(_userId, pageKey, PermissionAction.Delete, cancellationToken);
                _permissions[pageKey] = (canView, canAdd, canEdit, canDelete);
            }
        }

        public bool CanView(string pageKey) => _isAdmin || (_permissions.TryGetValue(pageKey, out var p) && p.CanView);
        public bool CanAdd(string pageKey) => _isAdmin || (_permissions.TryGetValue(pageKey, out var p) && p.CanAdd);
        public bool CanEdit(string pageKey) => _isAdmin || (_permissions.TryGetValue(pageKey, out var p) && p.CanEdit);
        public bool CanDelete(string pageKey) => _isAdmin || (_permissions.TryGetValue(pageKey, out var p) && p.CanDelete);

        private static IEnumerable<string> GetAllPageKeys()
        {
            yield return PageKeys.Dashboard;
            yield return PageKeys.SalesInvoices;
            yield return PageKeys.SalesCreate;
            yield return PageKeys.LoadSheets;
            yield return PageKeys.Customers;
            yield return PageKeys.CustomerPayments;
            yield return PageKeys.SalesReturns;
            yield return PageKeys.SalesCreateReturn;
            yield return PageKeys.PurchaseInvoices;
            yield return PageKeys.PurchaseCreate;
            yield return PageKeys.PurchaseReturns;
            yield return PageKeys.CompanyPayments;
            yield return PageKeys.Products;
            yield return PageKeys.Categories;
            yield return PageKeys.CompanyStockReport;
            yield return PageKeys.Companies;
            yield return PageKeys.Bookers;
            yield return PageKeys.Suppliers;
            yield return PageKeys.Units;
            yield return PageKeys.Promotions;
            yield return PageKeys.DiscountRules;
            yield return PageKeys.CustomerLedger;
            yield return PageKeys.CompanyLedger;
            yield return PageKeys.Cheques;
            yield return PageKeys.Analytics;
            yield return PageKeys.Users;
            yield return PageKeys.Roles;
        }
    }
}
