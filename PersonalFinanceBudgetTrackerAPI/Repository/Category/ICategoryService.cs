using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Category
{
    public interface ICategoryService
    {
        Task<CategoryListResult> GetAllCategoriesAsync();
        Task<CategoryResult> CreateCategoryAsync(CreateCategoryRequestDto request);
        Task<CategoryResult> UpdateCategoryAsync(UpdateCategoryRequestDto request);
        Task<CategoryResult> DeleteCategoryAsync(int categoryId);

    }
}
