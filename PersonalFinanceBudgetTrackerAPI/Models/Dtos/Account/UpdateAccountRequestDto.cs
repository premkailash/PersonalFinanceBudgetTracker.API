using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account
{
    public class UpdateAccountRequestDto
    {
        [Required(ErrorMessage = "AccountId is required.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Account name is required.")]
        [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
        public string AccountName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account type is required.")]
        [RegularExpression("^(Bank|Wallet|Credit|Investment)$",
            ErrorMessage = "AccountType must be one of: Bank, Wallet, Credit, Investment.")]
        public string AccountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Currency is required.")]
        [MaxLength(10, ErrorMessage = "Currency code cannot exceed 10 characters.")]
        public string Currency { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Balance must be zero or greater.")]
        public decimal Balance { get; set; }

    }
}
