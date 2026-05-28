namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class AccountNetWorthDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsAsset { get; set; }   // false = liability (Credit)

    }
}
