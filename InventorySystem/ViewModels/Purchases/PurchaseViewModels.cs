using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Purchases;

namespace InventorySystem.ViewModels.Purchases
{
    public class PurchaseListViewModel
    {
        public PagedResult<PurchaseListDto> Items { get; set; } = new PagedResult<PurchaseListDto>(new List<PurchaseListDto>(), 0, 1, 10);
        public PurchaseFilterViewModel Filter { get; set; } = new PurchaseFilterViewModel();
        public SelectList Companies { get; set; } = new SelectList(new List<SelectListItem>());
        public SelectList Warehouses { get; set; } = new SelectList(new List<SelectListItem>());
        public string CompanyName { get; set; } = string.Empty;
    }

    public class PurchaseFilterViewModel
    {
        [Display(Name = "Supplier Invoice #")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "Company")]
        public int? CompanyID { get; set; }

        [Display(Name = "Warehouse")]
        public int? WarehouseID { get; set; }

        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }

        [Display(Name = "Payment Status")]
        public string? PaymentStatus { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class CreatePurchaseViewModel
    {
        [Required(ErrorMessage = "Please select a Company.")]
        [Display(Name = "Company *")]
        public int CompanyID { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a Warehouse.")]
        [Display(Name = "Warehouse *")]
        public int WarehouseID { get; set; }

        [Display(Name = "Supplier Invoice Number")]
        [MaxLength(50, ErrorMessage = "Supplier invoice number cannot exceed 50 characters.")]
        public string? SupplierInvoiceNumber { get; set; }

        [Required(ErrorMessage = "Please select a Purchase Date.")]
        [Display(Name = "Purchase Date *")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Display(Name = "Notes")]
        [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        /// <summary>
        /// JSON-serialized list of line items from dynamic JavaScript grid.
        /// </summary>
        [Required(ErrorMessage = "At least one product line item is required.")]
        public string ItemsJson { get; set; } = string.Empty;

        public SelectList Companies { get; set; } = new SelectList(new List<SelectListItem>());
        public SelectList Warehouses { get; set; } = new SelectList(new List<SelectListItem>());
    }

    public class PurchaseDetailsViewModel
    {
        public PurchaseHeaderDto Header { get; set; } = new PurchaseHeaderDto();
        public List<PurchaseItemDto> Items { get; set; } = new List<PurchaseItemDto>();
        public PurchaseFinancialSummaryDto Financial { get; set; } = new PurchaseFinancialSummaryDto();
        public bool IsLocked => Header.PaidAmount > 0;
    }

    public class EditPurchaseViewModel
    {
        public int PurchaseInvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Invoice Date is required.")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        public string? Notes { get; set; }

        [Required(ErrorMessage = "An edit reason is required.")]
        [MaxLength(500, ErrorMessage = "Edit reason cannot exceed 500 characters.")]
        public string EditReason { get; set; } = string.Empty;

        public string ItemsJson { get; set; } = "[]";
    }
}
