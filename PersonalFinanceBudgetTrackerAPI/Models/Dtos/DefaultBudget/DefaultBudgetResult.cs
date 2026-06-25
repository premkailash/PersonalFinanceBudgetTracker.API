using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget
{
    public class DefaultBudgetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public bool Conflict { get; set; }
        public DefaultBudgetDto? Data { get; set; }

    }
}
