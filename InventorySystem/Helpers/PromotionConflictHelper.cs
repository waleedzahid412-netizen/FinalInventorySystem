using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Ensures only one active, non-deleted promotion campaign owns a given buy product per company.
    /// </summary>
    public static class PromotionConflictHelper
    {
        public sealed class ConflictInfo
        {
            public int BuyProductID { get; init; }
            public string ProductName { get; init; } = string.Empty;
            public int ExistingPromotionID { get; init; }
            public string ExistingPromotionName { get; init; } = string.Empty;
        }

        /// <summary>
        /// Returns the first conflicting active campaign rule for any of the given buy product IDs.
        /// </summary>
        public static async Task<ConflictInfo?> FindActiveBuyProductConflictAsync(
            ApplicationDbContext context,
            int companyId,
            IEnumerable<int> buyProductIds,
            int? excludePromotionId,
            CancellationToken cancellationToken = default)
        {
            var ids = buyProductIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (ids.Count == 0)
            {
                return null;
            }

            var query = context.PromotionCampaigns
                .AsNoTracking()
                .Where(c => c.CompanyID == companyId
                            && c.IsActive
                            && !c.IsDeleted);

            if (excludePromotionId.HasValue && excludePromotionId.Value > 0)
            {
                int excludeId = excludePromotionId.Value;
                query = query.Where(c => c.PromotionID != excludeId);
            }

            var conflict = await query
                .SelectMany(c => c.PromotionRules.Select(r => new
                {
                    r.BuyProductID,
                    ProductName = r.BuyProduct.ProductName,
                    c.PromotionID,
                    PromotionName = c.Name
                }))
                .Where(x => ids.Contains(x.BuyProductID))
                .OrderBy(x => x.BuyProductID)
                .FirstOrDefaultAsync(cancellationToken);

            if (conflict == null)
            {
                return null;
            }

            return new ConflictInfo
            {
                BuyProductID = conflict.BuyProductID,
                ProductName = conflict.ProductName ?? $"Product #{conflict.BuyProductID}",
                ExistingPromotionID = conflict.PromotionID,
                ExistingPromotionName = conflict.PromotionName
            };
        }
    }
}
