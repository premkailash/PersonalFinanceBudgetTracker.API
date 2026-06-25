using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Extensions;
using PersonalFinanceBudgetTrackerAPI.Models.Common;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Auth;
using PersonalFinanceBudgetTrackerAPI.Repository.Auth;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Controller
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]    // 10 req/min — per user or IP
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogService _logService;
        public AuthController(IAuthService authService,ILogService logService)
        {
            _authService = authService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // POST /api/auth/register
        // ---------------------------------------------------------------
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
                return Conflict(new { message = result.Message });
            
            await PostLogs(EventType.UserRegister, result?.UserId ?? 0);

            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // POST /api/auth/login
        // ---------------------------------------------------------------
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request);

            if (!result.Success)
                return Unauthorized(new { message = result.Message });


            await PostLogs(EventType.Login, result?.UserId ?? 0);

            return Ok(new
            {
                token = result.Token,
                role = result.Role,
                userId = result.UserId,
                userName = result.UserName
            });
        }

        // ---------------------------------------------------------------
        // POST /api/auth/logout
        // ---------------------------------------------------------------
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LogoutAsync(request.UserId);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            await PostLogs(EventType.Logout, result?.UserId ?? 0);

            return Ok(new { message = result.Message });
        }

        [NonAction]
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
