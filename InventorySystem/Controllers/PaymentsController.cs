using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Authorization;
using InventorySystem.Constants;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Payments;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.Payments;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class PaymentsController : InventoryController
    {
        private readonly IPaymentService _paymentService;
        private readonly ILookupService _lookupService;
        private readonly ICompanyContext _companyContext;

        public PaymentsController(IPaymentService paymentService, ILookupService lookupService, ICompanyContext companyContext)
        {
            _paymentService = paymentService;
            _lookupService = lookupService;
            _companyContext = companyContext;
        }

        // GET: /Payments/CustomerPayments
        [HttpGet]
        public async Task<IActionResult> CustomerPayments([FromQuery] PaymentFilterDto filter, CancellationToken cancellationToken)
        {
            filter ??= new PaymentFilterDto();
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }

            var pagedResult = await _paymentService.GetPagedCustomerPaymentsAsync(filter, cancellationToken);
            var customers = await _lookupService.GetCustomersAsync(cancellationToken);

            var vm = new CustomerPaymentListViewModel
            {
                Filter = filter,
                PagedResult = pagedResult,
                Customers = customers.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList()
            };

            return View(vm);
        }

        // GET: /Payments/CompanyPayments
        [HttpGet]
        public async Task<IActionResult> CompanyPayments([FromQuery] PaymentFilterDto filter, CancellationToken cancellationToken)
        {
            filter ??= new PaymentFilterDto();
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }

            var pagedResult = await _paymentService.GetPagedCompanyPaymentsAsync(filter, cancellationToken);
            var companies = await _lookupService.GetCompaniesAsync(cancellationToken);

            var vm = new CompanyPaymentListViewModel
            {
                Filter = filter,
                PagedResult = pagedResult,
                Companies = companies.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList()
            };

            return View(vm);
        }

        // GET: /Payments/GetUnpaidCustomerInvoices?customerId=X
        [HttpGet]
        [RequirePermission(PageKeys.CustomerPayments, PermissionAction.View)]
        public async Task<IActionResult> GetUnpaidCustomerInvoices(int customerId, CancellationToken cancellationToken)
        {
            // Soft-scope applied inside PaymentService when HasCompany (All Companies → consolidated).
            var invoices = await _paymentService.GetUnpaidCustomerInvoicesAsync(customerId, cancellationToken);
            return Json(invoices);
        }

        // GET: /Payments/GetUnpaidCompanyInvoices?companyId=X
        [HttpGet]
        [RequirePermission(PageKeys.CompanyPayments, PermissionAction.View)]
        public async Task<IActionResult> GetUnpaidCompanyInvoices(int companyId, CancellationToken cancellationToken)
        {
            var invoices = await _paymentService.GetUnpaidCompanyInvoicesAsync(companyId, cancellationToken);
            return Json(invoices);
        }

        // POST: /Payments/RecordCustomerPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PageKeys.CustomerPayments, PermissionAction.Add)]
        public async Task<IActionResult> RecordCustomerPayment([FromBody] RecordCustomerPaymentRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request payload." });
            }

            int userId = GetCurrentUserId();
            var result = await _paymentService.RecordCustomerPaymentAsync(request, userId, cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return Json(new { success = false, message = result.Message });
            }

            var payment = result.Data;
            return Json(new { success = true, message = $"Payment #{payment.PaymentNumber} of PKR {payment.Amount:N2} recorded successfully!", paymentId = payment.CustomerPaymentID });
        }

        // POST: /Payments/RecordCompanyPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PageKeys.CompanyPayments, PermissionAction.Add)]
        public async Task<IActionResult> RecordCompanyPayment([FromBody] RecordCompanyPaymentRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request payload." });
            }

            int userId = GetCurrentUserId();
            var result = await _paymentService.RecordCompanyPaymentAsync(request, userId, cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return Json(new { success = false, message = result.Message });
            }

            var payment = result.Data;
            return Json(new { success = true, message = $"Payment #{payment.PaymentNumber} of PKR {payment.Amount:N2} recorded successfully!", paymentId = payment.CompanyPaymentID });
        }

        // GET: /Payments/GetCustomerPaymentHistoryPartial?invoiceId=X
        [HttpGet]
        [RequirePermission(PageKeys.CustomerPayments, PermissionAction.View)]
        public async Task<IActionResult> GetCustomerPaymentHistoryPartial(int invoiceId, CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetPaymentsBySalesInvoiceAsync(invoiceId, cancellationToken);
            return PartialView("_CustomerPaymentHistoryPartial", payments);
        }

        // GET: /Payments/GetCompanyPaymentHistoryPartial?invoiceId=X
        [HttpGet]
        [RequirePermission(PageKeys.CompanyPayments, PermissionAction.View)]
        public async Task<IActionResult> GetCompanyPaymentHistoryPartial(int invoiceId, CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetPaymentsByPurchaseInvoiceAsync(invoiceId, cancellationToken);
            return PartialView("_CompanyPaymentHistoryPartial", payments);
        }

        // GET: /Payments/CustomerPaymentReceipt/5
        [HttpGet]
        [RequirePermission(PageKeys.CustomerPayments, PermissionAction.View)]
        public async Task<IActionResult> CustomerPaymentReceipt(int id, CancellationToken cancellationToken)
        {
            var payment = await _paymentService.GetCustomerPaymentByIdAsync(id, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }
            return View("CustomerReceipt", payment);
        }

        // GET: /Payments/CompanyPaymentReceipt/5
        [HttpGet]
        [RequirePermission(PageKeys.CompanyPayments, PermissionAction.View)]
        public async Task<IActionResult> CompanyPaymentReceipt(int id, CancellationToken cancellationToken)
        {
            var payment = await _paymentService.GetCompanyPaymentByIdAsync(id, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }
            return View("CompanyReceipt", payment);
        }

        // GET: /Payments/Cheques
        [HttpGet]
        public async Task<IActionResult> Cheques([FromQuery] ChequeFilterDto filter, CancellationToken cancellationToken)
        {
            filter ??= new ChequeFilterDto();
            if (await _companyContext.TryResolveAsync(cancellationToken) && _companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }

            var pagedResult = await _paymentService.GetPagedChequesAsync(filter, cancellationToken);

            var vm = new ChequeManagementViewModel
            {
                Filter = filter,
                PagedResult = pagedResult
            };

            return View("ChequeManagement", vm);
        }

        // POST: /Payments/UpdateChequeStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PageKeys.Cheques, PermissionAction.Edit)]
        public async Task<IActionResult> UpdateChequeStatus([FromBody] UpdateChequeStatusRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.PaymentID <= 0)
            {
                return Json(new { success = false, message = "Invalid request parameters." });
            }

            int userId = GetCurrentUserId();
            OperationResult result;

            bool isCustomer = string.Equals(request.PaymentType, "Customer", StringComparison.OrdinalIgnoreCase);
            string targetStatus = request.NewStatus?.Trim() ?? string.Empty;

            if (isCustomer)
            {
                if (string.Equals(targetStatus, "Cleared", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.ClearCustomerChequeAsync(request.PaymentID, userId, cancellationToken);
                }
                else if (string.Equals(targetStatus, "Bounced", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.BounceCustomerChequeAsync(request.PaymentID, request.Remarks ?? "Cheque bounced", userId, cancellationToken);
                }
                else if (string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.CancelCustomerChequeAsync(request.PaymentID, request.Remarks ?? "Cheque cancelled", userId, cancellationToken);
                }
                else
                {
                    return Json(new { success = false, message = $"Invalid target status '{targetStatus}'." });
                }
            }
            else
            {
                if (string.Equals(targetStatus, "Cleared", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.ClearCompanyChequeAsync(request.PaymentID, userId, cancellationToken);
                }
                else if (string.Equals(targetStatus, "Bounced", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.BounceCompanyChequeAsync(request.PaymentID, request.Remarks ?? "Cheque bounced", userId, cancellationToken);
                }
                else if (string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    result = await _paymentService.CancelCompanyChequeAsync(request.PaymentID, request.Remarks ?? "Cheque cancelled", userId, cancellationToken);
                }
                else
                {
                    return Json(new { success = false, message = $"Invalid target status '{targetStatus}'." });
                }
            }

            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
