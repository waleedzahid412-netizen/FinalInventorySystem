using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.DTOs.LoadSheets;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.LoadSheets;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class LoadSheetsController : Controller
    {
        private readonly ILoadSheetService _loadSheetService;
        private readonly ILookupService _lookupService;
        private readonly IPdfService _pdfService;
        private readonly ICompanyContext _companyContext;

        public LoadSheetsController(
            ILoadSheetService loadSheetService,
            ILookupService lookupService,
            IPdfService pdfService,
            ICompanyContext companyContext)
        {
            _loadSheetService = loadSheetService;
            _lookupService = lookupService;
            _pdfService = pdfService;
            _companyContext = companyContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = new LoadSheetIndexViewModel();
            await PopulateDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(LoadSheetIndexViewModel model, CancellationToken cancellationToken)
        {
            bool wantsJson = Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                if (wantsJson)
                {
                    return Json(new { success = false, message = FirstModelError() });
                }

                await PopulateDropdownsAsync(model, cancellationToken);
                return View("Index", model);
            }

            var filter = new LoadSheetFilterDto
            {
                BookerID = model.BookerID,
                Date = model.Date,
                SupplierID = model.SupplierID.HasValue && model.SupplierID.Value > 0
                    ? model.SupplierID
                    : null
            };

            var result = await _loadSheetService.GenerateLoadSheetAsync(filter, cancellationToken);

            if (!result.Success)
            {
                if (wantsJson)
                {
                    return Json(new { success = false, message = result.Message });
                }

                ModelState.AddModelError(string.Empty, result.Message);
                await PopulateDropdownsAsync(model, cancellationToken);
                return View("Index", model);
            }

            if (result.Data == null || !result.Data.HasInvoices)
            {
                var emptyMessage = result.Data?.EmptyMessage ?? "No sales invoices found for the selected filters.";
                if (wantsJson)
                {
                    return Json(new { success = false, message = emptyMessage });
                }

                ModelState.AddModelError(string.Empty, emptyMessage);
                await PopulateDropdownsAsync(model, cancellationToken);
                return View("Index", model);
            }

            var productsPdf = _pdfService.GenerateLoadSheetProductPdf(result.Data);
            var invoicesPdf = _pdfService.GenerateLoadSheetInvoicePdf(result.Data);

            var safeBooker = SanitizeFilePart(result.Data.BookerName);
            var dateStamp = model.Date.ToString("yyyyMMdd");
            var productsFileName = $"LoadSheet_Products_{safeBooker}_{dateStamp}.pdf";
            var invoicesFileName = $"LoadSheet_Invoices_{safeBooker}_{dateStamp}.pdf";

            if (wantsJson)
            {
                return Json(new
                {
                    success = true,
                    productsFileName,
                    productsPdf = Convert.ToBase64String(productsPdf),
                    invoicesFileName,
                    invoicesPdf = Convert.ToBase64String(invoicesPdf)
                });
            }

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipEntry(archive, productsFileName, productsPdf);
                WriteZipEntry(archive, invoicesFileName, invoicesPdf);
            }

            return File(zipStream.ToArray(), "application/zip", $"LoadSheet_{safeBooker}_{dateStamp}.zip");
        }

        private async Task PopulateDropdownsAsync(LoadSheetIndexViewModel model, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            int? companyId = _companyContext.HasCompany ? _companyContext.CompanyID : null;

            var bookers = await _lookupService.GetBookersAsync(companyId, cancellationToken);
            var Suppliers = await _lookupService.GetSuppliersAsync(cancellationToken);

            var deliveryItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Suppliers" }
            };
            foreach (var dp in Suppliers)
            {
                deliveryItems.Add(new SelectListItem { Value = dp.Id.ToString(), Text = dp.Name });
            }

            model.Bookers = new SelectList(bookers, "Id", "Name", model.BookerID);
            model.Suppliers = new SelectList(deliveryItems, "Value", "Text", model.SupplierID?.ToString() ?? "");
        }

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? "Please correct the highlighted fields.";
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "LoadSheet";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Select(ch => invalid.Contains(ch) || ch == ' ' ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "LoadSheet" : cleaned;
        }

        private static void WriteZipEntry(ZipArchive archive, string fileName, byte[] content)
        {
            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }
    }
}
