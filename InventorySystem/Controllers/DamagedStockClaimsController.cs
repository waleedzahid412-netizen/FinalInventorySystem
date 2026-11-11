using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Authorization;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.DamagedStockClaims;
using InventorySystem.Filters;
using InventorySystem.Helpers;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.DamagedStockClaims;

namespace InventorySystem.Controllers
{
    [Authorize]
    [RequireCompanyScope]
    public class DamagedStockClaimsController : InventoryController
    {
        private readonly IDamagedStockClaimService _claimService;
        private readonly ICompanyContext _companyContext;
        private readonly ILookupService _lookupService;
        private readonly ApplicationDbContext _context;

        public DamagedStockClaimsController(
            IDamagedStockClaimService claimService,
            ICompanyContext companyContext,
            ILookupService lookupService,
            ApplicationDbContext context)
        {
            _claimService = claimService;
            _companyContext = companyContext;
            _lookupService = lookupService;
            _context = context;
        }

        [HttpGet]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.View)]
        public async Task<IActionResult> Index(
            [FromQuery] DamagedStockClaimFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (_companyContext.HasCompany)
            {
                filter.CompanyID = _companyContext.CompanyID;
            }

            var (items, total) = await _claimService.GetClaimsAsync(filter, cancellationToken);

            var vm = new DamagedStockClaimIndexViewModel
            {
                Filter = filter,
                Claims = items,
                TotalCount = total,
                ScopeLabel = _companyContext.HasCompany
                    ? _companyContext.CompanyName ?? "Selected Company"
                    : "All Companies",
                ClaimStatuses = BuildClaimStatusOptions(),
                SettlementStatuses = BuildSettlementStatusOptions()
            };

            return View(vm);
        }

        [HttpGet]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Add)]
        public async Task<IActionResult> Create(
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var blocked = CompanyScopeGuards.RedirectIfCannotCreate(this, _companyContext);
            if (blocked != null)
            {
                return blocked;
            }

            var damaged = await _claimService.GetDamagedStockAsync(
                _companyContext.CompanyID,
                warehouseId,
                searchTerm,
                cancellationToken);

            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);

            var vm = new DamagedStockClaimCreateViewModel
            {
                CompanyID = _companyContext.CompanyID,
                CompanyName = _companyContext.CompanyName ?? string.Empty,
                DamagedStock = damaged,
                WarehouseID = warehouseId,
                SearchTerm = searchTerm,
                Warehouses = warehouses.Select(w => new SelectListItem(w.Name, w.Id.ToString())).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.View)]
        public async Task<IActionResult> GetDamagedStock(
            int? warehouseId = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (!_companyContext.HasCompany)
            {
                return BadRequest(new { message = "Select a specific company." });
            }

            var rows = await _claimService.GetDamagedStockAsync(
                _companyContext.CompanyID,
                warehouseId,
                searchTerm,
                cancellationToken);

            return Json(rows);
        }

        [HttpPost]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Add)]
        public async Task<IActionResult> Create(
            [FromBody] CreateDamagedStockClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            if (_companyContext.IsAllCompanies || !_companyContext.HasCompany)
            {
                return BadRequest(new { message = CompanyScopeGuards.SelectSpecificCompanyMessage });
            }

            var result = await _claimService.CreateClaimAsync(
                _companyContext.CompanyID,
                request,
                GetCurrentUserId(),
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message, errors = result.Errors });
            }

            return Ok(new { claimId = result.Data, message = result.Message });
        }

        [HttpGet]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.View)]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            await _companyContext.TryResolveAsync(cancellationToken);

            var claim = await _claimService.GetClaimDetailAsync(id, cancellationToken);
            if (claim == null)
            {
                TempData["ErrorMessage"] = "Claim not found.";
                return RedirectToAction(nameof(Index));
            }

            if (_companyContext.HasCompany && claim.CompanyID != _companyContext.CompanyID)
            {
                TempData["ErrorMessage"] = "This claim is outside the selected company scope.";
                return RedirectToAction(nameof(Index));
            }

            var warehouses = await _lookupService.GetWarehousesAsync(cancellationToken);
            var products = await _context.Products.AsNoTracking()
                .Where(p => p.CompanyID == claim.CompanyID && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new SelectListItem(p.ProductName, p.ProductID.ToString()))
                .ToListAsync(cancellationToken);

            var vm = new DamagedStockClaimDetailsViewModel
            {
                Claim = claim,
                Warehouses = warehouses.Select(w => new SelectListItem(w.Name, w.Id.ToString())).ToList(),
                CompanyProducts = products
            };

            return View(vm);
        }

        [HttpPost]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Edit)]
        public async Task<IActionResult> Confirm(
            int id,
            [FromBody] ConfirmDamagedStockClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            var scopeBlock = await EnsureClaimInScopeAsync(id, cancellationToken);
            if (scopeBlock != null)
            {
                return scopeBlock;
            }

            var result = await _claimService.ConfirmSupplierResponseAsync(
                id, request, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message, errors = result.Errors });
            }

            return Ok(new { message = result.Message });
        }

        [HttpPost]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Edit)]
        public async Task<IActionResult> DecideRejected(
            int id,
            [FromBody] RejectedStockDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var scopeBlock = await EnsureClaimInScopeAsync(id, cancellationToken);
            if (scopeBlock != null)
            {
                return scopeBlock;
            }

            var result = await _claimService.ApplyRejectedDecisionAsync(
                id, request, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message, errors = result.Errors });
            }

            return Ok(new { message = result.Message });
        }

        [HttpPost]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Edit)]
        public async Task<IActionResult> AddSettlement(
            int id,
            [FromBody] AddClaimSettlementRequest request,
            CancellationToken cancellationToken = default)
        {
            var scopeBlock = await EnsureClaimInScopeAsync(id, cancellationToken);
            if (scopeBlock != null)
            {
                return scopeBlock;
            }

            var result = await _claimService.AddSettlementAsync(
                id, request, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message, errors = result.Errors });
            }

            return Ok(new { settlementId = result.Data, message = result.Message });
        }

        [HttpPost]
        [RequirePermission(PageKeys.DamagedStockClaims, PermissionAction.Edit)]
        public async Task<IActionResult> Close(int id, CancellationToken cancellationToken = default)
        {
            var scopeBlock = await EnsureClaimInScopeAsync(id, cancellationToken);
            if (scopeBlock != null)
            {
                return scopeBlock;
            }

            var result = await _claimService.TryCloseClaimAsync(id, GetCurrentUserId(), cancellationToken);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
            }
            else
            {
                TempData["SuccessMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IActionResult?> EnsureClaimInScopeAsync(int claimId, CancellationToken cancellationToken)
        {
            await _companyContext.TryResolveAsync(cancellationToken);
            var claim = await _claimService.GetClaimDetailAsync(claimId, cancellationToken);
            if (claim == null)
            {
                return NotFound(new { message = "Claim not found." });
            }

            if (_companyContext.HasCompany && claim.CompanyID != _companyContext.CompanyID)
            {
                return BadRequest(new { message = "Claim is outside the selected company scope." });
            }

            return null;
        }

        private static List<SelectListItem> BuildClaimStatusOptions() => new()
        {
            new("Pending Supplier Confirmation", DamagedStockClaimStatuses.PendingSupplierConfirmation),
            new("Partially Accepted", DamagedStockClaimStatuses.PartiallyAccepted),
            new("Accepted", DamagedStockClaimStatuses.Accepted),
            new("Rejected", DamagedStockClaimStatuses.Rejected),
            new("Closed", DamagedStockClaimStatuses.Closed)
        };

        private static List<SelectListItem> BuildSettlementStatusOptions() => new()
        {
            new("Not Required", DamagedStockSettlementStatuses.NotRequired),
            new("Pending", DamagedStockSettlementStatuses.Pending),
            new("Partially Settled", DamagedStockSettlementStatuses.PartiallySettled),
            new("Settled", DamagedStockSettlementStatuses.Settled)
        };
    }
}
