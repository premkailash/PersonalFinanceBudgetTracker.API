using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    public interface IBudgetService
    {
        Task<BudgetListResult> GetBudgetsByMonthAsync(int userId, string month);
        Task<BudgetResult> GetBudgetByIdAsync(int budgetId, int callerId);
        Task<BudgetResult> CreateBudgetAsync(CreateBudgetRequestDto request);
        Task<BudgetResult> UpdateBudgetAsync(UpdateBudgetRequestDto request, int callerId);
        Task<BudgetResult> DeleteBudgetAsync(int budgetId, int callerId);
        Task<BudgetListResult> GetBudgetUtilizationAsync(int userId, string month);

    }
}
