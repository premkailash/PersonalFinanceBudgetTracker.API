using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget;

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

        // ── Admin CRUD for DefaultBudget templates ────────────────────────────

        /// <summary>Returns all DefaultBudget template rows.</summary>
        Task<DefaultBudgetListResult> GetAllDefaultBudgetsAsync();

        /// <summary>Creates a new DefaultBudget template row.</summary>
        Task<DefaultBudgetResult> CreateDefaultBudgetAsync(
            CreateDefaultBudgetRequestDto request);

        /// <summary>Updates an existing DefaultBudget template row.</summary>
        Task<DefaultBudgetResult> UpdateDefaultBudgetAsync(
            int defaultBudgetId,
            UpdateDefaultBudgetRequestDto request);

        /// <summary>
        /// Deletes (hard-delete) a DefaultBudget template row.
        /// Does NOT delete user Budgets that were seeded from this template.
        /// </summary>
        Task<DefaultBudgetResult> DeleteDefaultBudgetAsync(int defaultBudgetId);

    }

}
