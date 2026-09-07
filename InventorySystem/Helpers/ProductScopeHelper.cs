using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Validates that a product belongs to the ambient company scope.
    /// </summary>
    public static class ProductScopeHelper
    {
        /// <summary>
        /// When a specific company is selected, the product must belong to that company.
        /// In All Companies mode the check is skipped (returns true if the product exists).
        /// </summary>
        public static async Task<bool> ProductMatchesScopeAsync(
            ApplicationDbContext context,
            ICompanyContext companyContext,
            int productId,
            CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
            {
                return false;
            }

            var productCompanyId = await context.Products
                .AsNoTracking()
                .Where(p => p.ProductID == productId && !p.IsDeleted)
                .Select(p => (int?)p.CompanyID)
                .FirstOrDefaultAsync(cancellationToken);

            if (!productCompanyId.HasValue)
            {
                return false;
            }

            return !CompanyScopeGuards.IsOutOfScope(companyContext, productCompanyId.Value);
        }
    }
}
