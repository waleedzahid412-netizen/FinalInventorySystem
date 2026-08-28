using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Returns;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Option B: Sellable returns increase Quantity; Damaged returns increase DamagedQuantity only.
    /// Historical damaged returns may already sit in Quantity — these tests cover forward behavior only.
    /// </summary>
    public class DamagedReturnStockTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(
            ReturnService ReturnService,
            SalesService SalesService,
            ApplicationDbContext Context,
            SalesInvoice Invoice,
            SalesInvoiceItem ItemA,
            SalesInvoiceItem ItemB,
            InventoryStock StockA,
            InventoryStock StockB)> SetupTwoProductInvoiceAsync(
            string dbName,
            decimal stockAQty = 100m,
            decimal stockADamaged = 0m,
            decimal stockBQty = 50m,
            decimal stockBDamaged = 0m)
        {
            var context = GetInMemoryDbContext(dbName);
            var returnRepo = new ReturnRepository(context);
            var returnService = new ReturnService(returnRepo, context, NullLogger<ReturnService>.Instance);
            var salesRepo = new SalesRepository(context);
            var promoService = new PromotionDiscountService(context);
            var salesService = new SalesService(salesRepo, context, promoService, new FakeCompanyContext(1, "Co"), NullLogger<SalesService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Test Shop", Address = "123 St", IsActive = true };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse", IsActive = true };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" };
            var company = new Company { CompanyID = 1, CompanyName = "Co" };
            var role = new Role { RoleID = 1, RoleName = "Admin", IsActive = true };
            var user = new User { UserID = 1, RoleID = 1, FullName = "Test User", Username = "test", PasswordHash = "x", IsActive = true };
            var booker = new Booker { BookerID = 1, CompanyID = 1, Name = "Test Booker", CNIC = "35202-1111111", IsActive = true };

            var productA = new Product
            {
                ProductID = 1,
                ProductName = "Product A",
                SKU = "A-001",
                BaseSellingPrice = 10m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };
            var productB = new Product
            {
                ProductID = 2,
                ProductName = "Product B",
                SKU = "B-001",
                BaseSellingPrice = 20m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };

            var unitA = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 10m,
                IsActive = true
            };
            var unitB = new ProductUnit
            {
                ProductUnitID = 2,
                ProductID = 2,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 20m,
                IsActive = true
            };

            var stockA = new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = stockAQty,
                DamagedQuantity = stockADamaged,
                IsActive = true
            };
            var stockB = new InventoryStock
            {
                ProductID = 2,
                WarehouseID = 1,
                Quantity = stockBQty,
                DamagedQuantity = stockBDamaged,
                IsActive = true
            };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-DMG-001",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 300m,
                GrandTotal = 300m,
                Items = new List<SalesInvoiceItem>()
            };

            var itemA = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = productA,
                ProductUnitID = 1,
                ProductUnit = unitA,
                Quantity = 20m,
                ConvertedQuantity = 20m,
                UnitPrice = 10m,
                ItemType = "NORMAL"
            };
            var itemB = new SalesInvoiceItem
            {
                InvoiceItemID = 2,
                InvoiceID = 1,
                ProductID = 2,
                Product = productB,
                ProductUnitID = 2,
                ProductUnit = unitB,
                Quantity = 10m,
                ConvertedQuantity = 10m,
                UnitPrice = 20m,
                ItemType = "NORMAL"
            };

            invoice.Items.Add(itemA);
            invoice.Items.Add(itemB);

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Categories.Add(category);
            context.Companies.Add(company);
            context.Roles.Add(role);
            context.Users.Add(user);
            context.Bookers.Add(booker);
            context.Products.AddRange(productA, productB);
            context.ProductUnits.AddRange(unitA, unitB);
            context.InventoryStocks.AddRange(stockA, stockB);
            context.SalesInvoices.Add(invoice);
            context.SalesInvoiceItems.AddRange(itemA, itemB);
            await context.SaveChangesAsync();

            return (returnService, salesService, context, invoice, itemA, itemB, stockA, stockB);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoice Invoice, SalesInvoiceItem Item)> SetupSingleProductInvoiceAsync(
            string dbName,
            decimal stockQty = 100m,
            decimal damagedQty = 0m)
        {
            var setup = await SetupTwoProductInvoiceAsync(dbName, stockQty, damagedQty, 50m, 0m);
            return (setup.ReturnService, setup.Context, setup.Invoice, setup.ItemA);
        }

        [Fact]
        public async Task SellableReturn_IncreasesQuantity_NotDamagedQuantity()
        {
            var (service, context, invoice, item) = await SetupSingleProductInvoiceAsync(nameof(SellableReturn_IncreasesQuantity_NotDamagedQuantity), 100m, 0m);

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
                        Quantity = 10m,
                        ReturnCondition = "Sellable"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var stock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == item.ProductID && s.WarehouseID == invoice.WarehouseID);
            Assert.Equal(110m, stock.Quantity);
            Assert.Equal(0m, stock.DamagedQuantity);

            var returnItem = await context.SalesReturnItems.AsNoTracking()
                .FirstAsync(i => i.SalesReturnID == result.Data && i.InvoiceItemID == item.InvoiceItemID);
            var txn = await context.InventoryTransactions.AsNoTracking()
                .FirstAsync(t => t.SalesReturnItemID == returnItem.SalesReturnItemID);
            Assert.Equal("RETURN", txn.TransactionType);
            Assert.Equal(10m, txn.Quantity);
        }

        [Fact]
        public async Task DamagedReturn_IncreasesDamagedQuantity_NotSellableQuantity()
        {
            var (service, context, invoice, item) = await SetupSingleProductInvoiceAsync(nameof(DamagedReturn_IncreasesDamagedQuantity_NotSellableQuantity), 100m, 0m);

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
                        Quantity = 10m,
                        ReturnCondition = "Damaged"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var stock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == item.ProductID && s.WarehouseID == invoice.WarehouseID);
            Assert.Equal(100m, stock.Quantity);
            Assert.Equal(10m, stock.DamagedQuantity);

            var returnItem = await context.SalesReturnItems.AsNoTracking()
                .FirstAsync(i => i.SalesReturnID == result.Data && i.InvoiceItemID == item.InvoiceItemID);
            Assert.Equal("Damaged", returnItem.ReturnCondition);

            var txn = await context.InventoryTransactions.AsNoTracking()
                .FirstAsync(t => t.SalesReturnItemID == returnItem.SalesReturnItemID);
            Assert.Equal("RETURN_DAMAGED", txn.TransactionType);
            Assert.Equal(10m, txn.Quantity);
            Assert.Equal(returnItem.SalesReturnItemID, txn.SalesReturnItemID);
        }

        [Fact]
        public async Task MixedReturn_SellableAndDamaged_UpdateCorrectBuckets()
        {
            var (returnService, _, context, invoice, itemA, itemB, _, _) =
                await SetupTwoProductInvoiceAsync(nameof(MixedReturn_SellableAndDamaged_UpdateCorrectBuckets), 100m, 0m, 50m, 0m);

            var result = await returnService.ProcessSalesReturnAsync(new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = false,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput
                    {
                        InvoiceItemID = itemA.InvoiceItemID,
                        ProductID = itemA.ProductID ?? 0,
                        ProductUnitID = itemA.ProductUnitID ?? 0,
                        Quantity = 10m,
                        ReturnCondition = "Sellable"
                    },
                    new ReturnItemInput
                    {
                        InvoiceItemID = itemB.InvoiceItemID,
                        ProductID = itemB.ProductID ?? 0,
                        ProductUnitID = itemB.ProductUnitID ?? 0,
                        Quantity = 5m,
                        ReturnCondition = "Damaged"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var stockA = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == itemA.ProductID);
            var stockB = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == itemB.ProductID);

            Assert.Equal(110m, stockA.Quantity);
            Assert.Equal(0m, stockA.DamagedQuantity);
            Assert.Equal(50m, stockB.Quantity);
            Assert.Equal(5m, stockB.DamagedQuantity);

            var txns = await context.InventoryTransactions.AsNoTracking()
                .Where(t => t.ReferenceNumber != null)
                .ToListAsync();
            Assert.Contains(txns, t => t.TransactionType == "RETURN" && t.Quantity == 10m && t.ProductID == itemA.ProductID);
            Assert.Contains(txns, t => t.TransactionType == "RETURN_DAMAGED" && t.Quantity == 5m && t.ProductID == itemB.ProductID);
        }

        [Fact]
        public async Task DamagedStock_IsNotAvailableForSale()
        {
            var (_, salesService, context, _, itemA, _, _, _) =
                await SetupTwoProductInvoiceAsync(nameof(DamagedStock_IsNotAvailableForSale), 100m, 20m, 50m, 0m);

            var successSale = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = itemA.ProductID ?? 0,
                        ProductUnitID = itemA.ProductUnitID ?? 0,
                        Quantity = 100m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(successSale.Success, successSale.Message);

            var stockAfterSuccess = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == itemA.ProductID);
            Assert.Equal(0m, stockAfterSuccess.Quantity);
            Assert.Equal(20m, stockAfterSuccess.DamagedQuantity);

            // Restore sellable to 0 with damaged 20 still present — sale of 1 must fail
            var failSale = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = itemA.ProductID ?? 0,
                        ProductUnitID = itemA.ProductUnitID ?? 0,
                        Quantity = 1m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.False(failSale.Success);
            Assert.Contains("Insufficient stock", failSale.Message, StringComparison.OrdinalIgnoreCase);

            var stockFinal = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == itemA.ProductID);
            Assert.Equal(0m, stockFinal.Quantity);
            Assert.Equal(20m, stockFinal.DamagedQuantity);
        }

        [Fact]
        public async Task NullOrBlankReturnCondition_TreatedAsSellable()
        {
            var (service, context, invoice, item) = await SetupSingleProductInvoiceAsync(nameof(NullOrBlankReturnCondition_TreatedAsSellable), 100m, 5m);

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
                        Quantity = 10m,
                        ReturnCondition = "   "
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var stock = await context.InventoryStocks.AsNoTracking()
                .FirstAsync(s => s.ProductID == item.ProductID);
            Assert.Equal(110m, stock.Quantity);
            Assert.Equal(5m, stock.DamagedQuantity);

            var returnItem = await context.SalesReturnItems.AsNoTracking()
                .FirstAsync(i => i.SalesReturnID == result.Data && i.InvoiceItemID == item.InvoiceItemID);
            var txn = await context.InventoryTransactions.AsNoTracking()
                .FirstAsync(t => t.SalesReturnItemID == returnItem.SalesReturnItemID);
            Assert.Equal("RETURN", txn.TransactionType);
            Assert.Equal(10m, txn.Quantity);
        }

        [Fact]
        public async Task ManualDamagedReturn_IncreasesDamagedQuantityOnly()
        {
            var context = GetInMemoryDbContext(nameof(ManualDamagedReturn_IncreasesDamagedQuantityOnly));
            var returnRepo = new ReturnRepository(context);
            var service = new ReturnService(returnRepo, context, NullLogger<ReturnService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Shop", IsActive = true };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "WH", IsActive = true };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var product = new Product
            {
                ProductID = 1,
                ProductName = "P1",
                SKU = "P1",
                BaseSellingPrice = 5m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };
            var productUnit = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 5m,
                IsActive = true
            };
            var stock = new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = 40m,
                DamagedQuantity = 0m,
                IsActive = true
            };

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Categories.Add(new Category { CategoryID = 1, CompanyID = 1, Name = "C" });
            context.Companies.Add(new Company { CompanyID = 1, CompanyName = "Co" });
            context.Products.Add(product);
            context.ProductUnits.Add(productUnit);
            context.InventoryStocks.Add(stock);
            await context.SaveChangesAsync();

            var result = await service.ProcessManualSalesReturnAsync(new ProcessManualSalesReturnRequest
            {
                CustomerID = 1,
                WarehouseID = 1,
                ReturnDate = DateTime.UtcNow,
                Items = new List<ManualReturnItemInput>
                {
                    new ManualReturnItemInput
                    {
                        ProductID = 1,
                        ProductUnitID = 1,
                        Quantity = 7m,
                        ManualReturnUnitPrice = 5m,
                        ReturnCondition = "Damaged"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var updated = await context.InventoryStocks.AsNoTracking().FirstAsync(s => s.ProductID == 1);
            Assert.Equal(40m, updated.Quantity);
            Assert.Equal(7m, updated.DamagedQuantity);

            var txn = await context.InventoryTransactions.AsNoTracking()
                .FirstAsync(t => t.ProductID == 1 && t.TransactionType == "RETURN_DAMAGED");
            Assert.Equal(7m, txn.Quantity);
        }
    }
}
