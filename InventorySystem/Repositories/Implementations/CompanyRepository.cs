using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Companies;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly ApplicationDbContext _context;

        public CompanyRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Company>> GetPagedAsync(CompanyFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Companies
                .AsNoTracking()
                .AsQueryable();

            // Search filter across CompanyName, ContactPerson, Phone, Email, TaxID, Address
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(c =>
                    c.CompanyName.Contains(term) ||
                    (c.ContactPerson != null && c.ContactPerson.Contains(term)) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.Email != null && c.Email.Contains(term)) ||
                    (c.TaxID != null && c.TaxID.Contains(term)) ||
                    (c.Address != null && c.Address.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Phone))
            {
                query = query.Where(c => c.Phone != null && c.Phone.Contains(filter.Phone.Trim()));
            }

            // Sorting
            query = filter.SortBy?.ToLower() switch
            {
                "createdat" => filter.IsAscending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt),
                "contactperson" => filter.IsAscending ? query.OrderBy(c => c.ContactPerson) : query.OrderByDescending(c => c.ContactPerson),
                _ => filter.IsAscending ? query.OrderBy(c => c.CompanyName) : query.OrderByDescending(c => c.CompanyName)
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Company>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken = default)
        {
            return await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyID == companyId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeCompanyId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalizedName = name.Trim().ToLower();

            var query = _context.Companies.AsNoTracking().Where(c => c.CompanyName.ToLower() == normalizedName);
            if (excludeCompanyId.HasValue)
            {
                query = query.Where(c => c.CompanyID != excludeCompanyId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByPhoneAsync(string phone, int? excludeCompanyId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            var normalizedPhone = phone.Trim().ToLower();

            var query = _context.Companies.AsNoTracking().Where(c => c.Phone != null && c.Phone.ToLower() == normalizedPhone);
            if (excludeCompanyId.HasValue)
            {
                query = query.Where(c => c.CompanyID != excludeCompanyId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasHistoricalTransactionsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            bool hasPurchases = await _context.PurchaseInvoices.AnyAsync(pi => pi.CompanyID == companyId, cancellationToken);
            if (hasPurchases) return true;

            bool hasPayments = await _context.CompanyPayments.AnyAsync(cp => cp.CompanyID == companyId, cancellationToken);
            if (hasPayments) return true;

            bool hasLedger = await _context.CompanyLedgers.AnyAsync(cl => cl.CompanyID == companyId, cancellationToken);
            return hasLedger;
        }

        public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            await _context.Companies.AddAsync(company, cancellationToken);
        }

        public Task UpdateAsync(Company company, CancellationToken cancellationToken = default)
        {
            _context.Companies.Update(company);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int companyId, int userId, CancellationToken cancellationToken = default)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.CompanyID == companyId, cancellationToken);
            if (company != null)
            {
                var now = DateTime.UtcNow;
                company.IsDeleted = true;
                company.DeletedAt = now;
                company.UpdatedBy = userId;
                company.UpdatedAt = now;
            }
        }

        public async Task<CompanyFinancialSummaryDto?> GetFinancialSummaryAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyID == companyId, cancellationToken);

            if (company == null) return null;

            var totalPurchases = await _context.PurchaseInvoices
                .Where(pi => pi.CompanyID == companyId)
                .SumAsync(pi => (decimal?)pi.GrandTotal, cancellationToken) ?? 0m;

            var totalPaid = await _context.CompanyPayments
                .Where(cp => cp.CompanyID == companyId)
                .SumAsync(cp => (decimal?)cp.Amount, cancellationToken) ?? 0m;

            var outstanding = totalPurchases - totalPaid;

            var pendingInvoicesCount = await _context.PurchaseInvoices
                .Where(pi => pi.CompanyID == companyId && (pi.GrandTotal - pi.PaidAmount) > 0)
                .CountAsync(cancellationToken);

            var lastPurchaseDate = await _context.PurchaseInvoices
                .Where(pi => pi.CompanyID == companyId)
                .Select(pi => (DateTime?)pi.InvoiceDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync(cancellationToken);

            var lastPaymentDate = await _context.CompanyPayments
                .Where(cp => cp.CompanyID == companyId)
                .Select(cp => (DateTime?)cp.PaymentDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync(cancellationToken);

            return new CompanyFinancialSummaryDto
            {
                CompanyID = company.CompanyID,
                CompanyName = company.CompanyName,
                OutstandingPayable = Math.Max(0m, outstanding),
                TotalPurchases = totalPurchases,
                TotalPaid = totalPaid,
                PendingInvoicesCount = pendingInvoicesCount,
                LastPurchaseDate = lastPurchaseDate,
                LastPaymentDate = lastPaymentDate
            };
        }

        public async Task<PagedResult<CompanyPurchaseHistoryDto>> GetPurchaseHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.PurchaseInvoices
                .AsNoTracking()
                .Where(pi => pi.CompanyID == companyId)
                .OrderByDescending(pi => pi.InvoiceDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(pi => new CompanyPurchaseHistoryDto
                {
                    PurchaseInvoiceID = pi.PurchaseInvoiceID,
                    InvoiceNumber = pi.InvoiceNumber,
                    InvoiceDate = pi.InvoiceDate,
                    TotalAmount = pi.GrandTotal,
                    PaidAmount = pi.PaidAmount,
                    Status = pi.PaymentStatus
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CompanyPurchaseHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<CompanyPaymentHistoryDto>> GetPaymentHistoryAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CompanyPayments
                .AsNoTracking()
                .Include(cp => cp.PurchaseInvoice)
                .Where(cp => cp.CompanyID == companyId)
                .OrderByDescending(cp => cp.PaymentDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(cp => new CompanyPaymentHistoryDto
                {
                    CompanyPaymentID = cp.CompanyPaymentID,
                    PurchaseInvoiceID = cp.PurchaseInvoiceID,
                    InvoiceNumber = cp.PurchaseInvoice != null ? cp.PurchaseInvoice.InvoiceNumber : "-",
                    PaymentMethod = cp.PaymentMethod,
                    Amount = cp.Amount,
                    PaymentDate = cp.PaymentDate,
                    ReferenceNumber = cp.ReferenceNumber
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CompanyPaymentHistoryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<CompanyLedgerEntryDto>> GetLedgerAsync(int companyId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CompanyLedgers
                .AsNoTracking()
                .Where(cl => cl.CompanyID == companyId)
                .OrderByDescending(cl => cl.TransactionDate);

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(cl => new CompanyLedgerEntryDto
                {
                    CompanyLedgerID = cl.CompanyLedgerID,
                    TransactionDate = cl.TransactionDate,
                    TransactionType = cl.TransactionType,
                    Description = cl.Description,
                    DebitAmount = cl.DebitAmount,
                    CreditAmount = cl.CreditAmount
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<CompanyLedgerEntryDto>(items, totalCount, pageNumber, pageSize);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
