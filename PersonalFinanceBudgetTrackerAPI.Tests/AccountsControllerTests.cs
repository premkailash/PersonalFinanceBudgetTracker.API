using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PersonalFinanceBudgetTrackerAPI.Repository.Account;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class AccountsControllerTests
    {
        // ===============================================================
        // Helpers
        // ===============================================================

        /// <summary>
        /// Creates an AccountsController with a mocked IAccountService and
        /// a faked HttpContext that contains a JWT "userId" claim.
        /// </summary>
        private static AccountsController CreateController(
            Mock<IAccountService> mockService,
            Mock<ILogService> mockLogService,
            int callerId)
        {
            var controller = new AccountsController(mockService.Object, mockLogService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId", callerId.ToString()),
                new Claim(ClaimTypes.Role, "User")
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
        /// Creates a controller with NO userId claim (simulates a missing/invalid token).
        /// </summary>
        private static AccountsController CreateControllerWithoutUserClaim(
            Mock<IAccountService> mockService,Mock<ILogService> mockLogService)
        {
            var controller = new AccountsController(mockService.Object, mockLogService.Object);

            var identity = new ClaimsIdentity(new List<Claim>(), "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        private static AccountResponseDto MakeAccountDto(int accountId = 1, int userId = 10) =>
            new AccountResponseDto
            {
                AccountId = accountId,
                UserId = userId,
                AccountName = "My Bank",
                AccountType = "Bank",
                Currency = "USD",
                Balance = 1000.00m,
                LinkedAt = DateTime.UtcNow
            };

        private static CreateAccountRequestDto MakeCreateRequest(int userId = 10) =>
            new CreateAccountRequestDto
            {
                UserId = userId,
                AccountName = "My Bank",
                AccountType = "Bank",
                Currency = "USD",
                Balance = 500.00m,
                LinkedAt = DateTime.UtcNow
            };

        private static UpdateAccountRequestDto MakeUpdateRequest(int accountId = 1) =>
            new UpdateAccountRequestDto
            {
                AccountId = accountId,
                AccountName = "Updated Bank",
                AccountType = "Bank",
                Currency = "USD",
                Balance = 2000.00m
            };

        // ===============================================================
        // GET /api/accounts  —  GetAllAccounts
        // ===============================================================

        [Fact]
        public async Task GetAllAccounts_ReturnsOk_WhenUserIsAuthorizedAndAccountsExist()
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();
            var accounts = new List<AccountResponseDto> { MakeAccountDto(1, userId) };

            mockService.Setup(s => s.GetAllAccountsAsync(userId))
                       .ReturnsAsync(new AccountListResult
                       {
                           Success = true,
                           Message = "1 account(s) retrieved successfully.",
                           Data = accounts
                       });

            var controller = CreateController(mockService, mockLogService, callerId: userId);

            // Act
            var result = await controller.GetAllAccounts(userId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockService.Verify(s => s.GetAllAccountsAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetAllAccounts_ReturnsOk_WithEmptyList_WhenNoActiveAccounts()
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAllAccountsAsync(userId))
                       .ReturnsAsync(new AccountListResult
                       {
                           Success = true,
                           Message = "0 account(s) retrieved successfully.",
                           Data = new List<AccountResponseDto>()
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetAllAccounts(userId);

            // Xunit.Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
        }

        [Fact]
        public async Task GetAllAccounts_ReturnsForbid_WhenCallerIdDoesNotMatchRequestedUserId()
        {
            // Arrange — caller is user 10 but requests user 99's accounts
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act
            var result = await controller.GetAllAccounts(userId: 99);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.GetAllAccountsAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetAllAccounts_Returns500_WhenServiceFails()
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAllAccountsAsync(userId))
                       .ReturnsAsync(new AccountListResult
                       {
                           Success = false,
                           Message = "Database error."
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetAllAccounts(userId);

            // Assert
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);
            Xunit.Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetAllAccounts_ReturnsForbid_WhenNoUserIdClaimPresent()
        {
            // Arrange — controller has no userId claim (callerId resolves to 0)
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();
            var controller = CreateControllerWithoutUserClaim(mockService,mockLogService);

            // Act — requesting userId 10, but caller has no claim (0 != 10)
            var result = await controller.GetAllAccounts(userId: 10);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // GET /api/accounts/{id}  —  GetAccountById
        // ===============================================================

        [Fact]
        public async Task GetAccountById_ReturnsOk_WhenAccountExistsAndBelongsToCaller()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAccountByIdAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account retrieved successfully.",
                           Data = MakeAccountDto(accountId, callerId)
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetAccountById(accountId);

            // Xunit.Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
        }

        [Fact]
        public async Task GetAccountById_ReturnsNotFound_WhenAccountDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 999;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAccountByIdAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Account not found."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetAccountById(accountId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task GetAccountById_ReturnsForbid_WhenAccountBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 5;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAccountByIdAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetAccountById(accountId);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // POST /api/accounts  —  CreateAccount
        // ===============================================================

        [Fact]
        public async Task CreateAccount_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateAccountAsync(request))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account linked successfully.",
                           Data = MakeAccountDto(1, callerId)
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockService.Verify(s => s.CreateAccountAsync(request), Times.Once);
        }

        [Fact]
        public async Task CreateAccount_ReturnsForbid_WhenCallerIsNotOwner()
        {
            // Arrange — caller is 10 but request has userId 99
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.CreateAccountAsync(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateAccount_ReturnsConflict_WhenDuplicateAccountExists()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateAccountAsync(request))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           IsDuplicate = true,
                           Message = "Duplicate account."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            var conflict = Xunit.Assert.IsType<ConflictObjectResult>(result);
            Xunit.Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public async Task CreateAccount_ReturnsBadRequest_WhenServiceFailsWithoutDuplicate()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateAccountAsync(request))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           IsDuplicate = false,
                           Message = "Unexpected error."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task CreateAccount_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService, mockLogService, callerId: 10);
            controller.ModelState.AddModelError("AccountName", "Account name is required.");

            var request = new CreateAccountRequestDto { UserId = 10 };

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.CreateAccountAsync(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        // ===============================================================
        // PUT /api/accounts/{id}  —  UpdateAccount
        // ===============================================================

        [Fact]
        public async Task UpdateAccount_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var request = MakeUpdateRequest(accountId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateAccountAsync(request, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account updated successfully.",
                           Data = MakeAccountDto(accountId, callerId)
                       });

            var controller = CreateController(mockService, mockLogService, callerId);

            // Act
            var result = await controller.UpdateAccount(accountId, request);

            // Xunit.Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockService.Verify(s => s.UpdateAccountAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsBadRequest_WhenRouteIdAndBodyIdMismatch()
        {
            // Arrange
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            var request = MakeUpdateRequest(accountId: 5);  // body says 5
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act — route says 1, body says 5
            var result = await controller.UpdateAccount(id: 1, request);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);
            mockService.Verify(s => s.UpdateAccountAsync(It.IsAny<UpdateAccountRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);
            controller.ModelState.AddModelError("AccountName", "Required.");

            var request = new UpdateAccountRequestDto { AccountId = 1 };

            // Act
            var result = await controller.UpdateAccount(id: 1, request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.UpdateAccountAsync(It.IsAny<UpdateAccountRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsNotFound_WhenAccountDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 999;
            var request = MakeUpdateRequest(accountId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateAccountAsync(request, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Account not found."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateAccount(accountId, request);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsConflict_WhenDuplicateAccountExists()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var request = MakeUpdateRequest(accountId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateAccountAsync(request, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = false,
                           IsDuplicate = true,
                           Message = "Duplicate account."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateAccount(accountId, request);

            // Assert
            var conflict = Xunit.Assert.IsType<ConflictObjectResult>(result);
            Xunit.Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsForbid_WhenAccountBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var request = MakeUpdateRequest(accountId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateAccountAsync(request, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = false,
                           IsDuplicate = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateAccount(accountId, request);

            // Xunit.Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // DELETE /api/accounts/{id}  —  UnlinkAccount
        // ===============================================================

        [Fact]
        public async Task UnlinkAccount_ReturnsOk_WhenAccountIsSuccessfullyUnlinked()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UnlinkAccountAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account unlinked successfully."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UnlinkAccount(accountId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockService.Verify(s => s.UnlinkAccountAsync(accountId, callerId), Times.Once);
        }

        [Fact]
        public async Task UnlinkAccount_ReturnsNotFound_WhenAccountDoesNotExistOrAlreadyUnlinked()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 999;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UnlinkAccountAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Account not found or already unlinked."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UnlinkAccount(accountId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task UnlinkAccount_ReturnsForbid_WhenAccountBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 7;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UnlinkAccountAsync(accountId, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UnlinkAccount(accountId);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // Edge Cases  —  Missing / Zero UserId Claim
        // ===============================================================

        [Fact]
        public async Task GetAccountById_StillCallsService_WhenNoUserIdClaim()
        {
            // Arrange — no claim means callerId = 0
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetAccountByIdAsync(1, 0))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateControllerWithoutUserClaim(mockService,mockLogService);

            // Act
            var result = await controller.GetAccountById(1);

            // Assert — service is called with callerId = 0 and returns Forbid
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UnlinkAccount_StillCallsService_WhenNoUserIdClaim()
        {
            // Arrange — no claim means callerId = 0
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UnlinkAccountAsync(1, 0))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Account not found."
                       });

            var controller = CreateControllerWithoutUserClaim(mockService, mockLogService);

            // Act
            var result = await controller.UnlinkAccount(1);

            // Assert
            Xunit.Assert.IsType<NotFoundObjectResult>(result);
        }

        // ===============================================================
        // Response Body Assertions
        // ===============================================================

        [Fact]
        public async Task CreateAccount_ReturnsCorrectAccountData_InCreatedResponse()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();
            var expectedDto = MakeAccountDto(1, callerId);

            mockService.Setup(s => s.CreateAccountAsync(request))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account linked successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateAccount(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            // Assert — verify the response body contains the data and message
            Xunit.Assert.NotNull(created.Value);
            var value = created.Value!;
            var type = value.GetType();

            Xunit.Assert.Equal("Account linked successfully.", type.GetProperty("message")?.GetValue(value));
            Xunit.Assert.Equal(expectedDto, type.GetProperty("data")?.GetValue(value));
        }

        [Fact]
        public async Task GetAllAccounts_ReturnsCorrectListOfAccounts()
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();

            var accounts = new List<AccountResponseDto>
            {
                MakeAccountDto(1, userId),
                MakeAccountDto(2, userId)
            };

            mockService.Setup(s => s.GetAllAccountsAsync(userId))
                       .ReturnsAsync(new AccountListResult
                       {
                           Success = true,
                           Message = "2 account(s) retrieved.",
                           Data = accounts
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetAllAccounts(userId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            // Xunit.Assert
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<AccountResponseDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());
        }

        [Fact]
        public async Task UpdateAccount_ReturnsUpdatedAccountData_InOkResponse()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var request = MakeUpdateRequest(accountId);
            var mockService = new Mock<IAccountService>();
            var mockLogService = new Mock<ILogService>();
            var expectedDto = MakeAccountDto(accountId, callerId);

            mockService.Setup(s => s.UpdateAccountAsync(request, callerId))
                       .ReturnsAsync(new AccountResult
                       {
                           Success = true,
                           Message = "Account updated successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateAccount(accountId, request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            // Assert
            Xunit.Assert.NotNull(ok.Value);
            var value = ok.Value!;
            var type = value.GetType();

            Xunit.Assert.Equal("Account updated successfully.", type.GetProperty("message")?.GetValue(value));
            Xunit.Assert.Equal(expectedDto, type.GetProperty("data")?.GetValue(value));
        }
    }

}