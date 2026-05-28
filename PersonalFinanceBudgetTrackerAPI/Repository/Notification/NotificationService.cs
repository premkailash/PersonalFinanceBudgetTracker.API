using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Notification
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;

        public NotificationService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // GET ALL NOTIFICATIONS FOR USER
        // ---------------------------------------------------------------
        public async Task<NotificationListResult> GetAllNotificationsAsync(int userId)
        {
            try
            {
                var notifications = await _db.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new NotificationResponseDto
                    {
                        NotificationId = n.NotificationId,
                        UserId = n.UserId,
                        Message = n.Message,
                        Type = n.Type,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt
                    })
                    .ToListAsync();

                return new NotificationListResult
                {
                    Success = true,
                    Message = $"{notifications.Count} notification(s) retrieved.",
                    Data = notifications
                };
            }
            catch (Exception ex)
            {
                return new NotificationListResult
                {
                    Success = false,
                    Message = $"An error occurred while retrieving notifications: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // MARK AS READ
        // ---------------------------------------------------------------
        public async Task<NotificationResult> MarkAsReadAsync(int notificationId, int callerId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

            if (notification == null)
                return new NotificationResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Notification with ID {notificationId} was not found."
                };

            if (notification.UserId != callerId)
                return new NotificationResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to update this notification."
                };

            notification.IsRead = true;
            await _db.SaveChangesAsync();

            return new NotificationResult
            {
                Success = true,
                Message = $"Notification {notificationId} marked as read successfully.",
                Data = new NotificationResponseDto
                {
                    NotificationId = notification.NotificationId,
                    UserId = notification.UserId,
                    Message = notification.Message,
                    Type = notification.Type,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt
                }
            };
        }

        // ---------------------------------------------------------------
        // DELETE NOTIFICATION
        // ---------------------------------------------------------------
        public async Task<NotificationResult> DeleteNotificationAsync(int notificationId, int callerId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

            if (notification == null)
                return new NotificationResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Notification with ID {notificationId} was not found."
                };

            if (notification.UserId != callerId)
                return new NotificationResult
                {
                    Success = false,
                    NotFound = false,
                    Message = "You are not authorized to delete this notification."
                };

            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();

            return new NotificationResult
            {
                Success = true,
                Message = $"Notification {notificationId} deleted successfully."
            };
        }

        // ---------------------------------------------------------------
        // CREATE NOTIFICATION
        // Called internally by Budget, Transaction, and other API endpoints
        // ---------------------------------------------------------------
        public async Task<NotificationResult> CreateNotificationAsync(CreateNotificationRequestDto request)
        {
            try
            {
                var notification = new Models.Entity.Notification
                {
                    UserId = request.UserId,
                    Message = request.Message,
                    Type = request.Type,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync();

                return new NotificationResult
                {
                    Success = true,
                    Message = "Notification added successfully.",
                    Data = new NotificationResponseDto
                    {
                        NotificationId = notification.NotificationId,
                        UserId = notification.UserId,
                        Message = notification.Message,
                        Type = notification.Type,
                        IsRead = notification.IsRead,
                        CreatedAt = notification.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                return new NotificationResult
                {
                    Success = false,
                    Message = $"An error occurred while creating the notification: {ex.Message}"
                };
            }
        }

    }

}
