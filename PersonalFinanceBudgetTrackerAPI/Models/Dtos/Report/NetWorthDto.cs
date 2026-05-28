namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report
{
    public class NetWorthDto
    {
        public DateTime SnapshotDate { get; set; }
        public decimal TotalAssets { get; set; }    // sum of Bank + Investment + Wallet balances
        public decimal TotalLiabilit { get; set; }    // sum of Credit account balances
        public decimal NetWorth { get; set; }    // Assets - Liabilities
        public List<AccountNetWorthDto> Accounts { get; set; } = new();

    }
}
