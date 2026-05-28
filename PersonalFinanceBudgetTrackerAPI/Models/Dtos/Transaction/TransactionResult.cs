namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class TransactionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public TransactionResponseDto? Data { get; set; }

    }
}
