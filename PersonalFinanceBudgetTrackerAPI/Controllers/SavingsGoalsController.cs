using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/goals")]
    [Authorize(Roles = "User")]
    public class SavingsGoalsController : ControllerBase
    {
        private readonly ISavingsGoalService _goalService;
        private readonly ILogService _logService;

        public SavingsGoalsController(
            ISavingsGoalService goalService,
            ILogService logService)
        {
            _goalService = goalService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/goals?userId={userId}
        // Returns all savings goals for the logged-in user
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllGoals([FromQuery] int userId)
        {
            if (!IsCallerAuthorized(userId))
                return Forbid();

            var result = await _goalService.GetAllGoalsAsync(userId);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/goals/{id}
        // Returns a single savings goal by GoalId
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetGoalById(int id)
        {
            int callerId = GetCallerId();

            var result = await _goalService.GetGoalByIdAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/goals
        // Creates a new savings goal and writes an audit log
        // ---------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] CreateSavingsGoalRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!IsCallerAuthorized(request.UserId))
                return Forbid();

            var result = await _goalService.CreateGoalAsync(request);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // Audit log — fire and forget style; non-blocking
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {result.Data!.UserId} savings goal {result.Data.GoalId} created",
                EventType = "Savings Goal Created",
                UserId = GetCallerId()
            });

            return CreatedAtAction(
                nameof(GetGoalById),
                new { id = result.Data.GoalId },
                new { message = result.Message, data = result.Data }
            );
        }

        // ---------------------------------------------------------------
        // PUT /api/goals/{id}
        // Updates a savings goal and writes an audit log
        // ---------------------------------------------------------------
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGoal(
            int id,
            [FromBody] UpdateSavingsGoalRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.GoalId != id)
                return BadRequest(new { message = "GoalId in the request body does not match the route parameter." });

            int callerId = GetCallerId();

            var result = await _goalService.UpdateGoalAsync(request, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {result.Data!.UserId} savings goal {result.Data.GoalId} Updated",
                EventType = "Savings Goal Updated",
                UserId = callerId
            });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // DELETE /api/goals/{id}
        // Deletes a savings goal and writes an audit log
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            int callerId = GetCallerId();

            var result = await _goalService.DeleteGoalAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} savings goal {id} Deleted",
                EventType = "Savings Goal Deleted",
                UserId = callerId
            });

            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // POST /api/goals/{id}/contribute
        // Adds to AutoContributeAmount and writes an audit log
        // ---------------------------------------------------------------
        [HttpPost("{id:int}/contribute")]
        public async Task<IActionResult> Contribute(
            int id,
            [FromBody] ContributeRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.GoalId != id)
                return BadRequest(new { message = "GoalId in the request body does not match the route parameter." });

            int callerId = GetCallerId();

            var result = await _goalService.ContributeAsync(request, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {result.Data!.UserId} savings goal {result.Data.GoalId} autocontributeamount {request.AutoContributeAmount}",
                EventType = "Savings Goal Contributed",
                UserId = callerId
            });

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
