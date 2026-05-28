using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report;
using PersonalFinanceBudgetTrackerAPI.Repository.Report;
using System.Security.Claims;
using Xunit;


namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class ReportControllerTests
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        private static ReportsController CreateController(
            Mock<IReportService> mockService,
            int callerId = 10)
        {
            var controller = new ReportsController(mockService.Object);

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

        private static ReportsController CreateControllerNoUserIdClaim(
            Mock<IReportService> mockService)
        {
            var controller = new ReportsController(mockService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
            return controller;
        }

        // ── DTO factories ────────────────────────────────────────────────

        private static List<MonthlyReportDto> MakeMonthlyData(
            int userId = 10, string month = "2024-03") =>
            new List<MonthlyReportDto>
            {
                new MonthlyReportDto
                {
                    AccountId    = 1,
                    AccountName  = "Main Bank",
                    Month        = month,
                    TotalIncome  = 5000.00m,
                    TotalExpense = 2500.00m,
                    NetAmount    = 2500.00m
                },
                new MonthlyReportDto
                {
                    AccountId    = 2,
                    AccountName  = "Savings",
                    Month        = month,
                    TotalIncome  = 1000.00m,
                    TotalExpense = 0.00m,
                    NetAmount    = 1000.00m
                }
            };

        private static List<YearlyReportDto> MakeYearlyData(
            int year = 2024, int months = 3) =>
            Enumerable.Range(1, months).Select(m => new YearlyReportDto
            {
                AccountId = 1,
                AccountName = "Main Bank",
                Year = year,
                Month = m,
                MonthName = new DateTime(year, m, 1).ToString("MMMM"),
                TotalIncome = 5000.00m,
                TotalExpense = 2000.00m,
                NetAmount = 3000.00m
            }).ToList();

        private static List<CategoryBreakdownDto> MakeCategoryData() =>
            new List<CategoryBreakdownDto>
            {
                new CategoryBreakdownDto
                {
                    AccountId    = 1,
                    AccountName  = "Main Bank",
                    CategoryId   = 1,
                    CategoryName = "Food",
                    Type         = "Expense",
                    Total        = 800.00m,
                    Count        = 12
                },
                new CategoryBreakdownDto
                {
                    AccountId    = 1,
                    AccountName  = "Main Bank",
                    CategoryId   = 2,
                    CategoryName = "Salary",
                    Type         = "Income",
                    Total        = 5000.00m,
                    Count        = 1
                }
            };

        private static NetWorthDto MakeNetWorthData() =>
            new NetWorthDto
            {
                SnapshotDate = DateTime.UtcNow,
                TotalAssets = 20000.00m,
                TotalLiabilit = 5000.00m,
                NetWorth = 15000.00m,
                Accounts = new List<AccountNetWorthDto>
                {
                    new AccountNetWorthDto
                    {
                        AccountId   = 1,
                        AccountName = "Main Bank",
                        AccountType = "Bank",
                        Balance     = 15000.00m,
                        IsAsset     = true
                    },
                    new AccountNetWorthDto
                    {
                        AccountId   = 2,
                        AccountName = "Credit Card",
                        AccountType = "Credit",
                        Balance     = 5000.00m,
                        IsAsset     = false
                    }
                }
            };

        // ===============================================================
        // GET /api/reports/monthly  —  GetMonthlyReport
        // ===============================================================

        [Fact]
        public async Task GetMonthlyReport_ReturnsOk_WithData_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IReportService>();
            var data = MakeMonthlyData(callerId, month);

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, month))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = true,
                           Message = "2 account(s) returned.",
                           Data = data
                       });

            var controller = CreateController(mockService, callerId);

            // Act
            var result = await controller.GetMonthlyReport(month);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var returnData = Xunit.Assert.IsAssignableFrom<IEnumerable<MonthlyReportDto>>(ok.Value);
            Xunit.Assert.Equal(2, returnData.Count());
            mockService.Verify(s => s.GetMonthlyReportAsync(callerId, month), Times.Once);
        }

        [Fact]
        public async Task GetMonthlyReport_ReturnsOk_WithEmptyList_WhenNoTransactions()
        {
            // Arrange
            const int callerId = 10;
            const string month = "2024-06";
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, month))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = true,
                           Data = new List<MonthlyReportDto>()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetMonthlyReport(month);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<MonthlyReportDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetMonthlyReport_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            // Act
            var result = await controller.GetMonthlyReport("2024-03");
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);
            mockService.Verify(s => s.GetMonthlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2024")]
        [InlineData("03-2024")]
        [InlineData("2024-13")]
        [InlineData("2024-00")]
        [InlineData("abcd-ef")]
        [InlineData(null)]
        public async Task GetMonthlyReport_ReturnsBadRequest_WhenMonthFormatInvalid(string badMonth)
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateController(mockService, callerId: 10);

            // Act
            var result = await controller.GetMonthlyReport(badMonth);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(400, badRequest.StatusCode);
            mockService.Verify(s => s.GetMonthlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("2024-01")]
        [InlineData("2024-12")]
        [InlineData("2023-06")]
        [InlineData("2025-09")]
        public async Task GetMonthlyReport_AcceptsAllValidMonthFormats(string validMonth)
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, validMonth))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = true,
                           Data = new List<MonthlyReportDto>()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetMonthlyReport(validMonth);

            Xunit.Assert.IsType<OkObjectResult>(result);
            mockService.Verify(s => s.GetMonthlyReportAsync(callerId, validMonth), Times.Once);
        }

        [Fact]
        public async Task GetMonthlyReport_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, "2024-03"))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = false,
                           Message = "Database error."
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetMonthlyReport("2024-03");
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Database error.", message);
        }

        [Fact]
        public async Task GetMonthlyReport_ReturnsCorrectFieldValues()
        {
            // Arrange
            const int callerId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, month))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = true,
                           Data = MakeMonthlyData(callerId, month)
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetMonthlyReport(month);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<MonthlyReportDto>>(ok.Value).ToList();

            // Assert field values on first record
            Xunit.Assert.Equal("Main Bank", data[0].AccountName);
            Xunit.Assert.Equal(5000.00m, data[0].TotalIncome);
            Xunit.Assert.Equal(2500.00m, data[0].TotalExpense);
            Xunit.Assert.Equal(2500.00m, data[0].NetAmount);
            Xunit.Assert.Equal(month, data[0].Month);
        }

        // ===============================================================
        // GET /api/reports/yearly  —  GetYearlyReport
        // ===============================================================

        [Fact]
        public async Task GetYearlyReport_ReturnsOk_WithData_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            const string year = "2024";
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetYearlyReportAsync(callerId, year))
                       .ReturnsAsync(new ReportResult<IEnumerable<YearlyReportDto>>
                       {
                           Success = true,
                           Message = "3 record(s) returned.",
                           Data = MakeYearlyData(2024, 3)
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetYearlyReport(year);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<YearlyReportDto>>(ok.Value);
            Xunit.Assert.Equal(3, data.Count());
            mockService.Verify(s => s.GetYearlyReportAsync(callerId, year), Times.Once);
        }

        [Fact]
        public async Task GetYearlyReport_ReturnsOk_WithEmptyList_WhenNoTransactions()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetYearlyReportAsync(callerId, "2020"))
                       .ReturnsAsync(new ReportResult<IEnumerable<YearlyReportDto>>
                       {
                           Success = true,
                           Data = new List<YearlyReportDto>()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetYearlyReport("2020");

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<YearlyReportDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetYearlyReport_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            var result = await controller.GetYearlyReport("2024");
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            mockService.Verify(s => s.GetYearlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("24")]
        [InlineData("20244")]
        [InlineData("abcd")]
        [InlineData("2024-03")]
        [InlineData(null)]
        public async Task GetYearlyReport_ReturnsBadRequest_WhenYearFormatInvalid(string badYear)
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateController(mockService, callerId: 10);

            var result = await controller.GetYearlyReport(badYear);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            Xunit.Assert.Equal(400, badRequest.StatusCode);
            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid year format. Use YYYY (e.g. 2024).", message);
            mockService.Verify(s => s.GetYearlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("2020")]
        [InlineData("2024")]
        [InlineData("2030")]
        public async Task GetYearlyReport_AcceptsAllValidYearFormats(string validYear)
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetYearlyReportAsync(callerId, validYear))
                       .ReturnsAsync(new ReportResult<IEnumerable<YearlyReportDto>>
                       {
                           Success = true,
                           Data = new List<YearlyReportDto>()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetYearlyReport(validYear);

            Xunit.Assert.IsType<OkObjectResult>(result);
            mockService.Verify(s => s.GetYearlyReportAsync(callerId, validYear), Times.Once);
        }

        [Fact]
        public async Task GetYearlyReport_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetYearlyReportAsync(callerId, "2024"))
                       .ReturnsAsync(new ReportResult<IEnumerable<YearlyReportDto>>
                       {
                           Success = false,
                           Message = "Yearly query failed."
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetYearlyReport("2024");
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetYearlyReport_ReturnsCorrectMonthNames()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();
            var yearlyData = MakeYearlyData(2024, 3);

            mockService.Setup(s => s.GetYearlyReportAsync(callerId, "2024"))
                       .ReturnsAsync(new ReportResult<IEnumerable<YearlyReportDto>>
                       {
                           Success = true,
                           Data = yearlyData
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetYearlyReport("2024");
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<YearlyReportDto>>(ok.Value).ToList();

            // Assert month names are correct
            Xunit.Assert.Equal("January", data[0].MonthName);
            Xunit.Assert.Equal("February", data[1].MonthName);
            Xunit.Assert.Equal("March", data[2].MonthName);
        }

        // ===============================================================
        // GET /api/reports/category-breakdown  —  GetCategoryBreakdown
        // ===============================================================

        [Fact]
        public async Task GetCategoryBreakdown_ReturnsOk_WithData_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, month))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = true,
                           Data = MakeCategoryData()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetCategoryBreakdown(month);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryBreakdownDto>>(ok.Value);
            Xunit.Assert.Equal(2, data.Count());
            mockService.Verify(s => s.GetCategoryBreakdownAsync(callerId, month), Times.Once);
        }

        [Fact]
        public async Task GetCategoryBreakdown_ReturnsOk_WithEmptyList_WhenNoData()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, "2024-06"))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = true,
                           Data = new List<CategoryBreakdownDto>()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetCategoryBreakdown("2024-06");

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryBreakdownDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetCategoryBreakdown_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            var result = await controller.GetCategoryBreakdown("2024-03");
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            mockService.Verify(s => s.GetCategoryBreakdownAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2024")]
        [InlineData("03-2024")]
        [InlineData("2024-13")]
        [InlineData(null)]
        public async Task GetCategoryBreakdown_ReturnsBadRequest_WhenMonthFormatInvalid(string badMonth)
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateController(mockService, callerId: 10);

            var result = await controller.GetCategoryBreakdown(badMonth);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            Xunit.Assert.Equal(400, badRequest.StatusCode);
            mockService.Verify(s => s.GetCategoryBreakdownAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetCategoryBreakdown_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, "2024-03"))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = false,
                           Message = "Category breakdown query failed."
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetCategoryBreakdown("2024-03");
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetCategoryBreakdown_ReturnsCorrectFieldValues()
        {
            // Arrange
            const int callerId = 10;
            const string month = "2024-03";
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, month))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = true,
                           Data = MakeCategoryData()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetCategoryBreakdown(month);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryBreakdownDto>>(ok.Value).ToList();

            // Assert breakdown fields
            Xunit.Assert.Equal("Food", data[0].CategoryName);
            Xunit.Assert.Equal("Expense", data[0].Type);
            Xunit.Assert.Equal(800.00m, data[0].Total);
            Xunit.Assert.Equal(12, data[0].Count);
            Xunit.Assert.Equal("Salary", data[1].CategoryName);
            Xunit.Assert.Equal("Income", data[1].Type);
        }

        [Fact]
        public async Task GetCategoryBreakdown_BothIncomeAndExpense_ReturnedTogether()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, "2024-03"))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = true,
                           Data = MakeCategoryData()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetCategoryBreakdown("2024-03");
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryBreakdownDto>>(ok.Value).ToList();

            // Assert both Income and Expense types present
            Xunit.Assert.Contains(data, d => d.Type == "Income");
            Xunit.Assert.Contains(data, d => d.Type == "Expense");
        }

        // ===============================================================
        // GET /api/reports/net-worth  —  GetNetWorth
        // ===============================================================

        [Fact]
        public async Task GetNetWorth_ReturnsOk_WithNetWorthData_WhenValid()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();
            var netWorthData = MakeNetWorthData();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = true,
                           Message = "Net worth calculated successfully.",
                           Data = netWorthData
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetNetWorth();

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsType<NetWorthDto>(ok.Value);
            Xunit.Assert.Equal(20000.00m, data.TotalAssets);
            Xunit.Assert.Equal(5000.00m, data.TotalLiabilit);
            Xunit.Assert.Equal(15000.00m, data.NetWorth);
            mockService.Verify(s => s.GetNetWorthAsync(callerId), Times.Once);
        }

        [Fact]
        public async Task GetNetWorth_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            var result = await controller.GetNetWorth();
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);
            mockService.Verify(s => s.GetNetWorthAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetNetWorth_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = false,
                           Message = "Net worth calculation failed."
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetNetWorth();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Net worth calculation failed.", message);
        }

        [Fact]
        public async Task GetNetWorth_ReturnsCorrectAccountBreakdown()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = true,
                           Data = MakeNetWorthData()
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetNetWorth();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsType<NetWorthDto>(ok.Value);

            // Assert account breakdown
            Xunit.Assert.Equal(2, data.Accounts.Count);
            Xunit.Assert.True(data.Accounts[0].IsAsset);
            Xunit.Assert.False(data.Accounts[1].IsAsset);
            Xunit.Assert.Equal("Bank", data.Accounts[0].AccountType);
            Xunit.Assert.Equal("Credit", data.Accounts[1].AccountType);
        }

        [Fact]
        public async Task GetNetWorth_ReturnsZeroNetWorth_WhenNoAccounts()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = true,
                           Data = new NetWorthDto
                           {
                               SnapshotDate = DateTime.UtcNow,
                               TotalAssets = 0,
                               TotalLiabilit = 0,
                               NetWorth = 0,
                               Accounts = new List<AccountNetWorthDto>()
                           }
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetNetWorth();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsType<NetWorthDto>(ok.Value);

            Xunit.Assert.Equal(0, data.NetWorth);
            Xunit.Assert.Empty(data.Accounts);
        }

        [Fact]
        public async Task GetNetWorth_NegativeNetWorth_WhenLiabilitiesExceedAssets()
        {
            // Arrange
            const int callerId = 10;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = true,
                           Data = new NetWorthDto
                           {
                               SnapshotDate = DateTime.UtcNow,
                               TotalAssets = 2000.00m,
                               TotalLiabilit = 8000.00m,
                               NetWorth = -6000.00m,    // negative net worth
                               Accounts = new List<AccountNetWorthDto>()
                           }
                       });

            var controller = CreateController(mockService, callerId);
            var result = await controller.GetNetWorth();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsType<NetWorthDto>(ok.Value);

            // Assert negative net worth is returned correctly
            Xunit.Assert.True(data.NetWorth < 0);
            Xunit.Assert.Equal(-6000.00m, data.NetWorth);
        }

        [Fact]
        public async Task GetNetWorth_CallsServiceOnceWithCorrectCallerId()
        {
            // Arrange
            const int callerId = 42;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetNetWorthAsync(callerId))
                       .ReturnsAsync(new ReportResult<NetWorthDto>
                       {
                           Success = true,
                           Data = MakeNetWorthData()
                       });

            var controller = CreateController(mockService, callerId);
            await controller.GetNetWorth();

            mockService.Verify(s => s.GetNetWorthAsync(callerId), Times.Once);
            mockService.Verify(s => s.GetNetWorthAsync(It.Is<int>(x => x != callerId)), Times.Never);
        }

        // ===============================================================
        // Cross-endpoint — 401 guard consistency
        // ===============================================================

        [Fact]
        public async Task AllEndpoints_Return401_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            // Act
            var monthly = await controller.GetMonthlyReport("2024-03");
            var yearly = await controller.GetYearlyReport("2024");
            var breakdown = await controller.GetCategoryBreakdown("2024-03");
            var netWorth = await controller.GetNetWorth();

            // Assert all return 401
            Xunit.Assert.IsType<UnauthorizedObjectResult>(monthly);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(yearly);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(breakdown);
            Xunit.Assert.IsType<UnauthorizedObjectResult>(netWorth);
        }

        [Fact]
        public async Task AllEndpoints_NeverCallService_WhenNoUserIdClaim()
        {
            // Arrange
            var mockService = new Mock<IReportService>();
            var controller = CreateControllerNoUserIdClaim(mockService);

            // Act
            await controller.GetMonthlyReport("2024-03");
            await controller.GetYearlyReport("2024");
            await controller.GetCategoryBreakdown("2024-03");
            await controller.GetNetWorth();

            // Assert — service never called when guard fires
            mockService.Verify(s => s.GetMonthlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            mockService.Verify(s => s.GetYearlyReportAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            mockService.Verify(s => s.GetCategoryBreakdownAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            mockService.Verify(s => s.GetNetWorthAsync(It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Service called with exact callerId
        // ===============================================================

        [Fact]
        public async Task GetMonthlyReport_PassesExactCallerIdToService()
        {
            // Arrange
            const int callerId = 55;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetMonthlyReportAsync(callerId, "2024-05"))
                       .ReturnsAsync(new ReportResult<IEnumerable<MonthlyReportDto>>
                       {
                           Success = true,
                           Data = new List<MonthlyReportDto>()
                       });

            var controller = CreateController(mockService, callerId);
            await controller.GetMonthlyReport("2024-05");

            mockService.Verify(s => s.GetMonthlyReportAsync(callerId, "2024-05"), Times.Once);
            mockService.Verify(s => s.GetMonthlyReportAsync(It.Is<int>(x => x != callerId), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetCategoryBreakdown_PassesExactCallerIdToService()
        {
            // Arrange
            const int callerId = 77;
            var mockService = new Mock<IReportService>();

            mockService.Setup(s => s.GetCategoryBreakdownAsync(callerId, "2024-07"))
                       .ReturnsAsync(new ReportResult<IEnumerable<CategoryBreakdownDto>>
                       {
                           Success = true,
                           Data = new List<CategoryBreakdownDto>()
                       });

            var controller = CreateController(mockService, callerId);
            await controller.GetCategoryBreakdown("2024-07");

            mockService.Verify(s => s.GetCategoryBreakdownAsync(callerId, "2024-07"), Times.Once);
        }
    }

}
