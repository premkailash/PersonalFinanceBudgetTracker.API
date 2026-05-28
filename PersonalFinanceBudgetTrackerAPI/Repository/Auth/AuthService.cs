using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Auth;
using PersonalFinanceBudgetTrackerAPI.Repository.User;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ITokenBlacklist _tokenBlacklist;

        public AuthService(
            AppDbContext db,
            IConfiguration config,
            ITokenBlacklist tokenBlacklist)
        {
            _db = db;
            _config = config;
            _tokenBlacklist = tokenBlacklist;
        }

        // ---------------------------------------------------------------
        // Register
        // ---------------------------------------------------------------
        public async Task<AuthResult> RegisterAsync(RegisterRequestDto request)
        {
            // Check if email already exists
            bool emailExists = await _db.Users
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (emailExists)
                return new AuthResult
                {
                    Success = false,
                    Message = $"An account with email '{request.Email}' already exists."
                };

            // Hash and salt password using BCrypt (work factor 12)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

            var user = new Models.Entity.User
            {
                Username = request.Username,
                Email = request.Email,
                Password = passwordHash,
                Role = "User",
                Is2FAEnabled = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return new AuthResult
            {
                Success = true,
                Message = "Registration successful. Please log in."
            };
        }

        // ---------------------------------------------------------------
        // Login
        // ---------------------------------------------------------------
        public async Task<AuthResult> LoginAsync(LoginRequestDto request)
        {
            // Check if email exists
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
                return new AuthResult
                {
                    Success = false,
                    Message = $"No account found with email '{request.Email}'."
                };

            // Verify password against stored BCrypt hash
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

            if (!isPasswordValid)
                return new AuthResult
                {
                    Success = false,
                    Message = "Invalid password. Please try again."
                };

            // Generate JWT token
            string token = GenerateJwtToken(user);

            return new AuthResult
            {
                Success = true,
                Message = "Login successful.",
                Token = token,
                Role = user.Role,
                UserId = user.UserId,
                UserName = user.Username
            };
        }

        // ---------------------------------------------------------------
        // Logout
        // ---------------------------------------------------------------
        public async Task<AuthResult> LogoutAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return new AuthResult
                {
                    Success = false,
                    Message = $"User with ID {userId} not found."
                };

            // Blacklist all tokens for this user by storing userId + invalidation timestamp
            await _tokenBlacklist.InvalidateUserTokensAsync(userId);

            return new AuthResult
            {
                Success = true,
                Message = "Logout successful. Token has been invalidated."
            };
        }

        // ---------------------------------------------------------------
        // JWT Token Generation
        // ---------------------------------------------------------------
        private string GenerateJwtToken(Models.Entity.User user)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]
                              ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role,               user.Role),
                new Claim(ClaimTypes.Name,               user.Username),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new Claim("userId",                      user.UserId.ToString()),
                new Claim("issuedAt",                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            int expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}
