using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport;
using PersonalFinanceBudgetTrackerAPI.Repository.DataExport;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class DataExportControllerTests
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        private static DataExportController CreateController(
            Mock<IDataExportService> mockExportService,
            Mock<ILogService> mockLogService,
            int callerId = 10)
        {
            var controller = new DataExportController(
                mockExportService.Object,
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

        /// <summary>Controller with no userId claim — callerId resolves to 0.</summary>
        private static DataExportController CreateControllerNoUserIdClaim(
            Mock<IDataExportService> mockExportService,
            Mock<ILogService> mockLogService)
        {
            var controller = new DataExportController(
                mockExportService.Object,
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

        // ── Stub log service ─────────────────────────────────────────────
        private static Mock<ILogService> StubLog()
        {
            var mock = new Mock<ILogService>();
            mock.Setup(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()))
                .ReturnsAsync(new LogResult { Success = true });
            return mock;
        }

        // ── DTO factories ────────────────────────────────────────────────
        private static CreateDataExportRequestDto MakeCreateRequest(
            int userId = 10,
            int accountId = 1,
            string reportType = "Transaction",
            string reportOpts = "CSV",
            int dayOffset = 30) =>
            new CreateDataExportRequestDto
            {
                UserId = userId,
                AccountId = accountId,
                ReportType = reportType,
                FromDate = DateTime.UtcNow.AddDays(-dayOffset),
                ToDate = DateTime.UtcNow,
                ReportOptions = reportOpts
            };

        private static DataExportResponseDto MakeExportResponseDto(
            int exportId = 1,
            int userId = 10,
            int accountId = 1,
            string reportType = "Transaction",
            string reportOpts = "CSV",
            bool generated = false,
            string? link = null) =>
            new DataExportResponseDto
            {
                ExportId = exportId,
                ReportType = reportType,
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow,
                UserId = userId,
                AccountId = accountId,
                IsGenerated = generated,
                ReportOptions = reportOpts,
                ReportLink = link,
                Timestamp = DateTime.UtcNow
            };

        private static DataExportDownloadDto MakeDownloadDto(
            int exportId = 1,
            bool generated = true,
            string link = "https://storage.example.com/exports/report_1.csv") =>
            new DataExportDownloadDto
            {
                ExportId = exportId,
                IsGenerated = generated,
                ReportLink = link
            };

        // ===============================================================
        // POST /api/export/transactions  —  RequestExport
        // ===============================================================

        [Fact]
        public async Task RequestExport_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockExport = new Mock<IDataExportService>();
            var mockLog = StubLog();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Export request submitted successfully. Export ID: 1.",
                          Data = MakeExportResponseDto(1, callerId)
                      });

            var controller = CreateController(mockExport, mockLog, callerId);

            // Act
            var result = await controller.RequestExport(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockExport.Verify(s => s.RequestExportAsync(request), Times.Once);
        }

        [Fact]
        public async Task RequestExport_ReturnsCreated_WithCorrectResponseBody()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var expectedDto = MakeExportResponseDto(1, callerId);
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Export request submitted successfully.",
                          Data = expectedDto
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as DataExportResponseDto;

            // Assert
            Xunit.Assert.Contains("submitted successfully", message);
            Xunit.Assert.Equal(expectedDto.ExportId, data?.ExportId);
            Xunit.Assert.Equal(expectedDto.ReportType, data?.ReportType);
            Xunit.Assert.Equal(expectedDto.ReportOptions, data?.ReportOptions);
            Xunit.Assert.False(data?.IsGenerated);
            Xunit.Assert.Null(data?.ReportLink);
        }

        [Fact]
        public async Task RequestExport_ReturnsForbid_WhenCallerIsNotOwner()
        {
            // Arrange — caller 10 tries to request export for user 99
            var mockExport = new Mock<IDataExportService>();
            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockExport, StubLog(), callerId: 10);

            // Act
            var result = await controller.RequestExport(request);

            // Assert
            Xunit.Assert.IsType<ForbidResult>(result);
            mockExport.Verify(s => s.RequestExportAsync(It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockExport = new Mock<IDataExportService>();
            var controller = CreateController(mockExport, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("ReportType", "ReportType is required.");

            var request = new CreateDataExportRequestDto { UserId = 10, AccountId = 1 };

            // Act
            var result = await controller.RequestExport(request);

            // Assert
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockExport.Verify(s => s.RequestExportAsync(It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_ReturnsBadRequest_WhenToDateIsBeforeFromDate()
        {
            // Arrange
            const int callerId = 10;
            var mockExport = new Mock<IDataExportService>();
            var request = new CreateDataExportRequestDto
            {
                UserId = callerId,
                AccountId = 1,
                ReportType = "Transaction",
                FromDate = DateTime.UtcNow,                // FromDate = now
                ToDate = DateTime.UtcNow.AddDays(-10),   // ToDate is BEFORE FromDate
                ReportOptions = "CSV"
            };

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);
            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(400, badRequest.StatusCode);
            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("ToDate must be later than FromDate.", message);

            mockExport.Verify(s => s.RequestExportAsync(It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_ReturnsBadRequest_WhenToDateEqualsFromDate()
        {
            // Arrange
            const int callerId = 10;
            var sameDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var mockExport = new Mock<IDataExportService>();
            var request = new CreateDataExportRequestDto
            {
                UserId = callerId,
                AccountId = 1,
                ReportType = "Budget",
                FromDate = sameDate,
                ToDate = sameDate,   // Equal — not strictly greater
                ReportOptions = "PDF"
            };

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);
            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockExport.Verify(s => s.RequestExportAsync(It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_Returns500_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = false,
                          Message = "Database error while creating export."
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("Database error", message);
        }

        [Fact]
        public async Task RequestExport_CallsLogService_AfterSuccessfulRequest()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockExport = new Mock<IDataExportService>();
            var mockLog = StubLog();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Export submitted.",
                          Data = MakeExportResponseDto(1, callerId)
                      });

            var controller = CreateController(mockExport, mockLog, callerId);

            // Act
            await controller.RequestExport(request);

            // Assert log called with System event type and correct userId
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.EventType == "System" &&
                r.UserId == callerId)), Times.Once);
        }

        [Fact]
        public async Task RequestExport_LogEventContainsExportIdAndReportType()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 7;
            var request = MakeCreateRequest(userId: callerId, reportType: "Budget", reportOpts: "PDF");
            var mockExport = new Mock<IDataExportService>();
            var mockLog = StubLog();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Export submitted.",
                          Data = MakeExportResponseDto(exportId, callerId, reportType: "Budget", reportOpts: "PDF")
                      });

            var controller = CreateController(mockExport, mockLog, callerId);

            // Act
            await controller.RequestExport(request);

            // Assert event string contains userId, exportId, reportType and format
            mockLog.Verify(s => s.CreateLogAsync(It.Is<CreateLogRequestDto>(r =>
                r.Event.Contains(callerId.ToString()) &&
                r.Event.Contains(exportId.ToString()) &&
                r.Event.Contains("Budget") &&
                r.Event.Contains("PDF"))), Times.Once);
        }

        [Fact]
        public async Task RequestExport_LogServiceNeverCalled_WhenServiceFails()
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockExport = new Mock<IDataExportService>();
            var mockLog = StubLog();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult { Success = false, Message = "Error." });

            var controller = CreateController(mockExport, mockLog, callerId);

            // Act
            await controller.RequestExport(request);

            // Assert — no log written if export request failed
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_LogServiceNeverCalled_WhenForbid()
        {
            // Arrange — caller 10 requests export for user 99
            var mockExport = new Mock<IDataExportService>();
            var mockLog = StubLog();
            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockExport, mockLog, callerId: 10);

            // Act
            await controller.RequestExport(request);

            // Assert
            mockLog.Verify(s => s.CreateLogAsync(It.IsAny<CreateLogRequestDto>()), Times.Never);
        }

        // ---------------------------------------------------------------
        // Report type + format combinations
        // ---------------------------------------------------------------
        [Theory]
        [InlineData("Transaction", "CSV")]
        [InlineData("Transaction", "PDF")]
        [InlineData("Budget", "CSV")]
        [InlineData("Budget", "PDF")]
        public async Task RequestExport_ReturnsCreated_ForAllValidReportTypesAndFormats(
            string reportType, string reportOptions)
        {
            // Arrange
            const int callerId = 10;
            var request = MakeCreateRequest(
                userId: callerId,
                reportType: reportType,
                reportOpts: reportOptions);

            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Export submitted.",
                          Data = MakeExportResponseDto(1, callerId,
                                        reportType: reportType,
                                        reportOpts: reportOptions)
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);

            // Assert
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as DataExportResponseDto;
            Xunit.Assert.Equal(reportType, data?.ReportType);
            Xunit.Assert.Equal(reportOptions, data?.ReportOptions);
        }

        // ===============================================================
        // GET /api/export/{export_id}  —  GetExportDownload
        // ===============================================================

        [Fact]
        public async Task GetExportDownload_ReturnsOk_WithReportLink_WhenExportIsReady()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 1;
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = true,
                          Message = "Export is ready for download.",
                          Data = MakeDownloadDto(exportId, generated: true,
                                        link: "https://storage.example.com/exports/report_1.csv")
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockExport.Verify(s => s.GetExportDownloadAsync(exportId, callerId), Times.Once);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsOk_WithReportLink_InResponseBody()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 3;
            const string reportLink = "https://storage.example.com/exports/report_3.pdf";
            var mockExport = new Mock<IDataExportService>();

            var expectedDownload = MakeDownloadDto(exportId, generated: true, link: reportLink);

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = true,
                          Message = "Export is ready for download.",
                          Data = expectedDownload
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as DataExportDownloadDto;

            // Assert body fields
            Xunit.Assert.Equal("Export is ready for download.", message);
            Xunit.Assert.Equal(exportId, data?.ExportId);
            Xunit.Assert.Equal(reportLink, data?.ReportLink);
            Xunit.Assert.True(data?.IsGenerated);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsOk_WithNotReadyMessage_WhenExportStillProcessing()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 2;
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = false,
                          NotFound = false,
                          NotReady = true,
                          Message = "Export is still being processed. Please try again shortly.",
                          Data = new DataExportDownloadDto
                          {
                              ExportId = exportId,
                              IsGenerated = false,
                              ReportLink = null
                          }
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            // Assert — still 200 but message indicates not ready yet
            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("still being processed", message);

            var data = body.GetType().GetProperty("data")?.GetValue(body) as DataExportDownloadDto;
            Xunit.Assert.False(data?.IsGenerated);
            Xunit.Assert.Null(data?.ReportLink);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsNotFound_WhenExportDoesNotExist()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 999;
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = false,
                          NotFound = true,
                          Message = $"Export with ID {exportId} was not found."
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);
            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(404, notFound.StatusCode);
            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsForbid_WhenExportBelongsToAnotherUser()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 5;
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = false,
                          NotFound = false,
                          NotReady = false,
                          Message = "You are not authorized to access this export."
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);
            Xunit.Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsUnauthorized_WhenNoUserIdClaim()
        {
            // Arrange — callerId resolves to 0
            var mockExport = new Mock<IDataExportService>();
            var controller = CreateControllerNoUserIdClaim(mockExport, StubLog());

            // Act
            var result = await controller.GetExportDownload(export_id: 1);
            var unauthorized = Xunit.Assert.IsType<UnauthorizedObjectResult>(result);

            // Assert
            Xunit.Assert.Equal(401, unauthorized.StatusCode);
            var body = unauthorized.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Invalid token. User ID claim is missing.", message);

            mockExport.Verify(s => s.GetExportDownloadAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetExportDownload_CallsServiceOnceWithCorrectIds()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 4;
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = true,
                          Message = "Ready.",
                          Data = MakeDownloadDto(exportId)
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            await controller.GetExportDownload(exportId);

            // Assert strict ID verification
            mockExport.Verify(s => s.GetExportDownloadAsync(exportId, callerId), Times.Once);
            mockExport.Verify(s => s.GetExportDownloadAsync(
                It.Is<int>(x => x != exportId), It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Guard Clauses — Service Never Called
        // ===============================================================

        [Fact]
        public async Task RequestExport_ServiceNeverCalled_WhenModelStateInvalid()
        {
            var mockExport = new Mock<IDataExportService>();
            var controller = CreateController(mockExport, StubLog(), callerId: 10);
            controller.ModelState.AddModelError("ReportType", "Required.");
            controller.ModelState.AddModelError("ReportOptions", "Required.");

            await controller.RequestExport(new CreateDataExportRequestDto { UserId = 10 });

            mockExport.Verify(s => s.RequestExportAsync(
                It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_ServiceNeverCalled_WhenForbid()
        {
            var mockExport = new Mock<IDataExportService>();
            // caller = 10, request for userId = 99
            var request = MakeCreateRequest(userId: 99);
            var controller = CreateController(mockExport, StubLog(), callerId: 10);

            await controller.RequestExport(request);

            mockExport.Verify(s => s.RequestExportAsync(
                It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task RequestExport_ServiceNeverCalled_WhenDateRangeInvalid()
        {
            // Arrange
            const int callerId = 10;
            var mockExport = new Mock<IDataExportService>();
            var request = new CreateDataExportRequestDto
            {
                UserId = callerId,
                AccountId = 1,
                ReportType = "Transaction",
                FromDate = DateTime.UtcNow,
                ToDate = DateTime.UtcNow.AddDays(-5), // invalid
                ReportOptions = "CSV"
            };

            var controller = CreateController(mockExport, StubLog(), callerId);

            await controller.RequestExport(request);

            mockExport.Verify(s => s.RequestExportAsync(
                It.IsAny<CreateDataExportRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task GetExportDownload_ServiceNeverCalled_WhenNoUserIdClaim()
        {
            var mockExport = new Mock<IDataExportService>();
            var controller = CreateControllerNoUserIdClaim(mockExport, StubLog());

            await controller.GetExportDownload(1);

            mockExport.Verify(s => s.GetExportDownloadAsync(
                It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ===============================================================
        // Response field assertions — IsGenerated initial state
        // ===============================================================

        [Fact]
        public async Task RequestExport_IsGeneratedFalse_AndReportLinkNull_InCreatedResponse()
        {
            // Arrange — new export should always be IsGenerated=false, ReportLink=null
            const int callerId = 10;
            var request = MakeCreateRequest(userId: callerId);
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.RequestExportAsync(request))
                      .ReturnsAsync(new DataExportResult
                      {
                          Success = true,
                          Message = "Submitted.",
                          Data = MakeExportResponseDto(
                              exportId: 1,
                              userId: callerId,
                              generated: false,
                              link: null)
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.RequestExport(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as DataExportResponseDto;

            // Assert newly created export is always pending
            Xunit.Assert.False(data?.IsGenerated);
            Xunit.Assert.Null(data?.ReportLink);
        }

        [Fact]
        public async Task GetExportDownload_ReturnsCorrectReportLink_ForPdfExport()
        {
            // Arrange
            const int callerId = 10;
            const int exportId = 8;
            const string pdfLink = "https://storage.example.com/exports/budget_report_8.pdf";
            var mockExport = new Mock<IDataExportService>();

            mockExport.Setup(s => s.GetExportDownloadAsync(exportId, callerId))
                      .ReturnsAsync(new DataExportDownloadResult
                      {
                          Success = true,
                          Message = "Export is ready for download.",
                          Data = new DataExportDownloadDto
                          {
                              ExportId = exportId,
                              IsGenerated = true,
                              ReportLink = pdfLink
                          }
                      });

            var controller = CreateController(mockExport, StubLog(), callerId);

            // Act
            var result = await controller.GetExportDownload(exportId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as DataExportDownloadDto;

            // Assert PDF link is returned correctly
            Xunit.Assert.Equal(pdfLink, data?.ReportLink);
            Xunit.Assert.True(data?.IsGenerated);
        }
    }

}
