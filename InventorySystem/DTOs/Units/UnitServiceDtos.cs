using System;

namespace InventorySystem.DTOs.Units
{
    public class UnitListItemDto
    {
        public int UnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUnitDto
    {
        public string UnitName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class EditUnitDto
    {
        public int UnitID { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
