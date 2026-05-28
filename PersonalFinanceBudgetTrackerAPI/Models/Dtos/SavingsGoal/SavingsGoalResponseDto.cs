namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal
{
    public class SavingsGoalResponseDto
    {
        public int GoalId { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        /// <summary>
        /// Effective current amount = CurrentAmount + AutoContributeAmount
        /// </summary>
        public decimal CurrentAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
