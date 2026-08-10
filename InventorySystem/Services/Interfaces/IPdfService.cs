using System;
using System.Collections.Generic;
using InventorySystem.DTOs.Analytics;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Sales;

namespace InventorySystem.Services.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerateSalesInvoicePdf(SalesDetailsDto invoice);
        byte[] GeneratePurchaseInvoicePdf(PurchaseDetailsDto invoice);
        byte[] GenerateSalesReturnPdf(InventorySystem.DTOs.Returns.SalesReturnDetailsDto returnDto);
        byte[] GenerateAnalyticsReportPdf(AnalyticsKpiSummaryDto kpi, InventoryInsightsDto inventory, List<BusinessInsightDto> insights, List<StockRiskItemDto> stockRisk, string preset);
    }
}
