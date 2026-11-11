using System;

namespace InventorySystem.DTOs.Areas
{
    public class AreaListItemDto
    {
        public int AreaID { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SubAreaCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AreaOptionDto
    {
        public int AreaID { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }

    public class CreateAreaDto
    {
        public string AreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class EditAreaDto
    {
        public int AreaID { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
