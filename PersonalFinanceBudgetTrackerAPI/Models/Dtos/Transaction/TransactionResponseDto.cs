namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class TransactionResponseDto
    {
        public int TransactionId { get; set; }
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
