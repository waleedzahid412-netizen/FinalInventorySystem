using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Returns;

namespace InventorySystem.ViewModels.Returns
{
    public class CustomerReturnsListViewModel
    {
        public SalesReturnFilterDto Filter { get; set; } = new SalesReturnFilterDto();
        public DTOs.Common.PagedResult<SalesReturnListDto> Returns { get; set; } = new DTOs.Common.PagedResult<SalesReturnListDto>(new List<SalesReturnListDto>(), 0, 1, 10);
        public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
    }

    public class CompanyReturnsListViewModel
    {
        public PurchaseReturnFilterDto Filter { get; set; } = new PurchaseReturnFilterDto();
        public DTOs.Common.PagedResult<PurchaseReturnListDto> Returns { get; set; } = new DTOs.Common.PagedResult<PurchaseReturnListDto>(new List<PurchaseReturnListDto>(), 0, 1, 10);
        public IEnumerable<SelectListItem> Companies { get; set; } = new List<SelectListItem>();
    }

    public class ProcessSalesReturnViewModel
    {
        public SalesReturnEligibilityDto Eligibility { get; set; } = new SalesReturnEligibilityDto();
        public ProcessSalesReturnRequest Request { get; set; } = new ProcessSalesReturnRequest();
    }

    public class ProcessPurchaseReturnViewModel
    {
        public PurchaseReturnEligibilityDto Eligibility { get; set; } = new PurchaseReturnEligibilityDto();
        public ProcessPurchaseReturnRequest Request { get; set; } = new ProcessPurchaseReturnRequest();
    }
}
