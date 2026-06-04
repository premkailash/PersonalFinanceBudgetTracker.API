using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Extensions;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Auth;
using PersonalFinanceBudgetTrackerAPI.Repository.Auth;

namespace PersonalFinanceBudgetTrackerAPI.Controller
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]    // 10 req/min — per user or IP
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
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

            return Ok(new { message = result.Message });
        }
    }

}
