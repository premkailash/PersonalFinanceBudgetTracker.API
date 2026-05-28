namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category
{
    public class CategoryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public bool IsDuplicate { get; set; }
        public CategoryResponseDto? Data { get; set; }

    }
}
