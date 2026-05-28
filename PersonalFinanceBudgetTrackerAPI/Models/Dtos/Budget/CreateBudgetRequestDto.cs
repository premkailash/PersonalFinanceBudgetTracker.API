using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    public class CreateBudgetRequestDto
    {
        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "AccountId is required.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "CategoryId is required.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Budget name is required.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "TargetAmount must be greater than zero.")]
        public decimal TargetAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "CurrentAmount must be zero or greater.")]
        public decimal CurrentAmount { get; set; } = 0.00m;

        [Required(ErrorMessage = "TargetDate is required.")]
        public DateTime TargetDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "AutoContributeAmount must be zero or greater.")]
        public decimal AutoContributeAmount { get; set; } = 0.00m;

    }
}
