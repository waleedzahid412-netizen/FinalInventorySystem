using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Suppliers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class SupplierRepository : ISupplierRepository
    {
        private readonly ApplicationDbContext _context;

        public SupplierRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Supplier>> GetPagedAsync(SupplierFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Suppliers.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(s =>
                    s.Name.Contains(term) ||
                    (s.Phone != null && s.Phone.Contains(term)) ||
                    (s.CNIC != null && s.CNIC.Contains(term)));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == filter.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                query = query.Where(s => s.Type == filter.Type);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "phone" => filter.IsAscending ? query.OrderBy(s => s.Phone) : query.OrderByDescending(s => s.Phone),
                "cnic" => filter.IsAscending ? query.OrderBy(s => s.CNIC) : query.OrderByDescending(s => s.CNIC),
                "type" => filter.IsAscending ? query.OrderBy(s => s.Type).ThenBy(s => s.Name) : query.OrderByDescending(s => s.Type).ThenBy(s => s.Name),
                "status" => filter.IsAscending ? query.OrderBy(s => s.IsActive).ThenBy(s => s.Name) : query.OrderByDescending(s => s.IsActive).ThenBy(s => s.Name),
                _ => filter.IsAscending ? query.OrderBy(s => s.Name) : query.OrderByDescending(s => s.Name)
            };

            int totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Supplier>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierID == supplierId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeSupplierId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalized = name.Trim().ToLower();
            var query = _context.Suppliers.AsNoTracking()
                .Where(s => s.Name.ToLower() == normalized);
            if (excludeSupplierId.HasValue)
            {
                query = query.Where(s => s.SupplierID != excludeSupplierId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByCnicAsync(string cnic, int? excludeSupplierId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cnic)) return false;
            var normalized = cnic.Trim();
            var query = _context.Suppliers.AsNoTracking()
                .Where(s => s.CNIC != null && s.CNIC == normalized);
            if (excludeSupplierId.HasValue)
            {
                query = query.Where(s => s.SupplierID != excludeSupplierId.Value);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasSalesInvoicesAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .AnyAsync(si => si.SupplierID == supplierId, cancellationToken);
        }

        public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            await _context.Suppliers.AddAsync(supplier, cancellationToken);
        }

        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(supplier);
            if (entry.State == EntityState.Detached)
            {
                _context.Suppliers.Update(supplier);
            }
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int supplierId, int? userId = null, CancellationToken cancellationToken = default)
        {
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplierId, cancellationToken);
            if (supplier == null) return;
            supplier.IsDeleted = true;
            supplier.DeletedAt = DateTime.UtcNow;
            supplier.IsActive = false;
            supplier.UpdatedAt = DateTime.UtcNow;
            if (userId.HasValue)
            {
                supplier.UpdatedBy = userId;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
