using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class CreateTransactionRequestDto
    {
        [Required(ErrorMessage = "AccountId is required.")]
        public int AccountId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Currency is required.")]
        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(Income|Expense)$",
            ErrorMessage = "Type must be 'Income' or 'Expense'.")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "CategoryId is required.")]
        public int CategoryId { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "TransactionDate is required.")]
        public DateTime TransactionDate { get; set; }

        public bool IsRecurring { get; set; } = false;

    }
}
