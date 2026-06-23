using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using PersonalFinanceBudgetTrackerAPI.Repository.DataExport;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/export")]
    [Authorize(Roles = "User")]
    public class DataExportController : ControllerBase
    {
        private readonly IDataExportService _exportService;
        private readonly ILogService _logService;

        public DataExportController(
            IDataExportService exportService,
            ILogService logService)
        {
            _exportService = exportService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // POST /api/export/transactions
        // Submits a new export request (CSV or PDF)
        // ---------------------------------------------------------------
        [HttpPost("transactions")]
        public async Task<IActionResult> RequestExport(
            [FromBody] CreateDataExportRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ownership check — user can only request exports for themselves
            if (!IsCallerAuthorized(request.UserId))
                return Forbid();

            // Date range validation
            if (request.ToDate <= request.FromDate)
                return BadRequest(new { message = "ToDate must be later than FromDate." });

            var result = await _exportService.RequestExportAsync(request);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {request.UserId} data export {result.Data!.ExportId} " +
                            $"requested for {request.ReportType} ({request.ReportOptions})",
                EventType = "System",
                UserId = GetCallerId()
            });

            return CreatedAtAction(
                nameof(GetExportDownload),
                new { export_id = result.Data.ExportId },
                new { message = result.Message, data = result.Data }
            );
        }

        // ---------------------------------------------------------------
        // GET /api/export/{export_id}
        // Returns the ReportLink for a completed export
        // ---------------------------------------------------------------
        [HttpGet("{export_id:int}")]
        public async Task<IActionResult> GetExportDownload(int export_id)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _exportService.GetExportDownloadAsync(export_id, callerId);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                if (result.NotReady) return Ok(new { message = result.Message, data = result.Data });
                return Forbid();
            }

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // GET /api/export/
        // Returns the Report Request for the user
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetExportRequest()
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _exportService.GetExportRequestAsync(callerId);

            if (!result.Success)
            {
                return StatusCode(500, new { message = result.Message });
            }

            return Ok(new { message = result.Message, data = result.Data });
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
    }

}
