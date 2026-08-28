using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Suppliers;
using InventorySystem.Models.Entities;

namespace InventorySystem.Repositories.Interfaces
{
    public interface ISupplierRepository
    {
        Task<PagedResult<Supplier>> GetPagedAsync(SupplierFilterDto filter, CancellationToken cancellationToken = default);
        Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, int? excludeSupplierId = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsByCnicAsync(string cnic, int? excludeSupplierId = null, CancellationToken cancellationToken = default);
        Task<bool> HasSalesInvoicesAsync(int supplierId, CancellationToken cancellationToken = default);
        Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
        Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(int supplierId, int? userId = null, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
