namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    /// <summary>
    /// Result returned by the monthly Lambda reset operation.
    /// </summary>
    public class BudgetResetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalAccounts { get; set; }
        public int BudgetsCreated { get; set; }
        public int BudgetsSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

}
