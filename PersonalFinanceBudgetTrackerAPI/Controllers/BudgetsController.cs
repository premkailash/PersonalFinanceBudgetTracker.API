using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Common;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using System.Text.RegularExpressions;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/budgets")]
    [Authorize(Roles = "User")]
    public class BudgetsController : ControllerBase
    {
        private readonly IBudgetService _budgetService;
        private readonly ILogService _logService;
        public BudgetsController(IBudgetService budgetService, ILogService logService)
        {
            _budgetService = budgetService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/budgets?userId={userId}&month={YYYY-MM}
        // Returns budgets for the logged-in user for the given month
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetBudgetsByMonth(
            [FromQuery] int userId,
            [FromQuery] string month)
        {
            if (!IsCallerAuthorized(userId))
                return Forbid();

            if (!IsValidMonthFormat(month))
                return BadRequest(new { message = "Invalid month format. Use YYYY-MM (e.g. 2024-03)." });

            var result = await _budgetService.GetBudgetsByMonthAsync(userId, month);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/budgets/{id}
        // Returns a specific budget by BudgetId
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetBudgetById(int id)
        {
            int callerId = GetCallerId();

            var result = await _budgetService.GetBudgetByIdAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/budgets
        // Creates a new budget for the logged-in user
        // ---------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!IsCallerAuthorized(request.UserId))
                return Forbid();

            var result = await _budgetService.CreateBudgetAsync(request);

            if (!result.Success)
                return result.IsDuplicate
                    ? Conflict(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            await PostLogs(EventType.BudgetCreated, request.UserId);

            return CreatedAtAction(
                nameof(GetBudgetById),
                new { id = result.Data!.BudgetId },
                new { message = result.Message, data = result.Data }
            );
        }

        // ---------------------------------------------------------------
        // PUT /api/budgets/{id}
        // Updates an existing budget
        // ---------------------------------------------------------------
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateBudget(
            int id,
            [FromBody] UpdateBudgetRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.BudgetId != id)
                return BadRequest(new { message = "BudgetId in the request body does not match the route parameter." });

            int callerId = GetCallerId();

            var result = await _budgetService.UpdateBudgetAsync(request, callerId);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                return Forbid();
            }
            await PostLogs(EventType.BudgetUpdated, callerId);
            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // DELETE /api/budgets/{id}
        // Deletes a budget by Id
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            int callerId = GetCallerId();

            var result = await _budgetService.DeleteBudgetAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();
            await PostLogs(EventType.BudgetDeleted, callerId);
            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // GET /api/budgets/utilization?userId={userId}&month={YYYY-MM}
        // Returns real-time budget utilization for the given month
        // ---------------------------------------------------------------
        [HttpGet("utilization")]
        public async Task<IActionResult> GetBudgetUtilization(
            [FromQuery] int userId,
            [FromQuery] string month)
        {
            if (!IsCallerAuthorized(userId))
                return Forbid();

            if (!IsValidMonthFormat(month))
                return BadRequest(new { message = "Invalid month format. Use YYYY-MM (e.g. 2024-03)." });

            var result = await _budgetService.GetBudgetUtilizationAsync(userId, month);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private int GetCallerId()
        {
            var claim = User.FindFirst("userId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }

        private bool IsCallerAuthorized(int requestedUserId) =>
            GetCallerId() == requestedUserId;

        private static bool IsValidMonthFormat(string month) =>
            !string.IsNullOrWhiteSpace(month) &&
            Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$");

        private async Task PostLogs(string eventType, int userId)
        {
            await _logService.CreateLogAsync(new Models.Dtos.Log.CreateLogRequestDto
            {
                Event = $"For User {userId} - {eventType}",
                EventType = eventType,
                UserId = userId
            });
        }
    }

}
