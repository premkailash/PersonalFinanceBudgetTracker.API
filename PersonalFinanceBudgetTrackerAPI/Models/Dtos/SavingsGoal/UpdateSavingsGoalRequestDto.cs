using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal
{
    public class UpdateSavingsGoalRequestDto
    {
        [Required(ErrorMessage = "GoalId is required.")]
        public int GoalId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "TargetAmount must be greater than zero.")]
        public decimal TargetAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "CurrentAmount must be zero or greater.")]
        public decimal CurrentAmount { get; set; }

        [Required(ErrorMessage = "TargetDate is required.")]
        public DateTime TargetDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "AutoContributeAmount must be zero or greater.")]
        public decimal AutoContributeAmount { get; set; }

    }
}
