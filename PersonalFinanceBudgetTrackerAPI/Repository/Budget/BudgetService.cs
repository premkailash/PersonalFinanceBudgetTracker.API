using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    public class BudgetService : IBudgetService
    {
        private readonly AppDbContext _db;
        private readonly IBudgetAlertService _alertService;

        public BudgetService(AppDbContext db, IBudgetAlertService alertService)
        {
            _db = db;
            _alertService = alertService;
        }

        // ---------------------------------------------------------------
        // GET BUDGETS BY MONTH
        // ---------------------------------------------------------------
        public async Task<BudgetListResult> GetBudgetsByMonthAsync(int userId, string month)
        {
            try
            {
                if (!TryParseMonth(month, out int year, out int mon))
                    return new BudgetListResult { Success = false, Message = "Invalid month format." };

                var budgets = await _db.Budgets
                    .AsNoTracking()
                    .Include(b => b.Account)
                    .Include(b => b.Category)
                    .Where(b => b.UserId == userId
                             && b.TargetDate.Year == year
                             && b.TargetDate.Month == mon)
                    .OrderBy(b => b.CreatedAt)
                    .Select(b => MapToDto(b))
                    .ToListAsync();

                return new BudgetListResult
                {
                    Success = true,
                    Message = $"{budgets.Count} budget(s) found for {month}.",
                    Data = budgets
                };
            }
            catch (Exception ex)
            {
                return new BudgetListResult { Success = false, Message = ex.Message };
            }
        }

        // ---------------------------------------------------------------
        // GET BUDGET BY ID
        // ---------------------------------------------------------------
        public async Task<BudgetResult> GetBudgetByIdAsync(int budgetId, int callerId)
        {
            var budget = await _db.Budgets
                .AsNoTracking()
                .Include(b => b.Account)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.BudgetId == budgetId);

            if (budget == null)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Budget with ID {budgetId} was not found."
                };

            if (budget.UserId != callerId)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to access this budget."
                };

            return new BudgetResult { Success = true, Data = MapToDto(budget) };
        }

        // ---------------------------------------------------------------
        // CREATE BUDGET
        // ---------------------------------------------------------------
        public async Task<BudgetResult> CreateBudgetAsync(CreateBudgetRequestDto request)
        {
            // One budget per category per user
            bool duplicate = await _db.Budgets.AnyAsync(b =>
                b.UserId == request.UserId &&
                b.TargetDate.Month == request.TargetDate.Month &&
                b.TargetDate.Year == request.TargetDate.Year &&
                b.CategoryId == request.CategoryId);

            if (duplicate)
                return new BudgetResult
                {
                    Success = false,
                    IsDuplicate = true,
                    Message = "A budget for this category already exists for the user."
                };

            var budget = new Models.Entity.Budget
            {
                UserId = request.UserId,
                AccountId = request.AccountId,
                CategoryId = request.CategoryId,
                Name = request.Name,
                TargetAmount = request.TargetAmount,
                CurrentAmount = request.CurrentAmount,
                TargetDate = request.TargetDate,
                AutoContributeAmount = request.AutoContributeAmount,
                CreatedAt = DateTime.UtcNow
            };

            _db.Budgets.Add(budget);
            await _db.SaveChangesAsync();

            // Reload with navigation properties for response
            await _db.Entry(budget).Reference(b => b.Account).LoadAsync();
            await _db.Entry(budget).Reference(b => b.Category).LoadAsync();

            // ── Budget-alert check ───────────────────────────────────────────
            // Covers the edge-case where a budget is created with a CurrentAmount
            // that already meets a threshold (e.g. importing historical data).
            await _alertService.EvaluateAndNotifyAsync(budget);


            return new BudgetResult
            {
                Success = true,
                Message = $"Budget '{budget.Name}' created successfully.",
                Data = MapToDto(budget)
            };
        }

        // ---------------------------------------------------------------
        // UPDATE BUDGET
        // ---------------------------------------------------------------
        public async Task<BudgetResult> UpdateBudgetAsync(UpdateBudgetRequestDto request, int callerId)
        {
            var budget = await _db.Budgets
                .Include(b => b.Account)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.BudgetId == request.BudgetId);

            if (budget == null)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Budget with ID {request.BudgetId} was not found."
                };

            if (budget.UserId != callerId)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to update this budget."
                };

            budget.Name = request.Name;
            budget.TargetAmount = request.TargetAmount;
            budget.CurrentAmount = request.CurrentAmount;
            budget.TargetDate = request.TargetDate;
            budget.AutoContributeAmount = request.AutoContributeAmount;

            await _db.SaveChangesAsync();

            // ── Budget-alert check ───────────────────────────────────────────
            // Fires after every update so that direct edits to CurrentAmount
            // (e.g. via the admin panel or API) also trigger threshold alerts.
            await _alertService.EvaluateAndNotifyAsync(budget);

            return new BudgetResult
            {
                Success = true,
                Message = $"Budget '{budget.Name}' updated successfully.",
                Data = MapToDto(budget)
            };
        }

        // ---------------------------------------------------------------
        // DELETE BUDGET
        // ---------------------------------------------------------------
        public async Task<BudgetResult> DeleteBudgetAsync(int budgetId, int callerId)
        {
            var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId);

            if (budget == null)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Budget with ID {budgetId} was not found."
                };

            if (budget.UserId != callerId)
                return new BudgetResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to delete this budget."
                };

            _db.Budgets.Remove(budget);
            await _db.SaveChangesAsync();

            return new BudgetResult
            {
                Success = true,
                Message = $"Budget with ID {budgetId} deleted successfully."
            };
        }

        // ---------------------------------------------------------------
        // GET BUDGET UTILIZATION
        // ---------------------------------------------------------------
        public async Task<BudgetListResult> GetBudgetUtilizationAsync(int userId, string month)
        {
            try
            {
                if (!TryParseMonth(month, out int year, out int mon))
                    return new BudgetListResult { Success = false, Message = "Invalid month format." };

                var budgets = await _db.Budgets
                    .AsNoTracking()
                    .Include(b => b.Account)
                    .Include(b => b.Category)
                    .Where(b => b.UserId == userId
                             && b.TargetDate.Year == year
                             && b.TargetDate.Month == mon)
                    .OrderBy(b => b.CreatedAt)
                    .Select(b => MapToDto(b))
                    .ToListAsync();

                return new BudgetListResult
                {
                    Success = true,
                    Message = $"{budgets.Count} budget utilization record(s) found for {month}.",
                    Data = budgets
                };
            }
            catch (Exception ex)
            {
                return new BudgetListResult { Success = false, Message = ex.Message };
            }
        }

        // ---------------------------------------------------------------
        // Private Helpers
        // ---------------------------------------------------------------
        private static bool TryParseMonth(string month, out int year, out int mon)
        {
            year = 0; mon = 0;
            if (string.IsNullOrWhiteSpace(month) || !month.Contains('-')) return false;
            var parts = month.Split('-');
            return parts.Length == 2
                && int.TryParse(parts[0], out year)
                && int.TryParse(parts[1], out mon)
                && mon >= 1 && mon <= 12;
        }

        private static BudgetResponseDto MapToDto(Models.Entity.Budget b) => new BudgetResponseDto
        {
            BudgetId = b.BudgetId,
            UserId = b.UserId,
            AccountId = b.AccountId,
            AccountName = b.Account?.AccountName ?? string.Empty,
            CategoryId = b.CategoryId,
            CategoryName = b.Category?.Name ?? string.Empty,
            Name = b.Name,
            TargetAmount = b.TargetAmount,
            CurrentAmount = b.CurrentAmount,
            TargetDate = b.TargetDate,
            AutoContributeAmount = b.AutoContributeAmount,
            CreatedAt = b.CreatedAt
        };
    }

}
