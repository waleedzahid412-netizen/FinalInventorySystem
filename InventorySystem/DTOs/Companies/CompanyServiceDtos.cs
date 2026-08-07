using System;
using System.Collections.Generic;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Companies
{
    public class CompanyListItemDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public decimal OutstandingPayable { get; set; }
        public decimal TotalPurchases { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateCompanyDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; } = 0;
    }

    public class EditCompanyDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; }
    }

    public class CompanyDetailsDto
    {
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; }
        public DateTime CreatedAt { get; set; }

        public CompanyFinancialSummaryDto? FinancialSummary { get; set; }
    }
}
