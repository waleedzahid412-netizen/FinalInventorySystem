using System.Collections.Generic;

namespace InventorySystem.DTOs.UserManagement
{
    public class UserFilterDto
    {
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class UserListItemDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> CompanyNames { get; set; } = new();
    }

    public class CreateUserDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int RoleID { get; set; }
        public List<int> CompanyIds { get; set; } = new();
    }

    public class UpdateUserDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
        public string? Phone { get; set; }
        public int RoleID { get; set; }
        public bool IsActive { get; set; } = true;
        public List<int> CompanyIds { get; set; } = new();
    }

    public class UserEditDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<int> CompanyIds { get; set; } = new();
    }
}
