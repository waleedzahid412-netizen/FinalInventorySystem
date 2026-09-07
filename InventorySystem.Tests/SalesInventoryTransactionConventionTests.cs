using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Sales;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Inventory transaction sign/type convention:
    /// SALE = stock out (negative qty); REVERSAL_IN = stock restored on edit (positive qty).
    /// </summary>
    public class SalesInventoryTransactionConventionTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(SalesService SalesService, ApplicationDbContext Context, Product Product, ProductUnit Unit)> SetupCatalogAsync(
            string dbName,
            decimal stockQty = 500m)
        {
            var context = GetInMemoryDbContext(dbName);
            var salesRepo = new SalesRepository(context);
            var promoService = new PromotionDiscountService(context);
            var salesService = new SalesService(salesRepo, context, promoService, new FakeCompanyContext(1, "Co"), TestFifoHelper.CreateFifo(context), NullLogger<SalesService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Txn Shop", Address = "1 St", IsActive = true };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse", IsActive = true };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" };
            var company = new Company { CompanyID = 1, CompanyName = "Co" };
            var role = new Role { RoleID = 1, RoleName = "Admin", IsActive = true };
            var user = new User { UserID = 1, RoleID = 1, FullName = "Test User", Username = "test", PasswordHash = "x", IsActive = true };
            var booker = new Booker { BookerID = 1, CompanyID = 1, Name = "Test Booker", CNIC = "35202-1111111", IsActive = true };

            var product = new Product
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
            var productUnit = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 10m,
                IsDefaultSalesUnit = true,
                IsActive = true
            };
            var stock = new InventoryStock
            {
                ProductID = 1,
                WarehouseID = 1,
                Quantity = stockQty,
                DamagedQuantity = 0m,
                IsActive = true
            };

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Categories.Add(category);
            context.Companies.Add(company);
            context.Roles.Add(role);
            context.Users.Add(user);
            context.Bookers.Add(booker);
            context.Products.Add(product);
            context.ProductUnits.Add(productUnit);
            context.InventoryStocks.Add(stock);
            await context.SaveChangesAsync();

            return (salesService, context, product, productUnit);
        }

        [Fact]
        public async Task CreateSale_InventoryTransaction_IsSaleWithNegativeQuantity()
        {
            var (salesService, context, product, unit) =
                await SetupCatalogAsync(nameof(CreateSale_InventoryTransaction_IsSaleWithNegativeQuantity));

            var result = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = product.ProductID,
                        ProductUnitID = unit.ProductUnitID,
                        Quantity = 50m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var txns = await context.InventoryTransactions.AsNoTracking().ToListAsync();

            var saleTxn = Assert.Single(txns);
            Assert.Equal("SALE", saleTxn.TransactionType);
            Assert.True(saleTxn.Quantity < 0);
            Assert.Equal(-50m, saleTxn.Quantity);
            Assert.DoesNotContain(txns, t => string.Equals(t.TransactionType, "OUT", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task EditUnpaidSale_RestockIsReversalInPositive_NewDeductionIsSaleNegative()
        {
            var (salesService, context, product, unit) =
                await SetupCatalogAsync(nameof(EditUnpaidSale_RestockIsReversalInPositive_NewDeductionIsSaleNegative));

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = product.ProductID,
                        ProductUnitID = unit.ProductUnitID,
                        Quantity = 50m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(create.Success, create.Message);
            int invoiceId = create.Data;

            var update = await salesService.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = invoiceId,
                BookerID = 1,
                SalespersonID = 1,
                InvoiceDate = DateTime.UtcNow,
                EditReason = "Reduce quantity for convention test",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = product.ProductID,
                        ProductUnitID = unit.ProductUnitID,
                        Quantity = 30m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);

            Assert.True(update.Success, update.Message);

            var txns = await context.InventoryTransactions.AsNoTracking().ToListAsync();

            var restock = Assert.Single(txns, t => t.TransactionType == "REVERSAL_IN");
            Assert.True(restock.Quantity > 0);
            Assert.Equal(50m, restock.Quantity);

            var saleTxns = txns.Where(t => t.TransactionType == "SALE").ToList();
            Assert.Equal(2, saleTxns.Count);
            Assert.All(saleTxns, t => Assert.True(t.Quantity < 0));
            Assert.Contains(saleTxns, t => t.Quantity == -50m);
            Assert.Contains(saleTxns, t => t.Quantity == -30m);

            Assert.DoesNotContain(txns, t => string.Equals(t.TransactionType, "OUT", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CreateOrEdit_NeverPersistsOutTransactionType()
        {
            var (salesService, context, product, unit) =
                await SetupCatalogAsync(nameof(CreateOrEdit_NeverPersistsOutTransactionType));

            var create = await salesService.CreateAndFinalizeSalesInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerID = 1,
                BookerID = 1,
                SalespersonID = 1,
                WarehouseID = 1,
                InvoiceDate = DateTime.UtcNow,
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = product.ProductID,
                        ProductUnitID = unit.ProductUnitID,
                        Quantity = 20m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);
            Assert.True(create.Success, create.Message);

            var update = await salesService.UpdateSalesInvoiceAsync(new UpdateSalesInvoiceDto
            {
                InvoiceID = create.Data,
                BookerID = 1,
                SalespersonID = 1,
                InvoiceDate = DateTime.UtcNow,
                EditReason = "Increase quantity",
                ApplyPromotions = false,
                Items = new List<CreateSalesItemDto>
                {
                    new CreateSalesItemDto
                    {
                        ProductID = product.ProductID,
                        ProductUnitID = unit.ProductUnitID,
                        Quantity = 25m,
                        UnitPrice = 10m,
                        ItemType = "NORMAL"
                    }
                }
            }, userId: 1);
            Assert.True(update.Success, update.Message);

            Assert.False(await context.InventoryTransactions.AnyAsync(t => t.TransactionType == "OUT"));
        }
    }
}
