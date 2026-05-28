using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Notification
{
    public interface INotificationService
    {
        Task<NotificationListResult> GetAllNotificationsAsync(int userId);
        Task<NotificationResult> MarkAsReadAsync(int notificationId, int callerId);
        Task<NotificationResult> DeleteNotificationAsync(int notificationId, int callerId);
        Task<NotificationResult> CreateNotificationAsync(CreateNotificationRequestDto request);
    }
}
