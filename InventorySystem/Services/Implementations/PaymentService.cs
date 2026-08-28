using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Payments;
using InventorySystem.Models.Entities;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContext _companyContext;

        public PaymentService(ApplicationDbContext context, ICompanyContext companyContext)
        {
            _context = context;
            _companyContext = companyContext;
        }

        public async Task<CustomerPaymentDto> RecordCustomerPaymentAsync(RecordCustomerPaymentRequest request, int userId, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.Amount <= 0)
            {
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            bool isCheque = string.Equals(request.PaymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase);

            if ((string.Equals(request.PaymentMethod, "Bank", StringComparison.OrdinalIgnoreCase) || isCheque) &&
                string.IsNullOrWhiteSpace(request.ReferenceNumber) && string.IsNullOrWhiteSpace(request.ChequeNumber))
            {
                throw new InvalidOperationException($"Reference / Cheque number is required for {request.PaymentMethod} payments.");
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == request.CustomerID && !c.IsDeleted, cancellationToken);
            if (customer == null)
            {
                throw new KeyNotFoundException($"Customer ID #{request.CustomerID} not found.");
            }

            if (isCheque)
            {
                string chqNum = (request.ChequeNumber ?? request.ReferenceNumber ?? string.Empty).Trim();
                string bankName = (request.BankName ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(chqNum) && !string.IsNullOrEmpty(bankName))
                {
                    bool isDuplicate = await _context.CustomerPayments.AnyAsync(cp =>
                        !cp.IsDeleted &&
                        cp.CustomerID == request.CustomerID &&
                        cp.ChequeNumber != null && cp.ChequeNumber.ToLower() == chqNum.ToLower() &&
                        cp.BankName != null && cp.BankName.ToLower() == bankName.ToLower(),
                        cancellationToken);

                    if (isDuplicate)
                    {
                        throw new InvalidOperationException($"Duplicate Cheque Detected: Cheque #{chqNum} from Bank '{bankName}' has already been recorded for this customer.");
                    }
                }
            }

            SalesInvoice? targetInvoice = null;
            if (request.InvoiceID.HasValue && request.InvoiceID.Value > 0)
            {
                targetInvoice = await _context.SalesInvoices
                    .Include(s => s.Customer)
                    .FirstOrDefaultAsync(s => s.InvoiceID == request.InvoiceID.Value && !s.IsDeleted, cancellationToken);

                if (targetInvoice == null)
                {
                    throw new KeyNotFoundException($"Sales Invoice ID #{request.InvoiceID.Value} not found.");
                }
            }
            else
            {
                var openInvoices = await _context.SalesInvoices
                    .Include(s => s.Customer)
                    .Where(s => s.CustomerID == request.CustomerID && !s.IsDeleted && s.PaymentStatus != "PAID")
                    .OrderBy(s => s.InvoiceDate)
                    .ThenBy(s => s.InvoiceID)
                    .ToListAsync(cancellationToken);

                targetInvoice = openInvoices.FirstOrDefault();
            }

            int targetInvoiceId = targetInvoice?.InvoiceID ?? 0;
            string invoiceNumber = targetInvoice?.InvoiceNumber ?? "UNASSIGNED";
            string customerName = targetInvoice?.Customer?.ShopName ?? targetInvoice?.Customer?.OwnerName ?? customer.ShopName ?? customer.OwnerName ?? string.Empty;

            // If Cheque, default status is "Received" and DO NOT post to ledger or update invoice paid amount yet!
            if (isCheque)
            {
                var chequePayment = new CustomerPayment
                {
                    CustomerID = request.CustomerID,
                    InvoiceID = targetInvoiceId > 0 ? targetInvoiceId : 0,
                    PaymentDate = request.PaymentDate == default ? DateTime.UtcNow : request.PaymentDate,
                    Amount = request.Amount,
                    PaymentMethod = "Cheque",
                    ReferenceNumber = request.ReferenceNumber ?? request.ChequeNumber,
                    Notes = request.Remarks,
                    ReceivedBy = userId,
                    ChequeNumber = request.ChequeNumber ?? request.ReferenceNumber,
                    BankName = request.BankName,
                    IssueDate = request.IssueDate,
                    ChequeDate = request.ChequeDate ?? request.PaymentDate,
                    ChequeStatus = "Received",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _context.CustomerPayments.AddAsync(chequePayment, cancellationToken);

                // Audit Log for Cheque Creation
                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Customer",
                    PaymentID = chequePayment.CustomerPaymentID,
                    PreviousStatus = "None",
                    NewStatus = "Received",
                    Remarks = "Cheque received and logged into system pending clearance.",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

                return new CustomerPaymentDto
                {
                    CustomerPaymentID = chequePayment.CustomerPaymentID,
                    PaymentNumber = $"PAY-{chequePayment.CustomerPaymentID:D6}",
                    CustomerID = request.CustomerID,
                    CustomerName = customerName,
                    InvoiceID = chequePayment.InvoiceID,
                    InvoiceNumber = invoiceNumber,
                    PaymentDate = chequePayment.PaymentDate,
                    Amount = chequePayment.Amount,
                    PaymentMethod = chequePayment.PaymentMethod,
                    ReferenceNumber = chequePayment.ReferenceNumber,
                    Notes = chequePayment.Notes,
                    ReceivedBy = userId,
                    ReceivedByUserName = user?.FullName ?? user?.Username ?? "System",
                    ChequeNumber = chequePayment.ChequeNumber,
                    BankName = chequePayment.BankName,
                    IssueDate = chequePayment.IssueDate,
                    ChequeDate = chequePayment.ChequeDate,
                    ChequeStatus = chequePayment.ChequeStatus,
                    CreatedAt = chequePayment.CreatedAt
                };
            }

            // Cash, Bank Transfer, or Online Payments: Execute immediate clearance and ledger posting
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                List<SalesInvoice> openInvoicesToUpdate = new List<SalesInvoice>();
                if (request.InvoiceID.HasValue && request.InvoiceID.Value > 0)
                {
                    openInvoicesToUpdate.Add(targetInvoice!);
                }
                else
                {
                    openInvoicesToUpdate = await _context.SalesInvoices
                        .Include(s => s.Customer)
                        .Where(s => s.CustomerID == request.CustomerID && !s.IsDeleted && s.PaymentStatus != "PAID")
                        .OrderBy(s => s.InvoiceDate)
                        .ThenBy(s => s.InvoiceID)
                        .ToListAsync(cancellationToken);

                    if (!openInvoicesToUpdate.Any())
                    {
                        throw new InvalidOperationException("This customer has no open unpaid invoices.");
                    }
                    targetInvoice = openInvoicesToUpdate.First();
                }

                var payment = new CustomerPayment
                {
                    CustomerID = request.CustomerID,
                    InvoiceID = targetInvoice!.InvoiceID,
                    PaymentDate = request.PaymentDate == default ? DateTime.UtcNow : request.PaymentDate,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    ReferenceNumber = request.ReferenceNumber,
                    Notes = request.Remarks,
                    ReceivedBy = userId,
                    ChequeStatus = "Cleared",
                    ClearedAt = DateTime.UtcNow,
                    ClearedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _context.CustomerPayments.AddAsync(payment, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // Allocate payment amount across target invoice(s)
                decimal remainingToAllocate = request.Amount;
                foreach (var inv in openInvoicesToUpdate)
                {
                    if (remainingToAllocate <= 0) break;
                    decimal invOpenBal = inv.GrandTotal - inv.PaidAmount;
                    if (invOpenBal <= 0) continue;

                    decimal alloc = Math.Min(remainingToAllocate, invOpenBal);
                    inv.PaidAmount += alloc;
                    inv.PaymentStatus = (inv.GrandTotal - inv.PaidAmount <= 0.001m) ? "PAID" : "PARTIAL";
                    inv.IsLocked = true;

                    remainingToAllocate -= alloc;
                }

                // Insert Credit entry into CustomerLedger
                var ledger = new CustomerLedger
                {
                    CustomerID = request.CustomerID,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = "PAYMENT",
                    DebitAmount = 0.00m,
                    CreditAmount = request.Amount,
                    SalesInvoiceID = targetInvoice.InvoiceID,
                    CustomerPaymentID = payment.CustomerPaymentID,
                    Description = request.InvoiceID.HasValue && request.InvoiceID.Value > 0
                        ? $"Payment received for Sales Invoice #{targetInvoice.InvoiceNumber} ({request.PaymentMethod})"
                        : $"Payment received (Auto-allocated to open invoices) ({request.PaymentMethod})",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CustomerLedgers.AddAsync(ledger, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

                return new CustomerPaymentDto
                {
                    CustomerPaymentID = payment.CustomerPaymentID,
                    PaymentNumber = $"PAY-{payment.CustomerPaymentID:D6}",
                    CustomerID = request.CustomerID,
                    CustomerName = customerName,
                    InvoiceID = targetInvoice.InvoiceID,
                    InvoiceNumber = targetInvoice.InvoiceNumber,
                    PaymentDate = payment.PaymentDate,
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod,
                    ReferenceNumber = payment.ReferenceNumber,
                    Notes = payment.Notes,
                    ReceivedBy = userId,
                    ReceivedByUserName = user?.FullName ?? user?.Username ?? "System",
                    ChequeStatus = "Cleared",
                    ClearedAt = payment.ClearedAt,
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<CompanyPaymentDto> RecordCompanyPaymentAsync(RecordCompanyPaymentRequest request, int userId, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.Amount <= 0)
            {
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            bool isCheque = string.Equals(request.PaymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase);

            if ((string.Equals(request.PaymentMethod, "Bank", StringComparison.OrdinalIgnoreCase) || isCheque) &&
                string.IsNullOrWhiteSpace(request.ReferenceNumber) && string.IsNullOrWhiteSpace(request.ChequeNumber))
            {
                throw new InvalidOperationException($"Reference / Cheque number is required for {request.PaymentMethod} payments.");
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyID == request.CompanyID && !c.IsDeleted, cancellationToken);
            if (company == null)
            {
                throw new KeyNotFoundException($"Company ID #{request.CompanyID} not found.");
            }

            if (isCheque)
            {
                string chqNum = (request.ChequeNumber ?? request.ReferenceNumber ?? string.Empty).Trim();
                string bankName = (request.BankName ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(chqNum) && !string.IsNullOrEmpty(bankName))
                {
                    bool isDuplicate = await _context.CompanyPayments.AnyAsync(cp =>
                        !cp.IsDeleted &&
                        cp.CompanyID == request.CompanyID &&
                        cp.ChequeNumber != null && cp.ChequeNumber.ToLower() == chqNum.ToLower() &&
                        cp.BankName != null && cp.BankName.ToLower() == bankName.ToLower(),
                        cancellationToken);

                    if (isDuplicate)
                    {
                        throw new InvalidOperationException($"Duplicate Cheque Detected: Vendor cheque with Cheque # '{chqNum}' from Bank '{bankName}' has already been recorded for this vendor.");
                    }
                }
            }

            PurchaseInvoice? targetInvoice = null;
            if (request.PurchaseInvoiceID.HasValue && request.PurchaseInvoiceID.Value > 0)
            {
                targetInvoice = await _context.PurchaseInvoices
                    .Include(p => p.Company)
                    .FirstOrDefaultAsync(p => p.PurchaseInvoiceID == request.PurchaseInvoiceID.Value && !p.IsDeleted, cancellationToken);

                if (targetInvoice == null)
                {
                    throw new KeyNotFoundException($"Purchase Invoice ID #{request.PurchaseInvoiceID.Value} not found.");
                }
            }
            else
            {
                var openInvoices = await _context.PurchaseInvoices
                    .Include(p => p.Company)
                    .Where(p => p.CompanyID == request.CompanyID && !p.IsDeleted && p.PaymentStatus != "PAID")
                    .OrderBy(p => p.InvoiceDate)
                    .ThenBy(p => p.PurchaseInvoiceID)
                    .ToListAsync(cancellationToken);

                targetInvoice = openInvoices.FirstOrDefault();
            }

            int targetInvoiceId = targetInvoice?.PurchaseInvoiceID ?? 0;
            string invoiceNumber = targetInvoice?.InvoiceNumber ?? "UNASSIGNED";
            string companyName = targetInvoice?.Company?.CompanyName ?? company.CompanyName;

            if (isCheque)
            {
                var chequePayment = new CompanyPayment
                {
                    CompanyID = request.CompanyID,
                    PurchaseInvoiceID = targetInvoiceId > 0 ? targetInvoiceId : 0,
                    PaymentDate = request.PaymentDate == default ? DateTime.UtcNow : request.PaymentDate,
                    Amount = request.Amount,
                    PaymentMethod = "Cheque",
                    ReferenceNumber = request.ReferenceNumber ?? request.ChequeNumber,
                    PaidBy = userId,
                    ChequeNumber = request.ChequeNumber ?? request.ReferenceNumber,
                    BankName = request.BankName,
                    IssueDate = request.IssueDate,
                    ChequeDate = request.ChequeDate ?? request.PaymentDate,
                    ChequeStatus = "Received",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _context.CompanyPayments.AddAsync(chequePayment, cancellationToken);

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Company",
                    PaymentID = chequePayment.CompanyPaymentID,
                    PreviousStatus = "None",
                    NewStatus = "Received",
                    Remarks = "Vendor cheque issued and logged into system pending clearance.",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

                return new CompanyPaymentDto
                {
                    CompanyPaymentID = chequePayment.CompanyPaymentID,
                    PaymentNumber = $"VPAY-{chequePayment.CompanyPaymentID:D6}",
                    CompanyID = request.CompanyID,
                    CompanyName = companyName,
                    PurchaseInvoiceID = chequePayment.PurchaseInvoiceID,
                    InvoiceNumber = invoiceNumber,
                    PaymentDate = chequePayment.PaymentDate,
                    Amount = chequePayment.Amount,
                    PaymentMethod = chequePayment.PaymentMethod,
                    ReferenceNumber = chequePayment.ReferenceNumber,
                    PaidBy = userId,
                    PaidByUserName = user?.FullName ?? user?.Username ?? "System",
                    ChequeNumber = chequePayment.ChequeNumber,
                    BankName = chequePayment.BankName,
                    IssueDate = chequePayment.IssueDate,
                    ChequeDate = chequePayment.ChequeDate,
                    ChequeStatus = chequePayment.ChequeStatus,
                    CreatedAt = chequePayment.CreatedAt
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                List<PurchaseInvoice> openInvoicesToUpdate = new List<PurchaseInvoice>();
                if (request.PurchaseInvoiceID.HasValue && request.PurchaseInvoiceID.Value > 0)
                {
                    openInvoicesToUpdate.Add(targetInvoice!);
                }
                else
                {
                    openInvoicesToUpdate = await _context.PurchaseInvoices
                        .Include(p => p.Company)
                        .Where(p => p.CompanyID == request.CompanyID && !p.IsDeleted && p.PaymentStatus != "PAID")
                        .OrderBy(p => p.InvoiceDate)
                        .ThenBy(p => p.PurchaseInvoiceID)
                        .ToListAsync(cancellationToken);

                    if (!openInvoicesToUpdate.Any())
                    {
                        throw new InvalidOperationException("This vendor has no open unpaid purchase invoices.");
                    }
                    targetInvoice = openInvoicesToUpdate.First();
                }

                var payment = new CompanyPayment
                {
                    CompanyID = request.CompanyID,
                    PurchaseInvoiceID = targetInvoice!.PurchaseInvoiceID,
                    PaymentDate = request.PaymentDate == default ? DateTime.UtcNow : request.PaymentDate,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    ReferenceNumber = request.ReferenceNumber,
                    PaidBy = userId,
                    ChequeStatus = "Cleared",
                    ClearedAt = DateTime.UtcNow,
                    ClearedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _context.CompanyPayments.AddAsync(payment, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // Allocate payment amount across target invoice(s)
                decimal remainingToAllocate = request.Amount;
                foreach (var inv in openInvoicesToUpdate)
                {
                    if (remainingToAllocate <= 0) break;
                    decimal invOpenBal = inv.GrandTotal - inv.PaidAmount;
                    if (invOpenBal <= 0) continue;

                    decimal alloc = Math.Min(remainingToAllocate, invOpenBal);
                    inv.PaidAmount += alloc;
                    inv.PaymentStatus = (inv.GrandTotal - inv.PaidAmount <= 0.001m) ? "PAID" : "PARTIAL";

                    remainingToAllocate -= alloc;
                }

                // Insert Debit entry into CompanyLedger
                var ledger = new CompanyLedger
                {
                    CompanyID = request.CompanyID,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = "PAYMENT",
                    DebitAmount = request.Amount,
                    CreditAmount = 0.00m,
                    PurchaseInvoiceID = targetInvoice.PurchaseInvoiceID,
                    CompanyPaymentID = payment.CompanyPaymentID,
                    Description = request.PurchaseInvoiceID.HasValue && request.PurchaseInvoiceID.Value > 0
                        ? $"Payment made for Purchase Invoice #{targetInvoice.InvoiceNumber} ({request.PaymentMethod})"
                        : $"Payment made (Auto-allocated to open invoices) ({request.PaymentMethod})",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CompanyLedgers.AddAsync(ledger, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

                return new CompanyPaymentDto
                {
                    CompanyPaymentID = payment.CompanyPaymentID,
                    PaymentNumber = $"VPAY-{payment.CompanyPaymentID:D6}",
                    CompanyID = request.CompanyID,
                    CompanyName = companyName,
                    PurchaseInvoiceID = targetInvoice.PurchaseInvoiceID,
                    InvoiceNumber = targetInvoice.InvoiceNumber,
                    PaymentDate = payment.PaymentDate,
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod,
                    ReferenceNumber = payment.ReferenceNumber,
                    PaidBy = userId,
                    PaidByUserName = user?.FullName ?? user?.Username ?? "System",
                    ChequeStatus = "Cleared",
                    ClearedAt = payment.ClearedAt,
                    CreatedAt = payment.CreatedAt
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // ===== STATE MACHINE & CHEQUE VALIDATION HELPERS =====
        private static bool ValidateChequeTransition(string currentStatus, string targetStatus, out string errorMessage)
        {
            errorMessage = string.Empty;
            currentStatus = (currentStatus ?? string.Empty).Trim();
            targetStatus = (targetStatus ?? string.Empty).Trim();

            if (string.Equals(currentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Cheque is already in status '{currentStatus}'.";
                return false;
            }

            if (!string.Equals(currentStatus, "Received", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Invalid Cheque Status Transition: Cannot transition cheque from '{currentStatus}' to '{targetStatus}'. Cheques in status '{currentStatus}' are finalized and cannot be altered.";
                return false;
            }

            if (string.Equals(targetStatus, "Cleared", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetStatus, "Bounced", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            errorMessage = $"Invalid target status '{targetStatus}'. Allowed statuses are Cleared, Bounced, or Cancelled.";
            return false;
        }

        // ===== CHEQUE CLEARING / BOUNCING / CANCELLATION =====
        public async Task<OperationResult> ClearCustomerChequeAsync(int customerPaymentId, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CustomerPayments
                .Include(cp => cp.Customer)
                .Include(cp => cp.SalesInvoice)
                .FirstOrDefaultAsync(cp => cp.CustomerPaymentID == customerPaymentId && !cp.IsDeleted, cancellationToken);

            if (payment == null)
            {
                return OperationResult.Fail($"Customer Cheque Payment #{customerPaymentId} not found.");
            }

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Cleared", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            // 2. FUTURE-DATED CHEQUE VALIDATION
            DateTime today = DateTime.UtcNow.Date;
            if (payment.ChequeDate.HasValue && payment.ChequeDate.Value.Date > today)
            {
                return OperationResult.Fail($"Future-Dated Cheque Cannot Be Cleared: Cheque #{payment.ChequeNumber ?? $"PAY-{payment.CustomerPaymentID:D6}"} has a future date ({payment.ChequeDate.Value:dd MMM yyyy}) and cannot be cleared prior to its date.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Cleared";
                payment.ClearedAt = DateTime.UtcNow;
                payment.ClearedBy = userId;

                // Perform payment allocation to target or open invoice(s)
                List<SalesInvoice> openInvoices = new List<SalesInvoice>();
                if (payment.InvoiceID > 0)
                {
                    var targetInv = await _context.SalesInvoices.FirstOrDefaultAsync(s => s.InvoiceID == payment.InvoiceID && !s.IsDeleted, cancellationToken);
                    if (targetInv != null) openInvoices.Add(targetInv);
                }

                if (!openInvoices.Any())
                {
                    openInvoices = await _context.SalesInvoices
                        .Where(s => s.CustomerID == payment.CustomerID && !s.IsDeleted && s.PaymentStatus != "PAID")
                        .OrderBy(s => s.InvoiceDate)
                        .ThenBy(s => s.InvoiceID)
                        .ToListAsync(cancellationToken);
                }

                if (openInvoices.Any())
                {
                    decimal remaining = payment.Amount;
                    foreach (var inv in openInvoices)
                    {
                        if (remaining <= 0) break;
                        decimal due = inv.GrandTotal - inv.PaidAmount;
                        if (due <= 0) continue;

                        decimal alloc = Math.Min(remaining, due);
                        inv.PaidAmount += alloc;
                        inv.PaymentStatus = (inv.GrandTotal - inv.PaidAmount <= 0.001m) ? "PAID" : "PARTIAL";
                        inv.IsLocked = true;
                        remaining -= alloc;

                        if (payment.InvoiceID == 0) payment.InvoiceID = inv.InvoiceID;
                    }
                }

                // Post Credit entry to CustomerLedger
                var ledger = new CustomerLedger
                {
                    CustomerID = payment.CustomerID,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = "PAYMENT",
                    DebitAmount = 0.00m,
                    CreditAmount = payment.Amount,
                    SalesInvoiceID = payment.InvoiceID > 0 ? payment.InvoiceID : null,
                    CustomerPaymentID = payment.CustomerPaymentID,
                    Description = $"Cheque cleared (Cheque #{payment.ChequeNumber}, Bank: {payment.BankName ?? "N/A"})",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CustomerLedgers.AddAsync(ledger, cancellationToken);

                // Enhanced Audit Trail
                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Customer",
                    PaymentID = payment.CustomerPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Cleared",
                    Remarks = "Cheque verified and cleared by administrator.",
                    Reason = "Administrator clearance verification",
                    ReferenceNumber = payment.ChequeNumber ?? $"PAY-{payment.CustomerPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok($"Cheque #{payment.ChequeNumber ?? $"PAY-{payment.CustomerPaymentID:D6}"} cleared successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to clear cheque: {ex.Message}");
            }
        }

        public async Task<OperationResult> BounceCustomerChequeAsync(int customerPaymentId, string remarks, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CustomerPayments.FirstOrDefaultAsync(cp => cp.CustomerPaymentID == customerPaymentId && !cp.IsDeleted, cancellationToken);
            if (payment == null) return OperationResult.Fail("Cheque not found.");

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Bounced", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Bounced";
                payment.BouncedAt = DateTime.UtcNow;
                payment.BounceRemarks = remarks;

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Customer",
                    PaymentID = payment.CustomerPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Bounced",
                    Remarks = remarks,
                    Reason = remarks,
                    ReferenceNumber = payment.ChequeNumber ?? $"PAY-{payment.CustomerPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok("Cheque marked as Bounced successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to mark cheque as bounced: {ex.Message}");
            }
        }

        public async Task<OperationResult> CancelCustomerChequeAsync(int customerPaymentId, string remarks, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CustomerPayments.FirstOrDefaultAsync(cp => cp.CustomerPaymentID == customerPaymentId && !cp.IsDeleted, cancellationToken);
            if (payment == null) return OperationResult.Fail("Cheque not found.");

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Cancelled", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Cancelled";

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Customer",
                    PaymentID = payment.CustomerPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Cancelled",
                    Remarks = remarks,
                    Reason = remarks,
                    ReferenceNumber = payment.ChequeNumber ?? $"PAY-{payment.CustomerPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok("Cheque cancelled successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to cancel cheque: {ex.Message}");
            }
        }

        public async Task<OperationResult> ClearCompanyChequeAsync(int companyPaymentId, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CompanyPayments
                .Include(cp => cp.Company)
                .Include(cp => cp.PurchaseInvoice)
                .FirstOrDefaultAsync(cp => cp.CompanyPaymentID == companyPaymentId && !cp.IsDeleted, cancellationToken);

            if (payment == null)
            {
                return OperationResult.Fail($"Company Cheque Payment #{companyPaymentId} not found.");
            }

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Cleared", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            // 2. FUTURE-DATED CHEQUE VALIDATION
            DateTime today = DateTime.UtcNow.Date;
            if (payment.ChequeDate.HasValue && payment.ChequeDate.Value.Date > today)
            {
                return OperationResult.Fail($"Future-Dated Cheque Cannot Be Cleared: Vendor cheque #{payment.ChequeNumber ?? $"VPAY-{payment.CompanyPaymentID:D6}"} has a future date ({payment.ChequeDate.Value:dd MMM yyyy}) and cannot be cleared prior to its date.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Cleared";
                payment.ClearedAt = DateTime.UtcNow;
                payment.ClearedBy = userId;

                List<PurchaseInvoice> openInvoices = new List<PurchaseInvoice>();
                if (payment.PurchaseInvoiceID > 0)
                {
                    var targetInv = await _context.PurchaseInvoices.FirstOrDefaultAsync(p => p.PurchaseInvoiceID == payment.PurchaseInvoiceID && !p.IsDeleted, cancellationToken);
                    if (targetInv != null) openInvoices.Add(targetInv);
                }

                if (!openInvoices.Any())
                {
                    openInvoices = await _context.PurchaseInvoices
                        .Where(p => p.CompanyID == payment.CompanyID && !p.IsDeleted && p.PaymentStatus != "PAID")
                        .OrderBy(p => p.InvoiceDate)
                        .ThenBy(p => p.PurchaseInvoiceID)
                        .ToListAsync(cancellationToken);
                }

                if (openInvoices.Any())
                {
                    decimal remaining = payment.Amount;
                    foreach (var inv in openInvoices)
                    {
                        if (remaining <= 0) break;
                        decimal due = inv.GrandTotal - inv.PaidAmount;
                        if (due <= 0) continue;

                        decimal alloc = Math.Min(remaining, due);
                        inv.PaidAmount += alloc;
                        inv.PaymentStatus = (inv.GrandTotal - inv.PaidAmount <= 0.001m) ? "PAID" : "PARTIAL";
                        remaining -= alloc;

                        if (payment.PurchaseInvoiceID == 0) payment.PurchaseInvoiceID = inv.PurchaseInvoiceID;
                    }
                }

                var ledger = new CompanyLedger
                {
                    CompanyID = payment.CompanyID,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = "PAYMENT",
                    DebitAmount = payment.Amount,
                    CreditAmount = 0.00m,
                    PurchaseInvoiceID = payment.PurchaseInvoiceID > 0 ? payment.PurchaseInvoiceID : null,
                    CompanyPaymentID = payment.CompanyPaymentID,
                    Description = $"Cheque cleared (Cheque #{payment.ChequeNumber}, Bank: {payment.BankName ?? "N/A"})",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CompanyLedgers.AddAsync(ledger, cancellationToken);

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Company",
                    PaymentID = payment.CompanyPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Cleared",
                    Remarks = "Vendor cheque cleared by administrator.",
                    Reason = "Administrator clearance verification",
                    ReferenceNumber = payment.ChequeNumber ?? $"VPAY-{payment.CompanyPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok($"Vendor Cheque #{payment.ChequeNumber ?? $"VPAY-{payment.CompanyPaymentID:D6}"} cleared successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to clear vendor cheque: {ex.Message}");
            }
        }

        public async Task<OperationResult> BounceCompanyChequeAsync(int companyPaymentId, string remarks, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CompanyPayments.FirstOrDefaultAsync(cp => cp.CompanyPaymentID == companyPaymentId && !cp.IsDeleted, cancellationToken);
            if (payment == null) return OperationResult.Fail("Cheque not found.");

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Bounced", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Bounced";
                payment.BouncedAt = DateTime.UtcNow;
                payment.BounceRemarks = remarks;

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Company",
                    PaymentID = payment.CompanyPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Bounced",
                    Remarks = remarks,
                    Reason = remarks,
                    ReferenceNumber = payment.ChequeNumber ?? $"VPAY-{payment.CompanyPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok("Vendor cheque marked as Bounced successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to mark vendor cheque as bounced: {ex.Message}");
            }
        }

        public async Task<OperationResult> CancelCompanyChequeAsync(int companyPaymentId, string remarks, int userId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.CompanyPayments.FirstOrDefaultAsync(cp => cp.CompanyPaymentID == companyPaymentId && !cp.IsDeleted, cancellationToken);
            if (payment == null) return OperationResult.Fail("Cheque not found.");

            // 1. STATE MACHINE VALIDATION
            if (!ValidateChequeTransition(payment.ChequeStatus, "Cancelled", out string transitionError))
            {
                return OperationResult.Fail(transitionError);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string prevStatus = payment.ChequeStatus;
                payment.ChequeStatus = "Cancelled";

                var audit = new ChequeStatusAudit
                {
                    PaymentType = "Company",
                    PaymentID = payment.CompanyPaymentID,
                    PreviousStatus = prevStatus,
                    NewStatus = "Cancelled",
                    Remarks = remarks,
                    Reason = remarks,
                    ReferenceNumber = payment.ChequeNumber ?? $"VPAY-{payment.CompanyPaymentID:D6}",
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ChequeStatusAudits.AddAsync(audit, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OperationResult.Ok("Vendor cheque cancelled successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail($"Failed to cancel vendor cheque: {ex.Message}");
            }
        }

        public async Task<PagedResult<ChequeDetailDto>> GetPagedChequesAsync(ChequeFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new ChequeFilterDto();

            var list = new List<ChequeDetailDto>();

            // Query Customer Cheques
            if (string.IsNullOrWhiteSpace(filter.PaymentType) || string.Equals(filter.PaymentType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(filter.PaymentType, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                var custQuery = _context.CustomerPayments
                    .AsNoTracking()
                    .Include(cp => cp.Customer)
                    .Include(cp => cp.SalesInvoice)
                    .Include(cp => cp.ReceivedByUser)
                    .Where(cp => !cp.IsDeleted && cp.PaymentMethod == "Cheque");

                if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase))
                {
                    custQuery = custQuery.Where(cp => cp.ChequeStatus == filter.Status);
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    custQuery = custQuery.Where(cp =>
                        (cp.ChequeNumber != null && cp.ChequeNumber.Contains(filter.SearchTerm)) ||
                        cp.Customer.ShopName.Contains(filter.SearchTerm) ||
                        cp.Customer.OwnerName.Contains(filter.SearchTerm) ||
                        cp.SalesInvoice.InvoiceNumber.Contains(filter.SearchTerm));
                }

                if (filter.DateFrom.HasValue) custQuery = custQuery.Where(cp => cp.PaymentDate >= filter.DateFrom.Value);
                if (filter.DateTo.HasValue) custQuery = custQuery.Where(cp => cp.PaymentDate <= filter.DateTo.Value.AddDays(1).AddTicks(-1));

                if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
                {
                    int cid = filter.CompanyID.Value;
                    custQuery = custQuery.Where(cp => cp.SalesInvoice != null && cp.SalesInvoice.CompanyID == cid);
                }

                var custItems = await custQuery.Select(cp => new ChequeDetailDto
                {
                    PaymentID = cp.CustomerPaymentID,
                    PaymentType = "Customer",
                    PaymentNumber = $"PAY-{cp.CustomerPaymentID:D6}",
                    PartyName = cp.Customer.ShopName ?? cp.Customer.OwnerName ?? string.Empty,
                    InvoiceID = cp.InvoiceID,
                    InvoiceNumber = cp.SalesInvoice != null ? cp.SalesInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = "Cheque",
                    ChequeNumber = cp.ChequeNumber ?? cp.ReferenceNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    RecordedByUserName = cp.ReceivedByUser.FullName ?? cp.ReceivedByUser.Username,
                    CreatedAt = cp.CreatedAt
                }).ToListAsync(cancellationToken);

                list.AddRange(custItems);
            }

            // Query Company Cheques
            if (string.IsNullOrWhiteSpace(filter.PaymentType) || string.Equals(filter.PaymentType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(filter.PaymentType, "Company", StringComparison.OrdinalIgnoreCase))
            {
                var compQuery = _context.CompanyPayments
                    .AsNoTracking()
                    .Include(cp => cp.Company)
                    .Include(cp => cp.PurchaseInvoice)
                    .Include(cp => cp.PaidByUser)
                    .Where(cp => !cp.IsDeleted && cp.PaymentMethod == "Cheque");

                if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase))
                {
                    compQuery = compQuery.Where(cp => cp.ChequeStatus == filter.Status);
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    compQuery = compQuery.Where(cp =>
                        (cp.ChequeNumber != null && cp.ChequeNumber.Contains(filter.SearchTerm)) ||
                        cp.Company.CompanyName.Contains(filter.SearchTerm) ||
                        cp.PurchaseInvoice.InvoiceNumber.Contains(filter.SearchTerm));
                }

                if (filter.DateFrom.HasValue) compQuery = compQuery.Where(cp => cp.PaymentDate >= filter.DateFrom.Value);
                if (filter.DateTo.HasValue) compQuery = compQuery.Where(cp => cp.PaymentDate <= filter.DateTo.Value.AddDays(1).AddTicks(-1));

                if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
                {
                    int cid = filter.CompanyID.Value;
                    compQuery = compQuery.Where(cp => cp.CompanyID == cid);
                }

                var compItems = await compQuery.Select(cp => new ChequeDetailDto
                {
                    PaymentID = cp.CompanyPaymentID,
                    PaymentType = "Company",
                    PaymentNumber = $"VPAY-{cp.CompanyPaymentID:D6}",
                    PartyName = cp.Company.CompanyName,
                    InvoiceID = cp.PurchaseInvoiceID,
                    InvoiceNumber = cp.PurchaseInvoice != null ? cp.PurchaseInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = "Cheque",
                    ChequeNumber = cp.ChequeNumber ?? cp.ReferenceNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    RecordedByUserName = cp.PaidByUser.FullName ?? cp.PaidByUser.Username,
                    CreatedAt = cp.CreatedAt
                }).ToListAsync(cancellationToken);

                list.AddRange(compItems);
            }

            int totalCount = list.Count;
            var items = list
                .OrderByDescending(c => c.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return new PagedResult<ChequeDetailDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<decimal> GetCustomerOutstandingBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            int? companyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            var ledgerQuery = _context.CustomerLedgers
                .AsNoTracking()
                .Where(cl => cl.CustomerID == customerId);

            if (companyId.HasValue && companyId.Value > 0)
            {
                int cid = companyId.Value;
                ledgerQuery = ledgerQuery.Where(cl =>
                    (cl.SalesInvoiceID != null && cl.SalesInvoice!.CompanyID == cid)
                    || (cl.CustomerPaymentID != null && cl.CustomerPayment!.SalesInvoice.CompanyID == cid)
                    || (cl.SalesReturnID != null && cl.SalesReturn!.InvoiceID != null && cl.SalesReturn.SalesInvoice!.CompanyID == cid));
            }

            var entries = await ledgerQuery
                .Select(cl => new { cl.DebitAmount, cl.CreditAmount })
                .ToListAsync(cancellationToken);

            decimal totalDebit = entries.Sum(e => e.DebitAmount);
            decimal totalCredit = entries.Sum(e => e.CreditAmount);

            return totalDebit - totalCredit;
        }

        public async Task<decimal> GetCompanyOutstandingBalanceAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var entries = await _context.CompanyLedgers
                .AsNoTracking()
                .Where(cl => cl.CompanyID == companyId)
                .Select(cl => new { cl.DebitAmount, cl.CreditAmount })
                .ToListAsync(cancellationToken);

            decimal totalDebit = entries.Sum(e => e.DebitAmount);
            decimal totalCredit = entries.Sum(e => e.CreditAmount);

            return totalCredit - totalDebit;
        }

        public async Task<PagedResult<CustomerPaymentDto>> GetPagedCustomerPaymentsAsync(PaymentFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PaymentFilterDto();
            var query = _context.CustomerPayments
                .AsNoTracking()
                .Include(cp => cp.Customer)
                .Include(cp => cp.SalesInvoice)
                .Include(cp => cp.ReceivedByUser)
                .Where(cp => !cp.IsDeleted);

            if (filter.CustomerID.HasValue && filter.CustomerID.Value > 0)
            {
                query = query.Where(cp => cp.CustomerID == filter.CustomerID.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                query = query.Where(cp => cp.SalesInvoice.InvoiceNumber.Contains(filter.InvoiceNumber));
            }

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(cp => cp.SalesInvoice != null && cp.SalesInvoice.CompanyID == filter.CompanyID.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
            {
                query = query.Where(cp => cp.PaymentMethod == filter.PaymentMethod);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(cp => cp.PaymentDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(cp => cp.PaymentDate <= filter.DateTo.Value.AddDays(1).AddTicks(-1));
            }

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(cp => cp.PaymentDate)
                .ThenByDescending(cp => cp.CustomerPaymentID)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(cp => new CustomerPaymentDto
                {
                    CustomerPaymentID = cp.CustomerPaymentID,
                    PaymentNumber = $"PAY-{cp.CustomerPaymentID:D6}",
                    CustomerID = cp.CustomerID,
                    CustomerName = cp.Customer.ShopName ?? cp.Customer.OwnerName ?? string.Empty,
                    InvoiceID = cp.InvoiceID,
                    InvoiceNumber = cp.SalesInvoice != null ? cp.SalesInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    Notes = cp.Notes,
                    ReceivedBy = cp.ReceivedBy,
                    ReceivedByUserName = cp.ReceivedByUser.FullName ?? cp.ReceivedByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerPaymentDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<PagedResult<CompanyPaymentDto>> GetPagedCompanyPaymentsAsync(PaymentFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PaymentFilterDto();
            var query = _context.CompanyPayments
                .AsNoTracking()
                .Include(cp => cp.Company)
                .Include(cp => cp.PurchaseInvoice)
                .Include(cp => cp.PaidByUser)
                .Where(cp => !cp.IsDeleted);

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(cp => cp.CompanyID == filter.CompanyID.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                query = query.Where(cp => cp.PurchaseInvoice.InvoiceNumber.Contains(filter.InvoiceNumber));
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
            {
                query = query.Where(cp => cp.PaymentMethod == filter.PaymentMethod);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(cp => cp.PaymentDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(cp => cp.PaymentDate <= filter.DateTo.Value.AddDays(1).AddTicks(-1));
            }

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(cp => cp.PaymentDate)
                .ThenByDescending(cp => cp.CompanyPaymentID)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(cp => new CompanyPaymentDto
                {
                    CompanyPaymentID = cp.CompanyPaymentID,
                    PaymentNumber = $"VPAY-{cp.CompanyPaymentID:D6}",
                    CompanyID = cp.CompanyID,
                    CompanyName = cp.Company.CompanyName,
                    PurchaseInvoiceID = cp.PurchaseInvoiceID,
                    InvoiceNumber = cp.PurchaseInvoice != null ? cp.PurchaseInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    PaidBy = cp.PaidBy,
                    PaidByUserName = cp.PaidByUser.FullName ?? cp.PaidByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CompanyPaymentDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<List<CustomerPaymentDto>> GetPaymentsBySalesInvoiceAsync(int salesInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.CustomerPayments
                .AsNoTracking()
                .Include(cp => cp.Customer)
                .Include(cp => cp.SalesInvoice)
                .Include(cp => cp.ReceivedByUser)
                .Where(cp => cp.InvoiceID == salesInvoiceId && !cp.IsDeleted)
                .OrderByDescending(cp => cp.PaymentDate)
                .Select(cp => new CustomerPaymentDto
                {
                    CustomerPaymentID = cp.CustomerPaymentID,
                    PaymentNumber = $"PAY-{cp.CustomerPaymentID:D6}",
                    CustomerID = cp.CustomerID,
                    CustomerName = cp.Customer.ShopName ?? cp.Customer.OwnerName ?? string.Empty,
                    InvoiceID = cp.InvoiceID,
                    InvoiceNumber = cp.SalesInvoice != null ? cp.SalesInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    Notes = cp.Notes,
                    ReceivedBy = cp.ReceivedBy,
                    ReceivedByUserName = cp.ReceivedByUser.FullName ?? cp.ReceivedByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CompanyPaymentDto>> GetPaymentsByPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.CompanyPayments
                .AsNoTracking()
                .Include(cp => cp.Company)
                .Include(cp => cp.PurchaseInvoice)
                .Include(cp => cp.PaidByUser)
                .Where(cp => cp.PurchaseInvoiceID == purchaseInvoiceId && !cp.IsDeleted)
                .OrderByDescending(cp => cp.PaymentDate)
                .Select(cp => new CompanyPaymentDto
                {
                    CompanyPaymentID = cp.CompanyPaymentID,
                    PaymentNumber = $"VPAY-{cp.CompanyPaymentID:D6}",
                    CompanyID = cp.CompanyID,
                    CompanyName = cp.Company.CompanyName,
                    PurchaseInvoiceID = cp.PurchaseInvoiceID,
                    InvoiceNumber = cp.PurchaseInvoice != null ? cp.PurchaseInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    PaidBy = cp.PaidBy,
                    PaidByUserName = cp.PaidByUser.FullName ?? cp.PaidByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CustomerPaymentDto?> GetCustomerPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.CustomerPayments
                .AsNoTracking()
                .Include(cp => cp.Customer)
                .Include(cp => cp.SalesInvoice)
                .Include(cp => cp.ReceivedByUser)
                .Where(cp => cp.CustomerPaymentID == paymentId && !cp.IsDeleted)
                .Select(cp => new CustomerPaymentDto
                {
                    CustomerPaymentID = cp.CustomerPaymentID,
                    PaymentNumber = $"PAY-{cp.CustomerPaymentID:D6}",
                    CustomerID = cp.CustomerID,
                    CustomerName = cp.Customer.ShopName ?? cp.Customer.OwnerName ?? string.Empty,
                    InvoiceID = cp.InvoiceID,
                    InvoiceNumber = cp.SalesInvoice != null ? cp.SalesInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    Notes = cp.Notes,
                    ReceivedBy = cp.ReceivedBy,
                    ReceivedByUserName = cp.ReceivedByUser.FullName ?? cp.ReceivedByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CompanyPaymentDto?> GetCompanyPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.CompanyPayments
                .AsNoTracking()
                .Include(cp => cp.Company)
                .Include(cp => cp.PurchaseInvoice)
                .Include(cp => cp.PaidByUser)
                .Where(cp => cp.CompanyPaymentID == paymentId && !cp.IsDeleted)
                .Select(cp => new CompanyPaymentDto
                {
                    CompanyPaymentID = cp.CompanyPaymentID,
                    PaymentNumber = $"VPAY-{cp.CompanyPaymentID:D6}",
                    CompanyID = cp.CompanyID,
                    CompanyName = cp.Company.CompanyName,
                    PurchaseInvoiceID = cp.PurchaseInvoiceID,
                    InvoiceNumber = cp.PurchaseInvoice != null ? cp.PurchaseInvoice.InvoiceNumber : "N/A",
                    PaymentDate = cp.PaymentDate,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    ReferenceNumber = cp.ReferenceNumber,
                    PaidBy = cp.PaidBy,
                    PaidByUserName = cp.PaidByUser.FullName ?? cp.PaidByUser.Username,
                    ChequeNumber = cp.ChequeNumber,
                    BankName = cp.BankName,
                    IssueDate = cp.IssueDate,
                    ChequeDate = cp.ChequeDate,
                    ChequeStatus = cp.ChequeStatus,
                    ClearedAt = cp.ClearedAt,
                    BouncedAt = cp.BouncedAt,
                    BounceRemarks = cp.BounceRemarks,
                    CreatedAt = cp.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<CustomerStatementEntryDto>> GetCustomerStatementAsync(int customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            int? companyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            var ledgerQuery = _context.CustomerLedgers
                .AsNoTracking()
                .Include(cl => cl.SalesInvoice)
                .Where(cl => cl.CustomerID == customerId && cl.TransactionDate >= startDate && cl.TransactionDate <= endDate);

            if (companyId.HasValue && companyId.Value > 0)
            {
                int cid = companyId.Value;
                ledgerQuery = ledgerQuery.Where(cl =>
                    (cl.SalesInvoiceID != null && cl.SalesInvoice!.CompanyID == cid)
                    || (cl.CustomerPaymentID != null && cl.CustomerPayment!.SalesInvoice.CompanyID == cid)
                    || (cl.SalesReturnID != null && cl.SalesReturn!.InvoiceID != null && cl.SalesReturn.SalesInvoice!.CompanyID == cid));
            }

            var entries = await ledgerQuery
                .OrderBy(cl => cl.TransactionDate)
                .ThenBy(cl => cl.CustomerLedgerID)
                .ToListAsync(cancellationToken);

            var result = new List<CustomerStatementEntryDto>();
            decimal runningBalance = 0;

            foreach (var entry in entries)
            {
                runningBalance += (entry.DebitAmount - entry.CreditAmount);
                result.Add(new CustomerStatementEntryDto
                {
                    CustomerLedgerID = entry.CustomerLedgerID,
                    TransactionDate = entry.TransactionDate,
                    TransactionType = entry.TransactionType,
                    DebitAmount = entry.DebitAmount,
                    CreditAmount = entry.CreditAmount,
                    SalesInvoiceID = entry.SalesInvoiceID,
                    InvoiceNumber = entry.SalesInvoice?.InvoiceNumber,
                    CustomerPaymentID = entry.CustomerPaymentID,
                    Description = entry.Description,
                    RunningBalance = runningBalance
                });
            }

            return result;
        }

        public async Task<IEnumerable<CompanyStatementEntryDto>> GetCompanyStatementAsync(int companyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var entries = await _context.CompanyLedgers
                .AsNoTracking()
                .Include(cl => cl.PurchaseInvoice)
                .Where(cl => cl.CompanyID == companyId && cl.TransactionDate >= startDate && cl.TransactionDate <= endDate)
                .OrderBy(cl => cl.TransactionDate)
                .ThenBy(cl => cl.CompanyLedgerID)
                .ToListAsync(cancellationToken);

            var result = new List<CompanyStatementEntryDto>();
            decimal runningBalance = 0;

            foreach (var entry in entries)
            {
                runningBalance += (entry.CreditAmount - entry.DebitAmount);
                result.Add(new CompanyStatementEntryDto
                {
                    CompanyLedgerID = entry.CompanyLedgerID,
                    TransactionDate = entry.TransactionDate,
                    TransactionType = entry.TransactionType,
                    DebitAmount = entry.DebitAmount,
                    CreditAmount = entry.CreditAmount,
                    PurchaseInvoiceID = entry.PurchaseInvoiceID,
                    InvoiceNumber = entry.PurchaseInvoice?.InvoiceNumber,
                    CompanyPaymentID = entry.CompanyPaymentID,
                    Description = entry.Description,
                    RunningBalance = runningBalance
                });
            }

            return result;
        }

        public async Task<List<UnpaidInvoiceLookupDto>> GetUnpaidCustomerInvoicesAsync(int customerId, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            int? companyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            var query = _context.SalesInvoices
                .AsNoTracking()
                .Where(s => s.CustomerID == customerId && !s.IsDeleted && s.PaymentStatus != "PAID");

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(s => s.CompanyID == companyId.Value);
            }

            var raw = await query
                .OrderBy(s => s.InvoiceDate)
                .ThenBy(s => s.InvoiceID)
                .Select(s => new
                {
                    s.InvoiceID,
                    s.InvoiceNumber,
                    s.InvoiceDate,
                    s.GrandTotal,
                    s.PaidAmount,
                    ReturnedAmount = s.SalesReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m
                })
                .ToListAsync(cancellationToken);

            return raw
                .Where(s => (s.GrandTotal - s.PaidAmount - s.ReturnedAmount) > 0.001m)
                .Select(s => new UnpaidInvoiceLookupDto
                {
                    InvoiceID = s.InvoiceID,
                    InvoiceNumber = s.InvoiceNumber,
                    InvoiceDate = s.InvoiceDate,
                    GrandTotal = s.GrandTotal,
                    PaidAmount = s.PaidAmount + s.ReturnedAmount
                })
                .ToList();
        }

        public async Task<List<UnpaidInvoiceLookupDto>> GetUnpaidCompanyInvoicesAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var raw = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.CompanyID == companyId && !p.IsDeleted && p.PaymentStatus != "PAID")
                .OrderBy(p => p.InvoiceDate)
                .ThenBy(p => p.PurchaseInvoiceID)
                .Select(p => new
                {
                    sID = p.PurchaseInvoiceID,
                    p.InvoiceNumber,
                    p.InvoiceDate,
                    p.GrandTotal,
                    p.PaidAmount,
                    ReturnedAmount = p.PurchaseReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m
                })
                .ToListAsync(cancellationToken);

            return raw
                .Where(p => (p.GrandTotal - p.PaidAmount - p.ReturnedAmount) > 0.001m)
                .Select(p => new UnpaidInvoiceLookupDto
                {
                    InvoiceID = p.sID,
                    InvoiceNumber = p.InvoiceNumber,
                    InvoiceDate = p.InvoiceDate,
                    GrandTotal = p.GrandTotal,
                    PaidAmount = p.PaidAmount + p.ReturnedAmount
                })
                .ToList();
        }
    }
}
