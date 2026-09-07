using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Common;
using InventorySystem.DTOs.Warehouses;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _warehouseRepository;
        private readonly ILogger<WarehouseService> _logger;

        public WarehouseService(IWarehouseRepository warehouseRepository, ILogger<WarehouseService> logger)
        {
            _warehouseRepository = warehouseRepository;
            _logger = logger;
        }

        public async Task<PagedResult<WarehouseListItemDto>> GetPagedWarehousesAsync(WarehouseFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _warehouseRepository.GetPagedAsync(filter, cancellationToken);

            var listItems = paged.Items.Select(w => new WarehouseListItemDto
            {
                WarehouseID = w.WarehouseID,
                Name = w.Name,
                Address = w.Address,
                IsActive = w.IsActive,
                IsMain = w.IsMain,
                CreatedAt = w.CreatedAt
            }).ToList();

            return new PagedResult<WarehouseListItemDto>(listItems, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditWarehouseDto?> GetWarehouseForEditAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
            if (warehouse == null) return null;

            return new EditWarehouseDto
            {
                WarehouseID = warehouse.WarehouseID,
                Name = warehouse.Name,
                Address = warehouse.Address,
                IsActive = warehouse.IsActive,
                IsMain = warehouse.IsMain
            };
        }

        public async Task<bool> IsInUseAsync(int warehouseId, CancellationToken cancellationToken = default)
        {
            return await _warehouseRepository.IsInUseAsync(warehouseId, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateWarehouseAsync(CreateWarehouseDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateCreateDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                bool makeMain = dto.IsMain
                    || !await _warehouseRepository.HasAnyMainAsync(null, cancellationToken);

                if (makeMain)
                {
                    await _warehouseRepository.ClearMainFlagsExceptAsync(null, cancellationToken);
                }

                var now = DateTime.UtcNow;
                var warehouse = new Warehouse
                {
                    Name = dto.Name.Trim(),
                    Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                    IsActive = dto.IsActive,
                    IsMain = makeMain,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                await _warehouseRepository.AddAsync(warehouse, cancellationToken);
                await _warehouseRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Warehouse created successfully with ID {WarehouseID}", warehouse.WarehouseID);
                return OperationResult<int>.Ok(warehouse.WarehouseID, "Warehouse created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating warehouse {WarehouseName}", dto.Name);
                return OperationResult<int>.Fail(UserFacingErrorMessages.WarehouseCreateFailed);
            }
        }

        public async Task<OperationResult> UpdateWarehouseAsync(EditWarehouseDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateEditDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseID, cancellationToken);
            if (warehouse == null)
            {
                return OperationResult.Fail("Warehouse not found.");
            }

            try
            {
                if (warehouse.IsMain && !dto.IsMain)
                {
                    return OperationResult.Fail("Cannot remove Main status from the only main warehouse. Mark another warehouse as Main first.");
                }

                if (dto.IsMain)
                {
                    await _warehouseRepository.ClearMainFlagsExceptAsync(dto.WarehouseID, cancellationToken);
                }

                var now = DateTime.UtcNow;
                warehouse.Name = dto.Name.Trim();
                warehouse.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
                warehouse.IsActive = dto.IsActive;
                warehouse.IsMain = dto.IsMain;
                warehouse.UpdatedAt = now;
                warehouse.UpdatedBy = userId;

                await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
                await _warehouseRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Warehouse updated successfully for ID {WarehouseID}", warehouse.WarehouseID);
                return OperationResult.Ok("Warehouse updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating warehouse ID {WarehouseID}", dto.WarehouseID);
                return OperationResult.Fail(UserFacingErrorMessages.WarehouseUpdateFailed);
            }
        }

        public async Task<OperationResult> SoftDeleteWarehouseAsync(int warehouseId, int userId, CancellationToken cancellationToken = default)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
            if (warehouse == null)
            {
                return OperationResult.Fail("Warehouse not found.");
            }

            if (warehouse.IsMain)
            {
                return OperationResult.Fail("Cannot delete the main warehouse. Mark another warehouse as Main first.");
            }

            bool isInUse = await _warehouseRepository.IsInUseAsync(warehouseId, cancellationToken);
            if (isInUse)
            {
                return OperationResult.Fail("This warehouse cannot be deleted because it is used by stock, inventory movements, purchases, or sales.");
            }

            try
            {
                await _warehouseRepository.SoftDeleteAsync(warehouseId, userId, cancellationToken);
                await _warehouseRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Warehouse soft-deleted successfully for ID {WarehouseID}", warehouseId);
                return OperationResult.Ok("Warehouse deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting warehouse ID {WarehouseID}", warehouseId);
                return OperationResult.Fail(UserFacingErrorMessages.WarehouseDeleteFailed);
            }
        }

        private async Task<List<string>> ValidateCreateDtoAsync(CreateWarehouseDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Warehouse Name is required.");
            }
            else if (await _warehouseRepository.ExistsByNameAsync(dto.Name, null, cancellationToken))
            {
                errors.Add("A warehouse with this name already exists.");
            }

            return errors;
        }

        private async Task<List<string>> ValidateEditDtoAsync(EditWarehouseDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Warehouse Name is required.");
            }

            var existing = await _warehouseRepository.GetByIdAsync(dto.WarehouseID, cancellationToken);
            if (existing == null)
            {
                errors.Add("Warehouse not found.");
                return errors;
            }

            if (!string.IsNullOrWhiteSpace(dto.Name)
                && await _warehouseRepository.ExistsByNameAsync(dto.Name, dto.WarehouseID, cancellationToken))
            {
                errors.Add("A warehouse with this name already exists.");
            }

            return errors;
        }
    }
}
