using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.DTOs.Categories;
using InventorySystem.DTOs.Common;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            ICategoryRepository categoryRepository,
            ILogger<CategoryService> logger)
        {
            _categoryRepository = categoryRepository;
            _logger = logger;
        }

        public async Task<PagedResult<CategoryListItemDto>> GetPagedCategoriesAsync(CategoryFilterDto filter, CancellationToken cancellationToken = default)
        {
            var paged = await _categoryRepository.GetPagedAsync(filter, cancellationToken);

            var listItems = paged.Items.Select(c => new CategoryListItemDto
            {
                CategoryID = c.CategoryID,
                Name = c.Name,
                CompanyID = c.CompanyID,
                CompanyName = c.Company?.CompanyName ?? string.Empty,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            }).ToList();

            return new PagedResult<CategoryListItemDto>(listItems, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<EditCategoryDto?> GetCategoryForEditAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
            if (category == null) return null;

            return new EditCategoryDto
            {
                CategoryID = category.CategoryID,
                CompanyID = category.CompanyID,
                Name = category.Name,
                IsActive = category.IsActive
            };
        }

        public async Task<bool> HasProductsAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _categoryRepository.HasProductsAsync(categoryId, cancellationToken);
        }

        public async Task<OperationResult<int>> CreateCategoryAsync(CreateCategoryDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateCreateDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult<int>.Fail(errors);
            }

            try
            {
                var now = DateTime.UtcNow;
                var category = new Category
                {
                    CompanyID = dto.CompanyID,
                    Name = dto.Name.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                await _categoryRepository.AddAsync(category, cancellationToken);
                await _categoryRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Category created successfully with ID {CategoryID}", category.CategoryID);
                return OperationResult<int>.Ok(category.CategoryID, "Category created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating category {CategoryName}", dto.Name);
                return OperationResult<int>.Fail($"Failed to create category: {ex.Message}");
            }
        }

        public async Task<OperationResult> UpdateCategoryAsync(EditCategoryDto dto, int userId, CancellationToken cancellationToken = default)
        {
            var errors = await ValidateEditDtoAsync(dto, cancellationToken);
            if (errors.Any())
            {
                return OperationResult.Fail(errors);
            }

            var category = await _categoryRepository.GetByIdAsync(dto.CategoryID, cancellationToken);
            if (category == null)
            {
                return OperationResult.Fail("Category not found.");
            }

            try
            {
                var now = DateTime.UtcNow;
                category.CompanyID = dto.CompanyID;
                category.Name = dto.Name.Trim();
                category.IsActive = dto.IsActive;
                category.UpdatedAt = now;
                category.UpdatedBy = userId;

                await _categoryRepository.UpdateAsync(category, cancellationToken);
                await _categoryRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Category updated successfully for ID {CategoryID}", category.CategoryID);
                return OperationResult.Ok("Category updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating category ID {CategoryID}", dto.CategoryID);
                return OperationResult.Fail($"Failed to update category: {ex.Message}");
            }
        }

        public async Task<OperationResult> SoftDeleteCategoryAsync(int categoryId, int userId, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
            if (category == null)
            {
                return OperationResult.Fail("Category not found.");
            }

            bool hasProducts = await _categoryRepository.HasProductsAsync(categoryId, cancellationToken);
            if (hasProducts)
            {
                return OperationResult.Fail("This category cannot be deleted because it is assigned to one or more products.");
            }

            try
            {
                await _categoryRepository.SoftDeleteAsync(categoryId, userId, cancellationToken);
                await _categoryRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Category soft-deleted successfully for ID {CategoryID}", categoryId);
                return OperationResult.Ok("Category deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting category ID {CategoryID}", categoryId);
                return OperationResult.Fail($"Failed to delete category: {ex.Message}");
            }
        }

        private async Task<List<string>> ValidateCreateDtoAsync(CreateCategoryDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Category Name is required.");
            }

            if (dto.CompanyID <= 0)
            {
                errors.Add("Company is required.");
            }
            else if (!await _categoryRepository.CompanyExistsAsync(dto.CompanyID, cancellationToken))
            {
                errors.Add("Selected company does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.CompanyID > 0)
            {
                bool exists = await _categoryRepository.ExistsByNameAsync(dto.CompanyID, dto.Name, null, cancellationToken);
                if (exists)
                {
                    errors.Add("A category with this name already exists for the selected company.");
                }
            }

            return errors;
        }

        private async Task<List<string>> ValidateEditDtoAsync(EditCategoryDto dto, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Category Name is required.");
            }

            if (dto.CompanyID <= 0)
            {
                errors.Add("Company is required.");
            }
            else if (!await _categoryRepository.CompanyExistsAsync(dto.CompanyID, cancellationToken))
            {
                errors.Add("Selected company does not exist.");
            }

            var existing = await _categoryRepository.GetByIdAsync(dto.CategoryID, cancellationToken);
            if (existing == null)
            {
                errors.Add("Category not found.");
                return errors;
            }

            if (existing.CompanyID != dto.CompanyID)
            {
                bool hasProducts = await _categoryRepository.HasProductsAsync(dto.CategoryID, cancellationToken);
                if (hasProducts)
                {
                    errors.Add("Cannot change the company because this category is assigned to one or more products.");
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.CompanyID > 0)
            {
                bool exists = await _categoryRepository.ExistsByNameAsync(dto.CompanyID, dto.Name, dto.CategoryID, cancellationToken);
                if (exists)
                {
                    errors.Add("A category with this name already exists for the selected company.");
                }
            }

            return errors;
        }
    }
}
