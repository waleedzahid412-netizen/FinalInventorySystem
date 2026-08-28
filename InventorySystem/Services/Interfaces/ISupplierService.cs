using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Suppliers;

namespace InventorySystem.Services.Interfaces
{
    public interface ISupplierService
    {
        Task<PagedResult<SupplierListItemDto>> GetPagedSuppliersAsync(SupplierFilterDto filter, CancellationToken cancellationToken = default);
        Task<EditSupplierDto?> GetSupplierForEditAsync(int supplierId, CancellationToken cancellationToken = default);
        Task<OperationResult<int>> CreateSupplierAsync(CreateSupplierDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateSupplierAsync(EditSupplierDto dto, int userId, CancellationToken cancellationToken = default);
        Task<OperationResult> SoftDeleteSupplierAsync(int supplierId, int userId, CancellationToken cancellationToken = default);
    }
}
