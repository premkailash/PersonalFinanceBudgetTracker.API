using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal
{
    public class SavingsGoalService : ISavingsGoalService
    {
        private readonly AppDbContext _db;
        private readonly ISavingsGoalAlertService _alertService;
        public SavingsGoalService(AppDbContext db, ISavingsGoalAlertService alertService)
        {
            _db = db;
            _alertService = alertService;
        }

        // ---------------------------------------------------------------
        // GET ALL GOALS  (currentAmount = CurrentAmount + AutoContributeAmount)
        // ---------------------------------------------------------------
        public async Task<SavingsGoalListResult> GetAllGoalsAsync(int userId)
        {
            try
            {
                var goals = await _db.SavingsGoals
                    .AsNoTracking()
                    .Where(g => g.UserId == userId)
                    .OrderBy(g => g.CreatedAt)
                    .Select(g => MapToDto(g))
                    .ToListAsync();

                return new SavingsGoalListResult
                {
                    Success = true,
                    Message = $"{goals.Count} goal(s) retrieved successfully.",
                    Data = goals
                };
            }
            catch (Exception ex)
            {
                return new SavingsGoalListResult { Success = false, Message = ex.Message };
            }
        }

        // ---------------------------------------------------------------
        // GET GOAL BY ID
        // ---------------------------------------------------------------
        public async Task<SavingsGoalResult> GetGoalByIdAsync(int goalId, int callerId)
        {
            var goal = await _db.SavingsGoals
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GoalId == goalId);

            if (goal == null)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Savings goal with ID {goalId} was not found."
                };

            if (goal.UserId != callerId)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to access this savings goal."
                };

            return new SavingsGoalResult { Success = true, Data = MapToDto(goal) };
        }

        // ---------------------------------------------------------------
        // CREATE GOAL
        // ---------------------------------------------------------------
        public async Task<SavingsGoalResult> CreateGoalAsync(CreateSavingsGoalRequestDto request)
        {
            try
            {
                var goal = new Models.Entity.SavingsGoal
                {
                    UserId = request.UserId,
                    AccountId = request.AccountId,
                    Name = request.Name,
                    TargetAmount = request.TargetAmount,
                    CurrentAmount = request.CurrentAmount,
                    TargetDate = request.TargetDate,
                    AutoContributeAmount = request.AutoContributeAmount,
                    CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt
                };

                _db.SavingsGoals.Add(goal);
                await _db.SaveChangesAsync();

                // ── Goal-alert check ─────────────────────────────────────────
                // Covers the edge case where a goal is created with a CurrentAmount
                // that already meets a threshold (e.g. importing historical data).
                await _alertService.EvaluateAndNotifyAsync(goal);


                return new SavingsGoalResult
                {
                    Success = true,
                    Message = $"Savings goal '{goal.Name}' created successfully.",
                    Data = MapToDto(goal)
                };
            }
            catch (Exception ex)
            {
                return new SavingsGoalResult { Success = false, Message = ex.Message };
            }
        }

        // ---------------------------------------------------------------
        // UPDATE GOAL
        // ---------------------------------------------------------------
        public async Task<SavingsGoalResult> UpdateGoalAsync(
            UpdateSavingsGoalRequestDto request,
            int callerId)
        {
            var goal = await _db.SavingsGoals
                .FirstOrDefaultAsync(g => g.GoalId == request.GoalId);

            if (goal == null)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Savings goal with ID {request.GoalId} was not found."
                };

            if (goal.UserId != callerId)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to update this savings goal."
                };

            goal.TargetAmount = request.TargetAmount;
            goal.CurrentAmount = request.CurrentAmount;
            goal.TargetDate = request.TargetDate;
            goal.AutoContributeAmount = request.AutoContributeAmount;

            await _db.SaveChangesAsync();


            // ── Goal-alert check ─────────────────────────────────────────────
            // Fires on every update so that direct edits to CurrentAmount
            // (e.g. via the UI edit form) also trigger threshold alerts.
            await _alertService.EvaluateAndNotifyAsync(goal);

            return new SavingsGoalResult
            {
                Success = true,
                Message = $"Savings goal '{goal.Name}' updated successfully.",
                Data = MapToDto(goal)
            };
        }

        // ---------------------------------------------------------------
        // DELETE GOAL
        // ---------------------------------------------------------------
        public async Task<SavingsGoalResult> DeleteGoalAsync(int goalId, int callerId)
        {
            var goal = await _db.SavingsGoals
                .FirstOrDefaultAsync(g => g.GoalId == goalId);

            if (goal == null)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Savings goal with ID {goalId} was not found."
                };

            if (goal.UserId != callerId)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to delete this savings goal."
                };

            _db.SavingsGoals.Remove(goal);
            await _db.SaveChangesAsync();

            return new SavingsGoalResult
            {
                Success = true,
                Message = $"Savings goal with ID {goalId} deleted successfully."
            };
        }

        // ---------------------------------------------------------------
        // CONTRIBUTE  (accumulates AutoContributeAmount)
        // ---------------------------------------------------------------
        public async Task<SavingsGoalResult> ContributeAsync(
            ContributeRequestDto request,
            int callerId)
        {
            var goal = await _db.SavingsGoals
                .FirstOrDefaultAsync(g => g.GoalId == request.GoalId);

            if (goal == null)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Savings goal with ID {request.GoalId} was not found."
                };

            if (goal.UserId != callerId)
                return new SavingsGoalResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to contribute to this savings goal."
                };

            // Accumulate: existing + new contribution
            goal.AutoContributeAmount += request.AutoContributeAmount;

            await _db.SaveChangesAsync();

            // ── Goal-alert check ─────────────────────────────────────────────
            // Contributions always increase the effective amount, so we always
            // evaluate after a successful save.
            await _alertService.EvaluateAndNotifyAsync(goal);

            return new SavingsGoalResult
            {
                Success = true,
                Message = $"Contribution of {request.AutoContributeAmount:C} added. " +
                          $"New AutoContributeAmount: {goal.AutoContributeAmount:C}.",
                Data = MapToDto(goal)
            };
        }

        // ---------------------------------------------------------------
        // Private helper — maps entity to DTO
        // currentAmount in response = CurrentAmount + AutoContributeAmount
        // ---------------------------------------------------------------
        private static SavingsGoalResponseDto MapToDto(Models.Entity.SavingsGoal g) =>
            new SavingsGoalResponseDto
            {
                GoalId = g.GoalId,
                UserId = g.UserId,
                AccountId = g.AccountId,
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount + g.AutoContributeAmount,
                TargetDate = g.TargetDate,
                CreatedAt = g.CreatedAt
            };
    }

}
