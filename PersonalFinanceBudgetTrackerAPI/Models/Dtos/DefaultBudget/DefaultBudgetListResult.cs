namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget
{
    public class DefaultBudgetListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<DefaultBudgetDto>? Data { get; set; }

    }
}
