using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.User;
using PersonalFinanceBudgetTrackerAPI.Repository.User;
using System.Security.Claims;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ---------------------------------------------------------------
        // GET /api/users
        // Admin only — returns all users
        // ---------------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _userService.GetAllUsersAsync();

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // GET /api/users/{id}
        // Admin or User — Admin can view any user, User can only view own profile
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> GetUserById(int id)
        {
            // Extract calling user's ID and role from JWT claims
            var callerIdClaim = User.FindFirst("userId")?.Value;
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (callerIdClaim == null)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            int callerId = int.Parse(callerIdClaim);

            // A User role can only access their own profile
            if (callerRole == "User" && callerId != id)
                return Forbid();

            var result = await _userService.GetUserByIdAsync(id);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // PUT /api/users/{id}
        // User only — update own profile
        // ---------------------------------------------------------------
        [HttpPut("{id:int}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ensure the token's userId matches the route id (users can only update themselves)
            var callerIdClaim = User.FindFirst("userId")?.Value;

            if (callerIdClaim == null)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            int callerId = int.Parse(callerIdClaim);

            if (callerId != id)
                return Forbid();

            // Ensure route id and body UserId are consistent
            if (request.UserId != id)
                return BadRequest(new { message = "UserId in the request body does not match the route parameter." });

            var result = await _userService.UpdateUserAsync(request);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // DELETE /api/users/{id}
        // Admin only — deactivate a user
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var result = await _userService.DeleteUserAsync(id);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
    }

}
