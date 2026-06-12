using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Extensions;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    /// <summary>
    /// Exposes the monthly default-budget reset endpoint.
    ///
    /// POST /api/budgets/monthly-reset
    ///   Invoked exclusively by the AWS Lambda scheduled function on the
    ///   1st of every month at 00:05 IST (18:35 UTC previous day).
    ///
    /// Security: [AllowAnonymous] + [ApiKeyAuthorize] — same pattern as
    ///   /api/transactions/import.  The Lambda passes X-Reset-Key in the
    ///   header.  The expected key is read from ImportSettings:ResetApiKey
    ///   (stored in AWS Secrets Manager, never in appsettings.json).
    /// </summary>  
    [ApiController]
    [Route("api/budgets")]
    [EnableRateLimiting(RateLimitPolicies.Data)]    
    public class BudgetResetController : ControllerBase
    {
        private readonly IDefaultBudgetService _defaultBudgetService;
        private readonly IConfiguration _config;

        public BudgetResetController(
            IDefaultBudgetService defaultBudgetService,
            IConfiguration config)
        {
            _defaultBudgetService = defaultBudgetService;
            _config = config;
        }

        // ──────────────────────────────────────────────────────────────────────
        // POST /api/budgets/monthly-reset
        // Lambda-only endpoint — secured by X-Reset-Key header.
        // Accepts an optional body: { "targetMonth": "2024-06" }
        // If omitted, defaults to the current calendar month.
        // ──────────────────────────────────────────────────────────────────────
        [HttpPost("monthly-reset")]
        [AllowAnonymous]
        [BudgetResetKeyAuthorize]
        public async Task<IActionResult> MonthlyReset(
            [FromBody] MonthlyResetRequestDto? request = null)
        {
            // Resolve target month — body overrides default
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
    }      
}
