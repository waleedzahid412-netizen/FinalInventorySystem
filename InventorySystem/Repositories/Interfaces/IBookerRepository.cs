using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Bookers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IBookerRepository
    {
        Task<PagedResult<Booker>> GetPagedAsync(BookerFilterDto filter, CancellationToken cancellationToken = default);
        Task<Booker?> GetByIdAsync(int bookerId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int companyId, int? excludeBookerId = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsByCnicAsync(string cnic, int? excludeBookerId = null, CancellationToken cancellationToken = default);
        Task<bool> HasSalesInvoicesAsync(int bookerId, CancellationToken cancellationToken = default);
        Task<decimal> GetOutstandingBalanceAsync(int bookerId, CancellationToken cancellationToken = default);
        Task AddAsync(Booker booker, CancellationToken cancellationToken = default);
        Task UpdateAsync(Booker booker, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int bookerId, int? userId = null, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
