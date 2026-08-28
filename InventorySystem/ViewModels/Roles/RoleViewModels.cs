using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace InventorySystem.ViewModels.Roles
{
    public class RoleListViewModel
    {
        public List<RoleListItemViewModel> Roles { get; set; } = new();
    }

    public class RoleListItemViewModel
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

    public class RoleDetailsViewModel : EditRoleViewModel
    {
    }

    public class RolePermissionRowViewModel
    {
        public int ApplicationPageID { get; set; }
        public string PageKey { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class RolePermissionModuleViewModel
    {
        public int ApplicationModuleID { get; set; }
        public string ModuleKey { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public List<RolePermissionRowViewModel> Pages { get; set; } = new();
    }

    public class CreateRoleViewModel
    {
        [Required(ErrorMessage = "Please enter Role Name.")]
        [MaxLength(50)]
        [Display(Name = "Role Name")]
        public string RoleName { get; set; } = string.Empty;

        [MaxLength(255)]
        [Display(Name = "Role Description")]
        public string? RoleDescription { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public List<RolePermissionModuleViewModel> Modules { get; set; } = new();
    }

    public class EditRoleViewModel : CreateRoleViewModel
    {
        public int RoleID { get; set; }
        public bool IsSystemRole { get; set; }
    }
}
