namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification
{
    public class NotificationListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IEnumerable<NotificationResponseDto>? Data { get; set; }

    }
}
