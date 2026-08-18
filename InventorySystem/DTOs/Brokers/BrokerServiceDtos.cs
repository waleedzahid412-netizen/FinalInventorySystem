namespace InventorySystem.DTOs.Brokers
{
    public class BrokerFilterDto
    {
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "Name";
        public bool IsAscending { get; set; } = true;
    }

    public class BrokerListItemDto
    {
        public int BrokerID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateBrokerDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EditBrokerDto
    {
        public int BrokerID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
    }
}
