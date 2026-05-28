namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    public class BudgetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public bool IsDuplicate { get; set; }
        public BudgetResponseDto? Data { get; set; }

    }
}
