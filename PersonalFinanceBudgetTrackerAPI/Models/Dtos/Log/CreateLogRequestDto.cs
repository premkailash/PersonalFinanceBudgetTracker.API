using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log
{
    public class CreateLogRequestDto
    {
        [Required(ErrorMessage = "Event description is required.")]
        [MaxLength(255, ErrorMessage = "Event cannot exceed 255 characters.")]
        public string Event { get; set; } = string.Empty;

        [Required(ErrorMessage = "EventType is required.")]
        [MaxLength(50, ErrorMessage = "EventType cannot exceed 50 characters.")]
        public string EventType { get; set; } = string.Empty;

        // Nullable — system-generated events may have no user
        public int? UserId { get; set; }

    }
}
