using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    /// <summary>
    /// Implements <see cref="IDefaultBudgetService"/>.
    ///
    /// How seeds are created
    /// ──────────────────────
    /// For each active DefaultBudget template:
    ///   1. Check whether a Budget already exists for
    ///      (UserId, AccountId, CategoryId, TargetDate.Year, TargetDate.Month).
    ///   2. If not → insert a new Budget row with CurrentAmount = 0.
    ///   3. If yes → skip (idempotent — safe to call multiple times).
    ///
    /// TargetDate is set to the LAST DAY of the target month so the Budget
    /// aligns with the monthly reporting boundary used by the Reports API.
    ///
    /// Month-specific DefaultBudget rows (EffectiveMonth = 'YYYY-MM') take
    /// precedence over global rows (EffectiveMonth IS NULL) for the same
    /// category in the same month.
    /// </summary>
    public class DefaultBudgetService : IDefaultBudgetService

    {
        private readonly AppDbContext _db;
        private readonly ILogService _logService;

        public DefaultBudgetService(AppDbContext db, ILogService logService)
        {
            _db = db;
            _logService = logService;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Seed on account creation
        // ══════════════════════════════════════════════════════════════════════

        public async Task SeedDefaultBudgetsForAccountAsync(Models.Entity.Account account)
        {
            var now = DateTime.UtcNow;
            var targetMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthLabel = now.ToString("yyyy-MM");

            // Resolve effective templates for this month:
            // Month-specific rows override global rows for the same category.
            var templates = await ResolveEffectiveTemplatesAsync(monthLabel);

            if (!templates.Any()) return;

            int created = 0;

            foreach (var template in templates)
            {
                bool exists = await _db.Budgets.AnyAsync(b =>
                    b.UserId == account.UserId &&
                    b.AccountId == account.AccountId &&
                    b.CategoryId == template.CategoryId &&
                    b.TargetDate.Year == targetMonth.Year &&
                    b.TargetDate.Month == targetMonth.Month);

                if (exists) continue;

                _db.Budgets.Add(new Models.Entity.Budget
                {
                    UserId = account.UserId,
                    AccountId = account.AccountId,
                    CategoryId = template.CategoryId,
                    Name = template.Name,
                    TargetAmount = template.TargetAmount,
                    CurrentAmount = 0m,
                    AutoContributeAmount = template.AutoContributeAmount,
                    TargetDate = LastDayOfMonth(targetMonth),
                    CreatedAt = DateTime.UtcNow
                });

                created++;
            }

            if (created > 0)
            {
                await _db.SaveChangesAsync();

                await _logService.CreateLogAsync(new CreateLogRequestDto
                {
                    Event = $"Seeded {created} default budget(s) for Account {account.AccountId} " +
                                $"(User {account.UserId}) for {monthLabel}.",
                    EventType = "Budget Created",
                    UserId = account.UserId
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Monthly reset (Lambda trigger)
        // ══════════════════════════════════════════════════════════════════════

        public async Task<BudgetResetResult> ResetDefaultBudgetsForAllAccountsAsync(
            DateTime? targetMonth = null)
        {
            // Default to current month when called without a parameter
            var now = targetMonth ?? DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthLabel = monthStart.ToString("yyyy-MM");
            var monthEnd = LastDayOfMonth(monthStart);

            var result = new BudgetResetResult();

            // Resolve effective templates for the target month once —
            // same set applied to every account
            var templates = await ResolveEffectiveTemplatesAsync(monthLabel);

            if (!templates.Any())
            {
                result.Success = true;
                result.Message = $"No active default budget templates found for {monthLabel}.";
                return result;
            }

            // All active accounts across all users
            var activeAccounts = await _db.Accounts
                .AsNoTracking()
                .Where(a => a.IsActive)
                .ToListAsync();

            result.TotalAccounts = activeAccounts.Count;

            foreach (var account in activeAccounts)
            {
                try
                {
                    foreach (var template in templates)
                    {
                        bool exists = await _db.Budgets.AnyAsync(b =>
                            b.UserId == account.UserId &&
                            b.AccountId == account.AccountId &&
                            b.CategoryId == template.CategoryId &&
                            b.TargetDate.Year == monthStart.Year &&
                            b.TargetDate.Month == monthStart.Month);

                        if (exists)
                        {
                            result.BudgetsSkipped++;
                            continue;
                        }

                        _db.Budgets.Add(new Models.Entity.Budget
                        {
                            UserId = account.UserId,
                            AccountId = account.AccountId,
                            CategoryId = template.CategoryId,
                            Name = template.Name,
                            TargetAmount = template.TargetAmount,
                            CurrentAmount = 0m,
                            AutoContributeAmount = template.AutoContributeAmount,
                            TargetDate = monthEnd,
                            CreatedAt = DateTime.UtcNow
                        });

                        result.BudgetsCreated++;
                    }

                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    result.Errors.Add(
                        $"Account {account.AccountId} ({account.AccountName}): {ex.Message}");
                }
            }

            result.Success = true;
            result.Message =
                $"Monthly reset complete for {monthLabel}. " +
                $"Created: {result.BudgetsCreated}, " +
                $"Skipped: {result.BudgetsSkipped}, " +
                $"Errors: {result.Errors.Count}.";

            // System audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = result.Message,
                EventType = "System",
                UserId = null
            });

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Private helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the effective default budget templates for the given month.
        /// Month-specific rows (EffectiveMonth = 'YYYY-MM') take precedence
        /// over global rows (EffectiveMonth IS NULL) for the same category.
        /// </summary>
        private async Task<List<DefaultBudget>> ResolveEffectiveTemplatesAsync(
            string monthLabel)
        {
            var allActive = await _db.DefaultBudgets
                .AsNoTracking()
                .Where(d => d.IsActive &&
                            (d.EffectiveMonth == null ||
                             d.EffectiveMonth == monthLabel))
                .ToListAsync();

            // Group by CategoryId; prefer month-specific over global
            return allActive
                .GroupBy(d => d.CategoryId)
                .Select(g =>
                    g.FirstOrDefault(d => d.EffectiveMonth == monthLabel)
                    ?? g.First())
                .ToList();
        }

        private static DateTime LastDayOfMonth(DateTime month)
        {
            return new DateTime(
                month.Year,
                month.Month,
                DateTime.DaysInMonth(month.Year, month.Month),
                23, 59, 59,
                DateTimeKind.Utc);
        }

    }
}
