using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Configuration;
using InventorySystem.DTOs.Analytics;
using InventorySystem.DTOs.LoadSheets;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Reports;
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
            var customerPhone = string.IsNullOrWhiteSpace(header.CustomerPhone) ? null : header.CustomerPhone.Trim();
            var businessName = string.IsNullOrWhiteSpace(_printSettings.BusinessName)
                ? "Wholesale Distributor"
                : _printSettings.BusinessName;
            var businessPhone = string.IsNullOrWhiteSpace(_printSettings.Phone) ? "" : _printSettings.Phone;
            var terms = (_printSettings.TermsUrdu ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var termsTitle = string.IsNullOrWhiteSpace(_printSettings.TermsTitleUrdu)
                ? "اہم ہدایات برائے صارفین"
                : _printSettings.TermsTitleUrdu;
            var supplierDisplay = !string.IsNullOrWhiteSpace(header.SupplierName)
                ? header.SupplierName!
                : "-";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(14);
                    page.MarginVertical(12);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor(Colors.Black));

                    page.Content().Column(col =>
                    {
                        // ===== HEADER: distributor identity =====
                        col.Item().AlignCenter().Text(businessName).FontSize(15).Bold();
                        if (!string.IsNullOrWhiteSpace(businessPhone))
                        {
                            col.Item().AlignCenter().PaddingTop(1).Text($"Phone : {businessPhone}").FontSize(9);
                        }

                        // ===== META + BILL TO boxes =====
                        col.Item().PaddingTop(6).Row(boxes =>
                        {
                            boxes.RelativeItem().Border(1).BorderColor(Colors.Black).Padding(5).Column(meta =>
                            {
                                MetaRow(meta, "Invoice No. :", header.InvoiceNumber, boldValue: true);
                                meta.Item().PaddingTop(1);
                                MetaRow(meta, "Date :", header.InvoiceDate.ToString("ddd, dd/MM/yyyy"));
                                meta.Item().PaddingTop(1);
                                // Printed invoices always show Cash (business requirement for customer-facing PDF).
                                MetaRow(meta, "Payment Mode :", "Cash");
                            });

                            boxes.ConstantItem(6);

                            boxes.RelativeItem().Border(1).BorderColor(Colors.Black).Padding(5).Column(bill =>
                            {
                                MetaRow(bill, "Bill To :", billTo, boldValue: true);
                                bill.Item().PaddingTop(1);
                                MetaRow(bill, "Address :", string.IsNullOrWhiteSpace(address) ? "-" : address);
                                bill.Item().PaddingTop(1);
                                MetaRow(bill, "Phone :", customerPhone ?? "-");
                                bill.Item().PaddingTop(1);
                                MetaRow(bill, "Booker :", string.IsNullOrWhiteSpace(header.BookerName) ? "-" : header.BookerName!);
                                bill.Item().PaddingTop(1);
                                MetaRow(bill, "Supplier :", supplierDisplay);
                            });
                        });

                        col.Item().PaddingTop(5).AlignCenter().Text("SALES INVOICE").FontSize(11).Bold()
                            .FontColor(Colors.Black);

                        // ===== LINE ITEMS =====
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(52);   // Product Code
                                columns.RelativeColumn(2.4f); // Product Name
                                columns.ConstantColumn(36);   // Qty
                                columns.ConstantColumn(42);   // Free Qty
                                columns.RelativeColumn(1.6f); // Free Product
                                columns.ConstantColumn(55);   // Unit Price
                                columns.ConstantColumn(60);   // Amount
                            });

                            table.Header(h =>
                            {
                                void Head(string text, bool rightAlign = false)
                                {
                                    var cell = h.Cell().Border(0.75f).BorderColor(Colors.Black)
                                        .Background(Colors.Grey.Lighten3).PaddingVertical(2).PaddingHorizontal(2);
                                    if (rightAlign)
                                        cell.AlignRight().Text(text).FontSize(7.5f).Bold();
                                    else
                                        cell.Text(text).FontSize(7.5f).Bold();
                                }

                                Head("Product Code");
                                Head("ProductName");
                                Head("Qty", true);
                                Head("Free Qty", true);
                                Head("Free Product");
                                Head("Unit Price", true);
                                Head("Amount", true);
                            });

                            foreach (var row in lineRows)
                            {
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .Text(row.ProductCode).FontSize(7.5f);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .Text(row.ProductName).FontSize(7.5f);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .AlignRight().Text(FormatQty(row.Qty)).FontSize(7.5f);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .AlignRight().Text(FormatQty(row.FreeQty)).FontSize(7.5f);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .Text(string.IsNullOrWhiteSpace(row.FreeProductName) ? "-" : row.FreeProductName).FontSize(7);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .AlignRight().Text(row.UnitPrice.ToString("N2")).FontSize(7.5f);
                                table.Cell().Border(0.5f).BorderColor(Colors.Black).PaddingVertical(1.5f).PaddingHorizontal(2)
                                    .AlignRight().Text(row.Amount.ToString("N2")).FontSize(7.5f).Bold();
                            }
                        });

                        // ===== FOOTER: Urdu terms + totals / signature =====
                        col.Item().PaddingTop(6).Row(footer =>
                        {
                            footer.RelativeItem().Border(1).BorderColor(Colors.Black).Padding(5).Column(left =>
                            {
                                left.Item().AlignRight().Text(termsTitle).FontSize(9).Bold();
                                left.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Black);

                                int termIndex = 1;
                                foreach (var term in terms)
                                {
                                    // Number on the right = start of RTL sentence (not after the Urdu text).
                                    var index = termIndex;
                                    left.Item().PaddingBottom(2).Row(r =>
                                    {
                                        r.RelativeItem().AlignRight().Text(term)
                                            .FontSize(7)
                                            .FontFamily("Segoe UI");
                                        r.ConstantItem(18).AlignRight().Text($"{index}.")
                                            .FontSize(7)
                                            .FontFamily("Segoe UI")
                                            .Bold();
                                    });
                                    termIndex++;
                                }
                            });

                            footer.ConstantItem(8);

                            footer.ConstantItem(200).Column(right =>
                            {
                                right.Item().Border(1).BorderColor(Colors.Black).Padding(5).Column(tot =>
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text("Total Amount :").FontSize(8.5f).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.SubTotal.ToString("N2")).FontSize(8.5f);
                                    });
                                    tot.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.RelativeItem().Text("Discount :").FontSize(8.5f).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.DiscountTotal.ToString("N2")).FontSize(8.5f);
                                    });
                                    tot.Item().PaddingTop(2).BorderTop(1).BorderColor(Colors.Black).PaddingTop(2).Row(r =>
                                    {
                                        r.RelativeItem().Text("Net Amount :").FontSize(9).Bold();
                                        r.ConstantItem(80).AlignRight().Text(financial.GrandTotal.ToString("N2")).FontSize(9).Bold();
                                    });
                                });

                                right.Item().PaddingTop(14).AlignCenter().Column(sig =>
                                {
                                    sig.Item().AlignCenter().Text("______________________").FontSize(8);
                                    sig.Item().AlignCenter().PaddingTop(1).Text("Customer Signature").FontSize(8).Bold();
                                });
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void MetaRow(ColumnDescriptor col, string label, string value, bool boldValue = false)
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(82).Text(label).FontSize(7.5f).Bold();
                var text = r.RelativeItem().Text(value).FontSize(8);
                if (boldValue)
                {
                    text.Bold();
                }
            });
        }

        private static List<SalesPrintRow> BuildSalesPrintRows(List<SalesItemDto> items)
        {
            var freeItems = items
                .Where(i => string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var freeByProduct = freeItems
                .Where(i => i.ProductID.HasValue && i.ProductID.Value > 0)
                .GroupBy(i => i.ProductID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Qty: g.Sum(x => x.Quantity),
                        Name: FirstFreeDisplayName(g.First())
                    ));

            var freeByCustom = freeItems
                .Where(i => !i.ProductID.HasValue || i.ProductID.Value <= 0)
                .GroupBy(i => (i.CustomItemName ?? i.ProductName ?? string.Empty).Trim())
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Qty: g.Sum(x => x.Quantity),
                        Name: FirstFreeDisplayName(g.First())
                    ));

            var rows = new List<SalesPrintRow>();
            var consumedFreeProductIds = new HashSet<int>();
            var consumedFreeCustomKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items.Where(i => !string.Equals(i.ItemType, "FREE", StringComparison.OrdinalIgnoreCase)))
            {
                decimal freeQty = 0;
                string? freeProductName = null;

                if (item.ProductID.HasValue && item.ProductID.Value > 0
                    && freeByProduct.TryGetValue(item.ProductID.Value, out var sameProductFree))
                {
                    freeQty = sameProductFree.Qty;
                    freeProductName = sameProductFree.Name;
                    consumedFreeProductIds.Add(item.ProductID.Value);
                }
                else if (item.PromotionID.HasValue)
                {
                    // Different free reward linked by the same promotion (buy X get Y free).
                    var promoFrees = freeItems
                        .Where(f => f.PromotionID == item.PromotionID)
                        .ToList();

                    foreach (var promoFree in promoFrees)
                    {
                        if (promoFree.ProductID.HasValue && promoFree.ProductID.Value > 0)
                        {
                            if (consumedFreeProductIds.Contains(promoFree.ProductID.Value))
                                continue;
                            if (!freeByProduct.TryGetValue(promoFree.ProductID.Value, out var linked))
                                continue;

                            freeQty = linked.Qty;
                            freeProductName = linked.Name;
                            consumedFreeProductIds.Add(promoFree.ProductID.Value);
                            break;
                        }

                        var customKey = (promoFree.CustomItemName ?? promoFree.ProductName ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(customKey) || consumedFreeCustomKeys.Contains(customKey))
                            continue;
                        if (!freeByCustom.TryGetValue(customKey, out var customFree))
                            continue;

                        freeQty = customFree.Qty;
                        freeProductName = customFree.Name;
                        consumedFreeCustomKeys.Add(customKey);
                        break;
                    }
                }

                if (freeQty > 0 && string.IsNullOrWhiteSpace(freeProductName))
                {
                    freeProductName = item.ProductName;
                }

                rows.Add(new SalesPrintRow
                {
                    ProductCode = FormatProductCode(item),
                    ProductName = item.ProductName,
                    Qty = item.Quantity,
                    FreeQty = freeQty,
                    FreeProductName = freeQty > 0 ? freeProductName : null,
                    UnitPrice = item.UnitPrice,
                    Amount = item.LineTotal
                });
            }

            foreach (var free in freeItems)
            {
                if (free.ProductID.HasValue && free.ProductID.Value > 0)
                {
                    if (consumedFreeProductIds.Contains(free.ProductID.Value))
                        continue;
                    consumedFreeProductIds.Add(free.ProductID.Value);
                }
                else
                {
                    var key = (free.CustomItemName ?? free.ProductName ?? string.Empty).Trim();
                    if (consumedFreeCustomKeys.Contains(key))
                        continue;
                    consumedFreeCustomKeys.Add(key);
                }

                var freeName = FirstFreeDisplayName(free);
                rows.Add(new SalesPrintRow
                {
                    ProductCode = FormatProductCode(free),
                    ProductName = freeName,
                    Qty = 0,
                    FreeQty = free.Quantity,
                    FreeProductName = freeName,
                    UnitPrice = 0,
                    Amount = 0
                });
            }

            return rows;
        }

        private static string FirstFreeDisplayName(SalesItemDto free)
        {
            if (!string.IsNullOrWhiteSpace(free.CustomItemName))
                return free.CustomItemName.Trim();
            if (!string.IsNullOrWhiteSpace(free.ProductName))
                return free.ProductName.Trim();
            return "Free Item";
        }

        private static string FormatProductCode(SalesItemDto item)
        {
            if (!string.IsNullOrWhiteSpace(item.SKU))
            {
                return item.SKU!;
            }

            if (item.ProductID.HasValue && item.ProductID.Value > 0)
            {
                return item.ProductID.Value.ToString("D6");
            }

            return "-";
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
            public string? FreeProductName { get; set; }
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

        public byte[] GenerateCompanyStockReportPdf(
            IReadOnlyList<CompanyStockReportRowDto> rows,
            CompanyStockReportSummaryDto summary,
            string scopeLabel,
            bool includeCompanyColumn)
        {
            rows ??= Array.Empty<CompanyStockReportRowDto>();
            summary ??= new CompanyStockReportSummaryDto();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("WHOLESALE DISTRIBUTOR").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                c.Item().Text("Company Stock Report").FontSize(11).SemiBold().FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(200).Column(c =>
                            {
                                c.Item().Text($"Scope: {scopeLabel}").FontSize(10).Bold().AlignRight();
                                c.Item().Text($"Date: {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(9).AlignRight();
                            });
                        });

                        col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(10).Row(r =>
                        {
                            r.RelativeItem().Text($"Products: {summary.TotalProducts:N0}").Bold();
                            r.RelativeItem().Text($"Total Stock: {summary.TotalStockQuantity:N0}").Bold();
                            r.RelativeItem().Text($"Low Stock: {summary.LowStockCount:N0}").Bold().FontColor(Colors.Orange.Darken2);
                            r.RelativeItem().Text($"Out of Stock: {summary.OutOfStockCount:N0}").Bold().FontColor(Colors.Red.Darken2);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                if (includeCompanyColumn)
                                    columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(2.8f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                void H(string text, bool right = false)
                                {
                                    var cell = header.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                    if (right) cell.AlignRight().Text(text).Bold();
                                    else cell.Text(text).Bold();
                                }

                                if (includeCompanyColumn) H("Company");
                                H("Product");
                                H("SKU");
                                H("Base Unit");
                                H("Stock", true);
                                H("Reorder", true);
                                H("Status");
                            });

                            foreach (var item in rows)
                            {
                                void C(string text, bool right = false)
                                {
                                    var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3);
                                    if (right) cell.AlignRight().Text(text);
                                    else cell.Text(text);
                                }

                                if (includeCompanyColumn) C(item.CompanyName);
                                C(item.ProductName);
                                C(item.SKU ?? "-");
                                C(item.BaseUnitName);
                                C(item.CurrentStock.ToString("N0"), true);
                                C(item.ReorderLevel.ToString("N0"), true);
                                C(item.Status);
                            }

                            if (rows.Count == 0)
                            {
                                var span = includeCompanyColumn ? 7 : 6;
                                table.Cell().ColumnSpan((uint)span).Padding(8).AlignCenter()
                                    .Text("No stock rows for current filters.").FontColor(Colors.Grey.Medium);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
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
            var black = Colors.Black;
            var softGray = Color.FromHex("#F2F2F2");
            var line = Color.FromHex("#BDBDBD");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(16);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor(black));

                    page.Header().Element(header => ComposeLoadSheetHeader(
                        header, loadSheet, "LOAD SHEET", "Product Load Summary"));

                    page.Content().PaddingTop(4).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(22);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(2.8f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(70);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "#"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Product ID"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Product Name"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Base Unit Price", alignRight: true));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Total Count", alignRight: true));
                            });

                            int index = 1;
                            foreach (var row in productRows)
                            {
                                var bg = index % 2 == 0 ? softGray : Colors.White;
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, index.ToString()));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.ProductIdDisplay));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.ProductName));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.UnitPrice.ToString("N2"), alignRight: true));
                                table.Cell().Element(c => LoadSheetBodyCell(c, bg, row.TotalQuantity.ToString("0.##"), alignRight: true));
                                index++;
                            }

                            if (!productRows.Any())
                            {
                                table.Cell().ColumnSpan(5).Padding(8).AlignCenter()
                                    .Text("No products to load.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                table.Cell().ColumnSpan(4).Background(black).PaddingVertical(3).PaddingHorizontal(4)
                                    .Text("TOTAL COUNT").FontColor(Colors.White).Bold().FontSize(7);
                                table.Cell().Background(black).PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                                    .Text(loadSheet.TotalCountSum.ToString("0.##")).FontColor(Colors.White).Bold().FontSize(8);
                            }
                        });

                        col.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Height(36).Border(1).BorderColor(line).Padding(5).Column(c =>
                            {
                                c.Item().Text("Warehouse / Loading Notes").FontSize(7).SemiBold();
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Height(36).Border(1).BorderColor(line).Padding(5).Column(c =>
                            {
                                c.Item().Text("Checked By").FontSize(7).SemiBold();
                                c.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(line);
                            });
                        });
                    });

                    page.Footer().Element(footer => ComposeLoadSheetFooter(footer, "Product Load Summary"));
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
            var black = Colors.Black;
            var softGray = Color.FromHex("#F2F2F2");
            var line = Color.FromHex("#BDBDBD");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(16);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor(black));

                    page.Header().Element(header => ComposeLoadSheetHeader(
                        header, loadSheet, "LOAD SHEET", "Party Invoice Report"));

                    page.Content().PaddingTop(4).Column(col =>
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
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Bill No"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Order Date"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Party Name"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Address"));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Amount", alignRight: true));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Discount", alignRight: true));
                                h.Cell().Element(c => LoadSheetHeaderCell(c, "Total Amount", alignRight: true));
                            });

                            int index = 0;
                            foreach (var row in invoiceRows)
                            {
                                var bg = index % 2 == 0 ? softGray : Colors.White;
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
                                table.Cell().ColumnSpan(4).Background(black).PaddingVertical(3).PaddingHorizontal(4)
                                    .Text($"TOTAL  ·  {invoiceRows.Count} invoice{(invoiceRows.Count == 1 ? "" : "s")}").FontColor(Colors.White).Bold().FontSize(7);
                                table.Cell().Background(black).PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                                    .Text(loadSheet.TotalAmountSum.ToString("N2")).FontColor(Colors.White).Bold().FontSize(8);
                                table.Cell().Background(black).PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                                    .Text(loadSheet.TotalDiscountSum.ToString("N2")).FontColor(Colors.White).Bold().FontSize(8);
                                table.Cell().Background(black).PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                                    .Text(loadSheet.TotalGrandSum.ToString("N2")).FontColor(Colors.White).Bold().FontSize(8);
                            }
                        });

                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Height(34).Border(1).BorderColor(line).Padding(5).Column(c =>
                            {
                                c.Item().Text("Cash / Return").FontSize(7).SemiBold();
                            });
                            row.ConstantItem(6);
                            row.RelativeItem().Height(34).Border(1).BorderColor(line).Padding(5).Column(c =>
                            {
                                c.Item().Text("Return Stock Notes").FontSize(7).SemiBold();
                            });
                            row.ConstantItem(6);
                            row.RelativeItem().Height(34).Border(1).BorderColor(line).Padding(5).Column(c =>
                            {
                                c.Item().Text("Received By").FontSize(7).SemiBold();
                                c.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(line);
                            });
                        });
                    });

                    page.Footer().Element(footer => ComposeLoadSheetFooter(footer, "Party Invoice Report"));
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeLoadSheetHeader(
            IContainer container,
            LoadSheetDto loadSheet,
            string title,
            string subtitle)
        {
            container.Column(col =>
            {
                col.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(4).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("WIMS").FontSize(7).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(title).FontSize(13).Bold();
                        c.Item().Text(subtitle).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                    row.ConstantItem(180).AlignRight().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(loadSheet.FilterDate.ToString("dd MMMM yyyy")).FontSize(9).Bold().AlignRight();
                        c.Item().Text($"{loadSheet.InvoiceRows.Count} invoice{(loadSheet.InvoiceRows.Count == 1 ? "" : "s")}").FontSize(7).FontColor(Colors.Grey.Darken2).AlignRight();
                    });
                });

                col.Item().PaddingTop(4).PaddingBottom(2).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("BOOKER").FontSize(6).FontColor(Colors.Grey.Darken2).SemiBold();
                        c.Item().Text(loadSheet.BookerName).FontSize(9).Bold();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SUPPLIER").FontSize(6).FontColor(Colors.Grey.Darken2).SemiBold();
                        c.Item().Text(loadSheet.SupplierDisplay).FontSize(9).Bold();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SALESPERSON").FontSize(6).FontColor(Colors.Grey.Darken2).SemiBold();
                        c.Item().Text(string.IsNullOrWhiteSpace(loadSheet.SalespersonDisplay) ? "—" : loadSheet.SalespersonDisplay).FontSize(9).Bold();
                    });
                });
            });
        }

        private static void ComposeLoadSheetFooter(IContainer container, string documentLabel)
        {
            container.PaddingTop(4).BorderTop(0.5f).BorderColor(Color.FromHex("#BDBDBD")).PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"WIMS  ·  {documentLabel}").FontSize(7).FontColor(Colors.Grey.Darken2);
                row.RelativeItem().AlignCenter().Text($"Generated {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                    x.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private static void LoadSheetHeaderCell(IContainer container, string text, bool alignRight = false)
        {
            var cell = container.Background(Colors.Black).PaddingVertical(3).PaddingHorizontal(4);
            if (alignRight)
            {
                cell.AlignRight().Text(text).FontColor(Colors.White).SemiBold().FontSize(7);
            }
            else
            {
                cell.Text(text).FontColor(Colors.White).SemiBold().FontSize(7);
            }
        }

        private static void LoadSheetBodyCell(IContainer container, string background, string text, bool alignRight = false)
        {
            var cell = container.Background(background).BorderBottom(0.5f).BorderColor(Color.FromHex("#BDBDBD")).PaddingVertical(2).PaddingHorizontal(4);
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
