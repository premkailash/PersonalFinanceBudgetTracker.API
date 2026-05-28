namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget
{
    public class BudgetResponseDto
    {
        public int BudgetId { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public decimal AutoContributeAmount { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
