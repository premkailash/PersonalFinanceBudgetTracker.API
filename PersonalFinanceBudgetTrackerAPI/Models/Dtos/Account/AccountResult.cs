namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account
{
    public class AccountResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public bool IsDuplicate { get; set; }
        public AccountResponseDto? Data { get; set; }

    }
}
