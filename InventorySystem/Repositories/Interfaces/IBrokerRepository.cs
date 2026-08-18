using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Brokers;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface IBrokerRepository
    {
        Task<PagedResult<Broker>> GetPagedAsync(BrokerFilterDto filter, CancellationToken cancellationToken = default);
        Task<Broker?> GetByIdAsync(int brokerId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int? excludeBrokerId = null, CancellationToken cancellationToken = default);
        Task<bool> HasSalesInvoicesAsync(int brokerId, CancellationToken cancellationToken = default);
        Task AddAsync(Broker broker, CancellationToken cancellationToken = default);
        Task UpdateAsync(Broker broker, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int brokerId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
