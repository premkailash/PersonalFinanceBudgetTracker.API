using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using PersonalFinanceBudgetTrackerAPI.Repository.Report;
using System.Text.RegularExpressions;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Roles = "User")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // ---------------------------------------------------------------
        // GET /api/reports/monthly?month={YYYY-MM}
        // Monthly income vs expense summary per account
        // ---------------------------------------------------------------
        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] string month)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            if (!IsValidMonthFormat(month))
                return BadRequest(new { message = "Invalid month format. Use YYYY-MM (e.g. 2024-03)." });

            var result = await _reportService.GetMonthlyReportAsync(callerId, month);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/reports/yearly?year={YYYY}
        // Yearly financial report — monthly income/expense per account
        // ---------------------------------------------------------------
        [HttpGet("yearly")]
        public async Task<IActionResult> GetYearlyReport([FromQuery] string year)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            if (!IsValidYearFormat(year))
                return BadRequest(new { message = "Invalid year format. Use YYYY (e.g. 2024)." });

            var result = await _reportService.GetYearlyReportAsync(callerId, year);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/reports/category-breakdown?month={YYYY-MM}
        // Spending/income by category for a given month
        // ---------------------------------------------------------------
        [HttpGet("category-breakdown")]
        public async Task<IActionResult> GetCategoryBreakdown([FromQuery] string month)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            if (!IsValidMonthFormat(month))
                return BadRequest(new { message = "Invalid month format. Use YYYY-MM (e.g. 2024-03)." });

            var result = await _reportService.GetCategoryBreakdownAsync(callerId, month);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/reports/net-worth
        // Net worth snapshot: total assets minus total liabilities
        // ---------------------------------------------------------------
        [HttpGet("net-worth")]
        public async Task<IActionResult> GetNetWorth()
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _reportService.GetNetWorthAsync(callerId);

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

        private static bool IsValidMonthFormat(string month) =>
            !string.IsNullOrWhiteSpace(month) &&
            Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$");

        private static bool IsValidYearFormat(string year) =>
            !string.IsNullOrWhiteSpace(year) &&
            Regex.IsMatch(year, @"^\d{4}$");
    }

}
