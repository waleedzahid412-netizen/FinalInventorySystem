using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Sales;

namespace InventorySystem.ViewModels.Sales
{
    public class SalesFilterViewModel
    {
        [Display(Name = "Invoice Number")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerID { get; set; }

        [Display(Name = "Warehouse")]
        public int? WarehouseID { get; set; }

        [Display(Name = "Supplier")]
        public int? SupplierID { get; set; }

        /// <summary>Set from ambient scope in the controller — not from the form when HasCompany.</summary>
        public int? CompanyID { get; set; }

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

        public SalesFilterDto ToDto()
        {
            return new SalesFilterDto
            {
                InvoiceNumber = InvoiceNumber,
                CustomerID = CustomerID,
                WarehouseID = WarehouseID,
                SupplierID = SupplierID,
                CompanyID = CompanyID,
                DateFrom = DateFrom,
                DateTo = DateTo,
                PaymentStatus = PaymentStatus,
                PageNumber = PageNumber,
                PageSize = PageSize
            };
        }
    }

    public class SalesListViewModel
    {
        public PagedResult<SalesListDto> Items { get; set; } = new PagedResult<SalesListDto>(new List<SalesListDto>(), 0, 1, 10);
        public SalesFilterViewModel Filter { get; set; } = new SalesFilterViewModel();

        public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Warehouses { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> PaymentStatuses { get; set; } = new List<SelectListItem>();
    }

    public class CreateSalesItemInputViewModel
    {
        [Required(ErrorMessage = "Product is required.")]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        public int ProductUnitID { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.001, 1000000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Unit Price is required.")]
        [Range(0, 100000000, ErrorMessage = "Unit Price cannot be negative.")]
        public decimal UnitPrice { get; set; } = 0;

        [Range(0, 100000000, ErrorMessage = "Discount cannot be negative.")]
        public decimal DiscountAmount { get; set; } = 0;

        public string ItemType { get; set; } = "NORMAL";
        public int? PromotionID { get; set; }
    }

    public class CreateSalesViewModel
    {
        /// <summary>Read-only display of the ambient company scope (never posted as the invoice company source).</summary>
        [Display(Name = "Company")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Customer.")]
        [Display(Name = "Customer")]
        public int CustomerID { get; set; }

        /// <summary>UI filter helper — not stored on invoice; Area/SubArea snapshot comes from Customer on finalize.</summary>
        [Display(Name = "Area")]
        public int? AreaID { get; set; }

        /// <summary>UI filter helper — not stored on invoice; Area/SubArea snapshot comes from Customer on finalize.</summary>
        [Display(Name = "Sub-Area")]
        public int? SubAreaID { get; set; }

        [Required(ErrorMessage = "Booker is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Booker.")]
        [Display(Name = "Booker")]
        public int BookerID { get; set; }

        [Required(ErrorMessage = "Salesperson is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Salesperson.")]
        [Display(Name = "Salesperson")]
        public int SalespersonID { get; set; }

        [Required(ErrorMessage = "Warehouse is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a destination Warehouse.")]
        [Display(Name = "Warehouse")]
        public int WarehouseID { get; set; }

        [Display(Name = "Supplier")]
        public int? SupplierID { get; set; }

        [MaxLength(50, ErrorMessage = "Invoice Number cannot exceed 50 characters.")]
        [Display(Name = "Invoice Number (Auto-generated if empty)")]
        public string? InvoiceNumber { get; set; }

        [Required(ErrorMessage = "Invoice Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        [Display(Name = "Remarks / Notes")]
        public string? Remarks { get; set; }

        public int? AppliedDiscountRuleID { get; set; }

        /// <summary>None | Automatic | Manual</summary>
        [Display(Name = "Discount Mode")]
        [MaxLength(20)]
        public string DiscountMode { get; set; } = "None";

        /// <summary>Percentage | FixedAmount — used when DiscountMode = Manual.</summary>
        [Display(Name = "Manual Discount Type")]
        [MaxLength(20)]
        public string? ManualDiscountType { get; set; } = "Percentage";

        /// <summary>Percent or fixed amount — used when DiscountMode = Manual.</summary>
        [Display(Name = "Manual Discount Value")]
        [Range(0, 100000000, ErrorMessage = "Manual discount cannot be negative.")]
        public decimal ManualDiscountValue { get; set; }

        /// <summary>Seller choice: apply eligible promotions (FREE items) on finalize.</summary>
        [Display(Name = "Apply Promotions")]
        public bool ApplyPromotions { get; set; } = true;

        /// <summary>Serialized JSON array from dynamic client grid</summary>
        [Required(ErrorMessage = "At least one item is required.")]
        public string ItemsJson { get; set; } = "[]";

        // UI Dropdowns
        public IEnumerable<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> SubAreas { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Bookers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Salespersons { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Warehouses { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Products { get; set; } = new List<SelectListItem>();
    }

    public class SalesDetailsViewModel
    {
        public SalesDetailsDto Details { get; set; } = new SalesDetailsDto();
        public bool IsLocked => Details.Header.IsLocked || Details.Header.PaidAmount > 0;
    }

    public class EditSalesViewModel
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>Immutable invoice company (BR-046) — display only.</summary>
        public int CompanyID { get; set; }

        [Display(Name = "Company")]
        public string CompanyName { get; set; } = string.Empty;

        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int BookerID { get; set; }
        public int SalespersonID { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int? SupplierID { get; set; }

        [Required(ErrorMessage = "Invoice Date is required.")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        public string? Remarks { get; set; }
        public int? AppliedDiscountRuleID { get; set; }

        /// <summary>None | Automatic | Manual</summary>
        [Display(Name = "Discount Mode")]
        [MaxLength(20)]
        public string DiscountMode { get; set; } = "None";

        /// <summary>Percentage | FixedAmount — used when DiscountMode = Manual.</summary>
        [Display(Name = "Manual Discount Type")]
        [MaxLength(20)]
        public string? ManualDiscountType { get; set; } = "Percentage";

        /// <summary>Percent or fixed amount — used when DiscountMode = Manual.</summary>
        [Display(Name = "Manual Discount Value")]
        [Range(0, 100000000, ErrorMessage = "Manual discount cannot be negative.")]
        public decimal ManualDiscountValue { get; set; }

        /// <summary>Seller choice: apply eligible promotions (FREE items) on save.</summary>
        [Display(Name = "Apply Promotions")]
        public bool ApplyPromotions { get; set; } = true;

        [Required(ErrorMessage = "An edit reason is required.")]
        [MaxLength(500, ErrorMessage = "Edit reason cannot exceed 500 characters.")]
        public string EditReason { get; set; } = string.Empty;

        public string ItemsJson { get; set; } = "[]";

        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Bookers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Salespersons { get; set; } = new List<SelectListItem>();
    }
}
