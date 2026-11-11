using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Areas;
using InventorySystem.DTOs.Common;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class AreaService : IAreaService
    {
        private readonly IAreaRepository _areaRepository;
        private readonly ILogger<AreaService> _logger;

        public AreaService(IAreaRepository areaRepository, ILogger<AreaService> logger)
        {
            _areaRepository = areaRepository;
            _logger = logger;
        }

        public async Task<PagedResult<AreaListItemDto>> GetPagedAreasAsync(AreaFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _areaRepository.GetPagedAsync(filter, cancellationToken);
            var items = new List<AreaListItemDto>();

            foreach (var area in paged.Items)
            {
                items.Add(new AreaListItemDto
                {
                    AreaID = area.AreaID,
                    AreaName = area.AreaName,
                    IsActive = area.IsActive,
                    SubAreaCount = await _areaRepository.CountSubAreasAsync(area.AreaID, cancellationToken),
                    CreatedAt = area.CreatedAt
                });
            }

            return new PagedResult<AreaListItemDto>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditAreaDto?> GetAreaForEditAsync(int areaId, CancellationToken cancellationToken = default)
        {
            var area = await _areaRepository.GetByIdAsync(areaId, cancellationToken);
            if (area == null) return null;

            return new EditAreaDto
            {
                AreaID = area.AreaID,
                AreaName = area.AreaName,
                IsActive = area.IsActive
            };
        }

        public async Task<IReadOnlyList<AreaOptionDto>> GetActiveAreaOptionsAsync(CancellationToken cancellationToken = default)
        {
            var areas = await _areaRepository.GetActiveAsync(cancellationToken);
            return areas.Select(a => new AreaOptionDto
            {
                AreaID = a.AreaID,
                AreaName = a.AreaName
            }).ToList();
        }

        public async Task<bool> IsInUseAsync(int areaId, CancellationToken cancellationToken = default)
        {
            return await _areaRepository.IsInUseAsync(areaId, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateAreaAsync(CreateAreaDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateAsync(dto.AreaName, null, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var area = new Area
                {
                    AreaName = dto.AreaName.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId > 0 ? userId : null,
                    IsDeleted = false
                };

                await _areaRepository.AddAsync(area, cancellationToken);
                await _areaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Area created successfully with ID {AreaID}", area.AreaID);
                return OperationResult<int>.Ok(area.AreaID, "Area created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating area {AreaName}", dto.AreaName);
                return OperationResult<int>.Fail(UserFacingErrorMessages.AreaCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateAreaAsync(EditAreaDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var area = await _areaRepository.GetByIdAsync(dto.AreaID, cancellationToken);
            if (area == null)
            {
                return OperationResult.Fail("Area not found.");
            }

            var errors = await ValidateAsync(dto.AreaName, dto.AreaID, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            try
            {
                area.AreaName = dto.AreaName.Trim();
                area.IsActive = dto.IsActive;
                area.UpdatedAt = DateTime.UtcNow;
                area.UpdatedBy = userId > 0 ? userId : null;

                await _areaRepository.UpdateAsync(area, cancellationToken);
                await _areaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Area updated successfully for ID {AreaID}", area.AreaID);
                return OperationResult.Ok("Area updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating area ID {AreaID}", dto.AreaID);
                return OperationResult.Fail(UserFacingErrorMessages.AreaUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteAreaAsync(int areaId, int userId, CancellationToken cancellationToken = default)
        {
            var area = await _areaRepository.GetByIdAsync(areaId, cancellationToken);
            if (area == null)
            {
                return OperationResult.Fail("Area not found.");
            }

            if (await _areaRepository.IsInUseAsync(areaId, cancellationToken))
            {
                return OperationResult.Fail("Cannot delete area that has sub areas, customers, or sales invoices.");
            }

            try
            {
                await _areaRepository.SoftDeleteAsync(areaId, userId > 0 ? userId : 0, cancellationToken);
                await _areaRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Area soft-deleted successfully for ID {AreaID}", areaId);
                return OperationResult.Ok("Area deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting area ID {AreaID}", areaId);
                return OperationResult.Fail(UserFacingErrorMessages.AreaDeleteFailed);
            }
        }

        private async Task<List<string>> ValidateAsync(
            string areaName,
            int? excludeAreaId,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(areaName))
            {
                errors.Add("Area name is required.");
            }
            else if (await _areaRepository.ExistsByNameAsync(areaName, excludeAreaId, cancellationToken))
            {
                errors.Add("An area with this name already exists.");
            }

            return errors;
        }
    }
}
