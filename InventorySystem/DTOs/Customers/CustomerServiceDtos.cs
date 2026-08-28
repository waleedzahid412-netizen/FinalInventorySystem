using System;
using InventorySystem.DTOs.Common;

namespace InventorySystem.DTOs.Customers
{
    public class CreateCustomerDto
    {
        public string ShopName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? AreaID { get; set; }
        public int? SubAreaID { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; } = 0;
        public decimal? PreferredDiscountPercent { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EditCustomerDto
    {
        public int CustomerID { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? AreaID { get; set; }
        public int? SubAreaID { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; } = 0;
        public decimal? PreferredDiscountPercent { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CustomerListDto
    {
        public int CustomerID { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? AreaName { get; set; }
        public string? SubAreaName { get; set; }
        public string? TaxID { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; }
        public decimal OutstandingReceivable { get; set; }
        public bool IsCreditLimitExceeded => CreditLimit > 0 && OutstandingReceivable > CreditLimit;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Composed Customer Details DTO combining Info, Financial Summary, and optionally history tabs.
    /// </summary>
    public class CustomerDetailsDto
    {
        public CustomerInfoDto Info { get; set; } = new CustomerInfoDto();
        public CustomerFinancialSummaryDto FinancialSummary { get; set; } = new CustomerFinancialSummaryDto();
        public PagedResult<CustomerSalesHistoryDto>? SalesHistory { get; set; }
        public PagedResult<CustomerPaymentHistoryDto>? PaymentHistory { get; set; }
        public PagedResult<CustomerLedgerEntryDto>? CustomerLedger { get; set; }
    }
}
