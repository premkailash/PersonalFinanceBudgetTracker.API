namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal
{
    public class SavingsGoalListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<SavingsGoalResponseDto>? Data { get; set; }

    }
}
