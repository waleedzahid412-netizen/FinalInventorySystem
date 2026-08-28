using System;
using System.Globalization;
using InventorySystem.DTOs.Analytics;

namespace InventorySystem.Services
{
    public static class AnalyticsFilterHelper
    {
        public static (DateTime StartDate, DateTime EndDate, DateTime PrevStartDate, DateTime PrevEndDate) ResolveDates(AnalyticsFilterDto filter)
        {
            DateTime today = DateTime.Today;
            DateTime startDate;
            DateTime endDate = today.AddDays(1).AddTicks(-1);

            switch ((filter.Preset ?? "ThisMonth").ToLowerInvariant())
            {
                case "today":
                    startDate = today;
                    break;
                case "last7days":
                    startDate = today.AddDays(-6);
                    break;
                case "last30days":
                    startDate = today.AddDays(-29);
                    break;
                case "lastmonth":
                    var lastMonth = today.AddMonths(-1);
                    startDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    endDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)).AddDays(1).AddTicks(-1);
                    break;
                case "thisyear":
                    startDate = new DateTime(today.Year, 1, 1);
                    break;
                case "custom":
                    startDate = filter.StartDate?.Date ?? new DateTime(today.Year, today.Month, 1);
                    endDate = filter.EndDate.HasValue
                        ? filter.EndDate.Value.Date.AddDays(1).AddTicks(-1)
                        : endDate;
                    break;
                case "thismonth":
                default:
                    startDate = new DateTime(today.Year, today.Month, 1);
                    break;
            }

            TimeSpan duration = endDate - startDate;
            DateTime prevEndDate = startDate.AddTicks(-1);
            DateTime prevStartDate = prevEndDate - duration;
            return (startDate, endDate, prevStartDate, prevEndDate);
        }

        public static bool HasCategory(AnalyticsFilterDto filter) =>
            filter.CategoryID.HasValue && filter.CategoryID.Value > 0;

        public static bool HasWarehouse(AnalyticsFilterDto filter) =>
            filter.WarehouseID.HasValue && filter.WarehouseID.Value > 0;

        public static bool HasCustomer(AnalyticsFilterDto filter) =>
            filter.CustomerID.HasValue && filter.CustomerID.Value > 0;

        public static bool HasBooker(AnalyticsFilterDto filter) =>
            filter.BookerID.HasValue && filter.BookerID.Value != 0;

        public static bool HasCompany(AnalyticsFilterDto filter) =>
            filter.CompanyID.HasValue && filter.CompanyID.Value > 0;

        public static decimal LineRevenue(decimal quantity, decimal unitPrice, decimal discountAmount) =>
            (quantity * unitPrice) - discountAmount;

        public static decimal InvoiceRemaining(decimal grandTotal, decimal paidAmount, decimal returnedAmount) =>
            Math.Max(0m, grandTotal - paidAmount - returnedAmount);

        public static string AgingBucket(DateTime invoiceDate, DateTime asOf)
        {
            int days = (asOf.Date - invoiceDate.Date).Days;
            if (days <= 0) return "Current";
            if (days <= 30) return "1–30 days";
            if (days <= 60) return "31–60 days";
            if (days <= 90) return "61–90 days";
            return "90+ days";
        }

        public static string EffectivePaymentStatus(string storedStatus, decimal remaining)
        {
            if (remaining <= 0.001m) return "PAID";
            if (string.IsNullOrWhiteSpace(storedStatus)) return remaining > 0 ? "UNPAID" : "PAID";
            if (remaining > 0.001m && storedStatus.Equals("PAID", StringComparison.OrdinalIgnoreCase))
                return "PARTIAL";
            return storedStatus.ToUpperInvariant();
        }

        public static (DateTime BucketStart, DateTime BucketEnd, string Label) Bucket(DateTime date, string interval)
        {
            string mode = (interval ?? "Daily").ToLowerInvariant();
            if (mode == "monthly")
            {
                var start = new DateTime(date.Year, date.Month, 1);
                var end = start.AddMonths(1).AddTicks(-1);
                return (start, end, start.ToString("MMM yyyy"));
            }

            if (mode == "weekly")
            {
                int week = ISOWeek.GetWeekOfYear(date);
                int year = ISOWeek.GetYear(date);
                var start = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
                var end = start.AddDays(7).AddTicks(-1);
                return (start, end, $"Week {week} {year}");
            }

            var day = date.Date;
            return (day, day.AddDays(1).AddTicks(-1), day.ToString("dd MMM"));
        }
    }
}
