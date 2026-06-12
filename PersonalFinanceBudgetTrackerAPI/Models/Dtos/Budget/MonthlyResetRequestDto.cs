namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    public class MonthlyResetRequestDto
    {
        /// <summary>Optional override — YYYY-MM format. Defaults to current month.</summary>
        public string? TargetMonth { get; set; }
    }
}
