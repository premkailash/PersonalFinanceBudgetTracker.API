namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category
{
    public class CategoryListResult
    {

        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<CategoryResponseDto>? Data { get; set; }

    }
}
