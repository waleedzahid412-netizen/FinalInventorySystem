using System;
using System.Linq;
using InventorySystem.DTOs.Purchases;
using InventorySystem.DTOs.Sales;
using InventorySystem.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventorySystem.Services.Implementations
{
    public class PdfService : IPdfService
    {
        public byte[] GenerateSalesInvoicePdf(SalesDetailsDto invoice)
        {
            if (invoice == null || invoice.Header == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }

            var header = invoice.Header;
            var items = invoice.Items ?? new System.Collections.Generic.List<SalesItemDto>();
            var financial = invoice.Financial ?? new SalesFinancialSummaryDto();

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
                                c.Item().Text("WHOLESALE DISTRIBUTOR").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                                c.Item().Text("Official Sales Invoice").FontSize(12).SemiBold().FontColor(Colors.Grey.Medium);
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
                        // Customer & Warehouse Details Card
                        col.Item().Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CUSTOMER INFORMATION").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text(header.CustomerName).FontSize(11).Bold();
                                if (!string.IsNullOrWhiteSpace(header.ShopName))
                                {
                                    c.Item().Text($"Shop: {header.ShopName}").FontSize(9);
                                }
                                c.Item().Text($"Area: {header.AreaName ?? "N/A"} {(header.SubAreaName != null ? $"/ {header.SubAreaName}" : "")}").FontSize(9);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DELIVERY & WAREHOUSE").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"Warehouse: {header.WarehouseName}").FontSize(10);
                                c.Item().Text($"Agent: {header.DeliveryPersonName ?? "Unassigned"}").FontSize(9);
                                c.Item().Text($"Created By: {header.CreatedByUserName}").FontSize(9);
                            });
                        });

                        col.Item().Height(15);

                        // Sold Line Items Table
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
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("#").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Product Name").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("SKU").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Unit").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight().Text("Qty").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight().Text("Unit Price").FontColor(Colors.White).Bold().FontSize(9);
                                headerBlock.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight().Text("Total").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            int index = 1;
                            foreach (var item in items)
                            {
                                bool isFree = item.ItemType == "FREE";
                                var bgColor = (index % 2 == 0) ? Colors.Grey.Lighten5 : Colors.White;
                                if (isFree) bgColor = Colors.Green.Lighten5;

                                table.Cell().Background(bgColor).Padding(5).Text(index.ToString()).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).Column(c =>
                                {
                                    c.Item().Text(item.ProductName).Bold().FontSize(9);
                                    if (isFree)
                                    {
                                        c.Item().Text("[FREE PROMOTIONAL ITEM]").FontSize(8).Bold().FontColor(Colors.Green.Darken2);
                                    }
                                });
                                table.Cell().Background(bgColor).Padding(5).Text(item.SKU ?? "-").FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).Text(item.UnitName).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(item.Quantity.ToString("N2")).FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(isFree ? "FREE" : $"PKR {item.UnitPrice:N2}").FontSize(9);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(isFree ? "PKR 0.00" : $"PKR {item.LineTotal:N2}").Bold().FontSize(9);

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
                                c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Subtotal:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.SubTotal:N2}").FontSize(9);
                                });

                                if (financial.DiscountTotal > 0)
                                {
                                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Row(r =>
                                    {
                                        r.RelativeItem().Text("Discount Total:").FontSize(9).FontColor(Colors.Green.Darken2);
                                        r.RelativeItem().AlignRight().Text($"- PKR {financial.DiscountTotal:N2}").FontSize(9).FontColor(Colors.Green.Darken2);
                                    });
                                }

                                c.Item().BorderBottom(2).BorderColor(Colors.Blue.Darken2).Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Grand Total:").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.GrandTotal:N2}").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                                });

                                c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Paid Amount:").FontSize(9).FontColor(Colors.Green.Medium);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.PaidAmount:N2}").FontSize(9).FontColor(Colors.Green.Medium);
                                });

                                c.Item().Padding(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Outstanding Balance:").Bold().FontSize(10).FontColor(Colors.Red.Medium);
                                    r.RelativeItem().AlignRight().Text($"PKR {financial.OutstandingBalance:N2}").Bold().FontSize(10).FontColor(Colors.Red.Medium);
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
    }
}
