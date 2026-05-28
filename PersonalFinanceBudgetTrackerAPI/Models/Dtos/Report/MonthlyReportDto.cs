namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class MonthlyReportDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;   // YYYY-MM
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetAmount { get; set; }                    // Income - Expense

    }
}
