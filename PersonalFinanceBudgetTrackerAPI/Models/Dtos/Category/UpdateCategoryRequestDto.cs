using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category
{
    public class UpdateCategoryRequestDto
    {
        [Required(ErrorMessage = "CategoryId is required.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(Income|Expense)$",
            ErrorMessage = "Type must be 'Income' or 'Expense'.")]
        public string Type { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
        public string? Icon { get; set; }

        public bool IsDefault { get; set; } = false;

    }
}
