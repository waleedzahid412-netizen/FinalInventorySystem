using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class BookerRepository : IBookerRepository
    {
        private readonly ApplicationDbContext _context;

        public BookerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Booker>> GetPagedAsync(BookerFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Bookers.AsNoTracking().AsQueryable();

            if (filter.CompanyID > 0)
            {
                query = query.Where(b => b.CompanyID == filter.CompanyID);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(b =>
                    b.Name.Contains(term) ||
                    (b.Phone != null && b.Phone.Contains(term)) ||
                    (b.CNIC != null && b.CNIC.Contains(term)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(b => b.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "phone" => filter.IsAscending ? query.OrderBy(b => b.Phone) : query.OrderByDescending(b => b.Phone),
                "cnic" => filter.IsAscending ? query.OrderBy(b => b.CNIC) : query.OrderByDescending(b => b.CNIC),
                "status" => filter.IsAscending ? query.OrderBy(b => b.IsActive).ThenBy(b => b.Name) : query.OrderByDescending(b => b.IsActive).ThenBy(b => b.Name),
                _ => filter.IsAscending ? query.OrderBy(b => b.Name) : query.OrderByDescending(b => b.Name)
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Booker>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Booker?> GetByIdAsync(int bookerId, CancellationToken cancellationToken = default)
        {
            return await _context.Bookers
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.BookerID == bookerId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int companyId, int? excludeBookerId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name) || companyId <= 0) return false;
            var normalized = name.Trim().ToLower();
            var query = _context.Bookers.AsNoTracking()
                .Where(b => b.CompanyID == companyId && b.Name.ToLower() == normalized);
            if (excludeBookerId.HasValue)
            {
                query = query.Where(b => b.BookerID != excludeBookerId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByCnicAsync(string cnic, int? excludeBookerId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cnic)) return false;
            var normalized = cnic.Trim();
            var query = _context.Bookers.AsNoTracking()
                .Where(b => b.CNIC != null && b.CNIC == normalized);
            if (excludeBookerId.HasValue)
            {
                query = query.Where(b => b.BookerID != excludeBookerId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasSalesInvoicesAsync(int bookerId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .AnyAsync(si => si.BookerID == bookerId, cancellationToken);
        }

        public async Task<decimal> GetOutstandingBalanceAsync(int bookerId, CancellationToken cancellationToken = default)
        {
            var outstanding = await _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.BookerID == bookerId)
                .SumAsync(si => (decimal?)(si.GrandTotal - si.PaidAmount), cancellationToken);

            return outstanding ?? 0m;
        }

        public async Task AddAsync(Booker booker, CancellationToken cancellationToken = default)
        {
            await _context.Bookers.AddAsync(booker, cancellationToken);
        }

        public Task UpdateAsync(Booker booker, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(booker);
            if (entry.State == EntityState.Detached)
            {
                _context.Bookers.Update(booker);
            }
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int bookerId, int? userId = null, CancellationToken cancellationToken = default)
        {
            var booker = await _context.Bookers.FirstOrDefaultAsync(b => b.BookerID == bookerId, cancellationToken);
            if (booker == null) return;
            booker.IsDeleted = true;
            booker.DeletedAt = DateTime.UtcNow;
            booker.IsActive = false;
            booker.UpdatedAt = DateTime.UtcNow;
            if (userId.HasValue)
            {
                booker.UpdatedBy = userId;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
