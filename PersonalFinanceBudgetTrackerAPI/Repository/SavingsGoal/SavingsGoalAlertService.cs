using PersonalFinanceBudgetTrackerAPI.Context;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Repository.Notification;

namespace PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal
{
    public class SavingsGoalAlertService : ISavingsGoalAlertService
    {
        // ── Threshold constants ──────────────────────────────────────────────
        private const decimal Threshold50 = 0.50m;
        private const decimal Threshold100 = 1.00m;

        private const string Type50 = "GoalAlert50";
        private const string Type100 = "GoalAlert100";

        private readonly AppDbContext _db;
        private readonly INotificationService _notificationService;

        public SavingsGoalAlertService(AppDbContext db,INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        /// <inheritdoc />
        public async Task EvaluateAndNotifyAsync(Models.Entity.SavingsGoal goal)
        {
            // Guard: cannot compute progress without a positive target
            if (goal.TargetAmount <= 0) return;

            // Effective amount mirrors MapToDto
            decimal effectiveAmount = goal.CurrentAmount + goal.AutoContributeAmount;
            decimal progress = effectiveAmount / goal.TargetAmount;

            // Evaluate both thresholds independently so a single large
            // contribution that jumps from 0 → 100 % creates both alerts.
            await TrySendAlertAsync(goal, progress, Threshold50, Type50);
            await TrySendAlertAsync(goal, progress, Threshold100, Type100);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task TrySendAlertAsync(
            Models.Entity.SavingsGoal goal,
            decimal progress,
            decimal threshold,
            string notificationType)
        {
            // Has the threshold been reached or exceeded?
            if (progress < threshold) return;

            // Use the goal's TargetDate year-month as the "goal month"
            // for the duplicate check — one alert per goal per type per month.
            int alertYear = goal.TargetDate.Year;
            int alertMonth = goal.TargetDate.Month;

            // ── Duplicate guard ─────────────────────────────────────────────
            bool alreadySent = await _db.Notifications.AnyAsync(n =>
                n.UserId == goal.UserId &&
                n.Type == notificationType &&
                n.CreatedAt.Year == alertYear &&
                n.CreatedAt.Month == alertMonth &&
                n.Message.Contains($"Goal ID {goal.GoalId}"));

            if (alreadySent) return;

            // ── Build the human-readable message ────────────────────────────
            string pctLabel = threshold >= Threshold100 ? "100%" : "50%";
            decimal effective = goal.CurrentAmount + goal.AutoContributeAmount;

            string message =
                $"Goal \"{goal.Name}\" (Goal ID {goal.GoalId}) " +
                $"has reached {pctLabel} of its target " +
                $"({effective:N0} / {goal.TargetAmount:N0}).";

            await _notificationService.CreateNotificationAsync(new Models.Dtos.Notification.CreateNotificationRequestDto
            {
                UserId = goal.UserId,
                Message = message,
                Type = notificationType
            });            
        }
    }

}
