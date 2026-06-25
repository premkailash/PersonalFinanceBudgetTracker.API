namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget
{
    public class CreateDefaultBudgetRequestDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public int CategoryId { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue,
            ErrorMessage = "TargetAmount must be greater than 0.")]
        public decimal TargetAmount { get; set; }

        public decimal AutoContributeAmount { get; set; }
        public string CurrencyCode { get; set; } = "INR";
        public string? EffectiveMonth { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int? CreatedBy { get; set; }

    }
}
