using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification
{
    public class CreateNotificationRequestDto
    {
        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Message is required.")]
        [MaxLength(255, ErrorMessage = "Message cannot exceed 255 characters.")]
        public string Message { get; set; } = string.Empty;

        [Required(ErrorMessage = "Type is required.")]
        [MaxLength(50, ErrorMessage = "Type cannot exceed 50 characters.")]
        public string Type { get; set; } = string.Empty;
    }

}
