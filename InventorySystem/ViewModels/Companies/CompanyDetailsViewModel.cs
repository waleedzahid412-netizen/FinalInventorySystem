using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;

namespace InventorySystem.ViewModels.Companies
{
    public class CompanyDetailsViewModel
    {
        public CompanyDetailsDto Company { get; set; } = new CompanyDetailsDto();

        public string ActiveTab { get; set; } = "purchases"; // purchases | payments | ledger

        public PagedResult<CompanyPurchaseHistoryDto> PurchaseHistory { get; set; } = new PagedResult<CompanyPurchaseHistoryDto>();
        public PagedResult<CompanyPaymentHistoryDto> PaymentHistory { get; set; } = new PagedResult<CompanyPaymentHistoryDto>();
        public PagedResult<CompanyLedgerEntryDto> CompanyLedger { get; set; } = new PagedResult<CompanyLedgerEntryDto>();
    }
}
