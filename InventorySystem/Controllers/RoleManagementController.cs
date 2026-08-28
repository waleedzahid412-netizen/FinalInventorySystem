using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Constants;
using InventorySystem.DTOs.Roles;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Roles;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class RoleManagementController : Controller
    {
        private readonly IRoleManagementService _roleManagementService;

        public RoleManagementController(IRoleManagementService roleManagementService)
        {
            _roleManagementService = roleManagementService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);
            return View(new RoleListViewModel
            {
                Roles = roles.Select(r => new RoleListItemViewModel
                {
                    RoleID = r.RoleID,
                    RoleName = r.RoleName,
                    RoleDescription = r.RoleDescription,
                    IsActive = r.IsActive,
                    IsSystemRole = r.IsSystemRole,
                    ActiveUserCount = r.ActiveUserCount,
                    GrantedPageCount = r.GrantedPageCount,
                    HasFullAccess = r.HasFullAccess
                }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var role = await _roleManagementService.GetRoleForEditAsync(id, cancellationToken);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            var catalog = await _roleManagementService.GetEmptyPermissionCatalogAsync(cancellationToken);
            ApplySelections(catalog, role.Permissions);

            var roleEntity = (await _roleManagementService.GetAllRolesAsync(cancellationToken))
                .FirstOrDefault(r => r.RoleID == id);

            if (roleEntity?.HasFullAccess == true)
            {
                ApplyFullAccess(catalog);
            }

            return View(new RoleDetailsViewModel
            {
                RoleID = role.RoleID,
                RoleName = role.RoleName,
                RoleDescription = role.RoleDescription,
                IsActive = role.IsActive,
                IsSystemRole = roleEntity?.IsSystemRole ?? false,
                Modules = MapModules(catalog)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var catalog = await _roleManagementService.GetEmptyPermissionCatalogAsync(cancellationToken);
            return View(new CreateRoleViewModel
            {
                Modules = MapModules(catalog)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRoleViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await RepopulateModulesAsync(model, null, cancellationToken);
                return View(model);
            }

            var result = await _roleManagementService.CreateRoleAsync(new CreateRoleDto
            {
                RoleName = model.RoleName,
                RoleDescription = model.RoleDescription,
                IsActive = model.IsActive,
                Permissions = FlattenPermissions(model.Modules)
            }, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await RepopulateModulesAsync(model, null, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var role = await _roleManagementService.GetRoleForEditAsync(id, cancellationToken);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            var catalog = await _roleManagementService.GetEmptyPermissionCatalogAsync(cancellationToken);
            ApplySelections(catalog, role.Permissions);

            var roleEntity = (await _roleManagementService.GetAllRolesAsync(cancellationToken))
                .FirstOrDefault(r => r.RoleID == id);

            return View(new EditRoleViewModel
            {
                RoleID = role.RoleID,
                RoleName = role.RoleName,
                RoleDescription = role.RoleDescription,
                IsActive = role.IsActive,
                IsSystemRole = roleEntity?.IsSystemRole ?? false,
                Modules = MapModules(catalog)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditRoleViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await RepopulateModulesAsync(model, model.RoleID, cancellationToken);
                return View(model);
            }

            var result = await _roleManagementService.UpdateRoleAsync(new EditRoleDto
            {
                RoleID = model.RoleID,
                RoleName = model.RoleName,
                RoleDescription = model.RoleDescription,
                IsActive = model.IsActive,
                Permissions = FlattenPermissions(model.Modules)
            }, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await RepopulateModulesAsync(model, model.RoleID, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var result = await _roleManagementService.SoftDeleteRoleAsync(id, GetCurrentUserId(), cancellationToken);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
                ? result.Message
                : (result.Errors.FirstOrDefault() ?? result.Message);
            return RedirectToAction(nameof(Index));
        }

        private async Task RepopulateModulesAsync(CreateRoleViewModel model, int? roleId, CancellationToken cancellationToken)
        {
            if (roleId.HasValue)
            {
                var role = await _roleManagementService.GetRoleForEditAsync(roleId.Value, cancellationToken);
                if (role != null)
                {
                    var catalog = await _roleManagementService.GetEmptyPermissionCatalogAsync(cancellationToken);
                    MergePostedSelections(catalog, model.Modules);
                    model.Modules = MapModules(catalog);
                    return;
                }
            }

            if (model.Modules == null || !model.Modules.Any())
            {
                var catalog = await _roleManagementService.GetEmptyPermissionCatalogAsync(cancellationToken);
                model.Modules = MapModules(catalog);
            }
        }

        private static void MergePostedSelections(List<RolePermissionCatalogModuleDto> catalog, List<RolePermissionModuleViewModel> posted)
        {
            var postedLookup = posted
                .SelectMany(m => m.Pages)
                .ToDictionary(p => p.ApplicationPageID);

            foreach (var module in catalog)
            {
                foreach (var page in module.Pages)
                {
                    if (postedLookup.TryGetValue(page.ApplicationPageID, out var row))
                    {
                        page.CanView = row.CanView;
                        page.CanAdd = row.CanAdd;
                        page.CanEdit = row.CanEdit;
                        page.CanDelete = row.CanDelete;
                    }
                }
            }
        }

        private static void ApplySelections(List<RolePermissionCatalogModuleDto> catalog, List<RolePermissionInputDto> permissions)
        {
            var lookup = permissions.ToDictionary(p => p.ApplicationPageID);
            foreach (var module in catalog)
            {
                foreach (var page in module.Pages)
                {
                    if (lookup.TryGetValue(page.ApplicationPageID, out var perm))
                    {
                        page.CanView = perm.CanView;
                        page.CanAdd = perm.CanAdd;
                        page.CanEdit = perm.CanEdit;
                        page.CanDelete = perm.CanDelete;
                    }
                }
            }
        }

        private static void ApplyFullAccess(List<RolePermissionCatalogModuleDto> catalog)
        {
            foreach (var module in catalog)
            {
                foreach (var page in module.Pages)
                {
                    page.CanView = true;
                    page.CanAdd = true;
                    page.CanEdit = true;
                    page.CanDelete = true;
                }
            }
        }

        private static List<RolePermissionModuleViewModel> MapModules(List<RolePermissionCatalogModuleDto> catalog)
        {
            return catalog.Select(m => new RolePermissionModuleViewModel
            {
                ApplicationModuleID = m.ApplicationModuleID,
                ModuleKey = m.ModuleKey,
                ModuleName = m.ModuleName,
                Pages = m.Pages.Select(p => new RolePermissionRowViewModel
                {
                    ApplicationPageID = p.ApplicationPageID,
                    PageKey = p.PageKey,
                    PageName = p.PageName,
                    CanView = p.CanView,
                    CanAdd = p.CanAdd,
                    CanEdit = p.CanEdit,
                    CanDelete = p.CanDelete
                }).ToList()
            }).ToList();
        }

        private static List<RolePermissionInputDto> FlattenPermissions(List<RolePermissionModuleViewModel> modules)
        {
            return modules
                .SelectMany(m => m.Pages)
                .Select(p => new RolePermissionInputDto
                {
                    ApplicationPageID = p.ApplicationPageID,
                    CanView = p.CanView,
                    CanAdd = p.CanAdd,
                    CanEdit = p.CanEdit,
                    CanDelete = p.CanDelete
                })
                .ToList();
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var userId) ? userId : 0;
        }
    }
}
