namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category
{
    public class CategoryResponseDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public bool IsDefault { get; set; }

    }
}
