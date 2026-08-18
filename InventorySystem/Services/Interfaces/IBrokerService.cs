using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Brokers;
using InventorySystem.DTOs.Common;

namespace InventorySystem.Services.Interfaces
{
    public interface IBrokerService
    {
        Task<PagedResult<BrokerListItemDto>> GetPagedBrokersAsync(BrokerFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditBrokerDto?> GetBrokerForEditAsync(int brokerId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateBrokerAsync(CreateBrokerDto dto, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateBrokerAsync(EditBrokerDto dto, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteBrokerAsync(int brokerId, CancellationToken cancellationToken = default);
    }
}
