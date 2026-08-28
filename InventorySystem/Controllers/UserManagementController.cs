using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;
using InventorySystem.ViewModels.UserManagement;

namespace InventorySystem.Controllers
{
    [Authorize]
    public class UserManagementController : Controller
    {
        private readonly IUserManagementService _userManagementService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;

        public UserManagementController(
            IUserManagementService userManagementService,
            IUserRepository userRepository,
            ApplicationDbContext context)
        {
            _userManagementService = userManagementService;
            _userRepository = userRepository;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] UserFilterDto filter, CancellationToken cancellationToken)
        {
            var users = await _userManagementService.GetPagedUsersAsync(filter, cancellationToken);
            return View(new UserListViewModel
            {
                Filter = filter,
                Users = users
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var user = await _userManagementService.GetUserForEditAsync(id, cancellationToken);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            var companyNames = string.Equals(user.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
                ? new List<string> { "All Companies" }
                : await _context.Companies
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && user.CompanyIds.Contains(c.CompanyID))
                    .OrderBy(c => c.CompanyName)
                    .Select(c => c.CompanyName)
                    .ToListAsync(cancellationToken);

            return View(new UserDetailsViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Username = user.Username,
                Phone = user.Phone,
                RoleName = user.RoleName,
                IsActive = user.IsActive,
                CompanyNames = companyNames
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var model = await BuildCreateViewModelAsync(cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCreateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            var result = await _userManagementService.CreateUserAsync(new CreateUserDto
            {
                FullName = model.FullName,
                Username = model.Username,
                Password = model.Password,
                Phone = model.Phone,
                RoleID = model.RoleID,
                CompanyIds = model.CompanyIds ?? new()
            }, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await PopulateCreateDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var user = await _userManagementService.GetUserForEditAsync(id, cancellationToken);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            var model = new EditUserViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Username = user.Username,
                Phone = user.Phone,
                RoleID = user.RoleID,
                IsActive = user.IsActive,
                CompanyIds = user.CompanyIds
            };

            await PopulateEditDropdownsAsync(model, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateEditDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            var result = await _userManagementService.UpdateUserAsync(new UpdateUserDto
            {
                UserID = model.UserID,
                FullName = model.FullName,
                Username = model.Username,
                NewPassword = model.NewPassword,
                Phone = model.Phone,
                RoleID = model.RoleID,
                IsActive = model.IsActive,
                CompanyIds = model.CompanyIds ?? new()
            }, GetCurrentUserId(), cancellationToken);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await PopulateEditDropdownsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var result = await _userManagementService.SoftDeleteUserAsync(id, GetCurrentUserId(), cancellationToken);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
                ? result.Message
                : (result.Errors.FirstOrDefault() ?? result.Message);
            return RedirectToAction(nameof(Index));
        }

        private async Task<CreateUserViewModel> BuildCreateViewModelAsync(CancellationToken cancellationToken)
        {
            var roles = await _userRepository.GetAssignableRolesAsync(cancellationToken);
            var userRole = roles.FirstOrDefault(r => r.RoleName == RoleNames.User);

            var model = new CreateUserViewModel
            {
                RoleID = userRole?.RoleID ?? roles.FirstOrDefault()?.RoleID ?? 0
            };

            await PopulateCreateDropdownsAsync(model, cancellationToken);
            return model;
        }

        private async Task PopulateCreateDropdownsAsync(CreateUserViewModel model, CancellationToken cancellationToken)
        {
            var roles = await _userRepository.GetAssignableRolesAsync(cancellationToken);
            model.Roles = roles.Select(r => new RoleOptionViewModel
            {
                RoleID = r.RoleID,
                RoleName = r.RoleName
            }).ToList();

            model.Companies = await _context.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyOptionViewModel
                {
                    CompanyID = c.CompanyID,
                    CompanyName = c.CompanyName
                })
                .ToListAsync(cancellationToken);
        }

        private async Task PopulateEditDropdownsAsync(EditUserViewModel model, CancellationToken cancellationToken)
        {
            var roles = await _userRepository.GetAssignableRolesAsync(cancellationToken);
            model.Roles = roles.Select(r => new RoleOptionViewModel
            {
                RoleID = r.RoleID,
                RoleName = r.RoleName
            }).ToList();

            model.Companies = await _context.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyOptionViewModel
                {
                    CompanyID = c.CompanyID,
                    CompanyName = c.CompanyName
                })
                .ToListAsync(cancellationToken);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var userId) ? userId : 0;
        }
    }
}
