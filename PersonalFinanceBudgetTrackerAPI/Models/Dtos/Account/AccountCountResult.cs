namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account
{
    public class AccountCountResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AccountCountDto? Data { get; set; }
    }
}
