using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/logs")]
    public class LogsController : ControllerBase
    {
        private readonly ILogService _logService;

        public LogsController(ILogService logService)
        {
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/logs
        // Admin only — returns all log entries joined with Username
        // ---------------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllLogs()
        {
            var result = await _logService.GetAllLogsAsync();

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/logs/{id}
        // Admin only — returns a single log entry by LogId
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetLogById(int id)
        {
            var result = await _logService.GetLogByIdAsync(id);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/logs
        // Internal endpoint — called by Auth, Accounts, Transactions,
        // Budgets, SavingsGoals, Notifications, DataExport APIs.
        // Requires authentication but open to all roles.
        // ---------------------------------------------------------------
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateLog([FromBody] CreateLogRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _logService.CreateLogAsync(request);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return CreatedAtAction(
                nameof(GetLogById),
                new { id = result.Data!.LogId },
                new { message = result.Message, data = result.Data }
            );
        }
    }

}
