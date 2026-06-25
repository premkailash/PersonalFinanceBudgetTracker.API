namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget
{
    public class DefaultBudgetDto
    {
        public int DefaultBudgetId { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal AutoContributeAmount { get; set; }        
        public string? EffectiveMonth { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }
}
