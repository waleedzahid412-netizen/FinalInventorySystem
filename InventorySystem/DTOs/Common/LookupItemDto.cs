using System.Collections.Generic;

namespace InventorySystem.DTOs.Common
{
    public class LookupItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
    }
}
