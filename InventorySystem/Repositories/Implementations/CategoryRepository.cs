using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Category>> GetPagedAsync(CategoryFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .AsNoTracking()
                .Include(c => c.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term) ||
                    c.Company.CompanyName.Contains(term));
            }

            if (filter.CompanyID.HasValue && filter.CompanyID.Value > 0)
            {
                query = query.Where(c => c.CompanyID == filter.CompanyID.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "company" => filter.IsAscending
                    ? query.OrderBy(c => c.Company.CompanyName).ThenBy(c => c.Name)
                    : query.OrderByDescending(c => c.Company.CompanyName).ThenBy(c => c.Name),
                "createdat" => filter.IsAscending
                    ? query.OrderBy(c => c.CreatedAt)
                    : query.OrderByDescending(c => c.CreatedAt),
                "status" => filter.IsAscending
                    ? query.OrderBy(c => c.IsActive).ThenBy(c => c.Name)
                    : query.OrderByDescending(c => c.IsActive).ThenBy(c => c.Name),
                _ => filter.IsAscending
                    ? query.OrderBy(c => c.Name)
                    : query.OrderByDescending(c => c.Name)
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Category>(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<Category?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryID == categoryId, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(int companyId, string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var normalizedName = name.Trim().ToLower();

            var query = _context.Categories
                .AsNoTracking()
                .Where(c => c.CompanyID == companyId && c.Name.ToLower() == normalizedName);

            if (excludeCategoryId.HasValue)
            {
                query = query.Where(c => c.CategoryID != excludeCategoryId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasProductsAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products.AnyAsync(p => p.CategoryID == categoryId, cancellationToken);
        }

        public async Task<bool> CompanyExistsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            return await _context.Companies.AnyAsync(c => c.CompanyID == companyId, cancellationToken);
        }

        public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _context.Categories.AddAsync(category, cancellationToken);
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Categories.Update(category);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int categoryId, int userId, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryID == categoryId, cancellationToken);
            if (category != null)
            {
                var now = DateTime.UtcNow;
                category.IsDeleted = true;
                category.DeletedAt = now;
                category.UpdatedBy = userId;
                category.UpdatedAt = now;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
