using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Category
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _db;

        public CategoryService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // GET ALL CATEGORIES
        // ---------------------------------------------------------------
        public async Task<CategoryListResult> GetAllCategoriesAsync()
        {
            try
            {
                var categories = await _db.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Type)
                    .ThenBy(c => c.Name)
                    .Select(c => new CategoryResponseDto
                    {
                        CategoryId = c.CategoryId,
                        Name = c.Name,
                        Type = c.Type,
                        Icon = c.Icon,
                        IsDefault = c.IsDefault
                    })
                    .ToListAsync();

                return new CategoryListResult
                {
                    Success = true,
                    Message = $"{categories.Count} category/categories retrieved.",
                    Data = categories
                };
            }
            catch (Exception ex)
            {
                return new CategoryListResult
                {
                    Success = false,
                    Message = $"An error occurred while retrieving categories: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // CREATE CATEGORY
        // ---------------------------------------------------------------
        public async Task<CategoryResult> CreateCategoryAsync(CreateCategoryRequestDto request)
        {
            // Check for duplicate name + type combination
            bool isDuplicate = await _db.Categories.AnyAsync(c =>
                c.Name.ToLower() == request.Name.ToLower() &&
                c.Type.ToLower() == request.Type.ToLower());

            if (isDuplicate)
                return new CategoryResult
                {
                    Success = false,
                    IsDuplicate = true,
                    Message = $"A category named '{request.Name}' of type '{request.Type}' already exists."
                };

            var category = new Models.Entity.Category
            {
                Name = request.Name,
                Type = request.Type,
                Icon = request.Icon,
                IsDefault = request.IsDefault
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return new CategoryResult
            {
                Success = true,
                Message = $"Category '{category.Name}' created successfully.",
                Data = new CategoryResponseDto
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name,
                    Type = category.Type,
                    Icon = category.Icon,
                    IsDefault = category.IsDefault
                }
            };
        }

        // ---------------------------------------------------------------
        // UPDATE CATEGORY
        // ---------------------------------------------------------------
        public async Task<CategoryResult> UpdateCategoryAsync(UpdateCategoryRequestDto request)
        {
            var category = await _db.Categories.FindAsync(request.CategoryId);

            if (category == null)
                return new CategoryResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Category with ID {request.CategoryId} was not found."
                };

            // Check for duplicate name+type on a DIFFERENT category
            bool isDuplicate = await _db.Categories.AnyAsync(c =>
                c.Name.ToLower() == request.Name.ToLower() &&
                c.Type.ToLower() == request.Type.ToLower() &&
                c.CategoryId != request.CategoryId);

            if (isDuplicate)
                return new CategoryResult
                {
                    Success = false,
                    IsDuplicate = true,
                    Message = $"Another category named '{request.Name}' of type '{request.Type}' already exists."
                };

            category.Name = request.Name;
            category.Type = request.Type;
            category.Icon = request.Icon;
            category.IsDefault = request.IsDefault;

            await _db.SaveChangesAsync();

            return new CategoryResult
            {
                Success = true,
                Message = $"Category '{category.Name}' updated successfully.",
                Data = new CategoryResponseDto
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name,
                    Type = category.Type,
                    Icon = category.Icon,
                    IsDefault = category.IsDefault
                }
            };
        }

        // ---------------------------------------------------------------
        // DELETE CATEGORY
        // ---------------------------------------------------------------
        public async Task<CategoryResult> DeleteCategoryAsync(int categoryId)
        {
            var category = await _db.Categories.FindAsync(categoryId);

            if (category == null)
                return new CategoryResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Category with ID {categoryId} was not found."
                };

            // Guard: prevent deleting a category that is referenced by transactions or budgets
            bool hasTransactions = await _db.Transactions
                .AnyAsync(t => t.CategoryId == categoryId);

            bool hasBudgets = await _db.Budgets
                .AnyAsync(b => b.CategoryId == categoryId);

            if (hasTransactions || hasBudgets)
                return new CategoryResult
                {
                    Success = false,
                    NotFound = false,
                    Message = $"Category '{category.Name}' cannot be deleted because it is referenced by " +
                               $"{(hasTransactions ? "transactions" : "")} " +
                               $"{(hasTransactions && hasBudgets ? "and " : "")}" +
                               $"{(hasBudgets ? "budgets" : "")}. " +
                               "Reassign or delete those records first."
                };

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            return new CategoryResult
            {
                Success = true,
                Message = $"Category '{category.Name}' (ID: {categoryId}) deleted successfully."
            };
        }
    }
}
