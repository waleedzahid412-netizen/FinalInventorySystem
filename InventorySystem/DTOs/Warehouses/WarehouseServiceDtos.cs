using System;

namespace InventorySystem.DTOs.Warehouses
{
    public class WarehouseListItemDto
    {
        public int WarehouseID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public bool IsMain { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateWarehouseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsMain { get; set; }
    }

    public class EditWarehouseDto
    {
        public int WarehouseID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public bool IsMain { get; set; }
    }
}
