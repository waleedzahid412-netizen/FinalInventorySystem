using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs.LoadSheets;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InventorySystem.Tests
{
    public class LoadSheetServiceTests
    {
        private ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(LoadSheetService Service, ApplicationDbContext Context, SalesService SalesService)> SetupAsync(string dbName)
        {
            var context = CreateContext(dbName);
            var loadSheetService = new LoadSheetService(context);
            var salesRepo = new SalesRepository(context);
            var promoService = new PromotionDiscountService(context);
            var salesService = new SalesService(salesRepo, context, promoService, NullLogger<SalesService>.Instance);

            var role = new Role { RoleID = 1, RoleName = "Admin", IsActive = true };
            var user1 = new User { UserID = 1, RoleID = 1, FullName = "Shahzaib", Username = "shahzaib", PasswordHash = "x", IsActive = true };
            var user2 = new User { UserID = 2, RoleID = 1, FullName = "Ali", Username = "ali", PasswordHash = "x", IsActive = true };
            var brokerX = new Broker { BrokerID = 1, Name = "Nadeem", IsActive = true };
            var brokerY = new Broker { BrokerID = 2, Name = "Other Broker", IsActive = true };
            var companyA = new Company { CompanyID = 1, CompanyName = "Supplier A" };
            var companyB = new Company { CompanyID = 2, CompanyName = "Supplier B" };
            var companyC = new Company { CompanyID = 3, CompanyName = "Supplier C" };
            var customer = new Customer { CustomerID = 1, ShopName = "Party Shop", Address = "123 Main St", IsActive = true };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main", IsActive = true };
            var unitPiece = new Unit { UnitID = 1, UnitName = "Piece" };
            var unitCarton = new Unit { UnitID = 2, UnitName = "Carton" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" };

            var productA = new Product
            {
                ProductID = 1,
                ProductName = "Product A",
                SKU = "SKU-A",
                Description = "Desc A",
                CompanyID = 1,
                CategoryID = 1,
                BaseUnitID = 1,
                BaseSellingPrice = 10m,
                IsActive = true
            };
            var productB = new Product
            {
                ProductID = 2,
                ProductName = "Product B",
                SKU = "SKU-B",
                CompanyID = 2,
                CategoryID = 1,
                BaseUnitID = 1,
                BaseSellingPrice = 20m,
                IsActive = true
            };

            var puPiece = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1m, SellingPrice = 10m, IsDefaultSalesUnit = true, IsActive = true };
            var puCarton = new ProductUnit { ProductUnitID = 2, ProductID = 1, UnitID = 2, ConversionToBaseUnit = 12m, SellingPrice = 100m, IsActive = true };
            var puB = new ProductUnit { ProductUnitID = 3, ProductID = 2, UnitID = 1, ConversionToBaseUnit = 1m, SellingPrice = 20m, IsDefaultSalesUnit = true, IsActive = true };

            context.Roles.Add(role);
            context.Users.AddRange(user1, user2);
            context.Brokers.AddRange(brokerX, brokerY);
            context.Companies.AddRange(companyA, companyB, companyC);
            context.DeliveryPersons.AddRange(
                new DeliveryPerson { DeliveryPersonID = 1, Name = "Khalid", Type = "Employee", IsActive = true },
                new DeliveryPerson { DeliveryPersonID = 2, Name = "Waseem", Type = "Employee", IsActive = true });
            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.AddRange(unitPiece, unitCarton);
            context.Categories.Add(category);
            context.Products.AddRange(productA, productB);
            context.ProductUnits.AddRange(puPiece, puCarton, puB);
            context.InventoryStocks.AddRange(
                new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 1000m, IsActive = true },
                new InventoryStock { ProductID = 2, WarehouseID = 1, Quantity = 1000m, IsActive = true });

            await context.SaveChangesAsync();
            return (loadSheetService, context, salesService);
        }

        private static CreateSalesInvoiceDto BuildInvoice(int brokerId, int salespersonId, DateTime date, params CreateSalesItemDto[] items)
        {
            return new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BrokerID = brokerId,
                SalespersonID = salespersonId,
                WarehouseID = 1,
                InvoiceDate = date,
                ApplyPromotions = false,
                Items = items.ToList()
            };
        }

        private static CreateSalesItemDto LineA(decimal quantity, int productUnitId = 1, decimal unitPrice = 10m)
            => new CreateSalesItemDto { ProductID = 1, ProductUnitID = productUnitId, Quantity = quantity, UnitPrice = unitPrice, ItemType = "NORMAL" };

        private static CreateSalesItemDto LineB(decimal quantity, decimal unitPrice = 20m)
            => new CreateSalesItemDto { ProductID = 2, ProductUnitID = 3, Quantity = quantity, UnitPrice = unitPrice, ItemType = "NORMAL" };

        [Fact]
        public async Task NewInvoice_RequiresBroker()
        {
            var (_, _, salesService) = await SetupAsync(nameof(NewInvoice_RequiresBroker));
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(
                BuildInvoice(0, 1, DateTime.UtcNow, LineA(1m)), 1);

            Assert.False(result.Success);
            Assert.Contains("broker", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NewInvoice_RequiresSalesperson()
        {
            var (_, _, salesService) = await SetupAsync(nameof(NewInvoice_RequiresSalesperson));
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(
                BuildInvoice(1, 0, DateTime.UtcNow, LineA(1m)), 1);

            Assert.False(result.Success);
            Assert.Contains("salesperson", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NewInvoice_DoesNotRequireASupplier()
        {
            var (_, context, salesService) = await SetupAsync(nameof(NewInvoice_DoesNotRequireASupplier));
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(
                BuildInvoice(1, 1, DateTime.UtcNow, LineA(1m)), 1);

            Assert.True(result.Success, result.Message);
            var invoice = await context.SalesInvoices.FirstAsync(i => i.InvoiceID == result.Data);
            Assert.Null(invoice.CompanyID);
        }

        [Fact]
        public async Task ProductsFromDifferentSuppliers_AreAcceptedOnOneInvoice()
        {
            var (_, context, salesService) = await SetupAsync(nameof(ProductsFromDifferentSuppliers_AreAcceptedOnOneInvoice));
            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(
                BuildInvoice(1, 1, DateTime.UtcNow, LineA(2m), LineB(3m)), 1);

            Assert.True(result.Success, result.Message);

            var suppliers = await context.SalesInvoiceItems
                .Where(i => i.InvoiceID == result.Data && i.ProductID != null)
                .Select(i => i.Product!.CompanyID)
                .Distinct()
                .ToListAsync();

            Assert.Equal(2, suppliers.Count);
        }

        [Fact]
        public async Task BrokerAndDate_FindsCorrectInvoices()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BrokerAndDate_FindsCorrectInvoices));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(5m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.True(result.Success);
            Assert.True(result.Data!.HasInvoices);
            Assert.Single(result.Data.InvoiceRows);
        }

        [Fact]
        public async Task BrokerAndDate_ExcludesOtherBroker()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BrokerAndDate_ExcludesOtherBroker));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(2, 1, date, LineA(1m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.True(result.Success);
            Assert.False(result.Data!.HasInvoices);
        }

        [Fact]
        public async Task BrokerAndDate_ExcludesOtherDates()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BrokerAndDate_ExcludesOtherDates));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date.AddDays(-1), LineA(1m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.True(result.Success);
            Assert.False(result.Data!.HasInvoices);
        }

        [Fact]
        public async Task DeliveryPersonFilter_IncludesOnlyAssignedInvoices()
        {
            var (service, _, salesService) = await SetupAsync(nameof(DeliveryPersonFilter_IncludesOnlyAssignedInvoices));
            var date = new DateTime(2026, 8, 18);

            var invoiceKhalid = BuildInvoice(1, 1, date, LineA(5m), LineB(3m));
            invoiceKhalid.DeliveryPersonID = 1;
            await salesService.CreateAndFinalizeSalesInvoiceAsync(invoiceKhalid, 1);

            var invoiceWaseem = BuildInvoice(1, 1, date, LineA(2m));
            invoiceWaseem.DeliveryPersonID = 2;
            await salesService.CreateAndFinalizeSalesInvoiceAsync(invoiceWaseem, 1);

            var khalid = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date, DeliveryPersonID = 1 });
            Assert.True(khalid.Success);
            Assert.Single(khalid.Data!.InvoiceRows);
            Assert.Equal("Khalid", khalid.Data.DeliveryPersonDisplay);
            Assert.Equal(2, khalid.Data.ProductRows.Count);
            Assert.Equal(5m, khalid.Data.ProductRows.Single(r => r.ProductName == "Product A").TotalQuantity);
            Assert.Equal(3m, khalid.Data.ProductRows.Single(r => r.ProductName == "Product B").TotalQuantity);

            var waseem = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date, DeliveryPersonID = 2 });
            Assert.True(waseem.Success);
            Assert.Single(waseem.Data!.InvoiceRows);
            Assert.Equal("Waseem", waseem.Data.DeliveryPersonDisplay);
            var row = Assert.Single(waseem.Data.ProductRows);
            Assert.Equal("Product A", row.ProductName);
            Assert.Equal(2m, row.TotalQuantity);
        }

        [Fact]
        public async Task DeliveryPersonFilter_WithNoMatchingInvoices_ReportsEmpty()
        {
            var (service, _, salesService) = await SetupAsync(nameof(DeliveryPersonFilter_WithNoMatchingInvoices_ReportsEmpty));
            var date = new DateTime(2026, 8, 18);

            var invoice = BuildInvoice(1, 1, date, LineA(5m));
            invoice.DeliveryPersonID = 1;
            await salesService.CreateAndFinalizeSalesInvoiceAsync(invoice, 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date, DeliveryPersonID = 2 });
            Assert.True(result.Success);
            Assert.False(result.Data!.HasInvoices);
            Assert.Contains("Waseem", result.Data.EmptyMessage!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BrokerDateWithoutDeliveryPerson_IncludesAllDeliveryPersons()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BrokerDateWithoutDeliveryPerson_IncludesAllDeliveryPersons));
            var date = new DateTime(2026, 8, 18);

            var first = BuildInvoice(1, 1, date, LineA(1m), LineB(1m));
            first.DeliveryPersonID = 1;
            await salesService.CreateAndFinalizeSalesInvoiceAsync(first, 1);

            var second = BuildInvoice(1, 1, date, LineB(2m));
            second.DeliveryPersonID = 2;
            await salesService.CreateAndFinalizeSalesInvoiceAsync(second, 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.InvoiceRows.Count);
            Assert.Equal("All Delivery Persons", result.Data.DeliveryPersonDisplay);
            Assert.Equal(2, result.Data.ProductRows.Count);
        }

        [Fact]
        public async Task SameProductAcrossInvoices_IsAggregated()
        {
            var (service, _, salesService) = await SetupAsync(nameof(SameProductAcrossInvoices_IsAggregated));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(5m)), 1);
            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(3m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            var row = Assert.Single(result.Data!.ProductRows);
            Assert.Equal(8m, row.TotalQuantity);
        }

        [Fact]
        public async Task DifferentUnits_AggregateUsingConvertedQuantity()
        {
            var (service, _, salesService) = await SetupAsync(nameof(DifferentUnits_AggregateUsingConvertedQuantity));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date,
                LineA(5m),
                LineA(1m, productUnitId: 2, unitPrice: 100m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            var row = Assert.Single(result.Data!.ProductRows);
            Assert.Equal(17m, row.TotalQuantity);
        }

        [Fact]
        public async Task ReturnedQuantities_ReduceLoadQuantity()
        {
            var (service, context, salesService) = await SetupAsync(nameof(ReturnedQuantities_ReduceLoadQuantity));
            var date = new DateTime(2026, 8, 18);

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(20m)), 1);
            var item = await context.SalesInvoiceItems.FirstAsync(i => i.InvoiceID == create.Data);

            context.SalesReturns.Add(new SalesReturn
            {
                InvoiceID = create.Data,
                CustomerID = 1,
                ReturnNumber = "SR-001",
                ReturnDate = date.AddDays(2),
                ReturnType = "INVOICE",
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                NetRefundAmount = 50m,
                CreatedBy = 1,
                CreatedAt = date.AddDays(2),
                IsDeleted = false
            });
            await context.SaveChangesAsync();
            var salesReturn = await context.SalesReturns.FirstAsync();

            context.SalesReturnItems.Add(new SalesReturnItem
            {
                SalesReturnID = salesReturn.SalesReturnID,
                InvoiceItemID = item.InvoiceItemID,
                ProductID = 1,
                ProductUnitID = 1,
                Quantity = 5m,
                ConvertedQuantity = 5m,
                RefundUnitPrice = 10m,
                RefundAmount = 50m
            });
            await context.SaveChangesAsync();

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            var row = Assert.Single(result.Data!.ProductRows);
            Assert.Equal(15m, row.TotalQuantity);
        }

        [Fact]
        public async Task FreeProductBackedItems_AreIncluded()
        {
            var (service, context, salesService) = await SetupAsync(nameof(FreeProductBackedItems_AreIncluded));
            var date = new DateTime(2026, 8, 18);

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(10m)), 1);
            Assert.True(create.Success, create.Message);

            context.SalesInvoiceItems.Add(new SalesInvoiceItem
            {
                InvoiceID = create.Data,
                ProductID = 1,
                ProductUnitID = 1,
                Quantity = 2m,
                ConvertedQuantity = 2m,
                UnitPrice = 0m,
                ItemType = "FREE",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = date,
                IsDeleted = false
            });
            await context.SaveChangesAsync();

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            var row = Assert.Single(result.Data!.ProductRows);
            Assert.Equal(12m, row.TotalQuantity);
        }

        [Fact]
        public async Task MixedSupplierProducts_RemainOnTheSameInvoiceLoadSheet()
        {
            var (service, context, salesService) = await SetupAsync(nameof(MixedSupplierProducts_RemainOnTheSameInvoiceLoadSheet));
            var date = new DateTime(2026, 8, 18);

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(10m)), 1);
            Assert.True(create.Success, create.Message);

            context.SalesInvoiceItems.Add(new SalesInvoiceItem
            {
                InvoiceID = create.Data,
                ProductID = 2,
                ProductUnitID = 3,
                Quantity = 4m,
                ConvertedQuantity = 4m,
                UnitPrice = 0m,
                ItemType = "FREE",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = date,
                IsDeleted = false
            });
            await context.SaveChangesAsync();

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.Equal(2, result.Data!.ProductRows.Count);
            Assert.Equal(10m, result.Data.ProductRows.Single(r => r.ProductName == "Product A").TotalQuantity);
            Assert.Equal(4m, result.Data.ProductRows.Single(r => r.ProductName == "Product B").TotalQuantity);
        }

        [Fact]
        public async Task CustomFreeItemsWithoutProduct_AreExcluded()
        {
            var (service, context, salesService) = await SetupAsync(nameof(CustomFreeItemsWithoutProduct_AreExcluded));
            var date = new DateTime(2026, 8, 18);

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(5m)), 1);

            context.SalesInvoiceItems.Add(new SalesInvoiceItem
            {
                InvoiceID = create.Data,
                ProductID = null,
                ProductUnitID = null,
                Quantity = 99m,
                ConvertedQuantity = 99m,
                UnitPrice = 0m,
                ItemType = "FREE",
                CustomItemName = "Promo Gift",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = date,
                IsDeleted = false
            });
            await context.SaveChangesAsync();

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            var row = Assert.Single(result.Data!.ProductRows);
            Assert.Equal(5m, row.TotalQuantity);
        }

        [Fact]
        public async Task BackPage_ContainsOneRowPerInvoice()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BackPage_ContainsOneRowPerInvoice));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(1m), LineA(2m)), 1);
            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(3m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.Equal(2, result.Data!.InvoiceRows.Count);
        }

        [Fact]
        public async Task BackPageTotals_AreCalculatedCorrectly()
        {
            var (service, _, salesService) = await SetupAsync(nameof(BackPageTotals_AreCalculatedCorrectly));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(2m, unitPrice: 100m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.Equal(200m, result.Data!.TotalAmountSum);
            Assert.Equal(0m, result.Data.TotalDiscountSum);
            Assert.Equal(200m, result.Data.TotalGrandSum);
        }

        [Fact]
        public async Task MultipleSalespersons_DisplayVarious()
        {
            var (service, _, salesService) = await SetupAsync(nameof(MultipleSalespersons_DisplayVarious));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(1m)), 1);
            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 2, date, LineA(1m)), 2);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.Equal("Various", result.Data!.SalespersonDisplay);
        }

        [Fact]
        public async Task SingleSalesperson_DisplaysName()
        {
            var (service, _, salesService) = await SetupAsync(nameof(SingleSalesperson_DisplaysName));
            var date = new DateTime(2026, 8, 18);

            await salesService.CreateAndFinalizeSalesInvoiceAsync(BuildInvoice(1, 1, date, LineA(1m)), 1);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = date });
            Assert.Equal("Shahzaib", result.Data!.SalespersonDisplay);
        }

        [Fact]
        public async Task HistoricalInvoiceWithNullBroker_RemainsInDatabase()
        {
            var (service, context, _) = await SetupAsync(nameof(HistoricalInvoiceWithNullBroker_RemainsInDatabase));
            context.SalesInvoices.Add(new SalesInvoice
            {
                CustomerID = 1,
                WarehouseID = 1,
                InvoiceNumber = "INV-HIST-001",
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 10m,
                GrandTotal = 10m,
                PaymentStatus = "UNPAID",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                BrokerID = null,
                CompanyID = null,
                SalespersonID = null
            });
            await context.SaveChangesAsync();

            var invoice = await context.SalesInvoices.FirstAsync(i => i.InvoiceNumber == "INV-HIST-001");
            Assert.Null(invoice.BrokerID);

            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = DateTime.UtcNow.Date });
            Assert.True(result.Success);
            Assert.False(result.Data!.HasInvoices);
        }

        [Fact]
        public async Task EmptyResult_ProducesClearMessage()
        {
            var (service, _, _) = await SetupAsync(nameof(EmptyResult_ProducesClearMessage));
            var result = await service.GenerateLoadSheetAsync(new LoadSheetFilterDto { BrokerID = 1, Date = new DateTime(2099, 1, 1) });

            Assert.True(result.Success);
            Assert.False(result.Data!.HasInvoices);
            Assert.Contains("No sales invoices found", result.Data.EmptyMessage, StringComparison.OrdinalIgnoreCase);
        }
    }
}
