using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;

namespace InventorySystem.Repositories.Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserID == userId);
        }

        public async Task<User?> GetByIdWithCompaniesAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.UserCompanies)
                    .ThenInclude(uc => uc.Company)
                .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);
        }

        public async Task<PagedResult<User>> GetPagedAsync(UserFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.UserCompanies)
                    .ThenInclude(uc => uc.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(u =>
                    u.FullName.Contains(term)
                    || u.Username.Contains(term)
                    || (u.Phone != null && u.Phone.Contains(term)));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

            var items = await query
                .OrderBy(u => u.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<User>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<bool> UsernameExistsAsync(
            string username,
            int? excludeUserId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users.AsNoTracking().Where(u => u.Username == username);
            if (excludeUserId.HasValue)
            {
                query = query.Where(u => u.UserID != excludeUserId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role.RoleName == RoleNames.Admin)
                .CountAsync(cancellationToken);
        }

        public async Task<List<Role>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Roles
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RoleName)
                .ToListAsync(cancellationToken);
        }

        public async Task AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
