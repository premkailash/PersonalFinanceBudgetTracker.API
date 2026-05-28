using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.SavingsGoal;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal;

namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class SavingsGoalsControllerTests
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        private static SavingsGoalsController CreateController(
            Mock<ISavingsGoalService> mockGoalService,
            Mock<ILogService> mockLogService,
            int callerId = 10)
        {
            var controller = new SavingsGoalsController(
                mockGoalService.Object,
                mockLogService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",        callerId.ToString()),
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

        private static SavingsGoalsController CreateControllerNoUserClaim(
            Mock<ISavingsGoalService> mockGoalService,
            Mock<ILogService> mockLogService)
        {
            var controller = new SavingsGoalsController(
                mockGoalService.Object,
                mockLogService.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        // ── Stub log service that always succeeds ────────────────────────
        private static Mock<ILogService> StubLog()
        {
            var mock = new Mock<ILogService>();
            mock.Setup(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()))
                .ReturnsAsync(new LogResult { Success = true });
            return mock;
        }

        // ── DTO factories ────────────────────────────────────────────────
        private static SavingsGoalResponseDto MakeGoalDto(
            int goalId = 1,
            int userId = 10,
            int accountId = 1) =>
            new SavingsGoalResponseDto
            {
                GoalId = goalId,
                UserId = userId,
                AccountId = accountId,
                Name = "Holiday Fund",
                TargetAmount = 5000.00m,
                CurrentAmount = 1500.00m,   // CurrentAmount + AutoContributeAmount
                TargetDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

        private static CreateSavingsGoalRequestDto MakeCreateRequest(int userId = 10) =>
            new CreateSavingsGoalRequestDto
            {
                UserId = userId,
                AccountId = 1,
                Name = "Holiday Fund",
                TargetAmount = 5000.00m,
                CurrentAmount = 0.00m,
                TargetDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 200.00m,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

        private static UpdateSavingsGoalRequestDto MakeUpdateRequest(int goalId = 1) =>
            new UpdateSavingsGoalRequestDto
            {
                GoalId = goalId,
                TargetAmount = 6000.00m,
                CurrentAmount = 2000.00m,
                TargetDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                AutoContributeAmount = 300.00m
            };

        private static ContributeRequestDto MakeContributeRequest(
            int goalId = 1,
            decimal amount = 1000.00m) =>
            new ContributeRequestDto
            {
                GoalId = goalId,
                AutoContributeAmount = amount
            };

        // ===============================================================
        // GET /api/goals?userId=  —  GetAllGoals
        // ===============================================================

        [Fact]
        public async Task GetAllGoals_ReturnsOk_WithGoalList_WhenValid()
        {
            // Arrange
            const int userId = 10;
            var mockGoal = new Mock<ISavingsGoalService>();
            var goals = new List<SavingsGoalResponseDto> { MakeGoalDto(1, userId), MakeGoalDto(2, userId) };

            mockGoal.Setup(s => s.GetAllGoalsAsync(userId))
                    .ReturnsAsync(new SavingsGoalListResult { Success = true, Data = goals });

            var controller = CreateController(mockGoal, StubLog(), callerId: userId);

            // Act
            var result = await controller.GetAllGoals(userId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<SavingsGoalResponseDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());
            mockGoal.Verify(s => s.GetAllGoalsAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetAllGoals_ReturnsOk_WithEmptyList_WhenNoGoals()
        {
            // Arrange
            const int userId = 10;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.GetAllGoalsAsync(userId))
                    .ReturnsAsync(new SavingsGoalListResult
                    {
                        Success = true,
                        Data = new List<SavingsGoalResponseDto>()
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId: userId);

            // Act
            var result = await controller.GetAllGoals(userId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<SavingsGoalResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetAllGoals_ReturnsForbid_WhenCallerIdMismatch()
        {
            // Arrange — caller 10, request user 99
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);

            // Act
            var result = await controller.GetAllGoals(userId: 99);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockGoal.Verify(s => s.GetAllGoalsAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetAllGoals_Returns500_WhenServiceFails()
        {
            // Arrange
            const int userId = 10;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.GetAllGoalsAsync(userId))
                    .ReturnsAsync(new SavingsGoalListResult { Success = false, Message = "DB error." });

            var controller = CreateController(mockGoal, StubLog(), callerId: userId);

            // Act
            var result = await controller.GetAllGoals(userId);

            // Assert
            var status = Xunit.Assert.IsType<ObjectResult>(result);
            Xunit.Assert.Equal(500, status.StatusCode);
        }

        [Fact]
        public async Task GetAllGoals_ReturnsForbid_WhenNoUserIdClaim()
        {
            // Arrange — callerId resolves to 0, userId = 10 → mismatch
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateControllerNoUserClaim(mockGoal, StubLog());

            // Act
            var result = await controller.GetAllGoals(userId: 10);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // GET /api/goals/{id}  —  GetGoalById
        // ===============================================================

        [Fact]
        public async Task GetGoalById_ReturnsOk_WhenGoalExistsAndBelongsToCaller()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.GetGoalByIdAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult { Success = true, Data = MakeGoalDto(goalId, callerId) });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.GetGoalById(goalId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<SavingsGoalResponseDto>(ok.Value);
            Xunit.Assert.Equal(goalId, dto.GoalId);
            mockGoal.Verify(s => s.GetGoalByIdAsync(goalId, callerId), Times.Once);
        }

        [Fact]
        public async Task GetGoalById_ReturnsNotFound_WhenGoalDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.GetGoalByIdAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Savings goal with ID {goalId} was not found."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.GetGoalById(goalId);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task GetGoalById_ReturnsForbid_WhenGoalBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 5;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.GetGoalByIdAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.GetGoalById(goalId);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetGoalById_ReturnsCorrectFields_InResponseBody()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 2;
            var mockGoal = new Mock<ISavingsGoalService>();

            var expected = new SavingsGoalResponseDto
            {
                GoalId = goalId,
                UserId = callerId,
                AccountId = 3,
                Name = "Car Fund",
                TargetAmount = 20000.00m,
                CurrentAmount = 4500.00m,
                TargetDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            mockGoal.Setup(s => s.GetGoalByIdAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult { Success = true, Data = expected });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.GetGoalById(goalId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<SavingsGoalResponseDto>(ok.Value);

            // Assert all fields
            Xunit.Assert.Equal("Car Fund", dto.Name);
            Xunit.Assert.Equal(20000.00m, dto.TargetAmount);
            Xunit.Assert.Equal(4500.00m, dto.CurrentAmount);
            Xunit.Assert.Equal(3, dto.AccountId);
        }

        // ===============================================================
        // POST /api/goals  —  CreateGoal
        // ===============================================================

        [Fact]
        public async Task CreateGoal_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.CreateGoalAsync(request))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Savings goal 'Holiday Fund' created successfully.",
                        Data = MakeGoalDto(1, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            var result = await controller.CreateGoal(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockGoal.Verify(s => s.CreateGoalAsync(request), Times.Once);
        }

        [Fact]
        public async Task CreateGoal_CallsLogService_AfterSuccessfulCreation()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.CreateGoalAsync(request))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Created.",
                        Data = MakeGoalDto(1, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.CreateGoal(request);

            // Assert log was called with correct event type
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Savings Goal Created" &&
                r.UserId == callerId)), Times.Once);
        }

        [Fact]
        public async Task CreateGoal_ReturnsForbid_WhenCallerIsNotOwner()
        {
            // Arrange — caller 10, request userId 99
            var mockGoal = new Mock<ISavingsGoalService>();
            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);

            // Act
            var result = await controller.CreateGoal(request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockGoal.Verify(s => s.CreateGoalAsync(It.IsAny<CreateSavingsGoalRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateGoal_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Name", "Name is required.");

            // Act
            var result = await controller.CreateGoal(new CreateSavingsGoalRequestDto { UserId = 10 });

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockGoal.Verify(s => s.CreateGoalAsync(It.IsAny<CreateSavingsGoalRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateGoal_ReturnsBadRequest_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.CreateGoalAsync(request))
                    .ReturnsAsync(new SavingsGoalResult { Success = false, Message = "DB error." });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.CreateGoal(request);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task CreateGoal_ReturnsCorrectBody_OnSuccess()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(callerId);
            var expectedDto = MakeGoalDto(1, callerId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.CreateGoalAsync(request))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Savings goal 'Holiday Fund' created successfully.",
                        Data = expectedDto
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.CreateGoal(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as SavingsGoalResponseDto;

            Xunit.Assert.Contains("created successfully", message);
            Xunit.Assert.Equal(expectedDto.GoalId, data?.GoalId);
        }

        [Fact]
        public async Task CreateGoal_LogEventContainsUserIdAndGoalId()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 7;
            var request = MakeCreateRequest(callerId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.CreateGoalAsync(request))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Created.",
                        Data = MakeGoalDto(goalId, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.CreateGoal(request);

            // Assert log event contains both user and goal IDs
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.Event.Contains(callerId.ToString()) &&
                r.Event.Contains(goalId.ToString()))), Times.Once);
        }

        // ===============================================================
        // PUT /api/goals/{id}  —  UpdateGoal
        // ===============================================================

        [Fact]
        public async Task UpdateGoal_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var request = MakeUpdateRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.UpdateGoalAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Savings goal updated successfully.",
                        Data = MakeGoalDto(goalId, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            var result = await controller.UpdateGoal(goalId, request);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockGoal.Verify(s => s.UpdateGoalAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task UpdateGoal_CallsLogService_AfterSuccessfulUpdate()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var request = MakeUpdateRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.UpdateGoalAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Updated.",
                        Data = MakeGoalDto(goalId, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.UpdateGoal(goalId, request);

            // Assert
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Savings Goal Updated" &&
                r.UserId == callerId)), Times.Once);
        }

        [Fact]
        public async Task UpdateGoal_ReturnsBadRequest_WhenRouteIdBodyIdMismatch()
        {
            // Arrange
            var mockGoal = new Mock<ISavingsGoalService>();
            var request = MakeUpdateRequest(goalId: 5);  // body = 5
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);

            // Act — route = 1, body = 5
            var result = await controller.UpdateGoal(id: 1, request);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("does not match", message);
            mockGoal.Verify(s => s.UpdateGoalAsync(It.IsAny<UpdateSavingsGoalRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGoal_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("TargetAmount", "Required.");

            // Act
            var result = await controller.UpdateGoal(1, new UpdateSavingsGoalRequestDto { GoalId = 1 });
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockGoal.Verify(s => s.UpdateGoalAsync(It.IsAny<UpdateSavingsGoalRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGoal_ReturnsNotFound_WhenGoalDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var request = MakeUpdateRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.UpdateGoalAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Savings goal with ID {goalId} was not found."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.UpdateGoal(goalId, request);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task UpdateGoal_ReturnsForbid_WhenGoalBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var request = MakeUpdateRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.UpdateGoalAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.UpdateGoal(goalId, request);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        // ===============================================================
        // DELETE /api/goals/{id}  —  DeleteGoal
        // ===============================================================

        [Fact]
        public async Task DeleteGoal_ReturnsOk_WhenGoalIsSuccessfullyDeleted()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.DeleteGoalAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = $"Savings goal with ID {goalId} deleted successfully."
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            var result = await controller.DeleteGoal(goalId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("deleted successfully", message);
            mockGoal.Verify(s => s.DeleteGoalAsync(goalId, callerId), Times.Once);
        }

        [Fact]
        public async Task DeleteGoal_CallsLogService_AfterSuccessfulDeletion()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 3;
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.DeleteGoalAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.DeleteGoal(goalId);

            // Assert
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Savings Goal Deleted" &&
                r.UserId == callerId &&
                r.Event.Contains(goalId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task DeleteGoal_ReturnsNotFound_WhenGoalDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.DeleteGoalAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Savings goal with ID {goalId} was not found."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.DeleteGoal(goalId);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task DeleteGoal_ReturnsForbid_WhenGoalBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 7;
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.DeleteGoalAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.DeleteGoal(goalId);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteGoal_LogServiceNeverCalled_WhenGoalNotFound()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.DeleteGoalAsync(goalId, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = "Not found."
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.DeleteGoal(goalId);

            // Assert — no log written if operation failed
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        // ===============================================================
        // POST /api/goals/{id}/contribute  —  Contribute
        // ===============================================================

        [Fact]
        public async Task Contribute_ReturnsOk_WhenContributionIsSuccessful()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var request = MakeContributeRequest(goalId, amount: 1000.00m);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Contribution of 1,000.00 added. New AutoContributeAmount: 6,000.00.",
                        Data = MakeGoalDto(goalId, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            var result = await controller.Contribute(goalId, request);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockGoal.Verify(s => s.ContributeAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task Contribute_CallsLogService_WithContributionDetails()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            const decimal contributionAmt = 1000.00m;
            var request = MakeContributeRequest(goalId, amount: contributionAmt);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Contribution added.",
                        Data = MakeGoalDto(goalId, callerId)
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.Contribute(goalId, request);

            // Assert log event contains userId, goalId and amount
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Savings Goal Contributed" &&
                r.UserId == callerId &&
                r.Event.Contains(goalId.ToString()) &&
                r.Event.Contains(contributionAmt.ToString()))), Times.Once);
        }

        [Fact]
        public async Task Contribute_ReturnsBadRequest_WhenRouteIdBodyIdMismatch()
        {
            // Arrange
            var mockGoal = new Mock<ISavingsGoalService>();
            var request = MakeContributeRequest(goalId: 5); // body = 5
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);

            // Act — route = 1, body = 5
            var result = await controller.Contribute(id: 1, request);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("does not match", message);
        }

        [Fact]
        public async Task Contribute_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("AutoContributeAmount", "Required.");

            // Act
            var result = await controller.Contribute(1, new ContributeRequestDto { GoalId = 1 });
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockGoal.Verify(s => s.ContributeAsync(It.IsAny<ContributeRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Contribute_ReturnsNotFound_WhenGoalDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var request = MakeContributeRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Savings goal with ID {goalId} was not found."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.Contribute(goalId, request);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task Contribute_ReturnsForbid_WhenGoalBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 2;
            var request = MakeContributeRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = false,
                        Message = "Not authorized."
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.Contribute(goalId, request);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Contribute_LogServiceNeverCalled_WhenContributionFails()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 999;
            var request = MakeContributeRequest(goalId);
            var mockGoal = new Mock<ISavingsGoalService>();
            var mockLog = StubLog();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = "Not found."
                    });

            var controller = CreateController(mockGoal, mockLog, callerId);

            // Act
            await controller.Contribute(goalId, request);

            // Assert — log never written if contribution failed
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task Contribute_ReturnsCorrectBody_OnSuccess()
        {
            // Arrange
            const int callerId = 10;
            const int goalId = 1;
            var request = MakeContributeRequest(goalId, amount: 500.00m);
            var expectedDto = MakeGoalDto(goalId, callerId);
            var mockGoal = new Mock<ISavingsGoalService>();

            mockGoal.Setup(s => s.ContributeAsync(request, callerId))
                    .ReturnsAsync(new SavingsGoalResult
                    {
                        Success = true,
                        Message = "Contribution added.",
                        Data = expectedDto
                    });

            var controller = CreateController(mockGoal, StubLog(), callerId);

            // Act
            var result = await controller.Contribute(goalId, request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var type = body.GetType();
            var data = type.GetProperty("data")?.GetValue(body) as SavingsGoalResponseDto;

            Xunit.Assert.NotNull(data);
            Xunit.Assert.Equal(goalId, data!.GoalId);
        }

        // ===============================================================
        // Guard Clauses — Service Never Called
        // ===============================================================

        [Fact]
        public async Task CreateGoal_ServiceNeverCalled_WhenModelStateInvalid()
        {
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Name", "Required.");

            await controller.CreateGoal(new CreateSavingsGoalRequestDto { UserId = 10 });

            mockGoal.Verify(s => s.CreateGoalAsync(It.IsAny<CreateSavingsGoalRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGoal_ServiceNeverCalled_WhenRouteBodyMismatch()
        {
            var mockGoal = new Mock<ISavingsGoalService>();
            var request = MakeUpdateRequest(goalId: 99);
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);

            await controller.UpdateGoal(id: 1, request);

            mockGoal.Verify(s => s.UpdateGoalAsync(It.IsAny<UpdateSavingsGoalRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Contribute_ServiceNeverCalled_WhenModelStateInvalid()
        {
            var mockGoal = new Mock<ISavingsGoalService>();
            var controller = CreateController(mockGoal, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("AutoContributeAmount", "Required.");

            await controller.Contribute(1, new ContributeRequestDto { GoalId = 1 });

            mockGoal.Verify(s => s.ContributeAsync(It.IsAny<ContributeRequestDto>(), It.IsAny<int>()), Times.Never);
        }
    }

}
