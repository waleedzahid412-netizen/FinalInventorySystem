using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Constants;
using InventorySystem.Data;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.UserManagement;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            IUserRepository userRepository,
            ApplicationDbContext context,
            ILogger<UserManagementService> logger)
        {
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<UserListItemDto>> GetPagedUsersAsync(
            UserFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var paged = await _userRepository.GetPagedAsync(filter, cancellationToken);

            var items = paged.Items.Select(u => new UserListItemDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Username = u.Username,
                RoleName = u.Role.RoleName,
                IsActive = u.IsActive,
                CompanyNames = string.Equals(u.Role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
                    ? new List<string> { "All Companies" }
                    : u.UserCompanies
                        .Where(uc => uc.Company != null && !uc.Company.IsDeleted)
                        .Select(uc => uc.Company!.CompanyName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .OrderBy(name => name)
                        .ToList()
            }).ToList();

            return new PagedResult<UserListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<UserEditDto?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdWithCompaniesAsync(userId, cancellationToken);
            if (user == null)
            {
                return null;
            }

            return new UserEditDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Username = user.Username,
                Phone = user.Phone,
                RoleID = user.RoleID,
                RoleName = user.Role.RoleName,
                IsActive = user.IsActive,
                CompanyIds = user.UserCompanies
                    .Where(uc => !uc.IsDeleted)
                    .Select(uc => uc.CompanyID)
                    .ToList()
            };
        }

        public async Task<OperationResult> CreateUserAsync(
            CreateUserDto dto,
            int actorUserId,
            CancellationToken cancellationToken = default)
        {
            var validation = await ValidateUserPayloadAsync(
                dto.FullName,
                dto.Username,
                dto.Password,
                dto.RoleID,
                dto.CompanyIds,
                isPasswordRequired: true,
                excludeUserId: null,
                cancellationToken);

            if (!validation.Success)
            {
                return validation;
            }

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Username = dto.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                RoleID = dto.RoleID,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actorUserId
            };

            await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();

            var roleName = await _context.Roles
                .AsNoTracking()
                .Where(r => r.RoleID == dto.RoleID)
                .Select(r => r.RoleName)
                .FirstAsync(cancellationToken);

            if (!string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                await ReplaceCompanyPermissionsAsync(user.UserID, dto.CompanyIds, actorUserId, cancellationToken);
            }

            _logger.LogInformation("User {Username} created by {ActorUserId}", user.Username, actorUserId);
            return OperationResult.Ok("User created successfully.");
        }

        public async Task<OperationResult> UpdateUserAsync(
            UpdateUserDto dto,
            int actorUserId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdWithCompaniesAsync(dto.UserID, cancellationToken);
            if (user == null)
            {
                return OperationResult.Fail("User not found.");
            }

            var validation = await ValidateUserPayloadAsync(
                dto.FullName,
                dto.Username,
                dto.NewPassword,
                dto.RoleID,
                dto.CompanyIds,
                isPasswordRequired: false,
                excludeUserId: dto.UserID,
                cancellationToken);

            if (!validation.Success)
            {
                return validation;
            }

            var previousRoleName = user.Role.RoleName;
            var newRoleName = await _context.Roles
                .AsNoTracking()
                .Where(r => r.RoleID == dto.RoleID)
                .Select(r => r.RoleName)
                .FirstAsync(cancellationToken);

            if (dto.UserID == actorUserId && !dto.IsActive)
            {
                return OperationResult.Fail("You cannot deactivate your own account.");
            }

            if (string.Equals(previousRoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(newRoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await _userRepository.CountActiveAdminsAsync(cancellationToken);
                if (adminCount <= 1 && user.IsActive)
                {
                    return OperationResult.Fail("Cannot change the role of the last active administrator.");
                }
            }

            if (!dto.IsActive
                && string.Equals(previousRoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await _userRepository.CountActiveAdminsAsync(cancellationToken);
                if (adminCount <= 1)
                {
                    return OperationResult.Fail("Cannot deactivate the last active administrator.");
                }
            }

            user.FullName = dto.FullName.Trim();
            user.Username = dto.Username.Trim();
            user.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            user.RoleID = dto.RoleID;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = actorUserId;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            if (string.Equals(newRoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                await ClearCompanyPermissionsAsync(user.UserID, actorUserId, cancellationToken);
            }
            else
            {
                await ReplaceCompanyPermissionsAsync(user.UserID, dto.CompanyIds, actorUserId, cancellationToken);
            }

            await _userRepository.SaveChangesAsync();
            _logger.LogInformation("User {UserId} updated by {ActorUserId}", dto.UserID, actorUserId);
            return OperationResult.Ok("User updated successfully.");
        }

        public async Task<OperationResult> SoftDeleteUserAsync(
            int userId,
            int actorUserId,
            CancellationToken cancellationToken = default)
        {
            if (userId == actorUserId)
            {
                return OperationResult.Fail("You cannot delete your own account.");
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return OperationResult.Fail("User not found.");
            }

            if (string.Equals(user.Role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await _userRepository.CountActiveAdminsAsync(cancellationToken);
                if (adminCount <= 1 && user.IsActive)
                {
                    return OperationResult.Fail("Cannot delete the last active administrator.");
                }
            }

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = actorUserId;

            await ClearCompanyPermissionsAsync(userId, actorUserId, cancellationToken);
            await _userRepository.SaveChangesAsync();

            _logger.LogInformation("User {UserId} soft-deleted by {ActorUserId}", userId, actorUserId);
            return OperationResult.Ok("User deleted successfully.");
        }

        private async Task<OperationResult> ValidateUserPayloadAsync(
            string fullName,
            string username,
            string? password,
            int roleId,
            IReadOnlyList<int> companyIds,
            bool isPasswordRequired,
            int? excludeUserId,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add("Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                errors.Add("Username is required.");
            }

            if (isPasswordRequired && string.IsNullOrWhiteSpace(password))
            {
                errors.Add("Password is required.");
            }
            else if (!string.IsNullOrWhiteSpace(password) && password!.Length < 6)
            {
                errors.Add("Password must be at least 6 characters.");
            }

            var roleName = await _context.Roles
                .AsNoTracking()
                .Where(r => r.RoleID == roleId && r.IsActive)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync(cancellationToken);

            if (roleName == null)
            {
                errors.Add("Selected role is invalid.");
            }
            else if (!string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
                     && (companyIds == null || companyIds.Count == 0))
            {
                errors.Add("Select at least one company for a User role account.");
            }

            if (!string.IsNullOrWhiteSpace(username)
                && await _userRepository.UsernameExistsAsync(username.Trim(), excludeUserId, cancellationToken))
            {
                errors.Add("Username is already taken.");
            }

            if (roleName != null
                && !string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
                && companyIds is { Count: > 0 })
            {
                var validCompanyIds = await _context.Companies
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && companyIds.Contains(c.CompanyID))
                    .Select(c => c.CompanyID)
                    .ToListAsync(cancellationToken);

                if (validCompanyIds.Count != companyIds.Distinct().Count())
                {
                    errors.Add("One or more selected companies are invalid.");
                }
            }

            return errors.Count > 0 ? OperationResult.Fail(errors) : OperationResult.Ok();
        }

        private async Task ReplaceCompanyPermissionsAsync(
            int userId,
            IReadOnlyList<int> companyIds,
            int actorUserId,
            CancellationToken cancellationToken)
        {
            var distinctIds = companyIds.Distinct().ToList();

            var existing = await _context.UserCompanies
                .IgnoreQueryFilters()
                .Where(uc => uc.UserID == userId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var link in existing.Where(uc => !distinctIds.Contains(uc.CompanyID)))
            {
                if (!link.IsDeleted)
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                    link.UpdatedBy = actorUserId;
                }
            }

            foreach (var companyId in distinctIds)
            {
                var link = existing.FirstOrDefault(uc => uc.CompanyID == companyId);
                if (link == null)
                {
                    await _context.UserCompanies.AddAsync(new UserCompany
                    {
                        UserID = userId,
                        CompanyID = companyId,
                        CreatedAt = now,
                        CreatedBy = actorUserId
                    }, cancellationToken);
                }
                else if (link.IsDeleted)
                {
                    link.IsDeleted = false;
                    link.DeletedAt = null;
                    link.UpdatedAt = now;
                    link.UpdatedBy = actorUserId;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task ClearCompanyPermissionsAsync(
            int userId,
            int actorUserId,
            CancellationToken cancellationToken)
        {
            var existing = await _context.UserCompanies
                .Where(uc => uc.UserID == userId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var link in existing)
            {
                if (!link.IsDeleted)
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                    link.UpdatedBy = actorUserId;
                }
            }

            if (existing.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
