using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Extensions;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DefaultBudget;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    /// <summary>
    /// Exposes the monthly default-budget reset endpoint (Lambda-triggered)
    /// and Admin CRUD endpoints for DefaultBudget templates.
    ///
    /// Lambda endpoint  — POST /api/budgets/monthly-reset
    ///   Secured by X-Reset-Key header.
    ///
    /// Admin endpoints  — [Authorize(Roles = "Admin")]
    ///   GET    /api/budgets/default-budgets
    ///   POST   /api/budgets/default-budgets
    ///   PUT    /api/budgets/default-budgets/{id}
    ///   DELETE /api/budgets/default-budgets/{id}
    /// </summary>
    [ApiController]
    [Route("api/budgets")]
    [EnableRateLimiting(RateLimitPolicies.Data)]
    public class BudgetResetController : ControllerBase
    {
        private readonly IDefaultBudgetService _defaultBudgetService;
        private readonly ILogService _logService;
        private readonly IConfiguration _config;

        public BudgetResetController(
            IDefaultBudgetService defaultBudgetService,
            ILogService logService,
            IConfiguration config)
        {
            _defaultBudgetService = defaultBudgetService;
            _logService = logService;
            _config = config;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/budgets/monthly-reset
        // Lambda-only — secured by X-Reset-Key header.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("monthly-reset")]
        [AllowAnonymous]
        [BudgetResetKeyAuthorize]
        public async Task<IActionResult> MonthlyReset(
            [FromBody] MonthlyResetRequestDto? request = null)
        {
            DateTime? targetMonth = null;

            if (!string.IsNullOrWhiteSpace(request?.TargetMonth))
            {
                if (!DateTime.TryParseExact(request.TargetMonth + "-01",
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var parsed))
                {
                    return BadRequest(new
                    {
                        message = "Invalid targetMonth format. Expected YYYY-MM (e.g. '2024-06')."
                    });
                }
                targetMonth = parsed;
            }

            var result = await _defaultBudgetService
                .ResetDefaultBudgetsForAllAccountsAsync(targetMonth);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(new
            {
                message = result.Message,
                totalAccounts = result.TotalAccounts,
                budgetsCreated = result.BudgetsCreated,
                budgetsSkipped = result.BudgetsSkipped,
                errors = result.Errors
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/budgets/default-budgets
        // Returns all DefaultBudget templates. Admin only.
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("default-budgets")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDefaultBudgets()
        {
            var result = await _defaultBudgetService.GetAllDefaultBudgetsAsync();

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/budgets/default-budgets
        // Creates a new DefaultBudget template. Admin only.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("default-budgets")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDefaultBudget(
            [FromBody] CreateDefaultBudgetRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Stamp the admin who created this template
            request.CreatedBy = GetAdminId();

            var result = await _defaultBudgetService.CreateDefaultBudgetAsync(request);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                if (result.Conflict) return Conflict(new { message = result.Message });
                return StatusCode(500, new { message = result.Message });
            }

            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"Admin {GetAdminId()} created default budget template '{result.Data!.Name}' " +
                            $"(ID: {result.Data.DefaultBudgetId}).",
                EventType = "System",
                UserId = GetAdminId()
            });

            return CreatedAtAction(
                nameof(GetDefaultBudgets),
                null,
                new { message = result.Message, data = result.Data });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/budgets/default-budgets/{id}
        // Updates an existing DefaultBudget template. Admin only.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPut("default-budgets/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDefaultBudget(
            int id,
            [FromBody] UpdateDefaultBudgetRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            request.UpdatedBy = GetAdminId();

            var result = await _defaultBudgetService.UpdateDefaultBudgetAsync(id, request);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                if (result.Conflict) return Conflict(new { message = result.Message });
                return StatusCode(500, new { message = result.Message });
            }

            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"Admin {GetAdminId()} updated default budget template '{result.Data!.Name}' " +
                            $"(ID: {id}).",
                EventType = "System",
                UserId = GetAdminId()
            });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/budgets/default-budgets/{id}
        // Hard-deletes a DefaultBudget template. Admin only.
        // Does NOT affect user Budget rows already seeded from this template.
        // ─────────────────────────────────────────────────────────────────────
        [HttpDelete("default-budgets/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDefaultBudget(int id)
        {
            var result = await _defaultBudgetService.DeleteDefaultBudgetAsync(id);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : StatusCode(500, new { message = result.Message });

            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"Admin {GetAdminId()} deleted default budget template ID {id}.",
                EventType = "System",
                UserId = GetAdminId()
            });

            return Ok(new { message = result.Message });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        [NonAction]
        private int GetAdminId()
        {
            var claim = User?.FindFirst("userId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }
    }           
}
