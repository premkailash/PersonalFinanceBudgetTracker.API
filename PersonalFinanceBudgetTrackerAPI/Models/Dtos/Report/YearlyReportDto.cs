namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class YearlyReportDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetAmount { get; set; }
    }
}
