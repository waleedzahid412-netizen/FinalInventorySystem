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

        public LoadSheetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OperationResult<LoadSheetDto>> GenerateLoadSheetAsync(LoadSheetFilterDto filter, CancellationToken cancellationToken = default)
        {
            if (filter == null || filter.BrokerID <= 0)
            {
                return OperationResult<LoadSheetDto>.Fail("Please select a broker.");
            }

            if (filter.Date == default)
            {
                return OperationResult<LoadSheetDto>.Fail("Please select a date.");
            }

            var broker = await _context.Brokers
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BrokerID == filter.BrokerID && b.IsActive, cancellationToken);

            if (broker == null)
            {
                return OperationResult<LoadSheetDto>.Fail("Selected broker does not exist or is inactive.");
            }

            string? deliveryPersonName = null;
            if (filter.DeliveryPersonID.HasValue && filter.DeliveryPersonID.Value > 0)
            {
                deliveryPersonName = await _context.DeliveryPersons
                    .AsNoTracking()
                    .Where(dp => dp.DeliveryPersonID == filter.DeliveryPersonID.Value && dp.IsActive)
                    .Select(dp => dp.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (deliveryPersonName == null)
                {
                    return OperationResult<LoadSheetDto>.Fail("Selected delivery person does not exist or is inactive.");
                }
            }

            var filterDate = filter.Date.Date;

            // Invoices are matched on Broker + Date. Delivery person is an optional invoice-header
            // filter (SalesInvoice.DeliveryPersonID) — it never restricts products by supplier.
            var invoiceQuery = _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.BrokerID == filter.BrokerID)
                .Where(si => si.InvoiceDate.Date == filterDate);

            if (filter.DeliveryPersonID.HasValue && filter.DeliveryPersonID.Value > 0)
            {
                invoiceQuery = invoiceQuery.Where(si => si.DeliveryPersonID == filter.DeliveryPersonID.Value);
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
                BrokerName = broker.Name,
                FilterDate = filterDate,
                DeliveryPersonDisplay = deliveryPersonName ?? "All Delivery Persons",
                HasInvoices = invoices.Any()
            };

            if (!invoices.Any())
            {
                result.EmptyMessage = deliveryPersonName == null
                    ? "No sales invoices found for the selected broker and date."
                    : $"No sales invoices found for broker {broker.Name} on the selected date assigned to {deliveryPersonName}.";
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
                    i.ConvertedQuantity,
                    ProductName = i.Product!.ProductName,
                    SKU = i.Product.SKU,
                    Description = i.Product.Description,
                    BaseUnitName = i.Product.BaseUnit != null ? i.Product.BaseUnit.UnitName : string.Empty
                })
                .ToListAsync(cancellationToken);

            var returnedByItem = await _context.SalesReturnItems
                .AsNoTracking()
                .Where(ri => ri.InvoiceItemID != null)
                .Where(ri => ri.SalesReturn.InvoiceID != null && invoiceIds.Contains(ri.SalesReturn.InvoiceID!.Value))
                .GroupBy(ri => ri.InvoiceItemID!.Value)
                .Select(g => new { InvoiceItemID = g.Key, ReturnedBase = g.Sum(x => x.ConvertedQuantity) })
                .ToDictionaryAsync(x => x.InvoiceItemID, x => x.ReturnedBase, cancellationToken);

            var productGroups = items
                .Select(i =>
                {
                    decimal returned = returnedByItem.TryGetValue(i.InvoiceItemID, out var r) ? r : 0m;
                    decimal net = Math.Max(0m, i.ConvertedQuantity - returned);
                    return new
                    {
                        i.ProductID,
                        i.ProductName,
                        i.SKU,
                        i.Description,
                        i.BaseUnitName,
                        NetBaseQty = net
                    };
                })
                .Where(x => x.NetBaseQty > 0m)
                .GroupBy(x => x.ProductID!.Value)
                .Select(g => new LoadSheetProductRowDto
                {
                    ProductID = g.Key,
                    ProductIdDisplay = !string.IsNullOrWhiteSpace(g.First().SKU) ? g.First().SKU! : g.Key.ToString(),
                    ProductName = g.First().ProductName,
                    Description = string.IsNullOrWhiteSpace(g.First().Description) ? "—" : g.First().Description,
                    TotalQuantity = g.Sum(x => x.NetBaseQty),
                    BaseUnitName = g.First().BaseUnitName
                })
                .OrderBy(r => r.ProductName)
                .ToList();

            result.ProductRows = productGroups;
            result.TotalCountSum = productGroups.Sum(r => r.TotalQuantity);

            if (!productGroups.Any())
            {
                result.HasInvoices = false;
                result.EmptyMessage = "The sales invoices for the selected broker and date contain no loadable product quantities.";
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
