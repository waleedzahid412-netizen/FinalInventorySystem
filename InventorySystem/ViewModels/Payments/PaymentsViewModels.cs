using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Payments;

namespace InventorySystem.ViewModels.Payments
{
    public class RecordCustomerPaymentViewModel
    {
        [Required]
        public int InvoiceID { get; set; }

        [Required]
        public int CustomerID { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal AlreadyPaid { get; set; }
        public decimal OutstandingBalance { get; set; }

        [Required(ErrorMessage = "Payment amount is required.")]
        [Range(0.01, 999999999.99, ErrorMessage = "Payment amount must be greater than zero.")]
        [Display(Name = "Payment Amount (PKR)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Reference Number (Cheque / Bank Ref)")]
        public string? ReferenceNumber { get; set; }

        [Required]
        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [MaxLength(255)]
        [Display(Name = "Remarks / Notes")]
        public string? Remarks { get; set; }
    }

    public class RecordCompanyPaymentViewModel
    {
        [Required]
        public int PurchaseInvoiceID { get; set; }

        [Required]
        public int CompanyID { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal AlreadyPaid { get; set; }
        public decimal OutstandingBalance { get; set; }

        [Required(ErrorMessage = "Payment amount is required.")]
        [Range(0.01, 999999999.99, ErrorMessage = "Payment amount must be greater than zero.")]
        [Display(Name = "Payment Amount (PKR)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Reference Number (Cheque / Bank Ref)")]
        public string? ReferenceNumber { get; set; }

        [Required]
        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [MaxLength(255)]
        [Display(Name = "Remarks / Notes")]
        public string? Remarks { get; set; }
    }

    public class CustomerPaymentListViewModel
    {
        public PaymentFilterDto Filter { get; set; } = new PaymentFilterDto();
        public PagedResult<CustomerPaymentDto> PagedResult { get; set; } = new PagedResult<CustomerPaymentDto>();
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Customers { get; set; } = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }

    public class CompanyPaymentListViewModel
    {
        public PaymentFilterDto Filter { get; set; } = new PaymentFilterDto();
        public PagedResult<CompanyPaymentDto> PagedResult { get; set; } = new PagedResult<CompanyPaymentDto>();
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Companies { get; set; } = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }

    public class ChequeManagementViewModel
    {
        public ChequeFilterDto Filter { get; set; } = new ChequeFilterDto();
        public PagedResult<ChequeDetailDto> PagedResult { get; set; } = new PagedResult<ChequeDetailDto>();
    }
}
