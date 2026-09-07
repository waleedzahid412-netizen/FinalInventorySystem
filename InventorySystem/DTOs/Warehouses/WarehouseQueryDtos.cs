namespace InventorySystem.DTOs.Warehouses
{
    public class WarehouseFilterDto
    {
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsMain { get; set; }
        public string SortBy { get; set; } = "Name";
        public bool IsAscending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
