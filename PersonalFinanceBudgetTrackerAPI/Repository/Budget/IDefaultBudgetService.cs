using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    public interface IDefaultBudgetService
    {
        /// <summary>
        /// Seeds default budgets for a single newly-created account.
        /// Uses the current calendar month as the TargetDate month.
        /// </summary>
        Task SeedDefaultBudgetsForAccountAsync(Models.Entity.Account account);

        /// <summary>
        /// Seeds default budgets for ALL active accounts for the given month.
        /// Idempotent — skips any account/category combination that already
        /// has a budget for the target month.
        /// </summary>
        /// <param name="targetMonth">The month to seed (defaults to current month).</param>
        Task<BudgetResetResult> ResetDefaultBudgetsForAllAccountsAsync(
            DateTime? targetMonth = null);
    }

}
