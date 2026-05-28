using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;


namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    /// <summary>
    /// xUnit test suite for BudgetsController.
    /// Covers all branches across GetBudgetsByMonth, GetBudgetById,
    /// CreateBudget, UpdateBudget, DeleteBudget, and GetBudgetUtilization
    /// to achieve 80%+ code coverage.
    /// </summary>
    public class BudgetsControllerTests
    {
        // ===============================================================
        // Test Helpers & Factories
        // ===============================================================

        /// <summary>
        /// Creates a BudgetsController with a faked JWT ClaimsPrincipal
        /// containing the given callerId.
        /// </summary>
        private static BudgetsController CreateController(
            Mock<IBudgetService> mockService,
            Mock<ILogService> mockLogService,
            int callerId)
        {
            var controller = new BudgetsController(mockService.Object, mockLogService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",callerId.ToString()),
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
        /// Creates a BudgetsController with NO userId claim — simulates
        /// a missing / invalid token (callerId resolves to 0).
        /// </summary>
        private static BudgetsController CreateControllerWithoutUserIdClaim(
            Mock<IBudgetService> mockService,Mock<ILogService> mockLogService)
        {
            var controller = new BudgetsController(mockService.Object, mockLogService.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private static BudgetResponseDto MakeBudgetDto(
            int budgetId = 1,
            int userId = 10,
            int accountId = 1,
            int categoryId = 1,
            string month = "2024-03") =>
            new BudgetResponseDto
            {
                BudgetId = budgetId,
                UserId = userId,
                AccountId = accountId,
                AccountName = "Main Bank",
                CategoryId = categoryId,
                CategoryName = "Food",
                Name = "Monthly Food Budget",
                TargetAmount = 500.00m,
                CurrentAmount = 120.00m,
                TargetDate = new DateTime(2024, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 50.00m,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

        private static CreateBudgetRequestDto MakeCreateRequest(int userId = 10) =>
            new CreateBudgetRequestDto
            {
                UserId = userId,
                AccountId = 1,
                CategoryId = 1,
                Name = "Monthly Food Budget",
                TargetAmount = 500.00m,
                CurrentAmount = 0.00m,
                TargetDate = new DateTime(2024, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 50.00m
            };

        private static UpdateBudgetRequestDto MakeUpdateRequest(int budgetId = 1) =>
            new UpdateBudgetRequestDto
            {
                BudgetId = budgetId,
                Name = "Updated Budget",
                TargetAmount = 600.00m,
                CurrentAmount = 200.00m,
                TargetDate = new DateTime(2024, 4, 30, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 75.00m
            };

        // ===============================================================
        // GET /api/budgets?userId=&month=  —  GetBudgetsByMonth
        // ===============================================================

        [Fact]
        public async Task GetBudgetsByMonth_ReturnsOk_WithBudgetList_WhenValid()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var budgets = new List<BudgetResponseDto> { MakeBudgetDto(1, userId) };

            mockService.Setup(s => s.GetBudgetsByMonthAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = true,
                           Message = "1 budget(s) found.",
                           Data = budgets
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, month);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<BudgetResponseDto>>(ok.Value);
            Xunit.Assert.Single(data);

            mockService.Verify(s => s.GetBudgetsByMonthAsync(userId, month), Times.Once);
        }

        [Fact]
        public async Task GetBudgetsByMonth_ReturnsOk_WithEmptyList_WhenNoBudgetsForMonth()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-06";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetsByMonthAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = true,
                           Message = "0 budget(s) found.",
                           Data = new List<BudgetResponseDto>()
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, month);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<BudgetResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetBudgetsByMonth_ReturnsForbid_WhenCallerIdDoesNotMatchUserId()
        {
            // Arrange — caller 10 requests userId 99
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act
            var result = await controller.GetBudgetsByMonth(userId: 99, month: "2024-03");

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.GetBudgetsByMonthAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2024")]
        [InlineData("03-2024")]
        [InlineData("2024-13")]
        [InlineData("2024-00")]
        [InlineData("abcd-ef")]        
        public async Task GetBudgetsByMonth_ReturnsBadRequest_WhenMonthFormatIsInvalid(string invalidMonth)
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, invalidMonth);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            mockService.Verify(s => s.GetBudgetsByMonthAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("2024-01")]
        [InlineData("2024-12")]
        [InlineData("2023-06")]
        public async Task GetBudgetsByMonth_AcceptsValidMonthFormats(string validMonth)
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetsByMonthAsync(userId, validMonth))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = true,
                           Data = new List<BudgetResponseDto>()
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, validMonth);

            // Assert — valid formats pass through to service
            Xunit.Assert.IsType<OkObjectResult>(result);
            mockService.Verify(s => s.GetBudgetsByMonthAsync(userId, validMonth), Times.Once);
        }

        [Fact]
        public async Task GetBudgetsByMonth_Returns500_WhenServiceFails()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetsByMonthAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = false,
                           Message = "Database error."
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, month);

            // Assert
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);
            Xunit.Assert.Equal(500, statusResult.StatusCode);
        }

        // ===============================================================
        // GET /api/budgets/{id}  —  GetBudgetById
        // ===============================================================

        [Fact]
        public async Task GetBudgetById_ReturnsOk_WhenBudgetExistsAndBelongsToCaller()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetByIdAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Data = MakeBudgetDto(budgetId, callerId)
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetBudgetById(budgetId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var dto = Xunit.Assert.IsType<BudgetResponseDto>(ok.Value);
            Xunit.Assert.Equal(budgetId, dto.BudgetId);

            mockService.Verify(s => s.GetBudgetByIdAsync(budgetId, callerId), Times.Once);
        }

        [Fact]
        public async Task GetBudgetById_ReturnsNotFound_WhenBudgetDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 999;
            var mockService = new Mock<IBudgetService>();

            var mockLogService = new Mock<ILogService>();
            mockService.Setup(s => s.GetBudgetByIdAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = $"Budget with ID {budgetId} was not found."
                       });

            var controller = CreateController(mockService, mockLogService, callerId);

            // Act
            var result = await controller.GetBudgetById(budgetId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task GetBudgetById_ReturnsForbid_WhenBudgetBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 5;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetByIdAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetBudgetById(budgetId);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetBudgetById_PassesCallerIdZero_WhenNoUserIdClaim()
        {
            // Arrange — callerId resolves to 0 when claim is absent
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetByIdAsync(1, 0))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Not found."
                       });

            var controller = CreateControllerWithoutUserIdClaim(mockService,mockLogService);

            // Act
            var result = await controller.GetBudgetById(1);

            // Assert
            Xunit.Assert.IsType<NotFoundObjectResult>(result);
            mockService.Verify(s => s.GetBudgetByIdAsync(1, 0), Times.Once);
        }

        // ===============================================================
        // POST /api/budgets  —  CreateBudget
        // ===============================================================

        [Fact]
        public async Task CreateBudget_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateBudgetAsync(request))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Message = "Budget created successfully.",
                           Data = MakeBudgetDto(1, callerId)
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateBudget(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);

            mockService.Verify(s => s.CreateBudgetAsync(request), Times.Once);
        }

        [Fact]
        public async Task CreateBudget_ReturnsCreated_WithCorrectResponseBody()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var expectedDto = MakeBudgetDto(1, callerId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateBudgetAsync(request))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Message = "Budget created successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService, mockLogService, callerId);

            // Act
            var result = await controller.CreateBudget(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            // Assert body fields
            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as BudgetResponseDto;

            Xunit.Assert.Equal("Budget created successfully.", message);
            Xunit.Assert.Equal(expectedDto.BudgetId, data?.BudgetId);
        }

        [Fact]
        public async Task CreateBudget_ReturnsForbid_WhenCallerIsNotOwner()
        {
            // Arrange — caller 10 tries to create budget for user 99
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();
            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act
            var result = await controller.CreateBudget(request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.CreateBudgetAsync(It.IsAny<CreateBudgetRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateBudget_ReturnsConflict_WhenDuplicateBudgetExists()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.CreateBudgetAsync(request))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           IsDuplicate = true,
                           Message = "A budget for this category already exists."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateBudget(request);

            // Assert
            var conflict = Xunit.Assert.IsType<ConflictObjectResult>(result);
            Xunit.Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public async Task CreateBudget_ReturnsBadRequest_WhenServiceFailsWithoutDuplicate()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();


            mockService.Setup(s => s.CreateBudgetAsync(request))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           IsDuplicate = false,
                           Message = "Unexpected error."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.CreateBudget(request);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task CreateBudget_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);
            controller.ModelState.AddModelError("Name", "Budget name is required.");

            var request = new CreateBudgetRequestDto { UserId = 10 };

            // Act
            var result = await controller.CreateBudget(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.CreateBudgetAsync(It.IsAny<CreateBudgetRequestDto>()), Times.Never);
        }

        // ===============================================================
        // PUT /api/budgets/{id}  —  UpdateBudget
        // ===============================================================

        [Fact]
        public async Task UpdateBudget_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var request = MakeUpdateRequest(budgetId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateBudgetAsync(request, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Message = "Budget updated successfully.",
                           Data = MakeBudgetDto(budgetId, callerId)
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateBudget(budgetId, request);

            // Xunit.Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            mockService.Verify(s => s.UpdateBudgetAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task UpdateBudget_ReturnsOk_WithCorrectMessageAndData()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var request = MakeUpdateRequest(budgetId);
            var expectedDto = MakeBudgetDto(budgetId, callerId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateBudgetAsync(request, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Message = "Budget updated successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateBudget(budgetId, request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as BudgetResponseDto;

            // Assert
            Xunit.Assert.Equal("Budget updated successfully.", message);
            Xunit.Assert.Equal(expectedDto.BudgetId, data?.BudgetId);
        }

        [Fact]
        public async Task UpdateBudget_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);
            controller.ModelState.AddModelError("Name", "Required.");

            var request = new UpdateBudgetRequestDto { BudgetId = 1 };

            // Act
            var result = await controller.UpdateBudget(id: 1, request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.UpdateBudgetAsync(It.IsAny<UpdateBudgetRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBudget_ReturnsBadRequest_WhenRouteIdAndBodyBudgetIdMismatch()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();
            var request = MakeUpdateRequest(budgetId: 5);  // body says 5
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act — route says 1, body says 5
            var result = await controller.UpdateBudget(id: 1, request);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("does not match", message);

            mockService.Verify(s => s.UpdateBudgetAsync(It.IsAny<UpdateBudgetRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBudget_ReturnsNotFound_WhenBudgetDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 999;
            var request = MakeUpdateRequest(budgetId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateBudgetAsync(request, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = $"Budget with ID {budgetId} was not found."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.UpdateBudget(budgetId, request);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task UpdateBudget_ReturnsForbid_WhenBudgetBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var request = MakeUpdateRequest(budgetId);
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.UpdateBudgetAsync(request, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService, mockLogService, callerId);

            // Act
            var result = await controller.UpdateBudget(budgetId, request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // DELETE /api/budgets/{id}  —  DeleteBudget
        // ===============================================================

        [Fact]
        public async Task DeleteBudget_ReturnsOk_WhenBudgetIsSuccessfullyDeleted()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.DeleteBudgetAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = true,
                           Message = $"Budget with ID {budgetId} deleted successfully."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.DeleteBudget(budgetId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("deleted successfully", message);

            mockService.Verify(s => s.DeleteBudgetAsync(budgetId, callerId), Times.Once);
        }

        [Fact]
        public async Task DeleteBudget_ReturnsNotFound_WhenBudgetDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 999;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.DeleteBudgetAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = $"Budget with ID {budgetId} was not found."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.DeleteBudget(budgetId);

            // Assert
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task DeleteBudget_ReturnsForbid_WhenBudgetBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 7;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.DeleteBudgetAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Not authorized."
                       });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.DeleteBudget(budgetId);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteBudget_CallsServiceOnceWithCorrectIds()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 3;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.DeleteBudgetAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            await controller.DeleteBudget(budgetId);

            // Assert — strict: only called with the exact IDs
            mockService.Verify(s => s.DeleteBudgetAsync(budgetId, callerId), Times.Once);
            mockService.Verify(s => s.DeleteBudgetAsync(
                It.Is<int>(b => b != budgetId),
                It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // GET /api/budgets/utilization  —  GetBudgetUtilization
        // ===============================================================

        [Fact]
        public async Task GetBudgetUtilization_ReturnsOk_WithUtilizationList_WhenValid()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();
            var budgets = new List<BudgetResponseDto>
            {
                MakeBudgetDto(1, userId),
                MakeBudgetDto(2, userId)
            };

            mockService.Setup(s => s.GetBudgetUtilizationAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = true,
                           Message = "2 utilization record(s) found.",
                           Data = budgets
                       });

            var controller = CreateController(mockService, mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetUtilization(userId, month);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<BudgetResponseDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());

            mockService.Verify(s => s.GetBudgetUtilizationAsync(userId, month), Times.Once);
        }

        [Fact]
        public async Task GetBudgetUtilization_ReturnsForbid_WhenCallerIdDoesNotMatchUserId()
        {
            // Arrange — caller 10 requests utilization for user 99
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act
            var result = await controller.GetBudgetUtilization(userId: 99, month: "2024-03");

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockService.Verify(s => s.GetBudgetUtilizationAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2024")]
        [InlineData("03/2024")]
        [InlineData("2024-13")]        
        public async Task GetBudgetUtilization_ReturnsBadRequest_WhenMonthFormatIsInvalid(string invalidMonth)
        {
            // Arrange
            const int userId = 10;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();
            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetUtilization(userId, invalidMonth);

            // Assert
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            mockService.Verify(s => s.GetBudgetUtilizationAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetBudgetUtilization_Returns500_WhenServiceFails()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetUtilizationAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = false,
                           Message = "Service unavailable."
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetUtilization(userId, month);

            // Assert
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);
            Xunit.Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetBudgetUtilization_ReturnsOk_WithEmptyList_WhenNoBudgetsForMonth()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-06";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            mockService.Setup(s => s.GetBudgetUtilizationAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult
                       {
                           Success = true,
                           Data = new List<BudgetResponseDto>()
                       });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetUtilization(userId, month);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<BudgetResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        // ===============================================================
        // Response Field Assertions
        // ===============================================================

        [Fact]
        public async Task GetBudgetById_ReturnsCorrectFields_InResponseBody()
        {
            // Arrange
            const int callerId = 10;
            const int budgetId = 1;
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var expected = new BudgetResponseDto
            {
                BudgetId = budgetId,
                UserId = callerId,
                AccountId = 2,
                AccountName = "Savings Account",
                CategoryId = 3,
                CategoryName = "Groceries",
                Name = "Grocery Budget",
                TargetAmount = 300.00m,
                CurrentAmount = 75.00m,
                TargetDate = new DateTime(2024, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 25.00m,
                CreatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            };

            mockService.Setup(s => s.GetBudgetByIdAsync(budgetId, callerId))
                       .ReturnsAsync(new BudgetResult { Success = true, Data = expected });

            var controller = CreateController(mockService,mockLogService, callerId);

            // Act
            var result = await controller.GetBudgetById(budgetId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<BudgetResponseDto>(ok.Value);

            // Assert all fields
            Xunit.Assert.Equal(budgetId, dto.BudgetId);
            Xunit.Assert.Equal(callerId, dto.UserId);
            Xunit.Assert.Equal("Savings Account", dto.AccountName);
            Xunit.Assert.Equal("Groceries", dto.CategoryName);
            Xunit.Assert.Equal("Grocery Budget", dto.Name);
            Xunit.Assert.Equal(300.00m, dto.TargetAmount);
            Xunit.Assert.Equal(75.00m, dto.CurrentAmount);
            Xunit.Assert.Equal(25.00m, dto.AutoContributeAmount);
        }

        [Fact]
        public async Task GetBudgetsByMonth_ReturnsMultipleBudgets_WithCorrectCount()
        {
            // Arrange
            const int userId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var budgets = new List<BudgetResponseDto>
            {
                MakeBudgetDto(1, userId),
                MakeBudgetDto(2, userId),
                MakeBudgetDto(3, userId)
            };

            mockService.Setup(s => s.GetBudgetsByMonthAsync(userId, month))
                       .ReturnsAsync(new BudgetListResult { Success = true, Data = budgets });

            var controller = CreateController(mockService,mockLogService, callerId: userId);

            // Act
            var result = await controller.GetBudgetsByMonth(userId, month);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<BudgetResponseDto>>(ok.Value);

            // Xunit.Assert
            Xunit.Assert.Equal(3, data.Count());
        }

        // ===============================================================
        // Guard Clause — Service Never Called
        // ===============================================================

        [Fact]
        public async Task CreateBudget_ServiceNeverCalled_WhenModelStateInvalid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);
            controller.ModelState.AddModelError("TargetAmount", "Required.");

            // Act
            await controller.CreateBudget(new CreateBudgetRequestDto { UserId = 10 });

            // Assert
            mockService.Verify(s => s.CreateBudgetAsync(It.IsAny<CreateBudgetRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBudget_ServiceNeverCalled_WhenModelStateInvalid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);
            controller.ModelState.AddModelError("Name", "Required.");

            // Act
            await controller.UpdateBudget(1, new UpdateBudgetRequestDto { BudgetId = 1 });

            // Assert
            mockService.Verify(s => s.UpdateBudgetAsync(
                It.IsAny<UpdateBudgetRequestDto>(),
                It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBudget_ServiceNeverCalled_WhenRouteBodyMismatch()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var request = MakeUpdateRequest(budgetId: 99);
            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act — route = 1, body = 99 → early return
            await controller.UpdateBudget(id: 1, request);

            // Assert
            mockService.Verify(s => s.UpdateBudgetAsync(
                It.IsAny<UpdateBudgetRequestDto>(),
                It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetBudgetsByMonth_ServiceNeverCalled_WhenForbid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act — userId 99 != callerId 10
            await controller.GetBudgetsByMonth(userId: 99, month: "2024-03");

            // Assert
            mockService.Verify(s => s.GetBudgetsByMonthAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetBudgetUtilization_ServiceNeverCalled_WhenForbid()
        {
            // Arrange
            var mockService = new Mock<IBudgetService>();
            var mockLogService = new Mock<ILogService>();

            var controller = CreateController(mockService,mockLogService, callerId: 10);

            // Act — userId 99 != callerId 10
            await controller.GetBudgetUtilization(userId: 99, month: "2024-03");

            // Assert
            mockService.Verify(s => s.GetBudgetUtilizationAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }

}
