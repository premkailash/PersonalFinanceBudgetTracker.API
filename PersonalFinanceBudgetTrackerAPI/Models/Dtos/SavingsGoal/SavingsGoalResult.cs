namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal
{
    public class SavingsGoalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public SavingsGoalResponseDto? Data { get; set; }

    }
}
