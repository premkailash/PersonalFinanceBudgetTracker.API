using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal
{
    public class ContributeRequestDto
    {
        [Required(ErrorMessage = "GoalId is required.")]
        public int GoalId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "AutoContributeAmount must be greater than zero.")]
        public decimal AutoContributeAmount { get; set; }

    }
}
