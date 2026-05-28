using PersonalFinanceBudgetTrackerAPI.Context;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Repository.Notification;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Budget
{
    public class BudgetAlertService : IBudgetAlertService
    {
        private const decimal Threshold80 = 0.80m;
        private const decimal Threshold100 = 1.00m;

        private const string Type80 = "BudgetAlert80";
        private const string Type100 = "BudgetAlert100";

        private readonly AppDbContext _db;
        private readonly INotificationService _notificationService;

        public BudgetAlertService(AppDbContext db,INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService; 
        }

        /// <inheritdoc />
        public async Task EvaluateAndNotifyAsync(Models.Entity.Budget budget)
        {
            // Guard: cannot compute utilisation without a positive target
            if (budget.TargetAmount <= 0) return;

            decimal utilisation = budget.CurrentAmount / budget.TargetAmount;

            // Evaluate both thresholds in a single method call so we can send
            // both notifications in one round-trip if a large transaction jumps
            // straight from 0 % to 100 %+.
            await TrySendAlertAsync(budget, utilisation, Threshold80, Type80);
            await TrySendAlertAsync(budget, utilisation, Threshold100, Type100);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task TrySendAlertAsync(
            Models.Entity.Budget budget,
            decimal utilisation,
            decimal threshold,
            string notificationType)
        {
            // Has the threshold been reached or exceeded?
            if (utilisation < threshold) return;

            // Use the budget's TargetDate year-month as the "budget month" for
            // the duplicate check — this matches the business rule described in
            // the spec (one alert per budget per type per month).
            int alertYear = budget.TargetDate.Year;
            int alertMonth = budget.TargetDate.Month;

            // ── Duplicate guard ─────────────────────────────────────────────
            // Check for an existing notification for this user + budget type
            // within the same calendar month by embedding the budget name in
            // the message check. We match on both Type and a partial message
            // containing the BudgetId to make the check resilient to name changes.
            bool alreadySent = await _db.Notifications.AnyAsync(n =>
                n.UserId == budget.UserId &&
                n.Type == notificationType &&
                n.CreatedAt.Year == alertYear &&
                n.CreatedAt.Month == alertMonth &&
                n.Message.Contains($"Budget ID {budget.BudgetId}"));

            if (alreadySent) return;

            // ── Build the notification message ──────────────────────────────
            string pctLabel = threshold >= Threshold100 ? "100%" : "80%";
            string message = $"Budget \"{budget.Name}\" (Budget ID {budget.BudgetId}) " +
                               $"has reached {pctLabel} of its target " +
                               $"({budget.CurrentAmount:N0} / {budget.TargetAmount:N0}).";

            
            await _notificationService.CreateNotificationAsync(new Models.Dtos.Notification.CreateNotificationRequestDto
            {
                UserId = budget.UserId,
                Message = message,
                Type = notificationType
            });                        
        }
    }

}
