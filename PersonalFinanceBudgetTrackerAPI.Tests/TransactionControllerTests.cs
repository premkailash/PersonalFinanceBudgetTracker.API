using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Repository.Transaction;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;


namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class TransactionControllerTests
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        private static TransactionsController CreateController(
            Mock<ITransactionService> mockTxSvc,
            Mock<ITransactionImportService> mockImportSvc,
            Mock<ILogService> mockLogSvc,
            int callerId = 10)
        {
            var controller = new TransactionsController(
                mockTxSvc.Object,
                mockImportSvc.Object,
                mockLogSvc.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",        callerId.ToString()),
                new Claim(ClaimTypes.Role, "User")
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            return controller;
        }

        private static TransactionsController CreateControllerNoUserIdClaim(
            Mock<ITransactionService> mockTxSvc,
            Mock<ITransactionImportService> mockImportSvc,
            Mock<ILogService> mockLogSvc)
        {
            var controller = new TransactionsController(
                mockTxSvc.Object,
                mockImportSvc.Object,
                mockLogSvc.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private static Mock<ILogService> StubLog()
        {
            var mock = new Mock<ILogService>();
            mock.Setup(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()))
                .ReturnsAsync(new LogResult { Success = true });
            return mock;
        }

        private static Mock<ITransactionImportService> StubImport(bool success = true)
        {
            var mock = new Mock<ITransactionImportService>();
            mock.Setup(s => s.ImportAllLinkedAccountsAsync())
                .ReturnsAsync(new ImportResult
                {
                    Success = success,
                    Message = success ? "Import complete." : "Import failed.",
                    Data = success ? new ImportResultDto
                    {
                        TotalAccounts = 3,
                        TotalImported = 10,
                        TotalSkipped = 2,
                        Errors = new List<string>()
                    } : null
                });
            return mock;
        }

        // ── DTO factories ────────────────────────────────────────────────

        private static TransactionResponseDto MakeTransactionDto(
            int transactionId = 1,
            int accountId = 1,
            int callerId = 10) =>
            new TransactionResponseDto
            {
                TransactionId = transactionId,
                AccountId = accountId,
                AccountName = "Main Bank",
                Amount = 500.00m,
                Currency = "USD",
                Type = "Expense",
                CategoryId = 1,
                CategoryName = "Food",
                Description = "Grocery shopping",
                TransactionDate = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                IsRecurring = false,
                CreatedAt = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc)
            };

        private static CreateTransactionRequestDto MakeCreateRequest(
            int accountId = 1,
            string type = "Expense",
            decimal amount = 500.00m) =>
            new CreateTransactionRequestDto
            {
                AccountId = accountId,
                Amount = amount,
                Currency = "USD",
                Type = type,
                CategoryId = 1,
                Description = "Grocery shopping",
                TransactionDate = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                IsRecurring = false
            };

        private static UpdateTransactionRequestDto MakeUpdateRequest(
            int transactionId = 1,
            decimal amount = 750.00m) =>
            new UpdateTransactionRequestDto
            {
                TransactionId = transactionId,
                Amount = amount,
                Description = "Updated description",
                TransactionDate = new DateTime(2024, 5, 20, 0, 0, 0, DateTimeKind.Utc)
            };

        // ===============================================================
        // GET /api/transactions  —  GetTransactions
        // ===============================================================

        [Fact]
        public async Task GetTransactions_ReturnsOk_WithList_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var from = new DateTime(2024, 5, 1);
            var to = new DateTime(2024, 5, 31);
            var mockTxSvc = new Mock<ITransactionService>();
            var txList = new List<TransactionResponseDto>
            {
                MakeTransactionDto(1, accountId, callerId),
                MakeTransactionDto(2, accountId, callerId)
            };

            mockTxSvc.Setup(s => s.GetTransactionsAsync(accountId, from, to, callerId))
                     .ReturnsAsync(new TransactionListResult
                     {
                         Success = true,
                         Message = "2 transaction(s) found.",
                         Data = txList
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);

            // Act
            var result = await controller.GetTransactions(accountId, from, to);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<TransactionResponseDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());
            mockTxSvc.Verify(s => s.GetTransactionsAsync(accountId, from, to, callerId), Times.Once);
        }

        [Fact]
        public async Task GetTransactions_ReturnsOk_WithEmptyList_WhenNoTransactions()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var from = new DateTime(2024, 1, 1);
            var to = new DateTime(2024, 1, 31);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionsAsync(accountId, from, to, callerId))
                     .ReturnsAsync(new TransactionListResult
                     {
                         Success = true,
                         Data = new List<TransactionResponseDto>()
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactions(accountId, from, to);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<TransactionResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetTransactions_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var result = await controller.GetTransactions(1, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            mockTxSvc.Verify(s => s.GetTransactionsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetTransactions_ReturnsBadRequest_WhenToDateBeforeFromDate()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId: 10);

            // to < from — invalid range
            var from = new DateTime(2024, 5, 31);
            var to = new DateTime(2024, 5, 1);

            var result = await controller.GetTransactions(1, from, to);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            Xunit.Assert.Equal(400, badRequest.StatusCode);
            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("The 'to' date must be on or after the 'from' date.", message);
            mockTxSvc.Verify(s => s.GetTransactionsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetTransactions_ReturnsNotFound_WhenAccountDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 999;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionsAsync(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), callerId))
                     .ReturnsAsync(new TransactionListResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Account not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactions(accountId,
                new DateTime(2024, 5, 1), new DateTime(2024, 5, 31));

            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task GetTransactions_ReturnsForbid_WhenAccountBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 5;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionsAsync(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), callerId))
                     .ReturnsAsync(new TransactionListResult
                     {
                         Success = false,
                         NotFound = false,
                         Message = "Not authorized."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactions(accountId,
                new DateTime(2024, 5, 1), new DateTime(2024, 5, 31));

            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetTransactions_Returns504_WhenTimeout()
        {
            // Arrange
            const int callerId = 10;
            const int accountId = 1;
            var mockTxSvc = new Mock<ITransactionService>();

            // Simulate a task that never completes within the window
            mockTxSvc.Setup(s => s.GetTransactionsAsync(
                    accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), callerId))
                     .Returns(async () =>
                     {
                         await Task.Delay(TimeSpan.FromSeconds(35)); // longer than 30s
                         return new TransactionListResult { Success = true };
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);

            // Use a very short timeout for testing purposes — override via CancellationToken
            // We can't easily override the 30s hardcoded value, so we test the path via
            // a cancelled token by exercising the OperationCanceledException branch
            // using a pre-cancelled operation simulation
            mockTxSvc.Setup(s => s.GetTransactionsAsync(
                    accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), callerId))
                     .ThrowsAsync(new OperationCanceledException());

            var result = await controller.GetTransactions(accountId,
                new DateTime(2024, 5, 1), new DateTime(2024, 5, 31));
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(504, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("timed out", message);
        }

        // ===============================================================
        // GET /api/transactions/{id}  —  GetTransactionById
        // ===============================================================

        [Fact]
        public async Task GetTransactionById_ReturnsOk_WhenTransactionExists()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionByIdAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Data = MakeTransactionDto(transactionId, 1, callerId)
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactionById(transactionId);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<TransactionResponseDto>(ok.Value);
            Xunit.Assert.Equal(transactionId, dto.TransactionId);
            mockTxSvc.Verify(s => s.GetTransactionByIdAsync(transactionId, callerId), Times.Once);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var result = await controller.GetTransactionById(1);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
            mockTxSvc.Verify(s => s.GetTransactionByIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsNotFound_WhenTransactionDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 999;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionByIdAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = $"Transaction with ID {transactionId} was not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactionById(transactionId);

            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsForbid_WhenTransactionBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 5;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.GetTransactionByIdAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = false,
                         Message = "Not authorized."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactionById(transactionId);

            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsCorrectFields()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 3;
            var mockTxSvc = new Mock<ITransactionService>();
            var expected = new TransactionResponseDto
            {
                TransactionId = transactionId,
                AccountName = "Savings",
                Amount = 1200.00m,
                Currency = "USD",
                Type = "Income",
                CategoryName = "Salary",
                Description = "Monthly salary",
                TransactionDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                IsRecurring = true
            };

            mockTxSvc.Setup(s => s.GetTransactionByIdAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult { Success = true, Data = expected });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.GetTransactionById(transactionId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var dto = Xunit.Assert.IsType<TransactionResponseDto>(ok.Value);

            Xunit.Assert.Equal("Salary", dto.CategoryName);
            Xunit.Assert.Equal("Income", dto.Type);
            Xunit.Assert.Equal(1200.00m, dto.Amount);
            Xunit.Assert.True(dto.IsRecurring);
        }

        // ===============================================================
        // POST /api/transactions  —  CreateTransaction
        // ===============================================================

        [Fact]
        public async Task CreateTransaction_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(accountId: 1);
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Transaction 1 created successfully.",
                         Data = MakeTransactionDto(1, 1, callerId)
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            var result = await controller.CreateTransaction(request);

            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockTxSvc.Verify(s => s.CreateTransactionAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task CreateTransaction_CallsLogService_AfterSuccess()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 5;
            var request = MakeCreateRequest();
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Created.",
                         Data = MakeTransactionDto(transactionId, 1, callerId)
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.CreateTransaction(request);

            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Transaction Created" &&
                r.UserId == callerId &&
                r.Event.Contains(transactionId.ToString()) &&
                r.Event.Contains(callerId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Amount", "Required.");

            var result = await controller.CreateTransaction(new CreateTransactionRequestDto { AccountId = 1 });
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockTxSvc.Verify(s => s.CreateTransactionAsync(
                It.IsAny<CreateTransactionRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var result = await controller.CreateTransaction(MakeCreateRequest());
            Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsNotFound_WhenAccountDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(accountId: 999);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Account not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.CreateTransaction(request);

            Xunit.Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsBadRequest_WhenServiceFailsWithoutNotFound()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest();
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = false,
                         Message = "Unexpected error."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.CreateTransaction(request);

            Xunit.Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateTransaction_LogServiceNeverCalled_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest();
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult { Success = false, NotFound = true, Message = "Error." });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.CreateTransaction(request);

            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsCorrectResponseBody()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest();
            var expectedDto = MakeTransactionDto(1, 1, callerId);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Transaction 1 created successfully.",
                         Data = expectedDto
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.CreateTransaction(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as TransactionResponseDto;

            Xunit.Assert.Contains("created successfully", message);
            Xunit.Assert.Equal(expectedDto.TransactionId, data?.TransactionId);
        }

        // ===============================================================
        // PUT /api/transactions/{id}  —  UpdateTransaction
        // ===============================================================

        [Fact]
        public async Task UpdateTransaction_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var request = MakeUpdateRequest(transactionId);
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.UpdateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Transaction 1 updated successfully.",
                         Data = MakeTransactionDto(transactionId, 1, callerId)
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            var result = await controller.UpdateTransaction(transactionId, request);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockTxSvc.Verify(s => s.UpdateTransactionAsync(request, callerId), Times.Once);
        }

        [Fact]
        public async Task UpdateTransaction_CallsLogService_AfterSuccess()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var request = MakeUpdateRequest(transactionId);
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.UpdateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Updated.",
                         Data = MakeTransactionDto(transactionId, 1, callerId)
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.UpdateTransaction(transactionId, request);

            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Transaction Updated" &&
                r.UserId == callerId &&
                r.Event.Contains(transactionId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId: 10);
            controller.ModelState.AddModelError("Amount", "Required.");

            var result = await controller.UpdateTransaction(1, new UpdateTransactionRequestDto { TransactionId = 1 });
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockTxSvc.Verify(s => s.UpdateTransactionAsync(
                It.IsAny<UpdateTransactionRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsBadRequest_WhenRouteIdBodyIdMismatch()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var request = MakeUpdateRequest(transactionId: 99); // body = 99
            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId: 10);

            var result = await controller.UpdateTransaction(id: 1, request); // route = 1
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("does not match", message);
            mockTxSvc.Verify(s => s.UpdateTransactionAsync(
                It.IsAny<UpdateTransactionRequestDto>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var result = await controller.UpdateTransaction(1, MakeUpdateRequest(1));
            Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsNotFound_WhenTransactionDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 999;
            var request = MakeUpdateRequest(transactionId);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.UpdateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Transaction not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.UpdateTransaction(transactionId, request);

            Xunit.Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsForbid_WhenTransactionBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var request = MakeUpdateRequest(transactionId);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.UpdateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = false,
                         Message = "Not authorized."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.UpdateTransaction(transactionId, request);

            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UpdateTransaction_LogServiceNeverCalled_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var request = MakeUpdateRequest(transactionId);
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.UpdateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.UpdateTransaction(transactionId, request);

            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        // ===============================================================
        // DELETE /api/transactions/{id}  —  DeleteTransaction
        // ===============================================================

        [Fact]
        public async Task DeleteTransaction_ReturnsOk_WhenDeleted()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 1;
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.DeleteTransactionAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = $"Transaction {transactionId} deleted successfully."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            var result = await controller.DeleteTransaction(transactionId);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("deleted successfully", message);
            mockTxSvc.Verify(s => s.DeleteTransactionAsync(transactionId, callerId), Times.Once);
        }

        [Fact]
        public async Task DeleteTransaction_CallsLogService_AfterSuccess()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 2;
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.DeleteTransactionAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.DeleteTransaction(transactionId);

            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "Transaction Deleted" &&
                r.UserId == callerId &&
                r.Event.Contains(transactionId.ToString()) &&
                r.Event.Contains(callerId.ToString()))), Times.Once);
        }

        [Fact]
        public async Task DeleteTransaction_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var result = await controller.DeleteTransaction(1);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(result);
            mockTxSvc.Verify(s => s.DeleteTransactionAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteTransaction_ReturnsNotFound_WhenTransactionDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 999;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.DeleteTransactionAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Transaction not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.DeleteTransaction(transactionId);

            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);
        }

        [Fact]
        public async Task DeleteTransaction_ReturnsForbid_WhenTransactionBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 7;
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.DeleteTransactionAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = false,
                         Message = "Not authorized."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.DeleteTransaction(transactionId);

            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteTransaction_LogServiceNeverCalled_WhenNotFound()
        {
            // Arrange
            const int callerId = 10;
            const int transactionId = 999;
            var mockTxSvc = new Mock<ITransactionService>();
            var mockLog = StubLog();

            mockTxSvc.Setup(s => s.DeleteTransactionAsync(transactionId, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = false,
                         NotFound = true,
                         Message = "Not found."
                     });

            var controller = CreateController(mockTxSvc, StubImport(), mockLog, callerId);
            await controller.DeleteTransaction(transactionId);

            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        // ===============================================================
        // POST /api/transactions/import  —  ImportTransactions
        // ===============================================================

        [Fact]
        public async Task ImportTransactions_ReturnsOk_WhenImportSucceeds()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var mockImport = new Mock<ITransactionImportService>();

            mockImport.Setup(s => s.ImportAllLinkedAccountsAsync())
                      .ReturnsAsync(new ImportResult
                      {
                          Success = true,
                          Message = "Import complete. 10 new transaction(s) imported.",
                          Data = new ImportResultDto
                          {
                              TotalAccounts = 3,
                              TotalImported = 10,
                              TotalSkipped = 2,
                              Errors = new List<string>()
                          }
                      });

            var controller = CreateController(mockTxSvc, mockImport, StubLog());
            var result = await controller.ImportTransactions();

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("Import complete", message);

            var data = body.GetType().GetProperty("data")?.GetValue(body) as ImportResultDto;
            Xunit.Assert.Equal(10, data?.TotalImported);
            Xunit.Assert.Equal(2, data?.TotalSkipped);

            mockImport.Verify(s => s.ImportAllLinkedAccountsAsync(), Times.Once);
        }

        [Fact]
        public async Task ImportTransactions_Returns500_WhenImportFails()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var mockImport = new Mock<ITransactionImportService>();

            mockImport.Setup(s => s.ImportAllLinkedAccountsAsync())
                      .ReturnsAsync(new ImportResult
                      {
                          Success = false,
                          Message = "Import service unavailable."
                      });

            var controller = CreateController(mockTxSvc, mockImport, StubLog());
            var result = await controller.ImportTransactions();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Import service unavailable.", message);
        }

        [Fact]
        public async Task ImportTransactions_ReturnsOk_WithErrors_WhenSomeAccountsFailed()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var mockImport = new Mock<ITransactionImportService>();

            mockImport.Setup(s => s.ImportAllLinkedAccountsAsync())
                      .ReturnsAsync(new ImportResult
                      {
                          Success = true,
                          Message = "Import complete. 5 new transaction(s) imported.",
                          Data = new ImportResultDto
                          {
                              TotalAccounts = 5,
                              TotalImported = 5,
                              TotalSkipped = 0,
                              Errors = new List<string> { "Account 3: Connection timeout." }
                          }
                      });

            var controller = CreateController(mockTxSvc, mockImport, StubLog());
            var result = await controller.ImportTransactions();

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as ImportResultDto;

            Xunit.Assert.NotNull(data);
            Xunit.Assert.Equal(1, data!.Errors.Count);
            Xunit.Assert.Contains("Connection timeout", data.Errors[0]);
        }

        [Fact]
        public async Task ImportTransactions_CallsImportServiceOnce()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var mockImport = StubImport(success: true);

            var controller = CreateController(mockTxSvc, mockImport, StubLog());
            await controller.ImportTransactions();

            mockImport.Verify(s => s.ImportAllLinkedAccountsAsync(), Times.Once);
        }

        // ===============================================================
        // Cross-endpoint — 401 guard consistency
        // ===============================================================

        [Fact]
        public async Task UserEndpoints_Return401_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            var get = await controller.GetTransactionById(1);
            var create = await controller.CreateTransaction(MakeCreateRequest());
            var update = await controller.UpdateTransaction(1, MakeUpdateRequest(1));
            var delete = await controller.DeleteTransaction(1);

            Xunit.Assert.IsType<UnauthorizedObjectResult>(get);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(create);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(update);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(delete);
        }

        [Fact]
        public async Task UserEndpoints_NeverCallService_WhenNoUserIdClaim()
        {
            // Arrange
            var mockTxSvc = new Mock<ITransactionService>();
            var controller = CreateControllerNoUserIdClaim(mockTxSvc, StubImport(), StubLog());

            await controller.GetTransactionById(1);
            await controller.CreateTransaction(MakeCreateRequest());
            await controller.UpdateTransaction(1, MakeUpdateRequest(1));
            await controller.DeleteTransaction(1);

            mockTxSvc.Verify(s => s.GetTransactionByIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            mockTxSvc.Verify(s => s.CreateTransactionAsync(It.IsAny<CreateTransactionRequestDto>(), It.IsAny<int>()), Times.Never);
            mockTxSvc.Verify(s => s.UpdateTransactionAsync(It.IsAny<UpdateTransactionRequestDto>(), It.IsAny<int>()), Times.Never);
            mockTxSvc.Verify(s => s.DeleteTransactionAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Transaction type coverage
        // ===============================================================

        [Theory]
        [InlineData("Income")]
        [InlineData("Expense")]
        public async Task CreateTransaction_ReturnsCreated_ForBothTransactionTypes(string type)
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(type: type);
            var mockTxSvc = new Mock<ITransactionService>();

            mockTxSvc.Setup(s => s.CreateTransactionAsync(request, callerId))
                     .ReturnsAsync(new TransactionResult
                     {
                         Success = true,
                         Message = "Created.",
                         Data = new TransactionResponseDto
                         {
                             TransactionId = 1,
                             Type = type,
                             Amount = request.Amount
                         }
                     });

            var controller = CreateController(mockTxSvc, StubImport(), StubLog(), callerId);
            var result = await controller.CreateTransaction(request);

            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
        }
    }

}
