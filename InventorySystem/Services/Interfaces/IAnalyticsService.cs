using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InventorySystem.DTOs.Analytics;

namespace InventorySystem.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<AnalyticsKpiSummaryDto> GetKpiSummaryAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<SalesTrendItemDto>> GetSalesTrendAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default);
        Task<List<TopProductDto>> GetTopProductsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default);
        Task<List<TopCustomerDto>> GetTopCustomersAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", int topCount = 10, CancellationToken cancellationToken = default);
        Task<List<CategorySalesDto>> GetCategorySalesAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<PaymentAnalyticsDto> GetPaymentAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<InventoryInsightsDto> GetInventoryInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<StockRiskItemDto>> GetStockRiskItemsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<InventoryMovementDto> GetInventoryMovementAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<PromotionPerformanceDto> GetPromotionPerformanceAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<TopCompanyDto>> GetTopCompaniesAsync(AnalyticsFilterDto filter, int topCount = 5, CancellationToken cancellationToken = default);
        Task<List<BusinessInsightDto>> GetBusinessInsightsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<OverviewAnalyticsDto> GetOverviewAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default);
        Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(AnalyticsFilterDto filter, string interval = "Daily", CancellationToken cancellationToken = default);
        Task<List<InvoiceDrilldownDto>> GetSalesDrilldownAsync(AnalyticsFilterDto filter, DateTime bucketStart, DateTime bucketEnd, CancellationToken cancellationToken = default);
        Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(AnalyticsFilterDto filter, string sortBy = "Revenue", CancellationToken cancellationToken = default);
        Task<CustomerDetailDto?> GetCustomerDetailAsync(int customerId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<ProductAnalyticsDto> GetProductAnalyticsAsync(AnalyticsFilterDto filter, string sortBy = "Quantity", CancellationToken cancellationToken = default);
        Task<ProductDetailDto?> GetProductDetailAsync(int productId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<BrokerAnalyticsDto> GetBrokerAnalyticsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
        Task<BrokerDetailDto?> GetBrokerDetailAsync(int? brokerId, AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
    }
}
