namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account
{
    public class AccountListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<AccountResponseDto>? Data { get; set; }

    }
}
