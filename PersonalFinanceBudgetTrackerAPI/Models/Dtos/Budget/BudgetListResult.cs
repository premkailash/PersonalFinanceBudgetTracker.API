namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    public class BudgetListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<BudgetResponseDto>? Data { get; set; }

    }
}
