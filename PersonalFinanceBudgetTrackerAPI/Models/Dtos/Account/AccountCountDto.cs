namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account
{
    public class AccountCountDto
    {
        /// <summary>Total accounts rows in the database (active + inactive).</summary>
        public int TotalAccounts { get; set; }

        /// <summary>Accounts where IsActive = true.</summary>
        public int ActiveAccounts { get; set; }

        /// <summary>Accounts where IsActive = false (unlinked).</summary>
        public int InactiveAccounts => TotalAccounts - ActiveAccounts;
    }
}
