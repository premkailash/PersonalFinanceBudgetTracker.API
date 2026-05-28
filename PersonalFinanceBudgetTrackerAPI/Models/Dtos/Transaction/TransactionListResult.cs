namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class TransactionListResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; } = false;
        public IEnumerable<TransactionResponseDto>? Data { get; set; }

    }
}
