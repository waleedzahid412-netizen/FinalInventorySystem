using System;

namespace InventorySystem.DTOs.SubAreas
{
    public class SubAreaListItemDto
    {
        public int SubAreaID { get; set; }
        public int AreaID { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string SubAreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSubAreaDto
    {
        public int AreaID { get; set; }
        public string SubAreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class EditSubAreaDto
    {
        public int SubAreaID { get; set; }
        public int AreaID { get; set; }
        public string SubAreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
