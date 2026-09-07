using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Validation;

namespace InventorySystem.ViewModels.UserManagement
{
    public class UserListViewModel
    {
        public UserFilterDto Filter { get; set; } = new();
        public PagedResult<UserListItemDto> Users { get; set; } = new();
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        [MaxLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [PasswordPolicy]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Required]
        [Display(Name = "Role")]
        public int RoleID { get; set; }

        [Display(Name = "Assigned Companies")]
        public List<int> CompanyIds { get; set; } = new();

        public List<RoleOptionViewModel> Roles { get; set; } = new();
        public List<CompanyOptionViewModel> Companies { get; set; } = new();
    }

    public class EditUserViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        [MaxLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [PasswordPolicy]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [MaxLength(20)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Required]
        [Display(Name = "Role")]
        public int RoleID { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Assigned Companies")]
        public List<int> CompanyIds { get; set; } = new();

        public List<RoleOptionViewModel> Roles { get; set; } = new();
        public List<CompanyOptionViewModel> Companies { get; set; } = new();
    }

    public class RoleOptionViewModel
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class CompanyOptionViewModel
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }

    public class UserDetailsViewModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> CompanyNames { get; set; } = new();
    }
}
