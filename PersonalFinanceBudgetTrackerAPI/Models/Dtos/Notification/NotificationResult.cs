namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification
{
    public class NotificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool NotFound { get; set; }
        public NotificationResponseDto? Data { get; set; }

    }
}
