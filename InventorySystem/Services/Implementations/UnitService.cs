using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Units;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class UnitService : IUnitService
    {
        private readonly IUnitRepository _unitRepository;
        private readonly ILogger<UnitService> _logger;

        public UnitService(IUnitRepository unitRepository, ILogger<UnitService> logger)
        {
            _unitRepository = unitRepository;
            _logger = logger;
        }

        public async Task<PagedResult<UnitListItemDto>> GetPagedUnitsAsync(UnitFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _unitRepository.GetPagedAsync(filter, cancellationToken);

            var listItems = paged.Items.Select(u => new UnitListItemDto
            {
                UnitID = u.UnitID,
                UnitName = u.UnitName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();

            return new PagedResult<UnitListItemDto>(listItems, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditUnitDto?> GetUnitForEditAsync(int unitId, CancellationToken cancellationToken = default)
        {
            var unit = await _unitRepository.GetByIdAsync(unitId, cancellationToken);
            if (unit == null) return null;

            return new EditUnitDto
            {
                UnitID = unit.UnitID,
                UnitName = unit.UnitName,
                IsActive = unit.IsActive
            };
        }

        public async Task<bool> IsInUseAsync(int unitId, CancellationToken cancellationToken = default)
        {
            return await _unitRepository.IsInUseAsync(unitId, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateUnitAsync(CreateUnitDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateCreateDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var now = DateTime.UtcNow;
                var unit = new Unit
                {
                    UnitName = dto.UnitName.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                await _unitRepository.AddAsync(unit, cancellationToken);
                await _unitRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Unit created successfully with ID {UnitID}", unit.UnitID);
                return OperationResult<int>.Ok(unit.UnitID, "Unit created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating unit {UnitName}", dto.UnitName);
                return OperationResult<int>.Fail(UserFacingErrorMessages.UnitCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateUnitAsync(EditUnitDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateEditDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            var unit = await _unitRepository.GetByIdAsync(dto.UnitID, cancellationToken);
            if (unit == null)
            {
                return OperationResult.Fail("Unit not found.");
            }

            try
            {
                var now = DateTime.UtcNow;
                unit.UnitName = dto.UnitName.Trim();
                unit.IsActive = dto.IsActive;
                unit.UpdatedAt = now;
                unit.UpdatedBy = userId;

                await _unitRepository.UpdateAsync(unit, cancellationToken);
                await _unitRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Unit updated successfully for ID {UnitID}", unit.UnitID);
                return OperationResult.Ok("Unit updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating unit ID {UnitID}", dto.UnitID);
                return OperationResult.Fail(UserFacingErrorMessages.UnitUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteUnitAsync(int unitId, int userId, CancellationToken cancellationToken = default)
        {
            var unit = await _unitRepository.GetByIdAsync(unitId, cancellationToken);
            if (unit == null)
            {
                return OperationResult.Fail("Unit not found.");
            }

            bool isInUse = await _unitRepository.IsInUseAsync(unitId, cancellationToken);
            if (isInUse)
            {
                return OperationResult.Fail("This unit cannot be deleted because it is assigned to one or more products.");
            }

            try
            {
                await _unitRepository.SoftDeleteAsync(unitId, userId, cancellationToken);
                await _unitRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Unit soft-deleted successfully for ID {UnitID}", unitId);
                return OperationResult.Ok("Unit deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting unit ID {UnitID}", unitId);
                return OperationResult.Fail(UserFacingErrorMessages.UnitDeleteFailed);
            }
        }

        private async Task<List<string>> ValidateCreateDtoAsync(CreateUnitDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.UnitName))
            {
                errors.Add("Unit Name is required.");
            }
            else if (await _unitRepository.ExistsByNameAsync(dto.UnitName, null, cancellationToken))
            {
                errors.Add("A unit with this name already exists.");
            }

            return errors;
        }

        private async Task<List<string>> ValidateEditDtoAsync(EditUnitDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.UnitName))
            {
                errors.Add("Unit Name is required.");
            }

            var existing = await _unitRepository.GetByIdAsync(dto.UnitID, cancellationToken);
            if (existing == null)
            {
                errors.Add("Unit not found.");
                return errors;
            }

            if (!string.IsNullOrWhiteSpace(dto.UnitName)
                && await _unitRepository.ExistsByNameAsync(dto.UnitName, dto.UnitID, cancellationToken))
            {
                errors.Add("A unit with this name already exists.");
            }

            return errors;
        }
    }
}
