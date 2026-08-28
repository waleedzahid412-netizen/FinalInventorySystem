using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.LoadSheets;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class LoadSheetService : ILoadSheetService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContext _companyContext;

        public LoadSheetService(ApplicationDbContext context, ICompanyContext companyContext)
        {
            _context = context;
            _companyContext = companyContext;
        }

        public async Task<OperationResult<LoadSheetDto>> GenerateLoadSheetAsync(LoadSheetFilterDto filter, CancellationToken cancellationToken = default)
        {
            if (filter == null || filter.BookerID <= 0)
            {
                return OperationResult<LoadSheetDto>.Fail("Please select a booker.");
            }

            if (filter.Date == default)
            {
                return OperationResult<LoadSheetDto>.Fail("Please select a date.");
            }

            await _companyContext.TryResolveAsync(cancellationToken);
            int? scopedCompanyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            var bookerQuery = _context.Bookers
                .AsNoTracking()
                .Where(b => b.BookerID == filter.BookerID && b.IsActive);

            if (scopedCompanyId.HasValue)
            {
                bookerQuery = bookerQuery.Where(b => b.CompanyID == scopedCompanyId.Value);
            }

            var booker = await bookerQuery.FirstOrDefaultAsync(cancellationToken);

            if (booker == null)
            {
                return OperationResult<LoadSheetDto>.Fail("Selected booker does not exist or is inactive.");
            }

            string? supplierName = null;
            if (filter.SupplierID.HasValue && filter.SupplierID.Value > 0)
            {
                supplierName = await _context.Suppliers
                    .AsNoTracking()
                    .Where(dp => dp.SupplierID == filter.SupplierID.Value && dp.IsActive)
                    .Select(dp => dp.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (supplierName == null)
                {
                    return OperationResult<LoadSheetDto>.Fail("Selected supplier does not exist or is inactive.");
                }
            }

            var filterDate = filter.Date.Date;

            // Invoices are matched on Booker + Date. Supplier is an optional invoice-header
            // filter (SalesInvoice.SupplierID) — it never restricts products by company.
            var invoiceQuery = _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.BookerID == filter.BookerID)
                .Where(si => si.InvoiceDate.Date == filterDate);

            if (scopedCompanyId.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(si => si.CompanyID == scopedCompanyId.Value);
            }

            if (filter.SupplierID.HasValue && filter.SupplierID.Value > 0)
            {
                invoiceQuery = invoiceQuery.Where(si => si.SupplierID == filter.SupplierID.Value);
            }

            var invoices = await invoiceQuery
                .OrderBy(si => si.InvoiceNumber)
                .Select(si => new
                {
                    si.InvoiceID,
                    si.InvoiceNumber,
                    si.InvoiceDate,
                    si.SubTotal,
                    si.DiscountTotal,
                    si.GrandTotal,
                    si.SalespersonID,
                    SalespersonName = si.SalespersonUser != null ? si.SalespersonUser.FullName : null,
                    PartyName = si.Customer.ShopName,
                    CustomerAddress = si.Customer.Address,
                    AreaName = si.Area != null ? si.Area.AreaName : null,
                    SubAreaName = si.SubArea != null ? si.SubArea.SubAreaName : null
                })
                .ToListAsync(cancellationToken);

            var result = new LoadSheetDto
            {
                BookerName = booker.Name,
                FilterDate = filterDate,
                SupplierDisplay = supplierName ?? "All Suppliers",
                HasInvoices = invoices.Any()
            };

            if (!invoices.Any())
            {
                result.EmptyMessage = supplierName == null
                    ? "No sales invoices found for the selected booker and date."
                    : $"No sales invoices found for booker {booker.Name} on the selected date assigned to {supplierName}.";
                result.SalespersonDisplay = "—";
                return OperationResult<LoadSheetDto>.Ok(result, result.EmptyMessage);
            }

            var salespersonIds = invoices
                .Where(i => i.SalespersonID.HasValue)
                .Select(i => i.SalespersonID!.Value)
                .Distinct()
                .ToList();

            if (salespersonIds.Count == 1)
            {
                result.SalespersonDisplay = invoices.First(i => i.SalespersonID == salespersonIds[0]).SalespersonName ?? "—";
            }
            else
            {
                result.SalespersonDisplay = "Various";
            }

            result.InvoiceRows = invoices.Select(i => new LoadSheetInvoiceRowDto
            {
                InvoiceID = i.InvoiceID,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                PartyName = i.PartyName,
                Address = ResolveAddress(i.CustomerAddress, i.SubAreaName, i.AreaName),
                Amount = i.SubTotal,
                Discount = i.DiscountTotal,
                TotalAmount = i.GrandTotal
            }).ToList();

            result.TotalAmountSum = result.InvoiceRows.Sum(r => r.Amount);
            result.TotalDiscountSum = result.InvoiceRows.Sum(r => r.Discount);
            result.TotalGrandSum = result.InvoiceRows.Sum(r => r.TotalAmount);

            var invoiceIds = invoices.Select(i => i.InvoiceID).ToList();

            var items = await _context.SalesInvoiceItems
                .AsNoTracking()
                .Where(i => invoiceIds.Contains(i.InvoiceID))
                .Where(i => i.IsActive)
                .Where(i => i.ProductID != null)
                .Select(i => new
                {
                    i.InvoiceItemID,
                    i.ProductID,
                    i.Quantity,
                    i.ConvertedQuantity,
                    i.UnitPrice,
                    i.ItemType,
                    ProductName = i.Product!.ProductName,
                    SKU = i.Product.SKU,
                    BaseUnitName = i.Product.BaseUnit != null ? i.Product.BaseUnit.UnitName : string.Empty,
                    ConversionToBaseUnit = i.ProductUnit != null ? i.ProductUnit.ConversionToBaseUnit : 1m
                })
                .ToListAsync(cancellationToken);

            var returnedByItem = await _context.SalesReturnItems
                .AsNoTracking()
                .Where(ri => ri.InvoiceItemID != null)
                .Where(ri => ri.SalesReturn.InvoiceID != null && invoiceIds.Contains(ri.SalesReturn.InvoiceID!.Value))
                .GroupBy(ri => ri.InvoiceItemID!.Value)
                .Select(g => new { InvoiceItemID = g.Key, ReturnedBase = g.Sum(x => x.ConvertedQuantity) })
                .ToDictionaryAsync(x => x.InvoiceItemID, x => x.ReturnedBase, cancellationToken);

            // Aggregate in base units by product, but keep FREE lines on their own rows (not merged with paid).
            // Base unit price = historical packaging UnitPrice ÷ ConversionToBaseUnit (weighted by net base qty).
            var productGroups = items
                .Select(i =>
                {
                    decimal returned = returnedByItem.TryGetValue(i.InvoiceItemID, out var r) ? r : 0m;
                    decimal netBase = Math.Max(0m, i.ConvertedQuantity - returned);
                    decimal conversion = i.ConversionToBaseUnit > 0m ? i.ConversionToBaseUnit : 1m;
                    bool isFree = string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase);
                    decimal baseUnitPrice = !isFree && i.UnitPrice > 0m ? i.UnitPrice / conversion : 0m;
                    return new
                    {
                        i.ProductID,
                        i.ProductName,
                        i.SKU,
                        i.BaseUnitName,
                        IsFree = isFree,
                        NetBaseQty = netBase,
                        BaseUnitPrice = baseUnitPrice
                    };
                })
                .Where(x => x.NetBaseQty > 0m)
                .GroupBy(x => new { ProductID = x.ProductID!.Value, x.IsFree })
                .Select(g =>
                {
                    var first = g.First();
                    var priced = g.Where(x => x.BaseUnitPrice > 0m).ToList();
                    decimal weightedBasePrice = 0m;
                    if (priced.Count > 0)
                    {
                        decimal pricedBaseQty = priced.Sum(x => x.NetBaseQty);
                        weightedBasePrice = pricedBaseQty > 0m
                            ? priced.Sum(x => x.BaseUnitPrice * x.NetBaseQty) / pricedBaseQty
                            : priced.First().BaseUnitPrice;
                    }

                    return new LoadSheetProductRowDto
                    {
                        ProductID = g.Key.ProductID,
                        IsFree = g.Key.IsFree,
                        ProductIdDisplay = !string.IsNullOrWhiteSpace(first.SKU) ? first.SKU! : g.Key.ProductID.ToString(),
                        ProductName = g.Key.IsFree ? $"{first.ProductName} (Free)" : first.ProductName,
                        UnitPrice = Math.Round(weightedBasePrice, 4, MidpointRounding.AwayFromZero),
                        TotalQuantity = g.Sum(x => x.NetBaseQty),
                        BaseUnitName = first.BaseUnitName
                    };
                })
                .OrderBy(r => r.ProductName)
                .ThenBy(r => r.IsFree)
                .ToList();

            result.ProductRows = productGroups;
            result.TotalCountSum = productGroups.Sum(r => r.TotalQuantity);

            if (!productGroups.Any())
            {
                result.HasInvoices = false;
                result.EmptyMessage = "The sales invoices for the selected booker and date contain no loadable product quantities.";
                return OperationResult<LoadSheetDto>.Ok(result, result.EmptyMessage);
            }

            return OperationResult<LoadSheetDto>.Ok(result, "Load sheet generated successfully.");
        }

        private static string ResolveAddress(string? customerAddress, string? subAreaName, string? areaName)
        {
            if (!string.IsNullOrWhiteSpace(customerAddress))
            {
                return customerAddress.Trim();
            }

            if (!string.IsNullOrWhiteSpace(subAreaName) && !string.IsNullOrWhiteSpace(areaName))
            {
                return $"{subAreaName}, {areaName}";
            }

            if (!string.IsNullOrWhiteSpace(areaName))
            {
                return areaName;
            }

            return "—";
        }
    }
}
