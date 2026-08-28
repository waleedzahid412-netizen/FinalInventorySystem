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
    /// <summary>
    /// Remaining-state discount return tests.
    /// NORMAL refunds always use SalesInvoiceItem.UnitPrice (historical), never live product prices.
    /// Threshold eligibility uses InvoiceDiscount snapshots, never live DiscountRules.
    /// </summary>
    public class DiscountClawbackTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoice Invoice,
            SalesInvoiceItem ItemA, SalesInvoiceItem ItemB, SalesInvoiceItem ItemC)> SetupCanonical22000InvoiceAsync(string dbName)
        {
            var context = GetInMemoryDbContext(dbName);
            var repository = new ReturnRepository(context);
            var service = new ReturnService(repository, context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "testuser", PasswordHash = "hash", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "Test Shop", Address = "123 St" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };

            var productA = new Product { ProductID = 1, ProductName = "Product A", SKU = "A", BaseSellingPrice = 10000m };
            var productB = new Product { ProductID = 2, ProductName = "Product B", SKU = "B", BaseSellingPrice = 8000m };
            var productC = new Product { ProductID = 3, ProductName = "Product C", SKU = "C", BaseSellingPrice = 4000m };
            var puA = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 10000m };
            var puB = new ProductUnit { ProductUnitID = 2, ProductID = 2, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 8000m };
            var puC = new ProductUnit { ProductUnitID = 3, ProductID = 3, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 4000m };

            // Live rule deliberately DIFFERENT from snapshot — returns must ignore this.
            var liveRule = new DiscountRule
            {
                DiscountRuleID = 1,
                CompanyID = 1,
                RuleName = "5% Off 15K+",
                MinimumOrderAmount = 50_000m, // live changed
                DiscountType = "Percentage",
                DiscountValue = 5m,
                IsActive = true,
                CreatedBy = 1
            };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-22000",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 22_000m,
                DiscountTotal = 1_100m,
                GrandTotal = 20_900m,
                DiscountMode = "Automatic",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = 1,
                        RuleName = "5% Off 15K+",
                        DiscountType = "Percentage",
                        DiscountValue = 5m,
                        DiscountAmount = 1_100m,
                        MinimumOrderAmount = 15_000m, // HISTORICAL snapshot
                        MaximumOrderAmount = null,
                        DiscountSource = "Automatic",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var itemA = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = productA,
                ProductUnitID = 1,
                ProductUnit = puA,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 10_000m,
                DiscountRate = 5m,
                DiscountAmount = 500m,
                ItemType = "NORMAL"
            };
            var itemB = new SalesInvoiceItem
            {
                InvoiceItemID = 2,
                InvoiceID = 1,
                ProductID = 2,
                Product = productB,
                ProductUnitID = 2,
                ProductUnit = puB,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 8_000m,
                DiscountRate = 5m,
                DiscountAmount = 400m,
                ItemType = "NORMAL"
            };
            var itemC = new SalesInvoiceItem
            {
                InvoiceItemID = 3,
                InvoiceID = 1,
                ProductID = 3,
                Product = productC,
                ProductUnitID = 3,
                ProductUnit = puC,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 4_000m,
                DiscountRate = 5m,
                DiscountAmount = 200m,
                ItemType = "NORMAL"
            };

            invoice.Items.Add(itemA);
            invoice.Items.Add(itemB);
            invoice.Items.Add(itemC);

            context.Users.Add(user);
            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Products.AddRange(productA, productB, productC);
            context.ProductUnits.AddRange(puA, puB, puC);
            context.DiscountRules.Add(liveRule);
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return (service, context, invoice, itemA, itemB, itemC);
        }

        [Fact]
        public async Task Return1_Of4000_Refunds3800_Not4000()
        {
            var (service, _, invoice, _, _, itemC) = await SetupCanonical22000InvoiceAsync("Remaining_Return1");

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemC.InvoiceItemID, ProductID = 3, ProductUnitID = 3, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(4_000m, result.Data!.GrossReturnedValue);
            Assert.Equal(200m, result.Data.ItemDiscountReleased);
            Assert.Equal(0m, result.Data.DiscountClawback);
            Assert.Equal(3_800m, result.Data.NetRefundAmount);
            Assert.Equal(18_000m, result.Data.NextRemainingSubtotal);
            Assert.Equal(900m, result.Data.NextRemainingDiscount);
            Assert.Equal(17_100m, result.Data.NextRemainingNet);
        }

        [Fact]
        public async Task Return2_Of5000_Refunds4100_Not3900()
        {
            var context = GetInMemoryDbContext("Remaining_Return2");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);
            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var products = new[] {
                new Product { ProductID = 1, ProductName = "A", SKU = "A", BaseSellingPrice = 10000m },
                new Product { ProductID = 2, ProductName = "Mid", SKU = "M", BaseSellingPrice = 5000m },
                new Product { ProductID = 3, ProductName = "C", SKU = "C", BaseSellingPrice = 4000m },
                new Product { ProductID = 4, ProductName = "Rest", SKU = "R", BaseSellingPrice = 3000m }
            };
            var units = products.Select(p => new ProductUnit { ProductUnitID = p.ProductID, ProductID = p.ProductID, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = p.BaseSellingPrice }).ToArray();
            var invoice = new SalesInvoice {
                InvoiceID = 1, InvoiceNumber = "INV-R2", CustomerID = 1, CompanyID = 1, Customer = customer, WarehouseID = 1, Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow, SubTotal = 22000m, DiscountTotal = 1100m, GrandTotal = 20900m, DiscountMode = "Automatic", CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount> {
                    new InvoiceDiscount { InvoiceDiscountID = 1, InvoiceID = 1, DiscountRuleID = 1, RuleName = "5%", DiscountType = "Percentage", DiscountValue = 5m, DiscountAmount = 1100m, MinimumOrderAmount = 15000m, DiscountSource = "Automatic", AppliedBy = 1 }
                },
                Items = new List<SalesInvoiceItem>()
            };
            SalesInvoiceItem Mk(int id, decimal price, decimal disc) => new SalesInvoiceItem {
                InvoiceItemID = id, InvoiceID = 1, ProductID = id, Product = products[id-1], ProductUnitID = id, ProductUnit = units[id-1],
                Quantity = 1m, ConvertedQuantity = 1m, UnitPrice = price, DiscountRate = 5m, DiscountAmount = disc, ItemType = "NORMAL"
            };
            var itemC = Mk(3, 4000m, 200m);
            var itemMid = Mk(2, 5000m, 250m);
            invoice.Items.Add(Mk(1, 10000m, 500m));
            invoice.Items.Add(itemMid);
            invoice.Items.Add(itemC);
            invoice.Items.Add(Mk(4, 3000m, 150m));
            context.DiscountRules.Add(new DiscountRule { DiscountRuleID = 1, CompanyID = 1, RuleName = "5%", MinimumOrderAmount = 50000m, DiscountType = "Percentage", DiscountValue = 5m, IsActive = true, CreatedBy = 1 });
            context.AddRange(user, customer, warehouse, unit); context.AddRange(products); context.AddRange(units); context.Add(invoice);
            await context.SaveChangesAsync();
            context.SalesReturns.Add(new SalesReturn { SalesReturnID = 1, ReturnNumber = "SR-1", ReturnType = "INVOICE", InvoiceID = 1, CustomerID = 1, WarehouseID = 1, SettlementMethod = "ACCOUNT_ADJUSTMENT", ReturnDate = DateTime.UtcNow, GrossAmount = 4000m, NetRefundAmount = 3800m, IncludeSchemeCalculation = true, CreatedBy = 1 });
            context.SalesReturnItems.Add(new SalesReturnItem { SalesReturnID = 1, InvoiceItemID = itemC.InvoiceItemID, ProductID = 3, ProductUnitID = 3, Quantity = 1m, ConvertedQuantity = 1m, RefundUnitPrice = 4000m, RefundAmount = 4000m, DiscountAmount = 200m, ReturnCondition = "Sellable" });
            await context.SaveChangesAsync();
            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest {
                SalesInvoiceID = 1, IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput> { new ReturnItemInput { InvoiceItemID = itemMid.InvoiceItemID, ProductID = 2, ProductUnitID = 2, Quantity = 1m } }
            });
            Assert.True(result.Success);
            Assert.Equal(5000m, result.Data!.GrossReturnedValue);
            Assert.Equal(4100m, result.Data.NetRefundAmount);
            Assert.Equal(13000m, result.Data.NextRemainingSubtotal);
            Assert.Equal(0m, result.Data.NextRemainingDiscount);
            Assert.Equal(13000m, result.Data.NextRemainingNet);
        }

        [Fact]
        public async Task UsesHistoricalUnitPrice_NotCurrentProductPrice()
        {
            var (service, context, invoice, itemA, _, _) = await SetupCanonical22000InvoiceAsync("Historical_UnitPrice");

            // Change live master prices — must not affect return
            var pu = await context.ProductUnits.FirstAsync(p => p.ProductUnitID == 1);
            pu.SellingPrice = 12_000m;
            var prod = await context.Products.FirstAsync(p => p.ProductID == 1);
            prod.BaseSellingPrice = 12_000m;
            await context.SaveChangesAsync();

            Assert.Equal(10_000m, itemA.UnitPrice); // historical snapshot unchanged

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemA.InvoiceItemID, ProductID = 1, ProductUnitID = 1, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(10_000m, result.Data!.GrossReturnedValue); // NOT 12000
        }

        [Fact]
        public async Task UsesHistoricalThreshold_NotLiveDiscountRule()
        {
            var (service, context, invoice, _, _, itemC) = await SetupCanonical22000InvoiceAsync("Historical_Threshold");

            // Live rule already set to 50000 in setup. Snapshot is 15000.
            // After returning C, remaining 18000 still qualifies under SNAPSHOT 15000.
            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemC.InvoiceItemID, ProductID = 3, ProductUnitID = 3, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(900m, result.Data!.NextRemainingDiscount); // still eligible via snapshot
            Assert.Equal(0m, result.Data.DiscountClawback);

            // Delete live rule entirely — snapshot must still work
            context.DiscountRules.Remove(await context.DiscountRules.FirstAsync());
            await context.SaveChangesAsync();

            var result2 = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemC.InvoiceItemID, ProductID = 3, ProductUnitID = 3, Quantity = 1m }
                }
            });

            Assert.True(result2.Success);
            Assert.Equal(3_800m, result2.Data!.NetRefundAmount);
        }

        [Fact]
        public async Task Manual10Percent_Returns900_For1000Item()
        {
            var context = GetInMemoryDbContext("Manual_10pct");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var product = new Product { ProductID = 1, ProductName = "P", SKU = "P", BaseSellingPrice = 9999m };
            var pu = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 9999m };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-M",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 1_000m,
                DiscountTotal = 100m,
                GrandTotal = 900m,
                DiscountMode = "Manual",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = null,
                        RuleName = "Manual Discount",
                        DiscountType = "Percentage",
                        DiscountValue = 10m,
                        DiscountAmount = 100m,
                        MinimumOrderAmount = null,
                        DiscountSource = "Manual",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var item = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = product,
                ProductUnitID = 1,
                ProductUnit = pu,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 1_000m,
                DiscountRate = 10m,
                DiscountAmount = 100m,
                ItemType = "NORMAL"
            };
            invoice.Items.Add(item);

            context.AddRange(user, customer, warehouse, unit, product, pu, invoice);
            await context.SaveChangesAsync();

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = 1, ProductID = 1, ProductUnitID = 1, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(1_000m, result.Data!.GrossReturnedValue);
            Assert.Equal(100m, result.Data.ItemDiscountReleased);
            Assert.Equal(0m, result.Data.DiscountClawback); // no threshold for manual
            Assert.Equal(900m, result.Data.NetRefundAmount);
        }

        [Fact]
        public async Task CustomerPreferred5Percent_Returns950_For1000Item_NoClawback()
        {
            var context = GetInMemoryDbContext("Customer_5pct");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A", PreferredDiscountPercent = 5m };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var product = new Product { ProductID = 1, ProductName = "P", SKU = "P", BaseSellingPrice = 9999m };
            var pu = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 9999m };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-C",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 1_000m,
                DiscountTotal = 50m,
                GrandTotal = 950m,
                DiscountMode = "Customer",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = null,
                        RuleName = "Customer Preferred Discount",
                        DiscountType = "Percentage",
                        DiscountValue = 5m,
                        DiscountAmount = 50m,
                        MinimumOrderAmount = null,
                        DiscountSource = "Customer",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var item = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = product,
                ProductUnitID = 1,
                ProductUnit = pu,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 1_000m,
                DiscountRate = 5m,
                DiscountAmount = 50m,
                ItemType = "NORMAL"
            };
            invoice.Items.Add(item);

            context.AddRange(user, customer, warehouse, unit, product, pu, invoice);
            await context.SaveChangesAsync();

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = 1, ProductID = 1, ProductUnitID = 1, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(1_000m, result.Data!.GrossReturnedValue);
            Assert.Equal(50m, result.Data.ItemDiscountReleased);
            Assert.Equal(0m, result.Data.DiscountClawback); // same as Manual — no threshold
            Assert.Equal(950m, result.Data.NetRefundAmount);
        }

        [Fact]
        public async Task PartialQuantity_ReleasesProRata_AndFinalUsesRemainder()
        {
            var context = GetInMemoryDbContext("Partial_Qty");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var product = new Product { ProductID = 1, ProductName = "P", SKU = "P", BaseSellingPrice = 1000m };
            var pu = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = 1000m };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-P",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 10_000m,
                DiscountTotal = 500m,
                GrandTotal = 9_500m,
                DiscountMode = "Automatic",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = 1,
                        RuleName = "5%",
                        DiscountType = "Percentage",
                        DiscountValue = 5m,
                        DiscountAmount = 500m,
                        MinimumOrderAmount = 1m, // stay eligible
                        DiscountSource = "Automatic",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var item = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = product,
                ProductUnitID = 1,
                ProductUnit = pu,
                Quantity = 10m,
                ConvertedQuantity = 10m,
                UnitPrice = 1_000m,
                DiscountRate = 5m,
                DiscountAmount = 500m,
                ItemType = "NORMAL"
            };
            invoice.Items.Add(item);

            context.DiscountRules.Add(new DiscountRule
            {
                DiscountRuleID = 1,
                CompanyID = 1,
                RuleName = "5%",
                MinimumOrderAmount = 1m,
                DiscountType = "Percentage",
                DiscountValue = 5m,
                IsActive = true,
                CreatedBy = 1
            });
            context.AddRange(user, customer, warehouse, unit, product, pu, invoice);
            await context.SaveChangesAsync();

            var first = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = 1, ProductID = 1, ProductUnitID = 1, Quantity = 3m }
                }
            });

            Assert.True(first.Success);
            Assert.Equal(3_000m, first.Data!.GrossReturnedValue);
            Assert.Equal(150m, first.Data.ItemDiscountReleased);
            Assert.Equal(2_850m, first.Data.NetRefundAmount);

            // Seed first return
            context.SalesReturns.Add(new SalesReturn
            {
                SalesReturnID = 1,
                ReturnNumber = "SR-1",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 3_000m,
                NetRefundAmount = 2_850m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1
            });
            context.SalesReturnItems.Add(new SalesReturnItem
            {
                SalesReturnID = 1,
                InvoiceItemID = 1,
                ProductID = 1,
                ProductUnitID = 1,
                Quantity = 3m,
                ConvertedQuantity = 3m,
                RefundUnitPrice = 1_000m,
                RefundAmount = 3_000m,
                DiscountAmount = 150m,
                ReturnCondition = "Sellable"
            });
            await context.SaveChangesAsync();

            var final = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = 1, ProductID = 1, ProductUnitID = 1, Quantity = 7m }
                }
            });

            Assert.True(final.Success);
            Assert.Equal(350m, final.Data!.ItemDiscountReleased); // exact remainder
            Assert.Equal(6_650m, final.Data.NetRefundAmount); // 7000 - 350
        }

        [Fact]
        public async Task ExceedingRemainingQuantity_Fails()
        {
            var (service, _, invoice, itemA, _, _) = await SetupCanonical22000InvoiceAsync("Qty_Cap");

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemA.InvoiceItemID, ProductID = 1, ProductUnitID = 1, Quantity = 2m }
                }
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ConversionToBaseUnit_DoesNotAffectMonetaryReturn()
        {
            var context = GetInMemoryDbContext("Conversion_Money");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1 };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "Carton" };
            var product = new Product { ProductID = 1, ProductName = "P", SKU = "P", BaseSellingPrice = 1m };
            var pu = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 24m, SellingPrice = 17.50m };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-C",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 17.50m,
                DiscountTotal = 0m,
                GrandTotal = 17.50m,
                DiscountMode = "None",
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
                ProductUnit = pu,
                Quantity = 1m,
                ConvertedQuantity = 24m,
                UnitPrice = 17.50m,
                DiscountAmount = 0m,
                ItemType = "NORMAL"
            };
            invoice.Items.Add(item);

            context.AddRange(user, customer, warehouse, unit, product, pu, invoice);
            await context.SaveChangesAsync();

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = 1, ProductID = 1, ProductUnitID = 1, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(17.50m, result.Data!.GrossReturnedValue); // NOT 17.50*24 or 17.50/24
            Assert.Equal(17.50m, result.Data.NetRefundAmount);
        }

        [Fact]
        public async Task Return3_AfterThresholdLost_RefundsFullUnitPrice()
        {
            var (service, context, itemRest) = await SetupCanonical22000WithTwoPriorReturnsAsync("Remaining_Return3");

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemRest.InvoiceItemID, ProductID = 4, ProductUnitID = 4, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(3_000m, result.Data!.GrossReturnedValue);
            Assert.Equal(0m, result.Data.DiscountClawback);
            Assert.Equal(3_000m, result.Data.NetRefundAmount);
            Assert.Equal(10_000m, result.Data.NextRemainingSubtotal);
            Assert.Equal(0m, result.Data.NextRemainingDiscount);
            Assert.Equal(10_000m, result.Data.NextRemainingNet);
        }

        [Fact]
        public async Task ManualFixedAmount_ReturnUsesStoredLineAllocation_NoThresholdClawback()
        {
            var context = GetInMemoryDbContext("Manual_Fixed100");
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1, FullName = "User" };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };

            decimal[] grosses = { 10_000m, 8_000m, 4_000m };
            var allocation = DiscountAllocationHelper.Allocate(
                grosses.Select((g, i) => new DiscountAllocationHelper.LineGross { Index = i, Gross = g }).ToList(),
                100m,
                "FixedAmount",
                100m);
            decimal lineCDiscount = allocation[2].DiscountAmount;

            var products = new[]
            {
                new Product { ProductID = 1, ProductName = "A", SKU = "A", BaseSellingPrice = 10_000m },
                new Product { ProductID = 2, ProductName = "B", SKU = "B", BaseSellingPrice = 8_000m },
                new Product { ProductID = 3, ProductName = "C", SKU = "C", BaseSellingPrice = 4_000m }
            };
            var units = products.Select(p => new ProductUnit
            {
                ProductUnitID = p.ProductID,
                ProductID = p.ProductID,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = p.BaseSellingPrice
            }).ToArray();

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-MF",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 22_000m,
                DiscountTotal = 100m,
                GrandTotal = 21_900m,
                DiscountMode = "Manual",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = null,
                        RuleName = "Manual Discount",
                        DiscountType = "FixedAmount",
                        DiscountValue = 100m,
                        DiscountAmount = 100m,
                        MinimumOrderAmount = null,
                        DiscountSource = "Manual",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            SalesInvoiceItem Mk(int id, decimal price, decimal disc) => new SalesInvoiceItem
            {
                InvoiceItemID = id,
                InvoiceID = 1,
                ProductID = id,
                Product = products[id - 1],
                ProductUnitID = id,
                ProductUnit = units[id - 1],
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = price,
                DiscountRate = 0m,
                DiscountAmount = disc,
                ItemType = "NORMAL"
            };

            var itemC = Mk(3, 4_000m, lineCDiscount);
            invoice.Items.Add(Mk(1, 10_000m, allocation[0].DiscountAmount));
            invoice.Items.Add(Mk(2, 8_000m, allocation[1].DiscountAmount));
            invoice.Items.Add(itemC);

            context.AddRange(user, customer, warehouse, unit);
            context.AddRange(products);
            context.AddRange(units);
            context.Add(invoice);
            await context.SaveChangesAsync();

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemC.InvoiceItemID, ProductID = 3, ProductUnitID = 3, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(4_000m, result.Data!.GrossReturnedValue);
            Assert.Equal(lineCDiscount, result.Data.ItemDiscountReleased);
            Assert.Equal(0m, result.Data.DiscountClawback);
            Assert.Equal(4_000m - lineCDiscount, result.Data.NetRefundAmount);
            Assert.Equal(18_000m, result.Data.NextRemainingSubtotal);
            Assert.Equal(100m - lineCDiscount, result.Data.NextRemainingDiscount);
        }

        [Fact]
        public async Task SmallReturn_CrossingThreshold_ProducesAdditionalAmountDue()
        {
            var (service, _, _, itemSmall) = await SetupJustAboveThresholdInvoiceAsync("Negative_Preview");

            var result = await service.PreviewSalesClawbackAsync(new PreviewReturnRequest
            {
                SalesInvoiceID = 1,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = itemSmall.InvoiceItemID, ProductID = 2, ProductUnitID = 2, Quantity = 1m }
                }
            });

            Assert.True(result.Success);
            Assert.Equal(200m, result.Data!.GrossReturnedValue);
            Assert.Equal(-555m, result.Data.NetRefundAmount);
            Assert.True(result.Data.IsAdditionalAmountDue);
            Assert.Equal(555m, result.Data.AdditionalAmountDue);
            Assert.Equal(14_900m, result.Data.NextRemainingSubtotal);
            Assert.Equal(0m, result.Data.NextRemainingDiscount);
            Assert.Equal(14_900m, result.Data.NextRemainingNet);
        }

        [Fact]
        public async Task NegativeRemainingState_Process_PostsLedgerDebit_AndLeavesInvoiceTotalsUnchanged()
        {
            var (service, context, invoice, itemSmall) = await SetupJustAboveThresholdInvoiceAsync("Negative_Process", seedStock: true);

            decimal originalSubTotal = invoice.SubTotal;
            decimal originalDiscountTotal = invoice.DiscountTotal;
            decimal originalGrandTotal = invoice.GrandTotal;

            var result = await service.ProcessSalesReturnAsync(new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput
                    {
                        InvoiceItemID = itemSmall.InvoiceItemID,
                        ProductID = 2,
                        ProductUnitID = 2,
                        Quantity = 1m,
                        ReturnCondition = "Sellable"
                    }
                }
            }, userId: 1);

            Assert.True(result.Success, result.Message);

            var ledger = await context.CustomerLedgers.AsNoTracking()
                .SingleAsync(l => l.SalesReturnID == result.Data);
            Assert.Equal("ADJUSTMENT", ledger.TransactionType);
            Assert.Equal(555m, ledger.DebitAmount);
            Assert.Equal(0m, ledger.CreditAmount);

            var persisted = await context.SalesInvoices.AsNoTracking()
                .FirstAsync(i => i.InvoiceID == invoice.InvoiceID);
            Assert.Equal(originalSubTotal, persisted.SubTotal);
            Assert.Equal(originalDiscountTotal, persisted.DiscountTotal);
            Assert.Equal(originalGrandTotal, persisted.GrandTotal);
            Assert.True(persisted.IsLocked);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoiceItem ItemRest)>
            SetupCanonical22000WithTwoPriorReturnsAsync(string dbName)
        {
            var context = GetInMemoryDbContext(dbName);
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);
            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1, FullName = "User" };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var products = new[]
            {
                new Product { ProductID = 1, ProductName = "A", SKU = "A", BaseSellingPrice = 10_000m },
                new Product { ProductID = 2, ProductName = "Mid", SKU = "M", BaseSellingPrice = 5_000m },
                new Product { ProductID = 3, ProductName = "C", SKU = "C", BaseSellingPrice = 4_000m },
                new Product { ProductID = 4, ProductName = "Rest", SKU = "R", BaseSellingPrice = 3_000m }
            };
            var units = products.Select(p => new ProductUnit
            {
                ProductUnitID = p.ProductID,
                ProductID = p.ProductID,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = p.BaseSellingPrice
            }).ToArray();

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-R3",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 22_000m,
                DiscountTotal = 1_100m,
                GrandTotal = 20_900m,
                DiscountMode = "Automatic",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = 1,
                        RuleName = "5%",
                        DiscountType = "Percentage",
                        DiscountValue = 5m,
                        DiscountAmount = 1_100m,
                        MinimumOrderAmount = 15_000m,
                        DiscountSource = "Automatic",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            SalesInvoiceItem Mk(int id, decimal price, decimal disc) => new SalesInvoiceItem
            {
                InvoiceItemID = id,
                InvoiceID = 1,
                ProductID = id,
                Product = products[id - 1],
                ProductUnitID = id,
                ProductUnit = units[id - 1],
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = price,
                DiscountRate = 5m,
                DiscountAmount = disc,
                ItemType = "NORMAL"
            };

            var itemC = Mk(3, 4_000m, 200m);
            var itemMid = Mk(2, 5_000m, 250m);
            var itemRest = Mk(4, 3_000m, 150m);
            invoice.Items.Add(Mk(1, 10_000m, 500m));
            invoice.Items.Add(itemMid);
            invoice.Items.Add(itemC);
            invoice.Items.Add(itemRest);

            context.DiscountRules.Add(new DiscountRule
            {
                DiscountRuleID = 1,
                CompanyID = 1,
                RuleName = "5%",
                MinimumOrderAmount = 50_000m,
                DiscountType = "Percentage",
                DiscountValue = 5m,
                IsActive = true,
                CreatedBy = 1
            });
            context.AddRange(user, customer, warehouse, unit);
            context.AddRange(products);
            context.AddRange(units);
            context.Add(invoice);
            await context.SaveChangesAsync();

            context.SalesReturns.Add(new SalesReturn
            {
                SalesReturnID = 1,
                ReturnNumber = "SR-1",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 4_000m,
                NetRefundAmount = 3_800m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1
            });
            context.SalesReturnItems.Add(new SalesReturnItem
            {
                SalesReturnID = 1,
                InvoiceItemID = itemC.InvoiceItemID,
                ProductID = 3,
                ProductUnitID = 3,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                RefundUnitPrice = 4_000m,
                RefundAmount = 4_000m,
                DiscountAmount = 200m,
                ReturnCondition = "Sellable"
            });
            context.SalesReturns.Add(new SalesReturn
            {
                SalesReturnID = 2,
                ReturnNumber = "SR-2",
                ReturnType = "INVOICE",
                InvoiceID = 1,
                CustomerID = 1,
                WarehouseID = 1,
                SettlementMethod = "ACCOUNT_ADJUSTMENT",
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 5_000m,
                NetRefundAmount = 4_100m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1
            });
            context.SalesReturnItems.Add(new SalesReturnItem
            {
                SalesReturnID = 2,
                InvoiceItemID = itemMid.InvoiceItemID,
                ProductID = 2,
                ProductUnitID = 2,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                RefundUnitPrice = 5_000m,
                RefundAmount = 5_000m,
                DiscountAmount = 250m,
                ReturnCondition = "Sellable"
            });
            await context.SaveChangesAsync();

            return (service, context, itemRest);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoice Invoice, SalesInvoiceItem ItemSmall)>
            SetupJustAboveThresholdInvoiceAsync(string dbName, bool seedStock = false)
        {
            var context = GetInMemoryDbContext(dbName);
            var service = new ReturnService(new ReturnRepository(context), context, NullLogger<ReturnService>.Instance);

            var user = new User { UserID = 1, Username = "u", PasswordHash = "h", RoleID = 1, FullName = "User" };
            var customer = new Customer { CustomerID = 1, ShopName = "S", Address = "A" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "W" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };
            var company = new Company { CompanyID = 1, CompanyName = "Co" };
            var category = new Category { CategoryID = 1, CompanyID = 1, Name = "Cat" };

            var productLarge = new Product
            {
                ProductID = 1,
                ProductName = "Large",
                SKU = "L",
                BaseSellingPrice = 14_900m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };
            var productSmall = new Product
            {
                ProductID = 2,
                ProductName = "Small",
                SKU = "S",
                BaseSellingPrice = 200m,
                CategoryID = 1,
                CompanyID = 1,
                BaseUnitID = 1,
                IsActive = true
            };
            var puLarge = new ProductUnit
            {
                ProductUnitID = 1,
                ProductID = 1,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 14_900m,
                IsActive = true
            };
            var puSmall = new ProductUnit
            {
                ProductUnitID = 2,
                ProductID = 2,
                UnitID = 1,
                ConversionToBaseUnit = 1,
                SellingPrice = 200m,
                IsActive = true
            };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-15100",
                CustomerID = 1,
                CompanyID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 15_100m,
                DiscountTotal = 755m,
                GrandTotal = 14_345m,
                DiscountMode = "Automatic",
                PaymentStatus = "UNPAID",
                CreatedBy = 1,
                InvoiceDiscounts = new List<InvoiceDiscount>
                {
                    new InvoiceDiscount
                    {
                        InvoiceDiscountID = 1,
                        InvoiceID = 1,
                        DiscountRuleID = 1,
                        RuleName = "5% Off 15K+",
                        DiscountType = "Percentage",
                        DiscountValue = 5m,
                        DiscountAmount = 755m,
                        MinimumOrderAmount = 15_000m,
                        DiscountSource = "Automatic",
                        AppliedBy = 1
                    }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var itemLarge = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = productLarge,
                ProductUnitID = 1,
                ProductUnit = puLarge,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 14_900m,
                DiscountRate = 5m,
                DiscountAmount = 745m,
                ItemType = "NORMAL"
            };
            var itemSmall = new SalesInvoiceItem
            {
                InvoiceItemID = 2,
                InvoiceID = 1,
                ProductID = 2,
                Product = productSmall,
                ProductUnitID = 2,
                ProductUnit = puSmall,
                Quantity = 1m,
                ConvertedQuantity = 1m,
                UnitPrice = 200m,
                DiscountRate = 5m,
                DiscountAmount = 10m,
                ItemType = "NORMAL"
            };
            invoice.Items.Add(itemLarge);
            invoice.Items.Add(itemSmall);

            context.DiscountRules.Add(new DiscountRule
            {
                DiscountRuleID = 1,
                CompanyID = 1,
                RuleName = "5% Off 15K+",
                MinimumOrderAmount = 50_000m,
                DiscountType = "Percentage",
                DiscountValue = 5m,
                IsActive = true,
                CreatedBy = 1
            });
            context.AddRange(user, customer, warehouse, unit, company, category, productLarge, productSmall, puLarge, puSmall, invoice);

            if (seedStock)
            {
                context.InventoryStocks.AddRange(
                    new InventoryStock { ProductID = 1, WarehouseID = 1, Quantity = 100m, DamagedQuantity = 0m, IsActive = true },
                    new InventoryStock { ProductID = 2, WarehouseID = 1, Quantity = 100m, DamagedQuantity = 0m, IsActive = true });
            }

            await context.SaveChangesAsync();
            return (service, context, invoice, itemSmall);
        }
    }
}
