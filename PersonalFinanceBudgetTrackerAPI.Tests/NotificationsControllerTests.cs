using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.Notification;


namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class NotificationsControllerTests
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        /// <summary>
        /// Creates a NotificationsController with a faked JWT ClaimsPrincipal
        /// containing the given callerId.
        /// </summary>
        private static NotificationsController CreateController(
            Mock<INotificationService> mockNotificationService,
            Mock<ILogService> mockLogService,
            int callerId = 10)
        {
            var controller = new NotificationsController(
                mockNotificationService.Object,
                mockLogService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",        callerId.ToString()),
                new Claim(ClaimTypes.Role, "User")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        /// <summary>
        /// Creates a controller with NO userId claim — simulates missing/invalid token.
        /// callerId resolves to 0, triggering the 401 Unauthorized guard.
        /// </summary>
        private static NotificationsController CreateControllerNoUserIdClaim(
            Mock<INotificationService> mockNotificationService,
            Mock<ILogService> mockLogService)
        {
            var controller = new NotificationsController(
                mockNotificationService.Object,
                mockLogService.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        // ── Stub log service that always succeeds ────────────────────────
        private static Mock<ILogService> StubLog()
        {
            var mock = new Mock<ILogService>();
            mock.Setup(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()))
                .ReturnsAsync(new LogResult { Success = true });
            return mock;
        }

        // ── DTO factories ────────────────────────────────────────────────
        private static NotificationResponseDto MakeNotificationDto(
            int notificationId = 1,
            int userId = 10,
            bool isRead = false,
            string type = "Budget Alert") =>
            new NotificationResponseDto
            {
                NotificationId = notificationId,
                UserId = userId,
                Message = "Your budget is 80% utilised.",
                Type = type,
                IsRead = isRead,
                CreatedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc)
            };

        private static List<NotificationResponseDto> MakeNotificationList(
            int userId = 10,
            int count = 3)
        {
            var list = new List<NotificationResponseDto>();
            for (int i = 1; i <= count; i++)
                list.Add(MakeNotificationDto(notificationId: i, userId: userId));
            return list;
        }

        // ===============================================================
        // GET /api/notifications  —  GetAllNotifications
        // ===============================================================

        [Fact]
        public async Task GetAllNotifications_ReturnsOk_WithNotificationList_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var notifications = MakeNotificationList(callerId, count: 3);

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult
                    {
                        Success = true,
                        Message = "3 notification(s) retrieved.",
                        Data = notifications
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.GetAllNotifications();

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<NotificationResponseDto>>(ok.Value);
            Xunit.Assert.Equal(3, data.Count());

            mockNsvc.Verify(s => s.GetAllNotificationsAsync(callerId), Times.Once);
        }

        [Fact]
        public async Task GetAllNotifications_ReturnsOk_WithEmptyList_WhenNoNotifications()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult
                    {
                        Success = true,
                        Message = "0 notification(s) retrieved.",
                        Data = new List<NotificationResponseDto>()
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.GetAllNotifications();

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<NotificationResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetAllNotifications_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange — no userId claim → callerId = 0
            var mockNsvc = new Mock<INotificationService>();
            var controller = CreateControllerNoUserIdClaim(mockNsvc, StubLog());

            // Act
            var result = await controller.GetAllNotifications();
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(401, unauthorized.StatusCode);

            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);

            mockNsvc.Verify(s => s.GetAllNotificationsAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetAllNotifications_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult
                    {
                        Success = false,
                        Message = "Database connection error."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.GetAllNotifications();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(500, statusResult.StatusCode);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Database connection error.", message);
        }

        [Fact]
        public async Task GetAllNotifications_Returns500_WithExactErrorMessage()
        {
            // Arrange
            const string errorMsg = "Unexpected server error.";
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult { Success = false, Message = errorMsg });

            var controller = CreateController(mockNsvc, StubLog(), callerId);
            var result = await controller.GetAllNotifications();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal(errorMsg, message);
        }

        [Fact]
        public async Task GetAllNotifications_ReturnsCorrectFields_InResponseBody()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();

            var notifications = new List<NotificationResponseDto>
            {
                new NotificationResponseDto
                {
                    NotificationId = 1,
                    UserId         = callerId,
                    Message        = "Savings goal reached 50%.",
                    Type           = "Savings Goal",
                    IsRead         = false,
                    CreatedAt      = new DateTime(2024, 4, 1, 9, 0, 0, DateTimeKind.Utc)
                }
            };

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult { Success = true, Data = notifications });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.GetAllNotifications();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<NotificationResponseDto>>(ok.Value).ToList();

            // Assert field-level values
            Xunit.Assert.Single(data);
            Xunit.Assert.Equal(1, data[0].NotificationId);
            Xunit.Assert.Equal("Savings goal reached 50%.", data[0].Message);
            Xunit.Assert.Equal("Savings Goal", data[0].Type);
            Xunit.Assert.False(data[0].IsRead);
        }

        [Fact]
        public async Task GetAllNotifications_CallsServiceWithCallerId_NotHardcodedValue()
        {
            // Arrange — use a non-default callerId to ensure it flows through correctly
            const int callerId = 42;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult
                    {
                        Success = true,
                        Data = new List<NotificationResponseDto>()
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            await controller.GetAllNotifications();

            // Assert — called with exact callerId extracted from JWT
            mockNsvc.Verify(s => s.GetAllNotificationsAsync(callerId), Times.Once);
            mockNsvc.Verify(s => s.GetAllNotificationsAsync(It.Is<int>(x => x != callerId)), Times.Never);
        }

        // ===============================================================
        // PUT /api/notifications/{id}/read  —  MarkAsRead
        // ===============================================================

        [Fact]
        public async Task MarkAsRead_ReturnsOk_WhenNotificationExistsAndBelongsToCaller()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 1;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = $"Notification {notificationId} marked as read successfully.",
                        Data = MakeNotificationDto(notificationId, callerId, isRead: true)
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            var result = await controller.MarkAsRead(notificationId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("marked as read", message);

            mockNsvc.Verify(s => s.MarkAsReadAsync(notificationId, callerId), Times.Once);
        }

        [Fact]
        public async Task MarkAsRead_CallsLogService_AfterSuccessfulUpdate()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 1;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification marked as read.",
                        Data = MakeNotificationDto(notificationId, callerId, isRead: true)
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.MarkAsRead(notificationId);

            // Assert — log called with exact event type and user
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Notifications Read" &&
                r.UserId == callerId)), Times.Once);
        }

        [Fact]
        public async Task MarkAsRead_LogEventContainsUserIdAndNotificationId()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 5;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Marked as read.",
                        Data = MakeNotificationDto(notificationId, callerId, isRead: true)
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.MarkAsRead(notificationId);

            // Assert event string contains both IDs
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.Event.Contains(callerId.ToString()) &&
                r.Event.Contains(notificationId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task MarkAsRead_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();
            var controller = CreateControllerNoUserIdClaim(mockNsvc, mockLog);

            // Act
            var result = await controller.MarkAsRead(id: 1);
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            mockNsvc.Verify(s => s.MarkAsReadAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsRead_ReturnsNotFound_WhenNotificationDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 999;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Notification with ID {notificationId} was not found."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.MarkAsRead(notificationId);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task MarkAsRead_ReturnsForbid_WhenNotificationBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 3;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "You are not authorized to update this notification."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.MarkAsRead(notificationId);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task MarkAsRead_LogServiceNeverCalled_WhenNotificationNotFound()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 999;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = "Not found."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.MarkAsRead(notificationId);

            // Assert — no log written if operation failed
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsRead_LogServiceNeverCalled_WhenForbid()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 3;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.MarkAsRead(notificationId);

            // Assert — no log written if forbidden
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsRead_ReturnsCorrectSuccessMessage()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 2;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.MarkAsReadAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = $"Notification {notificationId} marked as read successfully.",
                        Data = MakeNotificationDto(notificationId, callerId, isRead: true)
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.MarkAsRead(notificationId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal($"Notification {notificationId} marked as read successfully.", message);
        }

        // ===============================================================
        // DELETE /api/notifications/{id}  —  DeleteNotification
        // ===============================================================

        [Fact]
        public async Task DeleteNotification_ReturnsOk_WhenNotificationIsSuccessfullyDeleted()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 1;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = $"Notification {notificationId} deleted successfully."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            var result = await controller.DeleteNotification(notificationId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("deleted successfully", message);

            mockNsvc.Verify(s => s.DeleteNotificationAsync(notificationId, callerId), Times.Once);
        }

        [Fact]
        public async Task DeleteNotification_CallsLogService_AfterSuccessfulDeletion()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 4;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Deleted."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.DeleteNotification(notificationId);

            // Assert log called with correct EventType and userId
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Notifications Deleted" &&
                r.UserId == callerId)), Times.Once);
        }

        [Fact]
        public async Task DeleteNotification_LogEventContainsUserIdAndNotificationId()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 7;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.DeleteNotification(notificationId);

            // Assert event string contains both IDs
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.Event.Contains(callerId.ToString()) &&
                r.Event.Contains(notificationId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task DeleteNotification_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();
            var controller = CreateControllerNoUserIdClaim(mockNsvc, mockLog);

            // Act
            var result = await controller.DeleteNotification(id: 1);
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            mockNsvc.Verify(s => s.DeleteNotificationAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteNotification_ReturnsNotFound_WhenNotificationDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 999;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Notification with ID {notificationId} was not found."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.DeleteNotification(notificationId);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task DeleteNotification_ReturnsForbid_WhenNotificationBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 8;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "You are not authorized to delete this notification."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.DeleteNotification(notificationId);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteNotification_LogServiceNeverCalled_WhenNotificationNotFound()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 999;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = "Not found."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.DeleteNotification(notificationId);

            // Assert — log never written on failure
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task DeleteNotification_LogServiceNeverCalled_WhenForbid()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 8;
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockNsvc, mockLog, callerId);

            // Act
            await controller.DeleteNotification(notificationId);

            // Assert — no log written if forbidden
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task DeleteNotification_CallsServiceOnceWithCorrectIds()
        {
            // Arrange
            const int callerId = 10;
            const int notificationId = 6;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.DeleteNotificationAsync(notificationId, callerId))
                    .ReturnsAsync(new NotificationResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            await controller.DeleteNotification(notificationId);

            // Assert strict call verification
            mockNsvc.Verify(s => s.DeleteNotificationAsync(notificationId, callerId), Times.Once);
            mockNsvc.Verify(s => s.DeleteNotificationAsync(
                It.Is<int>(x => x != notificationId), It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Cross-Endpoint — Unauthorised guard is consistent
        // ===============================================================

        [Fact]
        public async Task AllEndpoints_Return401_WhenNoUserIdClaim()
        {
            // Arrange — single controller instance with no claim
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();
            var controller = CreateControllerNoUserIdClaim(mockNsvc, mockLog);

            // Act
            var getResult = await controller.GetAllNotifications();
            var readResult = await controller.MarkAsRead(1);
            var deleteResult = await controller.DeleteNotification(1);

            // Assert — all three return 401
            Xunit.Assert.IsType<UnauthorizedObjectResult>(getResult);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(readResult);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(deleteResult);
        }

        [Fact]
        public async Task AllEndpoints_NeverCallService_WhenNoUserIdClaim()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var mockLog = StubLog();
            var controller = CreateControllerNoUserIdClaim(mockNsvc, mockLog);

            // Act
            await controller.GetAllNotifications();
            await controller.MarkAsRead(1);
            await controller.DeleteNotification(1);

            // Assert — service never called when guard fires
            mockNsvc.Verify(s => s.GetAllNotificationsAsync(It.IsAny<int>()), Times.Never);
            mockNsvc.Verify(s => s.MarkAsReadAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            mockNsvc.Verify(s => s.DeleteNotificationAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Notification Type Coverage
        // ===============================================================

        [Theory]
        [InlineData("Budget Alert")]
        [InlineData("Savings Goal")]
        [InlineData("Transaction")]
        [InlineData("Account")]
        [InlineData("Auto Contribute")]
        [InlineData("System")]
        [InlineData("Reminder")]
        public async Task GetAllNotifications_ReturnsAllSupportedNotificationTypes(string notificationType)
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();

            mockNsvc.Setup(s => s.GetAllNotificationsAsync(callerId))
                    .ReturnsAsync(new NotificationListResult
                    {
                        Success = true,
                        Data = new List<NotificationResponseDto>
                        {
                            MakeNotificationDto(1, callerId, isRead: false, type: notificationType)
                        }
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.GetAllNotifications();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<NotificationResponseDto>>(ok.Value).ToList();

            // Assert type is preserved in response
            Xunit.Assert.Single(data);
            Xunit.Assert.Equal(notificationType, data[0].Type);
        }

        // ===============================================================
        // POST /api/notifications  —  CreateNotification
        // ===============================================================

        [Fact]
        public async Task CreateNotification_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "Your budget has exceeded 80%.",
                Type = "Budget Alert"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = new NotificationResponseDto
                        {
                            NotificationId = 1,
                            UserId = callerId,
                            Message = request.Message,
                            Type = request.Type,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockNsvc.Verify(s => s.CreateNotificationAsync(request), Times.Once);
        }

        [Fact]
        public async Task CreateNotification_ReturnsCreated_WithCorrectResponseBody()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "Transaction of $500 posted.",
                Type = "Transaction"
            };

            var expectedDto = new NotificationResponseDto
            {
                NotificationId = 5,
                UserId = callerId,
                Message = request.Message,
                Type = request.Type,
                IsRead = false,
                CreatedAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = expectedDto
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            // Assert body fields
            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as NotificationResponseDto;

            Xunit.Assert.Equal("Notification added successfully.", message);
            Xunit.Assert.Equal(expectedDto.NotificationId, data?.NotificationId);
            Xunit.Assert.Equal(expectedDto.Message, data?.Message);
            Xunit.Assert.Equal(expectedDto.Type, data?.Type);
            Xunit.Assert.False(data?.IsRead);
        }

        [Fact]
        public async Task CreateNotification_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var controller = CreateController(mockNsvc, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Message", "Message is required.");

            var request = new CreateNotificationRequestDto { UserId = 10, Type = "Budget Alert" };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockNsvc.Verify(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotification_ReturnsBadRequest_WhenTypeIsMissing()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var controller = CreateController(mockNsvc, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Type", "Type is required.");

            var request = new CreateNotificationRequestDto
            {
                UserId = 10,
                Message = "Something happened."
            };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockNsvc.Verify(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotification_ReturnsBadRequest_WhenUserIdIsMissing()
        {
            // Arrange
            var mockNsvc = new Mock<INotificationService>();
            var controller = CreateController(mockNsvc, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("UserId", "UserId is required.");

            var request = new CreateNotificationRequestDto
            {
                Message = "Budget alert.",
                Type = "Budget Alert"
            };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockNsvc.Verify(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotification_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "Budget exceeded.",
                Type = "Budget Alert"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = false,
                        Message = "An error occurred while creating the notification: DB timeout."
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(500, statusResult.StatusCode);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("DB timeout", message);
        }

        [Fact]
        public async Task CreateNotification_Returns500_WithExactErrorMessage()
        {
            // Arrange
            const string errorMsg = "An error occurred while creating the notification: Connection refused.";
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "Test message.",
                Type = "System"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult { Success = false, Message = errorMsg });

            var controller = CreateController(mockNsvc, StubLog(), callerId);
            var result = await controller.CreateNotification(request);
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal(errorMsg, message);
        }

        [Fact]
        public async Task CreateNotification_IsAccessibleByUserRole()
        {
            // Arrange — User role (not just Admin) should be able to create notifications
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "Budget threshold reached.",
                Type = "Budget Alert"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = new NotificationResponseDto
                        {
                            NotificationId = 1,
                            UserId = callerId,
                            Message = request.Message,
                            Type = request.Type,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    });

            // Create controller with User role
            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);

            // Assert — User role can create notifications (called by Budget/Transaction APIs)
            Xunit.Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async Task CreateNotification_IsAccessibleByAdminRole()
        {
            // Arrange — Admin role should also be able to create notifications
            const int callerId = 99;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "System maintenance scheduled.",
                Type = "System"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = new NotificationResponseDto
                        {
                            NotificationId = 2,
                            UserId = callerId,
                            Message = request.Message,
                            Type = "System",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    });

            // Build controller with Admin role
            var controller = new NotificationsController(mockNsvc.Object, StubLog().Object);
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("userId",                          callerId.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(claims, "TestAuth"))
                }
            };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            Xunit.Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async Task CreateNotification_ServiceNeverCalled_WhenAllModelFieldsMissing()
        {
            // Arrange — completely empty request
            var mockNsvc = new Mock<INotificationService>();
            var controller = CreateController(mockNsvc, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("UserId", "Required.");
            controller.ModelState.AddModelError("Message", "Required.");
            controller.ModelState.AddModelError("Type", "Required.");

            // Act
            await controller.CreateNotification(new CreateNotificationRequestDto());

            // Assert — service never invoked on invalid input
            mockNsvc.Verify(s => s.CreateNotificationAsync(It.IsAny<CreateNotificationRequestDto>()), Times.Never);
        }

        [Theory]
        [InlineData("Budget Alert")]
        [InlineData("Savings Goal")]
        [InlineData("Transaction")]
        [InlineData("Account")]
        [InlineData("Auto Contribute")]
        [InlineData("System")]
        [InlineData("Reminder")]
        public async Task CreateNotification_ReturnsCreated_ForAllSupportedNotificationTypes(string notificationType)
        {
            // Arrange
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = $"Notification of type {notificationType}",
                Type = notificationType
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = new NotificationResponseDto
                        {
                            NotificationId = 1,
                            UserId = callerId,
                            Message = request.Message,
                            Type = notificationType,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as NotificationResponseDto;
            Xunit.Assert.Equal(notificationType, data?.Type);
        }

        [Fact]
        public async Task CreateNotification_IsReadDefaultsFalse_InCreatedResponse()
        {
            // Arrange — verify IsRead is always false for new notifications
            const int callerId = 10;
            var mockNsvc = new Mock<INotificationService>();
            var request = new CreateNotificationRequestDto
            {
                UserId = callerId,
                Message = "New transaction recorded.",
                Type = "Transaction"
            };

            mockNsvc.Setup(s => s.CreateNotificationAsync(request))
                    .ReturnsAsync(new NotificationResult
                    {
                        Success = true,
                        Message = "Notification added successfully.",
                        Data = new NotificationResponseDto
                        {
                            NotificationId = 3,
                            UserId = callerId,
                            Message = request.Message,
                            Type = request.Type,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    });

            var controller = CreateController(mockNsvc, StubLog(), callerId);

            // Act
            var result = await controller.CreateNotification(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as NotificationResponseDto;

            // Assert IsRead is always false on creation
            Xunit.Assert.False(data?.IsRead);
        }
    }
}
