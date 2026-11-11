using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.SubAreas;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class SubAreaService : ISubAreaService
    {
        private readonly ISubAreaRepository _subAreaRepository;
        private readonly ILogger<SubAreaService> _logger;

        public SubAreaService(ISubAreaRepository subAreaRepository, ILogger<SubAreaService> logger)
        {
            _subAreaRepository = subAreaRepository;
            _logger = logger;
        }

        public async Task<PagedResult<SubAreaListItemDto>> GetPagedSubAreasAsync(SubAreaFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _subAreaRepository.GetPagedAsync(filter, cancellationToken);
            var items = paged.Items.Select(s => new SubAreaListItemDto
            {
                SubAreaID = s.SubAreaID,
                AreaID = s.AreaID,
                AreaName = s.Area?.AreaName ?? string.Empty,
                SubAreaName = s.SubAreaName,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            }).ToList();

            return new PagedResult<SubAreaListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditSubAreaDto?> GetSubAreaForEditAsync(int subAreaId, CancellationToken cancellationToken = default)
        {
            var subArea = await _subAreaRepository.GetByIdAsync(subAreaId, cancellationToken);
            if (subArea == null) return null;

            return new EditSubAreaDto
            {
                SubAreaID = subArea.SubAreaID,
                AreaID = subArea.AreaID,
                SubAreaName = subArea.SubAreaName,
                IsActive = subArea.IsActive
            };
        }

        public async Task<bool> IsInUseAsync(int subAreaId, CancellationToken cancellationToken = default)
        {
            return await _subAreaRepository.IsInUseAsync(subAreaId, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateSubAreaAsync(CreateSubAreaDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateAsync(dto.AreaID, dto.SubAreaName, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var subArea = new SubArea
                {
                    AreaID = dto.AreaID,
                    SubAreaName = dto.SubAreaName.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId > 0 ? userId : null,
                    IsDeleted = false
                };

                await _subAreaRepository.AddAsync(subArea, cancellationToken);
                await _subAreaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("SubArea created successfully with ID {SubAreaID}", subArea.SubAreaID);
                return OperationResult<int>.Ok(subArea.SubAreaID, "Sub area created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating sub area {SubAreaName}", dto.SubAreaName);
                return OperationResult<int>.Fail(UserFacingErrorMessages.SubAreaCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateSubAreaAsync(EditSubAreaDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var subArea = await _subAreaRepository.GetByIdAsync(dto.SubAreaID, cancellationToken);
            if (subArea == null)
            {
                return OperationResult.Fail("Sub area not found.");
            }

            var errors = await ValidateAsync(dto.AreaID, dto.SubAreaName, dto.SubAreaID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            try
            {
                subArea.AreaID = dto.AreaID;
                subArea.SubAreaName = dto.SubAreaName.Trim();
                subArea.IsActive = dto.IsActive;
                subArea.UpdatedAt = DateTime.UtcNow;
                subArea.UpdatedBy = userId > 0 ? userId : null;

                await _subAreaRepository.UpdateAsync(subArea, cancellationToken);
                await _subAreaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("SubArea updated successfully for ID {SubAreaID}", subArea.SubAreaID);
                return OperationResult.Ok("Sub area updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating sub area ID {SubAreaID}", dto.SubAreaID);
                return OperationResult.Fail(UserFacingErrorMessages.SubAreaUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteSubAreaAsync(int subAreaId, int userId, CancellationToken cancellationToken = default)
        {
            var subArea = await _subAreaRepository.GetByIdAsync(subAreaId, cancellationToken);
            if (subArea == null)
            {
                return OperationResult.Fail("Sub area not found.");
            }

            if (await _subAreaRepository.IsInUseAsync(subAreaId, cancellationToken))
            {
                return OperationResult.Fail("Cannot delete sub area that has customers or sales invoices.");
            }

            try
            {
                await _subAreaRepository.SoftDeleteAsync(subAreaId, userId > 0 ? userId : 0, cancellationToken);
                await _subAreaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("SubArea soft-deleted successfully for ID {SubAreaID}", subAreaId);
                return OperationResult.Ok("Sub area deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting sub area ID {SubAreaID}", subAreaId);
                return OperationResult.Fail(UserFacingErrorMessages.SubAreaDeleteFailed);
            }
        }

        private async Task<List<string>> ValidateAsync(
            int areaId,
            string subAreaName,
            int? excludeSubAreaId,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (areaId <= 0 || !await _subAreaRepository.AreaExistsAsync(areaId, cancellationToken))
            {
                errors.Add("A valid parent area is required.");
            }

            if (string.IsNullOrWhiteSpace(subAreaName))
            {
                errors.Add("Sub area name is required.");
            }
            else if (areaId > 0
                && await _subAreaRepository.ExistsByNameAsync(areaId, subAreaName, excludeSubAreaId, cancellationToken))
            {
                errors.Add("A sub area with this name already exists in the selected area.");
            }

            return errors;
        }
    }
}
