using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Configuration;
using InventorySystem.DTOs.Analytics;
using InventorySystem.DTOs.LoadSheets;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Sales;
using InventorySystem.Services.Interfaces;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventorySystem.Services.Implementations
{
    public class PdfService : IPdfService
    {
        private readonly InvoicePrintSettings _printSettings;

        public PdfService(IOptions<InvoicePrintSettings> printSettings)
        {
            _printSettings = printSettings?.Value ?? new InvoicePrintSettings();
        }

        public byte[] GenerateSalesInvoicePdf(SalesDetailsDto invoice)
        {
            if (invoice == null || invoice.Header == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }

            var header = invoice.Header;
            var items = invoice.Items ?? new List<SalesItemDto>();
            var financial = invoice.Financial ?? new SalesFinancialSummaryDto();
            var lineRows = BuildSalesPrintRows(items);

            var billTo = !string.IsNullOrWhiteSpace(header.ShopName) ? header.ShopName! : header.CustomerName;
            var address = !string.IsNullOrWhiteSpace(header.CustomerAddress)
                ? header.CustomerAddress!
                : string.Join(" / ", new[] { header.AreaName, header.SubAreaName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var businessName = string.IsNullOrWhiteSpace(_printSettings.BusinessName)
                ? "Wholesale Distributor"
                : _printSettings.BusinessName;
            var phone = string.IsNullOrWhiteSpace(_printSettings.Phone) ? "" : _printSettings.Phone;
            var terms = _printSettings.TermsUrdu ?? new List<string>();
            var termsTitle = string.IsNullOrWhiteSpace(_printSettings.TermsTitleUrdu)
                ? "اہم ہدایات برائے صارفین"
                : _printSettings.TermsTitleUrdu;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(18);
                    page.MarginVertical(16);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9).FontColor(Colors.Black));

                    page.Content().Column(pageBody =>
                    {
                        // Column.Item sizes to content — avoids stretching the bordered boxes to full A4 height
                        pageBody.Item().Row(root =>
                        {
                            // Left: Urdu terms + signature
                            root.ConstantItem(155).Border(1).BorderColor(Colors.Black).Padding(6).Column(left =>
                            {
                                left.Item().AlignRight().Text(termsTitle).FontSize(10).Bold();
                                left.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                                int termIndex = 1;
                                foreach (var term in terms)
                                {
                                    left.Item().PaddingBottom(5).AlignRight().Text($"{termIndex}. {term}")
                                        .FontSize(7.5f)
                                        .FontFamily("Segoe UI");
                                    termIndex++;
                                }

                                left.Item().AlignCenter().PaddingTop(12).Column(sig =>
                                {
                                    sig.Item().Height(20);
                                    sig.Item().AlignCenter().Text("______________________").FontSize(8);
                                    sig.Item().AlignCenter().Text("Customer Signature").FontSize(8).Bold();
                                });
                            });

                            root.ConstantItem(8);

                            // Right: invoice body
                            root.RelativeItem().Column(right =>
                            {
                                right.Item().Row(top =>
                                {
                                    top.RelativeItem().Column(titleCol =>
                                    {
                                        titleCol.Item().Text(businessName).FontSize(14).Bold();
                                        if (!string.IsNullOrWhiteSpace(phone))
                                        {
                                            titleCol.Item().Text($"Phone: {phone}").FontSize(9);
                                        }
                                        titleCol.Item().PaddingTop(4).Text("SALES INVOICE").FontSize(16).Bold();
                                    });

                                    top.ConstantItem(170).Border(1).BorderColor(Colors.Black).Padding(6).Column(meta =>
                                    {
                                        meta.Item().Row(r =>
                                        {
                                            r.ConstantItem(72).Text("Invoice No.:").FontSize(8).Bold();
                                            r.RelativeItem().Text(header.InvoiceNumber).FontSize(9).Bold();
                                        });
                                        meta.Item().PaddingTop(2).Row(r =>
                                        {
                                            r.ConstantItem(72).Text("Date:").FontSize(8).Bold();
                                            r.RelativeItem().Text(header.InvoiceDate.ToString("ddd, dd/MM/yyyy")).FontSize(9);
                                        });
                                        meta.Item().PaddingTop(2).Row(r =>
                                        {
                                            r.ConstantItem(72).Text("Payment Mode:").FontSize(8).Bold();
                                            r.RelativeItem().Text(header.PaymentMode).FontSize(9);
                                        });
                                    });
                                });

                                right.Item().PaddingTop(8).Border(1).BorderColor(Colors.Black).Padding(6).Column(bill =>
                                {
                                    bill.Item().Row(r =>
                                    {
                                        r.ConstantItem(70).Text("Bill To:").FontSize(8).Bold();
                                        r.RelativeItem().Text(billTo).FontSize(10).Bold();
                                    });
                                    bill.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(70).Text("Address:").FontSize(8).Bold();
                                        r.RelativeItem().Text(string.IsNullOrWhiteSpace(address) ? "-" : address).FontSize(9);
                                    });
                                    bill.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(70).Text("Booker:").FontSize(8).Bold();
                                        r.RelativeItem().Text(string.IsNullOrWhiteSpace(header.BrokerName) ? "-" : header.BrokerName!).FontSize(9);
                                    });
                                    bill.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(70).Text("Supplier:").FontSize(8).Bold();
                                        r.RelativeItem().Text(string.IsNullOrWhiteSpace(header.CompanyName) ? "-" : header.CompanyName!).FontSize(9);
                                    });
                                });

                                right.Item().PaddingTop(8).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(55);
                                        columns.RelativeColumn(3.2f);
                                        columns.ConstantColumn(40);
                                        columns.ConstantColumn(48);
                                        columns.ConstantColumn(60);
                                        columns.ConstantColumn(65);
                                    });

                                    table.Header(h =>
                                    {
                                        void Head(string text, bool rightAlign = false)
                                        {
                                            var cell = h.Cell().Border(0.75f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).Padding(3);
                                            if (rightAlign)
                                            {
                                                cell.AlignRight().Text(text).FontSize(8).Bold();
                                            }
                                            else
                                            {
                                                cell.Text(text).FontSize(8).Bold();
                                            }
                                        }

                                        Head("Product Code");
                                        Head("ProductName");
                                        Head("Qty", true);
                                        Head("Free Qty", true);
                                        Head("Unit Price", true);
                                        Head("Amount", true);
                                    });

                                    foreach (var row in lineRows)
                                    {
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .Text(row.ProductCode).FontSize(8);
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .Text(row.ProductName).FontSize(8);
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .AlignRight().Text(FormatQty(row.Qty)).FontSize(8);
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .AlignRight().Text(FormatQty(row.FreeQty)).FontSize(8);
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .AlignRight().Text(row.UnitPrice.ToString("N2")).FontSize(8);
                                        table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3)
                                            .AlignRight().Text(row.Amount.ToString("N2")).FontSize(8).Bold();
                                    }
                                });

                                right.Item().PaddingTop(8).AlignRight().Width(200).Border(1).BorderColor(Colors.Black).Padding(6).Column(tot =>
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text("Total Amount:").FontSize(9).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.SubTotal.ToString("N2")).FontSize(9);
                                    });
                                    tot.Item().PaddingTop(3).Row(r =>
                                    {
                                        r.RelativeItem().Text("Discount:").FontSize(9).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.DiscountTotal.ToString("N2")).FontSize(9);
                                    });
                                    tot.Item().PaddingTop(3).BorderTop(1).BorderColor(Colors.Black).PaddingTop(3).Row(r =>
                                    {
                                        r.RelativeItem().Text("Net Amount:").FontSize(10).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.GrandTotal.ToString("N2")).FontSize(10).Bold();
                                    });
                                });
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static List<SalesPrintRow> BuildSalesPrintRows(List<SalesItemDto> items)
        {
            var freeByKey = items
                .Where(i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => PrintLineKey(i))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var rows = new List<SalesPrintRow>();
            var consumedFreeKeys = new HashSet<string>();

            foreach (var item in items.Where(i => !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase)))
            {
                var key = PrintLineKey(item);
                freeByKey.TryGetValue(key, out var freeQty);
                if (freeQty > 0)
                {
                    consumedFreeKeys.Add(key);
                }

                rows.Add(new SalesPrintRow
                {
                    ProductCode = FormatProductCode(item),
                    ProductName = item.ProductName,
                    Qty = item.Quantity,
                    FreeQty = freeQty,
                    UnitPrice = item.UnitPrice,
                    Amount = item.LineTotal
                });
            }

            foreach (var free in items.Where(i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase)))
            {
                var key = PrintLineKey(free);
                if (consumedFreeKeys.Contains(key))
                {
                    continue;
                }

                rows.Add(new SalesPrintRow
                {
                    ProductCode = FormatProductCode(free),
                    ProductName = free.ProductName,
                    Qty = 0,
                    FreeQty = free.Quantity,
                    UnitPrice = 0,
                    Amount = 0
                });
                consumedFreeKeys.Add(key);
            }

            return rows;
        }

        private static string PrintLineKey(SalesItemDto item)
        {
            if (item.ProductID.HasValue && item.ProductID.Value > 0)
            {
                return $"P:{item.ProductID.Value}";
            }

            return $"C:{item.CustomItemName ?? item.ProductName}";
        }

        private static string FormatProductCode(SalesItemDto item)
        {
            if (item.ProductID.HasValue && item.ProductID.Value > 0)
            {
                return item.ProductID.Value.ToString("D6");
            }

            return string.IsNullOrWhiteSpace(item.SKU) ? "-" : item.SKU!;
        }

        private static string FormatQty(decimal qty)
        {
            return qty == Math.Truncate(qty) ? qty.ToString("0") : qty.ToString("0.##");
        }

        private sealed class SalesPrintRow
        {
            public string ProductCode { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public decimal Qty { get; set; }
            public decimal FreeQty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }
        }

        public byte[] GeneratePurchaseInvoicePdf(PurchaseDetailsDto invoice)
        {
            if (invoice == null || invoice.Header == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }

            var header = invoice.Header;
            var items = invoice.Items ?? new System.Collections.Generic.List<PurchaseItemDto>();
            var financial = invoice.Financial ?? new PurchaseFinancialSummaryDto();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    // Header Block
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("WHOLESALE DISTRIBUTOR").FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                                c.Item().Text("Purchase Order / Vendor Invoice").FontSize(12).SemiBold().FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(200).Column(c =>
                            {
                                c.Item().Text($"Invoice #: {header.InvoiceNumber}").FontSize(14).Bold().AlignRight();
                                c.Item().Text($"Date: {header.InvoiceDate:dd MMM yyyy}").FontSize(10).AlignRight();
                                c.Item().Text($"Status: {header.PaymentStatus}").FontSize(11).Bold().AlignRight().FontColor(
                                    header.PaymentStatus == "PAID" ? Colors.Green.Medium :
                                    header.PaymentStatus == "PARTIAL" ? Colors.Orange.Medium : Colors.Red.Medium);
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    // Content Block
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Vendor Details Card
                        col.Item().Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("COMPANY / SUPPLIER").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text(header.CompanyName).FontSize(11).Bold();
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DESTINATION WAREHOUSE").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"Warehouse: {header.WarehouseName}").FontSize(10);
                                c.Item().Text($"Created By: {header.CreatedByName}").FontSize(9);
                            });
                        });

                        col.Item().Height(15);

                        // Purchased Line Items Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(headerBlock =>
                            {
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).Text("#").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).Text("Product Name").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).Text("SKU").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).Text("Unit").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).AlignRight().Text("Qty").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).AlignRight().Text("Unit Cost").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Green.Darken2).Padding(5).AlignRight().Text("Total Cost").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            int index = 1;
                            foreach (var item in items)
                            {
                                var bgColor = (index % 2 == 0) ? Colors.Grey.Lighten5 : Colors.White;

                                table.Cell().Background(bgColor).Padding(5).Text(index.ToString()).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).Text(item.ProductName).Bold().FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).Text(item.SKU ?? "-").FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).Text(item.UnitName).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(item.Quantity.ToString("N2")).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"PKR {item.UnitCost:N2}").FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"PKR {item.TotalCost:N2}").Bold().FontSize(9);

                                index++;
                            }
                        });

                        col.Item().Height(15);

                        // Totals Summary Box
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(2);

                            row.RelativeItem(3).Column(c =>
                            {
                                c.Item().BorderBottom(2).BorderColor(Colors.Green.Darken2).Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Grand Total:").Bold().FontSize(11).FontColor(Colors.Green.Darken2);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.GrandTotal:N2}").Bold().FontSize(11).FontColor(Colors.Green.Darken2);
                                });

                                c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Paid Amount:").FontSize(9).FontColor(Colors.Green.Medium);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.PaidAmount:N2}").FontSize(9).FontColor(Colors.Green.Medium);
                                });

                                c.Item().Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Outstanding Payable:").Bold().FontSize(10).FontColor(Colors.Red.Medium);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.Outstanding:N2}").Bold().FontSize(10).FontColor(Colors.Red.Medium);
                                });
                            });
                        });
                    });

                    // Footer Block
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text($"Generated on {DateTime.UtcNow:dd MMM yyyy HH:mm UTC}").FontSize(8).FontColor(Colors.Grey.Medium);
                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateSalesReturnPdf(InventorySystem.DTOs.Returns.SalesReturnDetailsDto returnDto)
        {
            if (returnDto == null || returnDto.Header == null)
            {
                throw new ArgumentNullException(nameof(returnDto));
            }

            var header = returnDto.Header;
            var items = returnDto.Items ?? new System.Collections.Generic.List<InventorySystem.DTOs.Returns.SalesReturnItemDto>();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    // Header Block
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("WHOLESALE INVENTORY MANAGEMENT").FontSize(16).Bold().FontColor(Colors.Red.Medium);
                                c.Item().Text("SALES RETURN RECEIPT").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Return #: {header.ReturnNumber}").FontSize(14).Bold().FontColor(Colors.Red.Medium);
                                c.Item().Text($"Invoice #: {header.InvoiceNumber}").FontSize(10);
                                c.Item().Text($"Date: {header.ReturnDate:dd MMM yyyy}").FontSize(10);
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CUSTOMER DETAILS").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                                c.Item().Text(header.CustomerName).FontSize(11).Bold();
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("PROCESSED BY").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                                c.Item().Text(header.CreatedByUserName).FontSize(10);
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    // Content Block: Items Table
                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("#");
                                h.Cell().Element(HeaderStyle).Text("Product");
                                h.Cell().Element(HeaderStyle).Text("Return Qty");
                                h.Cell().Element(HeaderStyle).Text("Condition");
                                h.Cell().Element(HeaderStyle).AlignRight().Text("Unit Price");
                                h.Cell().Element(HeaderStyle).AlignRight().Text("Refund Total");

                                static IContainer HeaderStyle(IContainer container) =>
                                    container.Background(Colors.Grey.Lighten3).Padding(5).DefaultTextStyle(x => x.Bold().FontSize(9));
                            });

                            int index = 1;
                            foreach (var item in items)
                            {
                                table.Cell().Element(CellStyle).Text(index++.ToString());
                                table.Cell().Element(CellStyle).Text(item.ProductName);
                                table.Cell().Element(CellStyle).Text($"{item.Quantity:N2} {item.UnitName}");
                                table.Cell().Element(CellStyle).Text(item.ReturnCondition);
                                table.Cell().Element(CellStyle).AlignRight().Text($"{item.RefundUnitPrice:C}");
                                table.Cell().Element(CellStyle).AlignRight().Text($"{item.RefundAmount:C}");

                                static IContainer CellStyle(IContainer container) =>
                                    container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).DefaultTextStyle(x => x.FontSize(9));
                            }
                        });

                        col.Item().PaddingTop(15).AlignRight().Width(250).Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Gross Return Value:");
                                r.RelativeItem().AlignRight().Text($"{header.GrossAmount:C}");
                            });

                            if (header.ClawbackPenalty > 0)
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Discount Clawback Penalty:").FontColor(Colors.Red.Medium);
                                    r.RelativeItem().AlignRight().Text($"-{header.ClawbackPenalty:C}").FontColor(Colors.Red.Medium);
                                });
                            }

                            if (header.PromoPenalty > 0)
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Promo Clawback Penalty:").FontColor(Colors.Red.Medium);
                                    r.RelativeItem().AlignRight().Text($"-{header.PromoPenalty:C}").FontColor(Colors.Red.Medium);
                                });
                            }

                            c.Item().PaddingVertical(5).LineHorizontal(1);

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("NET REFUND AMOUNT:").Bold();
                                r.RelativeItem().AlignRight().Text($"{header.NetRefundAmount:C}").Bold().FontSize(12).FontColor(Colors.Green.Medium);
                            });
                        });
                    });

                    // Footer Block
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text($"Generated on {DateTime.UtcNow:dd MMM yyyy HH:mm UTC}").FontSize(8).FontColor(Colors.Grey.Medium);
                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateAnalyticsReportPdf(AnalyticsKpiSummaryDto kpi, InventoryInsightsDto inventory, System.Collections.Generic.List<BusinessInsightDto> insights, System.Collections.Generic.List<StockRiskItemDto> stockRisk, string preset)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("WHOLESALE DISTRIBUTOR").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                                c.Item().Text("Analytics & Business Intelligence Executive Report").FontSize(11).SemiBold().FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(180).Column(c =>
                            {
                                c.Item().Text($"Filter Range: {preset}").FontSize(11).Bold().AlignRight();
                                c.Item().Text($"Date: {DateTime.Now:dd MMM yyyy}").FontSize(9).AlignRight();
                            });
                        });

                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().Column(col =>
                    {
                        // 1. KPI SUMMARY TABLE
                        col.Item().PaddingBottom(5).Text("1. Executive Financial Summary").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().PaddingBottom(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Metric").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Current").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Previous").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Change").Bold();
                            });

                            void AddRow(string title, KpiMetricDto metric)
                            {
                                if (metric == null) return;
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(title);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(metric.FormattedCurrentValue);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(metric.FormattedPreviousValue);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(metric.ComparisonText);
                            }

                            if (kpi != null)
                            {
                                AddRow("Total Sales", kpi.TotalSales);
                                AddRow("Total Purchases", kpi.TotalPurchases);
                                AddRow("Estimated Gross Profit (Avg Cost)", kpi.EstimatedGrossProfit);
                                AddRow("Total Discounts", kpi.TotalDiscounts);
                                AddRow("Total Returns", kpi.TotalReturns);
                                AddRow("Net Sales", kpi.NetSales);
                            }
                        });

                        // 2. INVENTORY INSIGHTS SUMMARY
                        col.Item().PaddingBottom(5).Text("2. Inventory Valuation & Risk Summary").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                        if (inventory != null)
                        {
                            col.Item().PaddingBottom(10).Row(r =>
                            {
                                r.RelativeItem().Text($"Total Stock Value: PKR {inventory.TotalInventoryValue:N2}").Bold();
                                r.RelativeItem().Text($"Products Count: {inventory.TotalProductCount}").Bold();
                                r.RelativeItem().Text($"Low Stock Items: {inventory.LowStockProductCount}").Bold().FontColor(Colors.Orange.Darken2);
                                r.RelativeItem().Text($"Out of Stock: {inventory.OutOfStockProductCount}").Bold().FontColor(Colors.Red.Darken2);
                            });
                        }

                        // 3. STOCK RISK ACTION TABLE
                        if (stockRisk != null && stockRisk.Any())
                        {
                            col.Item().PaddingBottom(5).Text("3. High Stock Risk Items").FontSize(12).Bold().FontColor(Colors.Red.Darken2);
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Product").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Category").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Current").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Reorder").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").Bold();
                                });

                                foreach (var item in stockRisk.Take(10))
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(item.ProductName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(item.CategoryName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text($"{item.CurrentStock:N0}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight().Text($"{item.ReorderLevel:N0}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(item.RiskLevel).FontColor(item.RiskLevel == "OUT_OF_STOCK" ? Colors.Red.Medium : Colors.Orange.Darken1).Bold();
                                }
                            });
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text($"WIMS ERP Analytics — Generated on {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateLoadSheetProductPdf(LoadSheetDto loadSheet)
        {
            if (loadSheet == null)
            {
                throw new ArgumentNullException(nameof(loadSheet));
            }

            var productRows = loadSheet.ProductRows ?? new System.Collections.Generic.List<LoadSheetProductRowDto>();
            var navy = Color.FromHex("#1B365D");
            var navySoft = Color.FromHex("#E8EEF4");
            var line = Color.FromHex("#D5DEE8");
            var ink = Color.FromHex("#243447");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(ink));

                    page.Header().Element(header => ComposeLoadSheetHeader(
                        header, loadSheet, "LOAD SHEET", "Product Load Summary", navy, navySoft));

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(28);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(2.6f);
                                columns.ConstantColumn(78);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "#"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Product ID"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Product Name"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Description"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Total Count", alignRight: true));
                            });

                            int index = 1;
                            foreach (var row in productRows)
                            {
                                var bg = index % 2 == 0 ? navySoft : Colors.White;
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, index.ToString()));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.ProductIdDisplay));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.ProductName));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.Description ?? "—"));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.TotalQuantity.ToString("0.##"), alignRight: true));
                                index++;
                            }

                            if (!productRows.Any())
                            {
                                table.Cell().ColumnSpan(5).Padding(14).AlignCenter()
                                    .Text("No products to load.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                table.Cell().ColumnSpan(4).Background(navy).Padding(6)
                                    .Text("TOTAL COUNT").FontColor(Colors.White).Bold().FontSize(8);
                                table.Cell().Background(navy).Padding(6).AlignRight()
                                    .Text(loadSheet.TotalCountSum.ToString("0.##")).FontColor(Colors.White).Bold();
                            }
                        });

                        col.Item().PaddingTop(28).Row(row =>
                        {
                            row.RelativeItem().Height(58).Border(1).BorderColor(line).Padding(8).Column(c =>
                            {
                                c.Item().Text("Warehouse / Loading Notes").FontSize(8).SemiBold().FontColor(navy);
                                c.Item().PaddingTop(4).Text(" ").FontSize(8);
                            });
                            row.ConstantItem(12);
                            row.RelativeItem().Height(58).Border(1).BorderColor(line).Padding(8).Column(c =>
                            {
                                c.Item().Text("Checked By").FontSize(8).SemiBold().FontColor(navy);
                                c.Item().PaddingTop(18).LineHorizontal(0.5f).LineColor(line);
                            });
                        });
                    });

                    page.Footer().Element(footer => ComposeLoadSheetFooter(footer, "Product Load Summary", navy));
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateLoadSheetInvoicePdf(LoadSheetDto loadSheet)
        {
            if (loadSheet == null)
            {
                throw new ArgumentNullException(nameof(loadSheet));
            }

            var invoiceRows = loadSheet.InvoiceRows ?? new System.Collections.Generic.List<LoadSheetInvoiceRowDto>();
            var navy = Color.FromHex("#1B365D");
            var navySoft = Color.FromHex("#E8EEF4");
            var line = Color.FromHex("#D5DEE8");
            var ink = Color.FromHex("#243447");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(ink));

                    page.Header().Element(header => ComposeLoadSheetHeader(
                        header, loadSheet, "LOAD SHEET", "Party Invoice Report", navy, navySoft));

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.25f);
                                columns.RelativeColumn(1.05f);
                                columns.RelativeColumn(1.7f);
                                columns.RelativeColumn(2.1f);
                                columns.RelativeColumn(1.05f);
                                columns.RelativeColumn(0.95f);
                                columns.RelativeColumn(1.1f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Bill No"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Order Date"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Party Name"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Address"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Amount", alignRight: true));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Discount", alignRight: true));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, navy, "Total Amount", alignRight: true));
                            });

                            int index = 0;
                            foreach (var row in invoiceRows)
                            {
                                var bg = index % 2 == 0 ? navySoft : Colors.White;
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.InvoiceNumber));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.InvoiceDate.ToString("dd-MMM-yyyy")));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.PartyName));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.Address));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.Amount.ToString("N2"), alignRight: true));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.Discount.ToString("N2"), alignRight: true));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.TotalAmount.ToString("N2"), alignRight: true));
                                index++;
                            }

                            if (invoiceRows.Any())
                            {
                                table.Cell().ColumnSpan(4).Background(navy).Padding(6)
                                    .Text($"TOTAL  ·  {invoiceRows.Count} invoice{(invoiceRows.Count == 1 ? "" : "s")}").FontColor(Colors.White).Bold().FontSize(8);
                                table.Cell().Background(navy).Padding(6).AlignRight()
                                    .Text(loadSheet.TotalAmountSum.ToString("N2")).FontColor(Colors.White).Bold();
                                table.Cell().Background(navy).Padding(6).AlignRight()
                                    .Text(loadSheet.TotalDiscountSum.ToString("N2")).FontColor(Colors.White).Bold();
                                table.Cell().Background(navy).Padding(6).AlignRight()
                                    .Text(loadSheet.TotalGrandSum.ToString("N2")).FontColor(Colors.White).Bold();
                            }
                        });

                        col.Item().PaddingTop(22).Row(row =>
                        {
                            row.RelativeItem().Height(54).Border(1).BorderColor(line).Padding(8).Column(c =>
                            {
                                c.Item().Text("Cash / Return").FontSize(8).SemiBold().FontColor(navy);
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Height(54).Border(1).BorderColor(line).Padding(8).Column(c =>
                            {
                                c.Item().Text("Return Stock Notes").FontSize(8).SemiBold().FontColor(navy);
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Height(54).Border(1).BorderColor(line).Padding(8).Column(c =>
                            {
                                c.Item().Text("Received By").FontSize(8).SemiBold().FontColor(navy);
                                c.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(line);
                            });
                        });
                    });

                    page.Footer().Element(footer => ComposeLoadSheetFooter(footer, "Party Invoice Report", navy));
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeLoadSheetHeader(
            IContainer container,
            LoadSheetDto loadSheet,
            string title,
            string subtitle,
            Color navy,
            Color navySoft)
        {
            container.Column(col =>
            {
                col.Item().Background(navy).PaddingVertical(12).PaddingHorizontal(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("WIMS").FontSize(8).FontColor(Colors.White);
                        c.Item().Text(title).FontSize(16).Bold().FontColor(Colors.White);
                        c.Item().Text(subtitle).FontSize(9).FontColor(Color.FromHex("#C5D4E4"));
                    });
                    row.ConstantItem(210).AlignRight().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(loadSheet.FilterDate.ToString("dd MMMM yyyy")).FontSize(11).Bold().FontColor(Colors.White).AlignRight();
                        c.Item().Text($"{loadSheet.InvoiceRows.Count} invoice{(loadSheet.InvoiceRows.Count == 1 ? "" : "s")}").FontSize(8).FontColor(Color.FromHex("#C5D4E4")).AlignRight();
                    });
                });

                col.Item().Background(navySoft).PaddingVertical(8).PaddingHorizontal(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("BROKER").FontSize(7).FontColor(navy).SemiBold();
                        c.Item().Text(loadSheet.BrokerName).FontSize(10).Bold();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("DELIVERY PERSON").FontSize(7).FontColor(navy).SemiBold();
                        c.Item().Text(loadSheet.DeliveryPersonDisplay).FontSize(10).Bold();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SALESPERSON").FontSize(7).FontColor(navy).SemiBold();
                        c.Item().Text(string.IsNullOrWhiteSpace(loadSheet.SalespersonDisplay) ? "—" : loadSheet.SalespersonDisplay).FontSize(10).Bold();
                    });
                });
            });
        }

        private static void ComposeLoadSheetFooter(IContainer container, string documentLabel, Color navy)
        {
            container.PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text($"WIMS  ·  {documentLabel}").FontSize(8).FontColor(navy);
                row.RelativeItem().AlignCenter().Text($"Generated {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private static void LoadSheetHeaderCell(IContainer container, Color navy, string text, bool alignRight = false)
        {
            var cell = container.Background(navy).PaddingVertical(6).PaddingHorizontal(6);
            if (alignRight)
            {
                cell.AlignRight().Text(text).FontColor(Colors.White).SemiBold().FontSize(8);
            }
            else
            {
                cell.Text(text).FontColor(Colors.White).SemiBold().FontSize(8);
            }
        }

        private static void LoadSheetBodyCell(IContainer container, string background, string text, bool alignRight = false)
        {
            var cell = container.Background(background).BorderBottom(0.5f).BorderColor(Color.FromHex("#D5DEE8")).PaddingVertical(5).PaddingHorizontal(6);
            if (alignRight)
            {
                cell.AlignRight().Text(text).FontSize(8);
            }
            else
            {
                cell.Text(text).FontSize(8);
            }
        }
    }
}
