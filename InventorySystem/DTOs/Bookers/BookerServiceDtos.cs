namespace InventorySystem.DTOs.Bookers
{
    public class BookerFilterDto
    {
        /// <summary>
        /// Set by BookerService from ICompanyContext when a specific company is selected.
        /// Leave 0 / unset in All Companies mode so the repository returns all companies.
        /// </summary>
        public int CompanyID { get; set; }

        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "Name";
        public bool IsAscending { get; set; } = true;
    }

    public class BookerListItemDto
    {
        public int BookerID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CNIC { get; set; }
        public string? Phone { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateBookerDto
    {
        public string Name { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EditBookerDto
    {
        public int BookerID { get; set; }
        public int CompanyID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; }

        public string CompanyName { get; set; } = string.Empty;
        public bool CanChangeCompany { get; set; }
        public decimal OutstandingBalance { get; set; }
    }
}
