using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using InventorySystem.Constants;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Roles;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class RoleManagementService : IRoleManagementService
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RoleManagementService> _logger;

        public RoleManagementService(
            IRoleRepository roleRepository,
            IPermissionService permissionService,
            ILogger<RoleManagementService> logger)
        {
            _roleRepository = roleRepository;
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task<List<RoleListItemDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            var roles = await _roleRepository.GetAllActiveAsync(cancellationToken);
            var result = new List<RoleListItemDto>();

            foreach (var role in roles)
            {
                var hasFullAccess = role.IsSystemRole
                    && string.Equals(role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase);
                var grantedPageCount = hasFullAccess
                    ? 0
                    : role.RolePermissions.Count(rp => rp.CanView || rp.CanAdd || rp.CanEdit || rp.CanDelete);

                result.Add(new RoleListItemDto
                {
                    RoleID = role.RoleID,
                    RoleName = role.RoleName,
                    RoleDescription = role.RoleDescription,
                    IsActive = role.IsActive,
                    IsSystemRole = role.IsSystemRole,
                    ActiveUserCount = await _roleRepository.GetActiveUserCountAsync(role.RoleID, cancellationToken),
                    HasFullAccess = hasFullAccess,
                    GrantedPageCount = grantedPageCount
                });
            }

            return result;
        }

        public async Task<EditRoleDto?> GetRoleForEditAsync(int roleId, CancellationToken cancellationToken = default)
        {
            var role = await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken);
            if (role == null) return null;

            var catalog = await BuildCatalogWithSelectionsAsync(role.RolePermissions, cancellationToken);

            return new EditRoleDto
            {
                RoleID = role.RoleID,
                RoleName = role.RoleName,
                RoleDescription = role.RoleDescription,
                IsActive = role.IsActive,
                Permissions = catalog
                    .SelectMany(m => m.Pages)
                    .Select(p => new RolePermissionInputDto
                    {
                        ApplicationPageID = p.ApplicationPageID,
                        CanView = p.CanView,
                        CanAdd = p.CanAdd,
                        CanEdit = p.CanEdit,
                        CanDelete = p.CanDelete
                    })
                    .ToList()
            };
        }

        public async Task<List<RolePermissionCatalogModuleDto>> GetEmptyPermissionCatalogAsync(CancellationToken cancellationToken = default)
        {
            return await BuildCatalogWithSelectionsAsync(Array.Empty<RolePermission>(), cancellationToken);
        }

        public async Task<OperationResult<int>> CreateRoleAsync(CreateRoleDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateRoleDtoAsync(dto.RoleName, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var now = DateTime.UtcNow;
                var role = new Role
                {
                    RoleName = dto.RoleName.Trim(),
                    RoleDescription = string.IsNullOrWhiteSpace(dto.RoleDescription) ? null : dto.RoleDescription.Trim(),
                    IsActive = dto.IsActive,
                    IsSystemRole = false,
                    CreatedAt = now,
                    CreatedBy = userId > 0 ? userId : null,
                    IsDeleted = false
                };

                await _roleRepository.AddAsync(role, cancellationToken);
                await _roleRepository.SaveChangesAsync(cancellationToken);

                var permissions = BuildRolePermissions(role.RoleID, dto.Permissions);
                await _roleRepository.SavePermissionsAsync(role.RoleID, permissions, cancellationToken);
                await _roleRepository.SaveChangesAsync(cancellationToken);

                _permissionService.InvalidateRoleCache(role.RoleID);
                _logger.LogInformation("Role {RoleName} created with ID {RoleID}", role.RoleName, role.RoleID);
                return OperationResult<int>.Ok(role.RoleID, "Role created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create role {RoleName}", dto.RoleName);
                return OperationResult<int>.Fail(UserFacingErrorMessages.RoleCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateRoleAsync(EditRoleDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var role = await _roleRepository.GetByIdAsync(dto.RoleID, cancellationToken);
            if (role == null)
            {
                return OperationResult.Fail("Role not found.");
            }

            if (role.IsSystemRole && !string.Equals(role.RoleName, dto.RoleName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("System role name cannot be changed.");
            }

            var errors = await ValidateRoleDtoAsync(dto.RoleName, dto.RoleID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            try
            {
                role.RoleName = dto.RoleName.Trim();
                role.RoleDescription = string.IsNullOrWhiteSpace(dto.RoleDescription) ? null : dto.RoleDescription.Trim();
                role.IsActive = dto.IsActive;
                role.UpdatedAt = DateTime.UtcNow;
                role.UpdatedBy = userId > 0 ? userId : null;

                if (!role.IsSystemRole)
                {
                    var permissions = BuildRolePermissions(role.RoleID, dto.Permissions);
                    await _roleRepository.SavePermissionsAsync(role.RoleID, permissions, cancellationToken);
                }

                await _roleRepository.SaveChangesAsync(cancellationToken);
                _permissionService.InvalidateRoleCache(role.RoleID);

                return OperationResult.Ok("Role updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update role {RoleID}", dto.RoleID);
                return OperationResult.Fail(UserFacingErrorMessages.RoleUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteRoleAsync(int roleId, int userId, CancellationToken cancellationToken = default)
        {
            var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
            if (role == null)
            {
                return OperationResult.Fail("Role not found.");
            }

            if (role.IsSystemRole)
            {
                return OperationResult.Fail("System roles cannot be deleted.");
            }

            var userCount = await _roleRepository.GetActiveUserCountAsync(roleId, cancellationToken);
            if (userCount > 0)
            {
                return OperationResult.Fail("Cannot delete a role that is assigned to active users.");
            }

            role.IsDeleted = true;
            role.DeletedAt = DateTime.UtcNow;
            role.IsActive = false;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = userId > 0 ? userId : null;

            await _roleRepository.SaveChangesAsync(cancellationToken);
            _permissionService.InvalidateRoleCache(roleId);

            return OperationResult.Ok("Role deleted successfully.");
        }

        private async Task<List<string>> ValidateRoleDtoAsync(string roleName, int? excludeRoleId, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(roleName))
            {
                errors.Add("Role name is required.");
            }
            else if (await _roleRepository.RoleNameExistsAsync(roleName, excludeRoleId, cancellationToken))
            {
                errors.Add("A role with this name already exists.");
            }

            return errors;
        }

        private async Task<List<RolePermissionCatalogModuleDto>> BuildCatalogWithSelectionsAsync(
            IEnumerable<RolePermission> selected,
            CancellationToken cancellationToken)
        {
            var selectedLookup = selected.ToDictionary(p => p.ApplicationPageID);
            var modules = await _roleRepository.GetPermissionCatalogAsync(cancellationToken);

            return modules.Select(module => new RolePermissionCatalogModuleDto
            {
                ApplicationModuleID = module.ApplicationModuleID,
                ModuleKey = module.ModuleKey,
                ModuleName = module.ModuleName,
                Pages = module.Pages
                    .OrderBy(p => p.DisplayOrder)
                    .Select(page =>
                    {
                        selectedLookup.TryGetValue(page.ApplicationPageID, out var perm);
                        return new RolePermissionCatalogPageDto
                        {
                            ApplicationPageID = page.ApplicationPageID,
                            PageKey = page.PageKey,
                            PageName = page.PageName,
                            CanView = perm?.CanView ?? false,
                            CanAdd = perm?.CanAdd ?? false,
                            CanEdit = perm?.CanEdit ?? false,
                            CanDelete = perm?.CanDelete ?? false
                        };
                    })
                    .ToList()
            }).ToList();
        }

        private static List<RolePermission> BuildRolePermissions(int roleId, IEnumerable<RolePermissionInputDto> inputs)
        {
            return inputs
                .Where(p => p.CanView || p.CanAdd || p.CanEdit || p.CanDelete)
                .Select(p => new RolePermission
                {
                    RoleID = roleId,
                    ApplicationPageID = p.ApplicationPageID,
                    CanView = p.CanView,
                    CanAdd = p.CanAdd,
                    CanEdit = p.CanEdit,
                    CanDelete = p.CanDelete
                })
                .ToList();
        }
    }
}
