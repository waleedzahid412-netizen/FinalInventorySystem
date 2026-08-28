using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Customers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        #region 1. CRUD Operations

        public async Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .Include(c => c.Area)
                .Include(c => c.SubArea)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.IsDeleted, cancellationToken);
        }

        public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            await _context.Customers.AddAsync(customer, cancellationToken);
        }

        public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            customer.UpdatedAt = DateTime.UtcNow;
            _context.Customers.Update(customer);
            await Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int customerId, int userId, CancellationToken cancellationToken = default)
        {
            var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
            if (customer != null && !customer.IsDeleted)
            {
                customer.IsDeleted = true;
                customer.DeletedAt = DateTime.UtcNow;
                customer.UpdatedBy = userId;
                customer.UpdatedAt = DateTime.UtcNow;
                _context.Customers.Update(customer);
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region 2. Search & Filtering

        public async Task<PagedResult<Customer>> GetPagedAsync(CustomerFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Customers
                .AsNoTracking()
                .Include(c => c.Area)
                .Include(c => c.SubArea)
                .Where(c => !c.IsDeleted);

            // Search Term (Case-insensitive across ShopName, OwnerName, Phone, Address, TaxID, AreaName, SubAreaName)
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.ShopName.ToLower().Contains(term) ||
                    (c.OwnerName != null && c.OwnerName.ToLower().Contains(term)) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                    (c.Address != null && c.Address.ToLower().Contains(term)) ||
                    (c.TaxID != null && c.TaxID.ToLower().Contains(term)) ||
                    (c.Area != null && c.Area.AreaName.ToLower().Contains(term)) ||
                    (c.SubArea != null && c.SubArea.SubAreaName.ToLower().Contains(term))
                );
            }

            // Area & SubArea Filters
            if (filter.AreaID.HasValue)
            {
                query = query.Where(c => c.AreaID == filter.AreaID.Value);
            }

            if (filter.SubAreaID.HasValue)
            {
                query = query.Where(c => c.SubAreaID == filter.SubAreaID.Value);
            }

            // IsActive Filter
            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }

            // Sorting
            query = filter.SortBy.ToLower() switch
            {
                "ownername" => filter.IsAscending ? query.OrderBy(c => c.OwnerName) : query.OrderByDescending(c => c.OwnerName),
                "phone" => filter.IsAscending ? query.OrderBy(c => c.Phone) : query.OrderByDescending(c => c.Phone),
                "createdat" => filter.IsAscending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt),
                "area" => filter.IsAscending ? query.OrderBy(c => c.Area != null ? c.Area.AreaName : "") : query.OrderByDescending(c => c.Area != null ? c.Area.AreaName : ""),
                _ => filter.IsAscending ? query.OrderBy(c => c.ShopName) : query.OrderByDescending(c => c.ShopName),
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Customer>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        #endregion

        #region 3. Validation / Existence Checks

        public async Task<bool> ExistsByShopNameAsync(string shopName, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shopName)) return false;

            var normalized = shopName.Trim().ToLower();
            var query = _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.ShopName.ToLower() == normalized);

            if (excludeCustomerId.HasValue)
            {
                query = query.Where(c => c.CustomerID != excludeCustomerId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByPhoneAsync(string phone, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;

            var normalized = phone.Trim().ToLower();
            var query = _context.Customers
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.Phone != null && c.Phone.ToLower() == normalized);

            if (excludeCustomerId.HasValue)
            {
                query = query.Where(c => c.CustomerID != excludeCustomerId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasHistoricalTransactionsAsync(int customerId, CancellationToken cancellationToken = default)
        {
            bool hasInvoices = await _context.SalesInvoices.AsNoTracking().AnyAsync(i => i.CustomerID == customerId, cancellationToken);
            if (hasInvoices) return true;

            bool hasPayments = await _context.CustomerPayments.AsNoTracking().AnyAsync(p => p.CustomerID == customerId && !p.IsDeleted, cancellationToken);
            if (hasPayments) return true;

            bool hasLedger = await _context.CustomerLedgers.AsNoTracking().AnyAsync(l => l.CustomerID == customerId, cancellationToken);
            if (hasLedger) return true;

            bool hasReturns = await _context.SalesReturns.AsNoTracking().AnyAsync(r => r.CustomerID == customerId, cancellationToken);
            return hasReturns;
        }

        #endregion

        #region 4. Financial Queries

        /// <summary>
        /// Calculates financial KPIs using CustomerLedger as the single source of truth:
        /// OutstandingReceivable = SUM(DebitAmount) - SUM(CreditAmount).
        /// When <paramref name="companyId"/> is set, only ledger rows tied to that company's
        /// sales invoices / payments / returns are included (customer master stays global).
        /// </summary>
        public async Task<CustomerFinancialSummaryDto?> GetFinancialSummaryAsync(
            int customerId,
            int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.IsDeleted, cancellationToken);

            if (customer == null) return null;

            var ledgerQuery = ScopeCustomerLedger(
                _context.CustomerLedgers.AsNoTracking().Where(l => l.CustomerID == customerId),
                companyId);

            var ledgerTotals = await ledgerQuery
                .GroupBy(l => l.CustomerID)
                .Select(g => new
                {
                    TotalDebits = g.Sum(l => (decimal?)l.DebitAmount) ?? 0m,
                    TotalCredits = g.Sum(l => (decimal?)l.CreditAmount) ?? 0m
                })
                .FirstOrDefaultAsync(cancellationToken);

            decimal totalDebits = ledgerTotals?.TotalDebits ?? 0m;
            decimal totalCredits = ledgerTotals?.TotalCredits ?? 0m;
            decimal outstanding = totalDebits - totalCredits;

            var invoiceQuery = _context.SalesInvoices
                .AsNoTracking()
                .Where(i => i.CustomerID == customerId && !i.IsDeleted);
            if (companyId.HasValue && companyId.Value > 0)
            {
                invoiceQuery = invoiceQuery.Where(i => i.CompanyID == companyId.Value);
            }

            int pendingInvoicesCount = await invoiceQuery
                .Where(i => i.PaymentStatus != "Paid")
                .CountAsync(cancellationToken);

            var lastSaleDate = await invoiceQuery
                .MaxAsync(i => (DateTime?)i.InvoiceDate, cancellationToken);

            var paymentQuery = _context.CustomerPayments
                .AsNoTracking()
                .Where(p => p.CustomerID == customerId && !p.IsDeleted);
            if (companyId.HasValue && companyId.Value > 0)
            {
                paymentQuery = paymentQuery.Where(p => p.SalesInvoice.CompanyID == companyId.Value);
            }

            var lastPaymentDate = await paymentQuery
                .MaxAsync(p => (DateTime?)p.PaymentDate, cancellationToken);

            return new CustomerFinancialSummaryDto
            {
                CustomerID = customer.CustomerID,
                ShopName = customer.ShopName,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                OutstandingReceivable = outstanding,
                CreditLimit = customer.CreditLimit,
                PendingInvoicesCount = pendingInvoicesCount,
                LastSaleDate = lastSaleDate,
                LastPaymentDate = lastPaymentDate
            };
        }

        /// <summary>
        /// Soft-scopes ledger rows to a company via related SalesInvoice / Payment / Return.
        /// Entries with no resolvable company (orphan adjustments) are excluded when scoped.
        /// </summary>
        private static IQueryable<CustomerLedger> ScopeCustomerLedger(IQueryable<CustomerLedger> query, int? companyId)
        {
            if (!companyId.HasValue || companyId.Value <= 0)
            {
                return query;
            }

            int cid = companyId.Value;
            return query.Where(l =>
                (l.SalesInvoiceID != null && l.SalesInvoice!.CompanyID == cid)
                || (l.CustomerPaymentID != null && l.CustomerPayment!.SalesInvoice.CompanyID == cid)
                || (l.SalesReturnID != null && l.SalesReturn!.InvoiceID != null && l.SalesReturn.SalesInvoice!.CompanyID == cid));
        }

        #endregion

        #region 5. History Queries

        public async Task<PagedResult<CustomerSalesHistoryDto>> GetSalesHistoryAsync(
            int customerId,
            int pageNumber,
            int pageSize,
            int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.SalesInvoices
                .AsNoTracking()
                .Where(i => i.CustomerID == customerId && !i.IsDeleted);

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(i => i.CompanyID == companyId.Value);
            }

            query = query.OrderByDescending(i => i.InvoiceDate);

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new CustomerSalesHistoryDto
                {
                    InvoiceID = i.InvoiceID,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    GrandTotal = i.GrandTotal,
                    PaidAmount = i.PaidAmount,
                    PaymentStatus = i.PaymentStatus ?? "Unpaid"
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerSalesHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<CustomerPaymentHistoryDto>> GetPaymentHistoryAsync(
            int customerId,
            int pageNumber,
            int pageSize,
            int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.CustomerPayments
                .AsNoTracking()
                .Include(p => p.SalesInvoice)
                .Where(p => p.CustomerID == customerId && !p.IsDeleted);

            if (companyId.HasValue && companyId.Value > 0)
            {
                query = query.Where(p => p.SalesInvoice.CompanyID == companyId.Value);
            }

            query = query.OrderByDescending(p => p.PaymentDate);

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new CustomerPaymentHistoryDto
                {
                    CustomerPaymentID = p.CustomerPaymentID,
                    InvoiceID = p.InvoiceID,
                    InvoiceNumber = p.SalesInvoice != null ? p.SalesInvoice.InvoiceNumber : "N/A",
                    PaymentMethod = p.PaymentMethod,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    ReferenceNumber = p.ReferenceNumber
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerPaymentHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<CustomerLedgerEntryDto>> GetLedgerAsync(
            int customerId,
            int pageNumber,
            int pageSize,
            int? companyId = null,
            CancellationToken cancellationToken = default)
        {
            var query = ScopeCustomerLedger(
                    _context.CustomerLedgers.AsNoTracking().Where(l => l.CustomerID == customerId),
                    companyId)
                .OrderByDescending(l => l.TransactionDate)
                .ThenByDescending(l => l.CustomerLedgerID);

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new CustomerLedgerEntryDto
                {
                    CustomerLedgerID = l.CustomerLedgerID,
                    TransactionDate = l.TransactionDate,
                    TransactionType = l.TransactionType,
                    Description = l.Description,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerLedgerEntryDto>(items, totalCount, pageNumber, pageSize);
        }

        #endregion
    }
}
