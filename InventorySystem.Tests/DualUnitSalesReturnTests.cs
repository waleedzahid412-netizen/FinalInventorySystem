using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Returns;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class DualUnitSalesReturnTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoice Invoice, SalesInvoiceItem Item)> SetupCartonInvoiceAsync(string dbName)
        {
            var context = GetInMemoryDbContext(dbName);
            var repository = new ReturnRepository(context);
            var service = new ReturnService(repository, context, NullLogger<ReturnService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Test Shop", Address = "123 St" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse" };
            var pieceUnit = new Unit { UnitID = 1, UnitName = "Piece" };
            var cartonUnit = new Unit { UnitID = 2, UnitName = "Carton" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Drinks" };
            var company = new Company { CompanyID = 1, CompanyName = "Co" };

            var product = new Product
            {
                ProductID = 1,
                ProductName = "Cola",
                SKU = "COLA-1",
                BaseSellingPrice = 10m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                BaseUnit = pieceUnit,
                IsActive = true
            };

            var productUnit = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 2,
                Unit = cartonUnit,
                ConversionToBaseUnit = 24m,
                SellingPrice = 240m,
                IsActive = true
            };

            var stock = new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = 1000m,
                DamagedQuantity = 0m,
                IsActive = true
            };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "SI-DUAL-001",
                CustomerID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 720m,
                GrandTotal = 720m,
                PaymentStatus = "UNPAID",
                CreatedBy = 1,
                Items = new List<SalesInvoiceItem>()
            };

            var item = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = product,
                ProductUnitID = 1,
                ProductUnit = productUnit,
                Quantity = 3m,
                ConvertedQuantity = 72m,
                UnitPrice = 240m,
                ItemType = "NORMAL",
                IsDeleted = false
            };

            invoice.Items.Add(item);

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.AddRange(pieceUnit, cartonUnit);
            context.Categories.Add(category);
            context.Companies.Add(company);
            context.Products.Add(product);
            context.ProductUnits.Add(productUnit);
            context.InventoryStocks.Add(stock);
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return (service, context, invoice, item);
        }

        [Fact]
        public async Task Eligibility_ExposesDualUnitRemaining()
        {
            var (service, _, invoice, _) = await SetupCartonInvoiceAsync(nameof(Eligibility_ExposesDualUnitRemaining));

            var result = await service.GetSalesReturnEligibilityAsync(invoice.InvoiceID);
            Assert.True(result.Success, result.Message);

            var line = result.Data!.Items.Single();
            Assert.Equal("Carton", line.UnitName);
            Assert.Equal("Piece", line.BaseUnitName);
            Assert.Equal(24m, line.ConversionToBaseUnit);
            Assert.Equal(3m, line.OriginalQuantity);
            Assert.Equal(72m, line.OriginalConvertedQuantity);
            Assert.Equal(3m, line.RemainingReturnableQuantity);
            Assert.Equal(72m, line.RemainingReturnableConvertedQuantity);
        }

        [Fact]
        public async Task Process_BaseMode_StoresPackagingFractionAndBaseQty()
        {
            var (service, context, invoice, item) = await SetupCartonInvoiceAsync(nameof(Process_BaseMode_StoresPackagingFractionAndBaseQty));

            var result = await service.ProcessSalesReturnAsync(new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = false,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput
                    {
                        InvoiceItemID = item.InvoiceItemID,
                        ProductID = item.ProductID ?? 0,
                        ProductUnitID = item.ProductUnitID ?? 0,
                        Quantity = 12m,
                        ReturnUnitMode = ReturnQuantityHelper.BaseMode,
                        ReturnCondition = "Sellable"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var returnItem = await context.SalesReturnItems.AsNoTracking()
                .FirstAsync(i => i.SalesReturnID == result.Data && i.InvoiceItemID == item.InvoiceItemID);

            Assert.Equal(0.5m, returnItem.Quantity);
            Assert.Equal(12m, returnItem.ConvertedQuantity);
            Assert.Equal(120m, returnItem.RefundAmount);
        }

        [Fact]
        public async Task Process_PackagingFractional_Rejected()
        {
            var (service, _, invoice, item) = await SetupCartonInvoiceAsync(nameof(Process_PackagingFractional_Rejected));

            var result = await service.ProcessSalesReturnAsync(new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = false,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput
                    {
                        InvoiceItemID = item.InvoiceItemID,
                        ProductID = item.ProductID ?? 0,
                        ProductUnitID = item.ProductUnitID ?? 0,
                        Quantity = 0.5m,
                        ReturnUnitMode = ReturnQuantityHelper.PackagingMode,
                        ReturnCondition = "Sellable"
                    }
                }
            }, userId: 1);

            Assert.False(result.Success);
            Assert.Contains("whole number", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
