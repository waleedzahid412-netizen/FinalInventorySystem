using System;

namespace InventorySystem.DTOs.Categories
{
    public class CategoryListItemDto
    {
        public int CategoryID { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateCategoryDto
    {
        public int CompanyID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class EditCategoryDto
    {
        public int CategoryID { get; set; }
        public int CompanyID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
