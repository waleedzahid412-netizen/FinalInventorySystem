namespace InventorySystem.DTOs.Suppliers
{
    public class SupplierFilterDto
    {
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public string? Type { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "Name";
        public bool IsAscending { get; set; } = true;
    }

    public class SupplierListItemDto
    {
        public int SupplierID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CNIC { get; set; }
        public string? Phone { get; set; }
        public string Type { get; set; } = "Employee";
        public bool IsActive { get; set; }
    }

    public class CreateSupplierDto
    {
        public string Name { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Type { get; set; } = "Employee";
        public bool IsActive { get; set; } = true;
    }

    public class EditSupplierDto
    {
        public int SupplierID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Type { get; set; } = "Employee";
        public bool IsActive { get; set; }
    }
}
