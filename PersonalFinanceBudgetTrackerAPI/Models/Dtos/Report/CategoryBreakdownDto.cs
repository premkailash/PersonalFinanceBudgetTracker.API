namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class CategoryBreakdownDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;   // Income | Expense
        public decimal Total { get; set; }
        public int Count { get; set; }

    }
}
