using System;
using System.Linq;
using System.Security.Claims;
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
    public class PaymentsController : Controller
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

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Fallback default admin user ID
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
        public async Task<IActionResult> GetUnpaidCustomerInvoices(int customerId, CancellationToken cancellationToken)
        {
            // Soft-scope applied inside PaymentService when HasCompany (All Companies → consolidated).
            var invoices = await _paymentService.GetUnpaidCustomerInvoicesAsync(customerId, cancellationToken);
            return Json(invoices);
        }

        // GET: /Payments/GetUnpaidCompanyInvoices?companyId=X
        [HttpGet]
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

            try
            {
                int userId = GetCurrentUserId();
                var payment = await _paymentService.RecordCustomerPaymentAsync(request, userId, cancellationToken);
                return Json(new { success = true, message = $"Payment #{payment.PaymentNumber} of PKR {payment.Amount:N2} recorded successfully!", paymentId = payment.CustomerPaymentID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Payments/RecordCompanyPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordCompanyPayment([FromBody] RecordCompanyPaymentRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request payload." });
            }

            try
            {
                int userId = GetCurrentUserId();
                var payment = await _paymentService.RecordCompanyPaymentAsync(request, userId, cancellationToken);
                return Json(new { success = true, message = $"Payment #{payment.PaymentNumber} of PKR {payment.Amount:N2} recorded successfully!", paymentId = payment.CompanyPaymentID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Payments/GetCustomerPaymentHistoryPartial?invoiceId=X
        [HttpGet]
        public async Task<IActionResult> GetCustomerPaymentHistoryPartial(int invoiceId, CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetPaymentsBySalesInvoiceAsync(invoiceId, cancellationToken);
            return PartialView("_CustomerPaymentHistoryPartial", payments);
        }

        // GET: /Payments/GetCompanyPaymentHistoryPartial?invoiceId=X
        [HttpGet]
        public async Task<IActionResult> GetCompanyPaymentHistoryPartial(int invoiceId, CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetPaymentsByPurchaseInvoiceAsync(invoiceId, cancellationToken);
            return PartialView("_CompanyPaymentHistoryPartial", payments);
        }

        // GET: /Payments/CustomerPaymentReceipt/5
        [HttpGet]
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
