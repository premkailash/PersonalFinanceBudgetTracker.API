using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.User;
using PersonalFinanceBudgetTrackerAPI.Repository.User;

namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    /// <summary>
    /// xUnit test suite for UsersController.
    /// Covers all branches across GetAllUsers, GetUserById,
    /// UpdateUser and DeleteUser to achieve 80%+ code coverage.
    /// </summary>
    public class UsersControllerTests
    {
        // ===============================================================
        // Helpers
        // ===============================================================

        /// <summary>
        /// Builds a UsersController whose HttpContext carries a JWT-style
        /// ClaimsPrincipal for the given callerId and role.
        /// </summary>
        private static UsersController CreateController(
            Mock<IUserService> mockService,
            int callerId,
            string role = "User")
        {
            var controller = new UsersController(mockService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",         callerId.ToString()),
                new Claim(ClaimTypes.Role,  role),
                new Claim(ClaimTypes.Name,  $"user_{callerId}")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        /// <summary>
        /// Builds a UsersController whose HttpContext has NO userId claim —
        /// simulates a token that is missing the custom claim.
        /// </summary>
        private static UsersController CreateControllerWithoutUserIdClaim(
            Mock<IUserService> mockService,
            string role = "User")
        {
            var controller = new UsersController(mockService.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, role)
                // "userId" claim intentionally omitted
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        // ── Factories ───────────────────────────────────────────────────

        private static UserResponseDto MakeUserDto(int userId = 1) =>
            new UserResponseDto
            {
                UserId = userId,
                Username = $"user_{userId}",
                Email = $"user{userId}@example.com",
                Is2FAEnabled = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

        private static UpdateUserRequestDto MakeUpdateRequest(int userId = 1) =>
            new UpdateUserRequestDto
            {
                UserId = userId,
                Username = "updated_username",
                Is2FAEnabled = true
            };

        // ===============================================================
        // GET /api/users  —  GetAllUsers  (Admin only)
        // ===============================================================

        [Fact]
        public async Task GetAllUsers_ReturnsOk_WithUserList_WhenServiceSucceeds()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var users = new List<UserResponseDto> { MakeUserDto(1), MakeUserDto(2) };

            mockService.Setup(s => s.GetAllUsersAsync())
                       .ReturnsAsync(new UserListResult
                       {
                           Success = true,
                           Message = "2 user(s) retrieved successfully.",
                           Data = users
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.GetAllUsers();

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());

            mockService.Verify(s => s.GetAllUsersAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllUsers_ReturnsOk_WithEmptyList_WhenNoUsersExist()
        {
            // Arrange
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetAllUsersAsync())
                       .ReturnsAsync(new UserListResult
                       {
                           Success = true,
                           Message = "0 user(s) retrieved successfully.",
                           Data = new List<UserResponseDto>()
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.GetAllUsers();

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetAllUsers_Returns500_WhenServiceFails()
        {
            // Arrange
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetAllUsersAsync())
                       .ReturnsAsync(new UserListResult
                       {
                           Success = false,
                           Message = "Database connection error."
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.GetAllUsers();

            // Assert
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);
            Xunit.Assert.Equal(500, statusResult.StatusCode);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Database connection error.", message);
        }

        [Fact]
        public async Task GetAllUsers_Returns500_WithCorrectMessage_WhenServiceFails()
        {
            // Arrange
            const string errorMsg = "Unexpected server error.";
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetAllUsersAsync())
                       .ReturnsAsync(new UserListResult { Success = false, Message = errorMsg });

            var controller = CreateController(mockService, callerId: 1, role: "Admin");

            // Act
            var result = await controller.GetAllUsers();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            // Assert — verify exact message is propagated
            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal(errorMsg, message);
        }

        // ===============================================================
        // GET /api/users/{id}  —  GetUserById  (Admin or User)
        // ===============================================================

        [Fact]
        public async Task GetUserById_ReturnsOk_WhenAdminAccessesAnyUser()
        {
            // Arrange
            const int targetUserId = 5;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetUserByIdAsync(targetUserId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Message = "User retrieved successfully.",
                           Data = MakeUserDto(targetUserId)
                       });

            // Admin can access any user's profile
            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.GetUserById(targetUserId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var dto = Xunit.Assert.IsType<UserResponseDto>(ok.Value);
            Xunit.Assert.Equal(targetUserId, dto.UserId);

            mockService.Verify(s => s.GetUserByIdAsync(targetUserId), Times.Once);
        }

        [Fact]
        public async Task GetUserById_ReturnsOk_WhenUserAccessesOwnProfile()
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetUserByIdAsync(userId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Message = "User retrieved successfully.",
                           Data = MakeUserDto(userId)
                       });

            // User accessing their own profile (callerId == id)
            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.GetUserById(userId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
        }

        [Fact]
        public async Task GetUserById_ReturnsForbid_WhenUserTriesToAccessAnotherUsersProfile()
        {
            // Arrange — caller is user 10 but requests user 99
            var mockService = new Mock<IUserService>();
            var controller = CreateController(mockService, callerId: 10, role: "User");

            // Act
            var result = await controller.GetUserById(id: 99);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.GetUserByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetUserById_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var controller = CreateControllerWithoutUserIdClaim(mockService, role: "User");

            // Act
            var result = await controller.GetUserById(id: 1);

            // Assert
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
            Xunit.Assert.Equal(401, unauthorized.StatusCode);

            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);

            mockService.Verify(s => s.GetUserByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetUserById_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            const int userId = 999;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetUserByIdAsync(userId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = false,
                           Message = $"User with ID {userId} was not found."
                       });

            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.GetUserById(userId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task GetUserById_AdminRole_CallsService_EvenWhenIdDiffersFromCallerId()
        {
            // Arrange — Admin with callerId=99 accesses user id=1 (allowed)
            const int targetId = 1;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.GetUserByIdAsync(targetId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Data = MakeUserDto(targetId)
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            await controller.GetUserById(targetId);

            // Assert — service IS called for Admin regardless of id mismatch
            mockService.Verify(s => s.GetUserByIdAsync(targetId), Times.Once);
        }

        // ===============================================================
        // PUT /api/users/{id}  —  UpdateUser  (User only)
        // ===============================================================

        [Fact]
        public async Task UpdateUser_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            const int userId = 10;
            var request = MakeUpdateRequest(userId);
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.UpdateUserAsync(request))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Message = "User profile updated successfully.",
                           Data = MakeUserDto(userId)
                       });

            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.UpdateUser(userId, request);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("User profile updated successfully.", message);

            mockService.Verify(s => s.UpdateUserAsync(request), Times.Once);
        }

        [Fact]
        public async Task UpdateUser_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var controller = CreateController(mockService, callerId: 10, role: "User");
            controller.ModelState.AddModelError("Username", "Username is required.");

            var request = new UpdateUserRequestDto { UserId = 10 };

            // Act
            var result = await controller.UpdateUser(id: 10, request);

            // Assert
            Xunit.  Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUser_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var controller = CreateControllerWithoutUserIdClaim(mockService, role: "User");
            var request = MakeUpdateRequest(userId: 10);

            // Act
            var result = await controller.UpdateUser(id: 10, request);

            // Assert
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
            Xunit.Assert.Equal(401, unauthorized.StatusCode);

            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);

            mockService.Verify(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUser_ReturnsForbid_WhenCallerTriesToUpdateAnotherUser()
        {
            // Arrange — caller is 10, trying to update user 99
            var mockService = new Mock<IUserService>();
            var request = MakeUpdateRequest(userId: 99);
            var controller = CreateController(mockService, callerId: 10, role: "User");

            // Act
            var result = await controller.UpdateUser(id: 99, request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUser_ReturnsBadRequest_WhenRouteIdAndBodyUserIdMismatch()
        {
            // Arrange — route id = 10, but body UserId = 99
            var mockService = new Mock<IUserService>();
            var request = MakeUpdateRequest(userId: 99); // body says 99
            var controller = CreateController(mockService, callerId: 10, role: "User");

            // Act — route says 10
            var result = await controller.UpdateUser(id: 10, request);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("UserId in the request body does not match the route parameter.", message);

            mockService.Verify(s => s.UpdateUserAsync(It.IsAny<UpdateUserRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUser_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            const int userId = 10;
            var request = MakeUpdateRequest(userId);
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.UpdateUserAsync(request))
                       .ReturnsAsync(new UserResult
                       {
                           Success = false,
                           Message = $"User with ID {userId} was not found."
                       });

            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.UpdateUser(userId, request);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task UpdateUser_ReturnsOk_WithUpdatedUserData_InResponseBody()
        {
            // Arrange
            const int userId = 10;
            var request = MakeUpdateRequest(userId);
            var mockService = new Mock<IUserService>();
            var expectedDto = MakeUserDto(userId);

            mockService.Setup(s => s.UpdateUserAsync(request))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Message = "User profile updated successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.UpdateUser(userId, request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            // Assert — verify data is present in response body
            var body = ok.Value!;
            var type = body.GetType();

            Xunit.Assert.Equal("User profile updated successfully.",
                type.GetProperty("message")?.GetValue(body) as string);

            var data = type.GetProperty("data")?.GetValue(body) as UserResponseDto;
            Xunit.Assert.NotNull(data);
            Xunit.Assert.Equal(userId, data!.UserId);
        }

        // ===============================================================
        // DELETE /api/users/{id}  —  DeleteUser  (Admin only)
        // ===============================================================

        [Fact]
        public async Task DeleteUser_ReturnsOk_WhenUserIsSuccessfullyDeleted()
        {
            // Arrange
            const int targetUserId = 5;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.DeleteUserAsync(targetUserId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = true,
                           Message = $"User 'user_5' (ID: {targetUserId}) has been successfully deleted."
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.DeleteUser(targetUserId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("successfully deleted", message);

            mockService.Verify(s => s.DeleteUserAsync(targetUserId), Times.Once);
        }

        [Fact]
        public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            const int targetUserId = 999;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.DeleteUserAsync(targetUserId))
                       .ReturnsAsync(new UserResult
                       {
                           Success = false,
                           Message = $"User with ID {targetUserId} was not found."
                       });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.DeleteUser(targetUserId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task DeleteUser_CallsServiceOnce_WithCorrectId()
        {
            // Arrange
            const int targetUserId = 3;
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.DeleteUserAsync(targetUserId))
                       .ReturnsAsync(new UserResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            await controller.DeleteUser(targetUserId);

            // Assert — strict verification that only correct ID was passed
            mockService.Verify(s => s.DeleteUserAsync(targetUserId), Times.Once);
            mockService.Verify(s => s.DeleteUserAsync(It.Is<int>(x => x != targetUserId)), Times.Never);
        }

        // ===============================================================
        // Response Body — Field-level Assertions
        // ===============================================================

        [Fact]
        public async Task GetAllUsers_ReturnsCorrectUserFields_InResponseBody()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var users = new List<UserResponseDto>
            {
                new UserResponseDto
                {
                    UserId       = 1,
                    Username     = "alice",
                    Email        = "alice@example.com",
                    Is2FAEnabled = true,
                    CreatedAt    = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };

            mockService.Setup(s => s.GetAllUsersAsync())
                       .ReturnsAsync(new UserListResult { Success = true, Data = users });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            var result = await controller.GetAllUsers();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(ok.Value);

            // Assert field values
            var first = data.First();
            Xunit.Assert.Equal(1, first.UserId);
            Xunit.Assert.Equal("alice", first.Username);
            Xunit.Assert.Equal("alice@example.com", first.Email);
            Xunit.Assert.True(first.Is2FAEnabled);
        }

        [Fact]
        public async Task GetUserById_ReturnsCorrectUserFields_InResponseBody()
        {
            // Arrange
            const int userId = 7;
            var mockService = new Mock<IUserService>();
            var expected = new UserResponseDto
            {
                UserId = userId,
                Username = "bob",
                Email = "bob@example.com",
                Is2FAEnabled = false,
                CreatedAt = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc)
            };

            mockService.Setup(s => s.GetUserByIdAsync(userId))
                       .ReturnsAsync(new UserResult { Success = true, Data = expected });

            var controller = CreateController(mockService, callerId: userId, role: "User");

            // Act
            var result = await controller.GetUserById(userId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<UserResponseDto>(ok.Value);

            // Assert
            Xunit.Assert.Equal("bob", dto.Username);
            Xunit.Assert.Equal("bob@example.com", dto.Email);
            Xunit.Assert.False(dto.Is2FAEnabled);
        }

        // ===============================================================
        // Service Not Called — Guard Clause Verification
        // ===============================================================

        [Fact]
        public async Task UpdateUser_ServiceIsNeverCalled_WhenModelStateInvalid()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var controller = CreateController(mockService, callerId: 10, role: "User");
            controller.ModelState.AddModelError("Username", "Required.");

            // Act
            await controller.UpdateUser(10, new UpdateUserRequestDto { UserId = 10 });

            // Assert — strict: service must not be called at all
            mockService.Verify(
                s => s.UpdateUserAsync(It.IsAny<UpdateUserRequestDto>()),
                Times.Never);
        }

        [Fact]
        public async Task GetUserById_ServiceIsNeverCalled_WhenUserRoleAccessesDifferentId()
        {
            // Arrange
            var mockService = new Mock<IUserService>();
            var controller = CreateController(mockService, callerId: 10, role: "User");

            // Act
            await controller.GetUserById(id: 50);

            // Assert — service must not be called when Forbid is returned early
            mockService.Verify(s => s.GetUserByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUser_ServiceIsCalledOnce_ForValidRequest()
        {
            // Arrange
            var mockService = new Mock<IUserService>();

            mockService.Setup(s => s.DeleteUserAsync(It.IsAny<int>()))
                       .ReturnsAsync(new UserResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockService, callerId: 99, role: "Admin");

            // Act
            await controller.DeleteUser(id: 2);

            // Assert
            mockService.Verify(s => s.DeleteUserAsync(2), Times.Once);
        }
    }
}
