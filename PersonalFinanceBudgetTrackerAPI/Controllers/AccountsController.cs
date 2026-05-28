using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Common;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account;
using PersonalFinanceBudgetTrackerAPI.Repository.Account;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using System.Security.Claims;


namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    [Authorize(Roles = "User")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogService _logService;

        public AccountsController(IAccountService accountService, ILogService logService)
        {
            _accountService = accountService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/accounts?userId={userId}
        // Returns all active accounts for the logged-in user
        // ---------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllAccounts([FromQuery] int userId)
        {
            // Ensure the requesting user can only fetch their own accounts
            if (!IsCallerAuthorized(userId))
                return Forbid();

            var result = await _accountService.GetAllAccountsAsync(userId);

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/accounts/{id}
        // Returns specific account details by AccountId
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetAccountById(int id)
        {
            int callerId = GetCallerId();

            var result = await _accountService.GetAccountByIdAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/accounts
        // Link a new account for the logged-in user
        // ---------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ensure the requesting user can only create accounts for themselves
            if (!IsCallerAuthorized(request.UserId))
                return Forbid();

            var result = await _accountService.CreateAccountAsync(request);

            if (!result.Success)
                return result.IsDuplicate
                    ? Conflict(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            await PostLogs(EventType.AccountCreated, request.UserId);           

            return CreatedAtAction(
                nameof(GetAccountById),
                new { id = result.Data!.AccountId },
                new { message = result.Message, data = result.Data }
            );
        }

        // ---------------------------------------------------------------
        // PUT /api/accounts/{id}
        // Update an existing account
        // ---------------------------------------------------------------
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.AccountId != id)
                return BadRequest(new { message = "AccountId in the request body does not match the route parameter." });

            int callerId = GetCallerId();

            var result = await _accountService.UpdateAccountAsync(request, callerId);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                if (result.IsDuplicate) return Conflict(new { message = result.Message });
                return Forbid();
            }
            await PostLogs(EventType.AccountUpdated, callerId);
            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------  
        // DELETE /api/accounts/{id}
        // Soft-delete: sets IsActive = false
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> UnlinkAccount(int id)
        {
            int callerId = GetCallerId();

            var result = await _accountService.UnlinkAccountAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();
            
            await PostLogs(EventType.AccountDeleted, callerId);
            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private int GetCallerId()
        {
            var claim = User.FindFirst("userId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }

        private bool IsCallerAuthorized(int requestedUserId)
        {
            return GetCallerId() == requestedUserId;
        }

        private async Task PostLogs(string eventType,int userId)
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
