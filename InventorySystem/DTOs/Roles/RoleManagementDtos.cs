using System.Collections.Generic;

namespace InventorySystem.DTOs.Roles
{
    public class RoleListItemDto
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? RoleDescription { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemRole { get; set; }
        public int ActiveUserCount { get; set; }
        public int GrantedPageCount { get; set; }
        public bool HasFullAccess { get; set; }
    }

    public class RolePermissionInputDto
    {
        public int ApplicationPageID { get; set; }
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class CreateRoleDto
    {
        public string RoleName { get; set; } = string.Empty;
        public string? RoleDescription { get; set; }
        public bool IsActive { get; set; } = true;
        public List<RolePermissionInputDto> Permissions { get; set; } = new();
    }

    public class EditRoleDto
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? RoleDescription { get; set; }
        public bool IsActive { get; set; } = true;
        public List<RolePermissionInputDto> Permissions { get; set; } = new();
    }

    public class RolePermissionCatalogModuleDto
    {
        public int ApplicationModuleID { get; set; }
        public string ModuleKey { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public List<RolePermissionCatalogPageDto> Pages { get; set; } = new();
    }

    public class RolePermissionCatalogPageDto
    {
        public int ApplicationPageID { get; set; }
        public string PageKey { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
