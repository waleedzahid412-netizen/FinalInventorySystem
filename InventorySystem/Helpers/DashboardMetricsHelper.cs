using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models.Entities;

namespace InventorySystem.Helpers
{
    /// <summary>
    /// Dashboard KPI calculations aligned with Analytics net-sales semantics.
    /// </summary>
    public static class DashboardMetricsHelper
    {
        public static async Task<decimal> SumGrossSalesAsync(
            IQueryable<SalesInvoice> salesQuery,
            DateTime fromDate,
            CancellationToken cancellationToken = default)
        {
            return await salesQuery
                .Where(i => i.InvoiceDate >= fromDate)
                .SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;
        }

        public static async Task<decimal> SumReturnsAsync(
            IQueryable<SalesReturn> returnsQuery,
            DateTime fromDate,
            CancellationToken cancellationToken = default)
        {
            return await returnsQuery
                .Where(r => r.ReturnDate >= fromDate)
                .SumAsync(r => (decimal?)r.NetRefundAmount, cancellationToken) ?? 0m;
        }

        public static decimal ComputeNetSales(decimal grossSales, decimal returns) => grossSales - returns;

        public static async Task<decimal> SumCustomerReceivablesAsync(
            IQueryable<SalesInvoice> salesQuery,
            CancellationToken cancellationToken = default)
        {
            // Per-invoice remaining in SQL; final SUM in memory (SQL Server rejects nested aggregates).
            var rows = await salesQuery
                .Select(i => new
                {
                    Remaining = i.GrandTotal - i.PaidAmount - (i.SalesReturns.Where(r => !r.IsDeleted).Sum(r => (decimal?)r.NetRefundAmount) ?? 0m)
                })
                .ToListAsync(cancellationToken);

            return rows.Where(r => r.Remaining > 0m).Sum(r => r.Remaining);
        }

        public static async Task<decimal> SumCompanyPayablesAsync(
            IQueryable<PurchaseInvoice> purchaseQuery,
            CancellationToken cancellationToken = default)
        {
            var rows = await purchaseQuery
                .Select(i => new { Remaining = i.GrandTotal - i.PaidAmount })
                .ToListAsync(cancellationToken);

            return rows.Where(r => r.Remaining > 0m).Sum(r => r.Remaining);
        }
    }
}
