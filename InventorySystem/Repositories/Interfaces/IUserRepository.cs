using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByIdWithCompaniesAsync(int userId, CancellationToken cancellationToken = default);
        Task<PagedResult<User>> GetPagedAsync(UserFilterDto filter, CancellationToken cancellationToken = default);
        Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default);
        Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
        Task<List<Role>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);
        Task AddUserAsync(User user);
        Task SaveChangesAsync();
        Task<bool> RecordFailedLoginAsync(int userId, int maxFailedAttempts, int lockoutMinutes, CancellationToken cancellationToken = default);
        Task ResetLoginFailuresAsync(int userId, CancellationToken cancellationToken = default);
    }
}
