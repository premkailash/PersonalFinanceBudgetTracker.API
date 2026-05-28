namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class ImportedTransactionDto
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Type { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
        public bool IsRecurring { get; set; } = false;

    }
}
