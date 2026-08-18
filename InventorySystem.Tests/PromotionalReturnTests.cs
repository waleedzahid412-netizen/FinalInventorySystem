using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using InventorySystem.Data;
using InventorySystem.DTOs.Returns;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Implementations;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class PromotionalReturnTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<(ReturnService Service, ApplicationDbContext Context, SalesInvoice Invoice, SalesInvoiceItem PaidItem, SalesInvoiceItem FreeItem)> SetupInvoiceWithPromotionAsync(
            string dbName,
            decimal paidQty = 2m,
            decimal paidPrice = 10m,
            decimal freeQty = 2m,
            decimal freeUnitPrice = 1.20m)
        {
            var context = GetInMemoryDbContext(dbName);
            var repository = new ReturnRepository(context);
            var service = new ReturnService(repository, context, NullLogger<ReturnService>.Instance);

            var customer = new Customer { CustomerID = 1, ShopName = "Test Shop", Address = "123 St" };
            var warehouse = new Warehouse { WarehouseID = 1, Name = "Main Warehouse" };
            var unit = new Unit { UnitID = 1, UnitName = "PCS" };

            var paidProduct = new Product { ProductID = 1, ProductName = "Paid Product", SKU = "P-PAID", BaseSellingPrice = paidPrice };
            var freeProduct = new Product { ProductID = 2, ProductName = "Free Product", SKU = "P-FREE", BaseSellingPrice = freeUnitPrice };

            var paidProductUnit = new ProductUnit { ProductUnitID = 1, ProductID = 1, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = paidPrice };
            var freeProductUnit = new ProductUnit { ProductUnitID = 2, ProductID = 2, UnitID = 1, ConversionToBaseUnit = 1, SellingPrice = freeUnitPrice };

            var campaign = new PromotionCampaign
            {
                PromotionID = 1,
                Name = "Buy 1 Get 1 Free",
                PromotionRules = new List<PromotionRule>
                {
                    new PromotionRule
                    {
                        RuleID = 1,
                        PromotionID = 1,
                        BuyProductID = 1,
                        BuyQuantity = 1,
                        FreeProductID = 2,
                        FreeQuantity = 1
                    }
                }
            };

            var invoice = new SalesInvoice
            {
                InvoiceID = 1,
                InvoiceNumber = "INV-00001",
                CustomerID = 1,
                Customer = customer,
                WarehouseID = 1,
                Warehouse = warehouse,
                InvoiceDate = DateTime.UtcNow,
                SubTotal = paidQty * paidPrice,
                GrandTotal = paidQty * paidPrice,
                InvoicePromotions = new List<InvoicePromotion>
                {
                    new InvoicePromotion { InvoicePromotionID = 1, InvoiceID = 1, PromotionID = 1 }
                },
                Items = new List<SalesInvoiceItem>()
            };

            var paidItem = new SalesInvoiceItem
            {
                InvoiceItemID = 1,
                InvoiceID = 1,
                ProductID = 1,
                Product = paidProduct,
                ProductUnitID = 1,
                ProductUnit = paidProductUnit,
                Quantity = paidQty,
                ConvertedQuantity = paidQty,
                UnitPrice = paidPrice,
                ItemType = "NORMAL",
                PromotionID = 1
            };

            var freeItem = new SalesInvoiceItem
            {
                InvoiceItemID = 2,
                InvoiceID = 1,
                ProductID = 2,
                Product = freeProduct,
                ProductUnitID = 2,
                ProductUnit = freeProductUnit,
                Quantity = freeQty,
                ConvertedQuantity = freeQty,
                UnitPrice = freeUnitPrice,
                ItemType = "FREE",
                PromotionID = 1
            };

            invoice.Items.Add(paidItem);
            invoice.Items.Add(freeItem);

            context.Customers.Add(customer);
            context.Warehouses.Add(warehouse);
            context.Units.Add(unit);
            context.Products.AddRange(paidProduct, freeProduct);
            context.ProductUnits.AddRange(paidProductUnit, freeProductUnit);
            context.PromotionCampaigns.Add(campaign);
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return (service, context, invoice, paidItem, freeItem);
        }

        [Fact]
        public async Task Test1_FirstReturnCreatesOnePenalty()
        {
            // Initial state: Original FREE = 2, Unit price = $1.20
            // Return #1: Return 2 Paid items (all paid items), 1 FREE item
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test1_DB", 2m, 10m, 2m, 1.20m);

            var previewReq = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 2m },
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var previewRes = await service.PreviewSalesClawbackAsync(previewReq);
            Assert.True(previewRes.Success);
            var freeDto = previewRes.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);

            Assert.Equal(0m, freeDto.UnreturnedChargedFreeQuantity); // PreviouslyCharged = 0
            Assert.Equal(0m, freeDto.ReturnedPreviouslyChargedQuantity); // ReturnedPreviouslyCharged = 0
            Assert.Equal(0m, freeDto.RemainingPreviouslyChargedQuantity); // RemainingPreviouslyCharged = 0
            Assert.Equal(1m, freeDto.NewPenaltyChargedQuantity); // NewPenaltyQty = 1
            Assert.Equal(1.20m, freeDto.RetainedValue); // PromoPenalty = $1.20
            Assert.Equal(1.20m, previewRes.Data.PromoPenalty);

            // Process Return #1
            var processReq = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = previewReq.Items
            };

            var processRes = await service.ProcessSalesReturnAsync(processReq, 1);
            Assert.True(processRes.Success);

            // Verify persisted row
            var returnItem = await context.SalesReturnItems.FirstOrDefaultAsync(s => s.InvoiceItemID == freeItem.InvoiceItemID);
            Assert.NotNull(returnItem);
            Assert.Equal(1m, returnItem.PromoPenaltyQuantity);
            Assert.Equal(1.20m, returnItem.PromoPenaltyAmount);
        }

        [Fact]
        public async Task Test2_SecondReturnReturnsPreviouslyChargedItem()
        {
            // Initial state: Original FREE = 2, Unit price = $1.20
            // Return #1: 2 Paid returned, 0 FREE returned -> creates 2 penalty units (since both free items retained but entitlement = 0)
            // Or Return #1: 2 Paid returned, 1 FREE returned -> creates 1 penalty unit (1 retained free item)
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test2_DB", 2m, 10m, 2m, 1.20m);

            // Process Return #1: Return 2 Paid items, 1 FREE item
            var req1 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 2m },
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };
            var res1 = await service.ProcessSalesReturnAsync(req1, 1);
            Assert.True(res1.Success);

            // Current Return #2: Return remaining 1 FREE item
            var previewReq2 = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var previewRes2 = await service.PreviewSalesClawbackAsync(previewReq2);
            Assert.True(previewRes2.Success);
            var freeDto = previewRes2.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);

            Assert.Equal(1m, freeDto.UnreturnedChargedFreeQuantity); // PreviouslyCharged = 1
            Assert.Equal(1m, freeDto.ReturnedPreviouslyChargedQuantity); // ReturnedPreviouslyCharged = 1
            Assert.Equal(0m, freeDto.RemainingPreviouslyChargedQuantity); // RemainingPreviouslyCharged = 0
            Assert.Equal(0m, freeDto.NewPenaltyChargedQuantity); // NewPenaltyQty = 0
            Assert.Equal(0m, freeDto.RetainedValue); // PromoPenalty = $0
            Assert.Equal(0m, previewRes2.Data.PromoPenalty);
            Assert.Equal(1.20m, previewRes2.Data.GrossReturnedValue); // FreeRefund = $1.20

            // Process Return #2
            var req2 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = previewReq2.Items
            };

            var res2 = await service.ProcessSalesReturnAsync(req2, 1);
            Assert.True(res2.Success);

            var returnItem2 = await context.SalesReturnItems
                .Where(s => s.InvoiceItemID == freeItem.InvoiceItemID)
                .OrderByDescending(s => s.SalesReturnItemID)
                .FirstOrDefaultAsync();

            Assert.NotNull(returnItem2);
            Assert.Equal(0m, returnItem2.PromoPenaltyQuantity);
            Assert.Equal(0m, returnItem2.PromoPenaltyAmount);
            Assert.Equal(1.20m, returnItem2.RefundAmount);
        }

        [Fact]
        public async Task Test3_PreviouslyChargedItemMustNotBeChargedAgain()
        {
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test3_DB", 2m, 10m, 2m, 1.20m);

            // Return #1: 2 Paid returned, 1 FREE returned -> 1 penalty unit created on retained free item
            var req1 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 2m },
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };
            await service.ProcessSalesReturnAsync(req1, 1);

            // Return #2: Returning the remaining previously charged free item
            var req2 = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };
            var res2 = await service.PreviewSalesClawbackAsync(req2);
            Assert.True(res2.Success);

            var freeDto = res2.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);
            Assert.Equal(0m, freeDto.NewPenaltyChargedQuantity);
            Assert.Equal(0m, freeDto.RetainedValue);
        }

        [Fact]
        public async Task Test4_MultiplePreviouslyChargedUnits()
        {
            // Initial: PreviouslyCharged = 3, ReturnedNow = 2, Unit price = $1.20
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test4_DB", 4m, 10m, 4m, 1.20m);

            // Manually insert prior return with PromoPenaltyQuantity = 3
            var salesReturn1 = new SalesReturn
            {
                ReturnNumber = "SR-00001",
                ReturnType = "INVOICE",
                InvoiceID = invoice.InvoiceID,
                CustomerID = invoice.CustomerID,
                WarehouseID = invoice.WarehouseID,
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 0m,
                PromoPenalty = 3.60m,
                NetRefundAmount = 0m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            context.SalesReturns.Add(salesReturn1);
            await context.SaveChangesAsync();

            var returnItem1 = new SalesReturnItem
            {
                SalesReturnID = salesReturn1.SalesReturnID,
                InvoiceItemID = freeItem.InvoiceItemID,
                ProductID = freeItem.ProductID ?? 0,
                ProductUnitID = freeItem.ProductUnitID ?? 0,
                Quantity = 0m,
                ConvertedQuantity = 0m,
                RefundUnitPrice = 1.20m,
                RefundAmount = 0m,
                PromoPenaltyQuantity = 3m,
                PromoPenaltyAmount = 3.60m,
                Reason = "Prior penalty",
                ReturnCondition = "Sellable"
            };
            context.SalesReturnItems.Add(returnItem1);
            await context.SaveChangesAsync();

            // Action: Return 2 FREE items now
            var previewReq = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 2m }
                }
            };

            var previewRes = await service.PreviewSalesClawbackAsync(previewReq);
            Assert.True(previewRes.Success);

            var freeDto = previewRes.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);
            Assert.Equal(2m, freeDto.ReturnedPreviouslyChargedQuantity); // ReturnedPreviouslyCharged = 2
            Assert.Equal(1m, freeDto.RemainingPreviouslyChargedQuantity); // RemainingPreviouslyCharged = 1
            Assert.Equal(2.40m, previewRes.Data.GrossReturnedValue); // FreeRefund = $2.40
            Assert.Equal(0m, freeDto.NewPenaltyChargedQuantity); // NewPenaltyQty = 0
            Assert.Equal(0m, previewRes.Data.PromoPenalty); // PromoPenalty = $0
        }

        [Fact]
        public async Task Test5_ReturnFewerThanPreviouslyCharged()
        {
            // Initial: PreviouslyCharged = 3, ReturnedNow = 1
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test5_DB", 4m, 10m, 4m, 1.20m);

            var salesReturn1 = new SalesReturn
            {
                ReturnNumber = "SR-00001",
                ReturnType = "INVOICE",
                InvoiceID = invoice.InvoiceID,
                CustomerID = invoice.CustomerID,
                WarehouseID = invoice.WarehouseID,
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 0m,
                PromoPenalty = 3.60m,
                NetRefundAmount = 0m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            context.SalesReturns.Add(salesReturn1);
            await context.SaveChangesAsync();

            var returnItem1 = new SalesReturnItem
            {
                SalesReturnID = salesReturn1.SalesReturnID,
                InvoiceItemID = freeItem.InvoiceItemID,
                ProductID = freeItem.ProductID ?? 0,
                ProductUnitID = freeItem.ProductUnitID ?? 0,
                Quantity = 0m,
                ConvertedQuantity = 0m,
                RefundUnitPrice = 1.20m,
                RefundAmount = 0m,
                PromoPenaltyQuantity = 3m,
                PromoPenaltyAmount = 3.60m,
                Reason = "Prior penalty",
                ReturnCondition = "Sellable"
            };
            context.SalesReturnItems.Add(returnItem1);
            await context.SaveChangesAsync();

            var previewReq = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var previewRes = await service.PreviewSalesClawbackAsync(previewReq);
            Assert.True(previewRes.Success);

            var freeDto = previewRes.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);
            Assert.Equal(1m, freeDto.ReturnedPreviouslyChargedQuantity); // ReturnedPreviouslyCharged = 1
            Assert.Equal(2m, freeDto.RemainingPreviouslyChargedQuantity); // RemainingPreviouslyCharged = 2
            Assert.Equal(0m, freeDto.NewPenaltyChargedQuantity); // NewPenaltyQty = 0
        }

        [Fact]
        public async Task Test6_ReturnMoreThanPreviouslyCharged()
        {
            // Initial: PreviouslyCharged = 1, ReturnedNow = 3
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test6_DB", 4m, 10m, 4m, 1.20m);

            var salesReturn1 = new SalesReturn
            {
                ReturnNumber = "SR-00001",
                ReturnType = "INVOICE",
                InvoiceID = invoice.InvoiceID,
                CustomerID = invoice.CustomerID,
                WarehouseID = invoice.WarehouseID,
                ReturnDate = DateTime.UtcNow,
                GrossAmount = 0m,
                PromoPenalty = 1.20m,
                NetRefundAmount = 0m,
                IncludeSchemeCalculation = true,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            context.SalesReturns.Add(salesReturn1);
            await context.SaveChangesAsync();

            var returnItem1 = new SalesReturnItem
            {
                SalesReturnID = salesReturn1.SalesReturnID,
                InvoiceItemID = freeItem.InvoiceItemID,
                ProductID = freeItem.ProductID ?? 0,
                ProductUnitID = freeItem.ProductUnitID ?? 0,
                Quantity = 0m,
                ConvertedQuantity = 0m,
                RefundUnitPrice = 1.20m,
                RefundAmount = 0m,
                PromoPenaltyQuantity = 1m,
                PromoPenaltyAmount = 1.20m,
                Reason = "Prior penalty",
                ReturnCondition = "Sellable"
            };
            context.SalesReturnItems.Add(returnItem1);
            await context.SaveChangesAsync();

            var previewReq = new PreviewReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 2m },
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 3m }
                }
            };

            var previewRes = await service.PreviewSalesClawbackAsync(previewReq);
            Assert.True(previewRes.Success);

            var freeDto = previewRes.Data!.FreePromotions.First(f => f.FreeInvoiceItemId == freeItem.InvoiceItemID);
            Assert.Equal(1m, freeDto.ReturnedPreviouslyChargedQuantity); // ReturnedPreviouslyCharged = 1
            Assert.Equal(0m, freeDto.RemainingPreviouslyChargedQuantity); // RemainingPreviouslyCharged = 0
            Assert.Equal(21.20m, previewRes.Data.GrossReturnedValue); // 2 paid items @ $10 ($20) + 1 previously charged free item refund ($1.20) = $21.20
        }

        [Fact]
        public async Task Test7_ThreeConsecutiveReturns_ProvesNonCumulativeState()
        {
            // Setup: 3 Paid items @ $10, 3 Free items @ $1.20 (Buy 1 Get 1 Free)
            var (service, context, invoice, paidItem, freeItem) = await SetupInvoiceWithPromotionAsync("Test7_DB", 3m, 10m, 3m, 1.20m);

            // ===== RETURN #1 =====
            // Return 1 Paid item, 0 Free items. Entitlement drops from 3 to 2.
            // Customer retains 3 Free items. 1 is unearned.
            var req1 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var res1 = await service.ProcessSalesReturnAsync(req1, 1);
            Assert.True(res1.Success);

            // Verify DB after Return #1: 1 zero-quantity penalty row inserted for FreeItem
            var returnItem1 = await context.SalesReturnItems
                .FirstOrDefaultAsync(s => s.SalesReturn.ReturnNumber == "SR-00001" && s.InvoiceItemID == freeItem.InvoiceItemID);
            Assert.NotNull(returnItem1);
            Assert.Equal(0m, returnItem1.Quantity);
            Assert.Equal(1m, returnItem1.PromoPenaltyQuantity);
            Assert.Equal(1.20m, returnItem1.PromoPenaltyAmount);

            // ===== RETURN #2 =====
            // Return 1 Free item. Returns the 1 previously charged free item.
            var req2 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var res2 = await service.ProcessSalesReturnAsync(req2, 1);
            Assert.True(res2.Success);

            // Verify DB after Return #2: Free item returned receives $1.20 refund, 0 promo penalty quantity
            var returnItem2 = await context.SalesReturnItems
                .FirstOrDefaultAsync(s => s.SalesReturn.ReturnNumber == "SR-00002" && s.InvoiceItemID == freeItem.InvoiceItemID);
            Assert.NotNull(returnItem2);
            Assert.Equal(1m, returnItem2.Quantity);
            Assert.Equal(1.20m, returnItem2.RefundAmount);
            Assert.Equal(0m, returnItem2.PromoPenaltyQuantity);
            Assert.Equal(0m, returnItem2.PromoPenaltyAmount);

            // ===== RETURN #3 =====
            // Return 1 more Paid item (Total returned paid = 2, 1 paid remaining -> Entitlement = 1 Free item).
            // Customer physically holds 2 Free items (1 returned in R2).
            // Historically previously charged unreturned free items before R3 = 1 (R1) - 1 (R2) = 0.
            // 2 retained free items - 0 previously charged = 2 uncharged retained.
            // Retained unearned = MAX(0, 2 - 1 entitlement) = 1.
            // Return #3 must generate NewlyChargedFreeQty = 1.
            var req3 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = paidItem.InvoiceItemID, ProductID = paidItem.ProductID ?? 0, ProductUnitID = paidItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var res3 = await service.ProcessSalesReturnAsync(req3, 1);
            Assert.True(res3.Success);

            // Verify DB after Return #3: 1 penalty unit created
            var returnItem3 = await context.SalesReturnItems
                .FirstOrDefaultAsync(s => s.SalesReturn.ReturnNumber == "SR-00003" && s.InvoiceItemID == freeItem.InvoiceItemID);
            Assert.NotNull(returnItem3);
            Assert.Equal(0m, returnItem3.Quantity);
            Assert.Equal(1m, returnItem3.PromoPenaltyQuantity);
            Assert.Equal(1.20m, returnItem3.PromoPenaltyAmount);

            // ===== RETURN #4 =====
            // Return the free item penalized in R3.
            var req4 = new ProcessSalesReturnRequest
            {
                SalesInvoiceID = invoice.InvoiceID,
                ReturnDate = DateTime.UtcNow,
                IncludeSchemeCalculation = true,
                Items = new List<ReturnItemInput>
                {
                    new ReturnItemInput { InvoiceItemID = freeItem.InvoiceItemID, ProductID = freeItem.ProductID ?? 0, ProductUnitID = freeItem.ProductUnitID ?? 0, Quantity = 1m }
                }
            };

            var res4 = await service.ProcessSalesReturnAsync(req4, 1);
            Assert.True(res4.Success);

            var returnItem4 = await context.SalesReturnItems
                .FirstOrDefaultAsync(s => s.SalesReturn.ReturnNumber == "SR-00004" && s.InvoiceItemID == freeItem.InvoiceItemID);
            Assert.NotNull(returnItem4);
            Assert.Equal(1m, returnItem4.Quantity);
            Assert.Equal(1.20m, returnItem4.RefundAmount);
            Assert.Equal(0m, returnItem4.PromoPenaltyQuantity);
            Assert.Equal(0m, returnItem4.PromoPenaltyAmount);
        }
    }
}
