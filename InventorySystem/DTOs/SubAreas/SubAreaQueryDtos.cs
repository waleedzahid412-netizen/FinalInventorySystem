namespace InventorySystem.DTOs.SubAreas
{
    public class SubAreaFilterDto
    {
        public string? SearchTerm { get; set; }
        public int? AreaID { get; set; }
        public bool? IsActive { get; set; }
        public string SortBy { get; set; } = "Name";
        public bool IsAscending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
