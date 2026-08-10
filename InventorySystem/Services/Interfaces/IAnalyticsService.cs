using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Analytics;

namespace InventorySystem.Services.Interfaces
{
    public interface IAnalyticsService
    {
        // Wave 1 & 2
        Task<AnalyticsKpiSummaryDto> GetKpiSummaryAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<SalesTrendItemDto>> GetSalesTrendAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default);
        Task<List<TopProductDto>> GetTopProductsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default);
        Task<List<TopCustomerDto>> GetTopCustomersAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default);
        Task<List<CategorySalesDto>> GetCategorySalesAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);

        // Wave 3
        Task<PaymentAnalyticsDto> GetPaymentAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<InventoryInsightsDto> GetInventoryInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<StockRiskItemDto>> GetStockRiskItemsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<InventoryMovementDto> GetInventoryMovementAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<PromotionPerformanceDto> GetPromotionPerformanceAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<TopCompanyDto>> GetTopCompaniesAsync(AnalyticsFilterDto filter, int topCount = 5, CancellationToken cancellationToken = default);
        Task<List<BusinessInsightDto>> GetBusinessInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
    }
}
