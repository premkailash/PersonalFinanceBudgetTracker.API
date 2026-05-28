using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Notification;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.Notification;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize(Roles = "User")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogService _logService;

        public NotificationsController(
            INotificationService notificationService,
            ILogService logService)
        {
            _notificationService = notificationService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/notifications
        // Returns all notifications for the logged-in user
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllNotifications()
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _notificationService.GetAllNotificationsAsync(callerId);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // PUT /api/notifications/{id}/read
        // Marks a notification as read and writes an audit log
        // ---------------------------------------------------------------
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _notificationService.MarkAsReadAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} notification {id} is read",
                EventType = "Notifications Read",
                UserId = callerId
            });

            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // DELETE /api/notifications/{id}
        // Deletes a notification and writes an audit log
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _notificationService.DeleteNotificationAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} notification {id} is deleted",
                EventType = "Notifications Deleted",
                UserId = callerId
            });

            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // POST /api/notifications
        // Internal endpoint — called by Budget, Transaction, and other APIs.
        // Accessible by any authenticated role (User or Admin).
        // ---------------------------------------------------------------
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateNotification(
            [FromBody] CreateNotificationRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _notificationService.CreateNotificationAsync(request);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return CreatedAtAction(
                nameof(GetAllNotifications),
                new { },
                new { message = result.Message, data = result.Data }
            );
        }


        // ---------------------------------------------------------------
        // Helper
        // ---------------------------------------------------------------
        private int GetCallerId()
        {
            var claim = User.FindFirst("userId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }
    }

}
