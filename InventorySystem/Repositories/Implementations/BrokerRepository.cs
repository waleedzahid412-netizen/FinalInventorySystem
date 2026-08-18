using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Brokers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class BrokerRepository : IBrokerRepository
    {
        private readonly ApplicationDbContext _context;

        public BrokerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Broker>> GetPagedAsync(BrokerFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Brokers.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(b =>
                    b.Name.Contains(term) ||
                    (b.Phone != null && b.Phone.Contains(term)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(b => b.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "phone" => filter.IsAscending ? query.OrderBy(b => b.Phone) : query.OrderByDescending(b => b.Phone),
                "status" => filter.IsAscending ? query.OrderBy(b => b.IsActive).ThenBy(b => b.Name) : query.OrderByDescending(b => b.IsActive).ThenBy(b => b.Name),
                _ => filter.IsAscending ? query.OrderBy(b => b.Name) : query.OrderByDescending(b => b.Name)
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Broker>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Broker?> GetByIdAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            return await _context.Brokers
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BrokerID == brokerId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeBrokerId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            var query = _context.Brokers.AsNoTracking().Where(b => b.Name.ToLower() == normalized);
            if (excludeBrokerId.HasValue)
            {
                query = query.Where(b => b.BrokerID != excludeBrokerId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasSalesInvoicesAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .AnyAsync(si => si.BrokerID == brokerId, cancellationToken);
        }

        public async Task AddAsync(Broker broker, CancellationToken cancellationToken = default)
        {
            await _context.Brokers.AddAsync(broker, cancellationToken);
        }

        public Task UpdateAsync(Broker broker, CancellationToken cancellationToken = default)
        {
            _context.Brokers.Update(broker);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int brokerId, CancellationToken cancellationToken = default)
        {
            var broker = await _context.Brokers.FirstOrDefaultAsync(b => b.BrokerID == brokerId, cancellationToken);
            if (broker == null) return;
            broker.IsDeleted = true;
            broker.DeletedAt = DateTime.UtcNow;
            broker.IsActive = false;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
