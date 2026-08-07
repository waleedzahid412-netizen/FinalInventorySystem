using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Sales;

namespace InventorySystem.Services.Interfaces
{
    public interface IPromotionDiscountService
    {
        Task<EvaluationResultDto> EvaluatePromotionsAndDiscountsAsync(OrderContextDto context, CancellationToken cancellationToken = default);
    }
}
