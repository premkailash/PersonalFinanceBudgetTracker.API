using System.Security.Claims;
using PersonalFinanceBudgetTrackerAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using PersonalFinanceBudgetTrackerAPI.Repository.Category;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category;

namespace PersonalFinanceBudgetTrackerAPI.Tests
{
    public class CategoriesControllerTest
    {
        // ===============================================================
        // Helpers & Factories
        // ===============================================================

        private static CategoriesController CreateController(
            Mock<ICategoryService> mockService,
            string role = "Admin",
            int callerId = 99)
        {
            var controller = new CategoriesController(mockService.Object);

            var claims = new List<Claim>
            {
                new Claim("userId",        callerId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Name, $"user_{callerId}")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        // ── DTO factories ────────────────────────────────────────────────

        private static CategoryResponseDto MakeCategoryDto(
            int categoryId = 1,
            string name = "Food",
            string type = "Expense",
            string icon = "utensils",
            bool isDefault = false) =>
            new CategoryResponseDto
            {
                CategoryId = categoryId,
                Name = name,
                Type = type,
                Icon = icon,
                IsDefault = isDefault
            };

        private static List<CategoryResponseDto> MakeCategoryList() =>
            new List<CategoryResponseDto>
            {
                MakeCategoryDto(1, "Food",       "Expense", "utensils",    true),
                MakeCategoryDto(2, "Salary",      "Income",  "briefcase",   true),
                MakeCategoryDto(3, "Transport",   "Expense", "car",         false),
                MakeCategoryDto(4, "Freelance",   "Income",  "laptop",      false),
                MakeCategoryDto(5, "Healthcare",  "Expense", "heart",       false)
            };

        private static CreateCategoryRequestDto MakeCreateRequest(
            string name = "Shopping",
            string type = "Expense",
            string icon = "shopping-bag",
            bool isDefault = false) =>
            new CreateCategoryRequestDto
            {
                Name = name,
                Type = type,
                Icon = icon,
                IsDefault = isDefault
            };

        private static UpdateCategoryRequestDto MakeUpdateRequest(
            int categoryId = 1,
            string name = "Updated Food",
            string type = "Expense",
            string icon = "utensils-crossed",
            bool isDefault = false) =>
            new UpdateCategoryRequestDto
            {
                CategoryId = categoryId,
                Name = name,
                Type = type,
                Icon = icon,
                IsDefault = isDefault
            };

        // ===============================================================
        // GET /api/categories  —  GetAllCategories
        // ===============================================================

        [Fact]
        public async Task GetAllCategories_ReturnsOk_WithList_WhenAdminRole()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            var categories = MakeCategoryList();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult
                       {
                           Success = true,
                           Message = "5 category/categories retrieved.",
                           Data = categories
                       });

            var controller = CreateController(mockService, role: "Admin");

            // Act
            var result = await controller.GetAllCategories();

            // Assert
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value);
            Xunit.Assert.Equal(5, data.Count());
            mockService.Verify(s => s.GetAllCategoriesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllCategories_ReturnsOk_WithList_WhenUserRole()
        {
            // Arrange — User role is also allowed to GET categories
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult
                       {
                           Success = true,
                           Data = MakeCategoryList()
                       });

            var controller = CreateController(mockService, role: "User", callerId: 10);
            var result = await controller.GetAllCategories();

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value);
            Xunit.Assert.Equal(5, data.Count());
        }

        [Fact]
        public async Task GetAllCategories_ReturnsOk_WithEmptyList_WhenNoCategories()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult
                       {
                           Success = true,
                           Data = new List<CategoryResponseDto>()
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.GetAllCategories();

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value);
            Xunit.Assert.Empty(data);
        }

        [Fact]
        public async Task GetAllCategories_Returns500_WhenServiceFails()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult
                       {
                           Success = false,
                           Message = "Database connection error."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.GetAllCategories();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            Xunit.Assert.Equal(500, statusResult.StatusCode);
            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal("Database connection error.", message);
        }

        [Fact]
        public async Task GetAllCategories_ReturnsCorrectFields()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            var categories = new List<CategoryResponseDto>
            {
                new CategoryResponseDto
                {
                    CategoryId = 1,
                    Name       = "Food",
                    Type       = "Expense",
                    Icon       = "utensils",
                    IsDefault  = true
                }
            };

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult { Success = true, Data = categories });

            var controller = CreateController(mockService, role: "User");
            var result = await controller.GetAllCategories();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value).ToList();

            Xunit.Assert.Single(data);
            Xunit.Assert.Equal(1, data[0].CategoryId);
            Xunit.Assert.Equal("Food", data[0].Name);
            Xunit.Assert.Equal("Expense", data[0].Type);
            Xunit.Assert.Equal("utensils", data[0].Icon);
            Xunit.Assert.True(data[0].IsDefault);
        }

        [Fact]
        public async Task GetAllCategories_ContainsBothIncomeAndExpenseTypes()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult
                       {
                           Success = true,
                           Data = MakeCategoryList()
                       });

            var controller = CreateController(mockService, role: "User");
            var result = await controller.GetAllCategories();
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            var data = Xunit.Assert.IsAssignableFrom<IEnumerable<CategoryResponseDto>>(ok.Value).ToList();

            Xunit.Assert.Contains(data, c => c.Type == "Income");
            Xunit.Assert.Contains(data, c => c.Type == "Expense");
        }

        [Fact]
        public async Task GetAllCategories_ServiceCalledExactlyOnce()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult { Success = true, Data = MakeCategoryList() });

            var controller = CreateController(mockService, role: "Admin");

            // Act — call twice
            await controller.GetAllCategories();
            await controller.GetAllCategories();

            // Assert — each request triggers exactly one service call
            mockService.Verify(s => s.GetAllCategoriesAsync(), Times.Exactly(2));
        }

        // ===============================================================
        // POST /api/categories  —  CreateCategory
        // ===============================================================

        [Fact]
        public async Task CreateCategory_ReturnsCreated_WhenRequestIsValid()
        {
            // Arrange
            var request = MakeCreateRequest();
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Category 'Shopping' created successfully.",
                           Data = MakeCategoryDto(6, "Shopping", "Expense", "shopping-bag")
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);

            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);
            mockService.Verify(s => s.CreateCategoryAsync(request), Times.Once);
        }

        [Fact]
        public async Task CreateCategory_ReturnsCreated_WithCorrectResponseBody()
        {
            // Arrange
            var request = MakeCreateRequest(name: "Entertainment", type: "Expense", icon: "film");
            var expectedDto = MakeCategoryDto(7, "Entertainment", "Expense", "film");
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Category 'Entertainment' created successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as CategoryResponseDto;

            Xunit.Assert.Contains("created successfully", message);
            Xunit.Assert.Equal(expectedDto.CategoryId, data?.CategoryId);
            Xunit.Assert.Equal("Entertainment", data?.Name);
            Xunit.Assert.Equal("Expense", data?.Type);
        }

        [Fact]
        public async Task CreateCategory_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            var controller = CreateController(mockService, role: "Admin");
            controller.ModelState.AddModelError("Name", "Name is required.");

            var request = new CreateCategoryRequestDto { Type = "Expense" };
            var result = await controller.CreateCategory(request);

            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.CreateCategoryAsync(It.IsAny<CreateCategoryRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateCategory_ReturnsConflict_WhenDuplicateCategoryExists()
        {
            // Arrange
            var request = MakeCreateRequest(name: "Food", type: "Expense");
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           IsDuplicate = true,
                           Message = "A category named 'Food' of type 'Expense' already exists."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);

            var conflict = Xunit.Assert.IsType<ConflictObjectResult>(result);
            Xunit.Assert.Equal(409, conflict.StatusCode);

            var body = conflict.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("already exists", message);
        }

        [Fact]
        public async Task CreateCategory_ReturnsBadRequest_WhenServiceFailsWithoutDuplicate()
        {
            // Arrange
            var request = MakeCreateRequest();
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           IsDuplicate = false,
                           Message = "Unexpected error."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);

            Xunit.Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateCategory_ServiceNeverCalled_WhenModelStateInvalid()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            var controller = CreateController(mockService, role: "Admin");
            controller.ModelState.AddModelError("Type", "Required.");
            controller.ModelState.AddModelError("Name", "Required.");

            await controller.CreateCategory(new CreateCategoryRequestDto());

            mockService.Verify(s => s.CreateCategoryAsync(It.IsAny<CreateCategoryRequestDto>()), Times.Never);
        }

        // ---------------------------------------------------------------
        // Category type coverage — both Income and Expense
        // ---------------------------------------------------------------
        [Theory]
        [InlineData("Income", "Bonus", "star")]
        [InlineData("Expense", "Dining", "coffee")]
        [InlineData("Income", "Dividend", "bar-chart")]
        [InlineData("Expense", "Subscriptions", "repeat")]
        public async Task CreateCategory_ReturnsCreated_ForBothValidTypes(
            string type, string name, string icon)
        {
            // Arrange
            var request = MakeCreateRequest(name: name, type: type, icon: icon);
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = $"Category '{name}' created successfully.",
                           Data = MakeCategoryDto(10, name, type, icon)
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);

            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);
            Xunit.Assert.Equal(201, created.StatusCode);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as CategoryResponseDto;
            Xunit.Assert.Equal(name, data?.Name);
            Xunit.Assert.Equal(type, data?.Type);
        }

        // ===============================================================
        // PUT /api/categories  —  UpdateCategory
        // ===============================================================

        [Fact]
        public async Task UpdateCategory_ReturnsOk_WhenUpdateIsSuccessful()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 1);
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Category 'Updated Food' updated successfully.",
                           Data = MakeCategoryDto(1, "Updated Food", "Expense")
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);
            mockService.Verify(s => s.UpdateCategoryAsync(request), Times.Once);
        }

        [Fact]
        public async Task UpdateCategory_ReturnsOk_WithCorrectResponseBody()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 3, name: "Transport & Fuel", type: "Expense");
            var expectedDto = MakeCategoryDto(3, "Transport & Fuel", "Expense");
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Category 'Transport & Fuel' updated successfully.",
                           Data = expectedDto
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var type = body.GetType();
            var message = type.GetProperty("message")?.GetValue(body) as string;
            var data = type.GetProperty("data")?.GetValue(body) as CategoryResponseDto;

            Xunit.Assert.Contains("updated successfully", message);
            Xunit.Assert.Equal(expectedDto.CategoryId, data?.CategoryId);
            Xunit.Assert.Equal("Transport & Fuel", data?.Name);
        }

        [Fact]
        public async Task UpdateCategory_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var mockService = new Mock<ICategoryService>();
            var controller = CreateController(mockService, role: "Admin");
            controller.ModelState.AddModelError("Name", "Required.");

            var request = new UpdateCategoryRequestDto { CategoryId = 1, Type = "Expense" };
            var result = await controller.UpdateCategory(request);

            Xunit.Assert.IsType<BadRequestObjectResult>(result);
            mockService.Verify(s => s.UpdateCategoryAsync(It.IsAny<UpdateCategoryRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCategory_ReturnsNotFound_WhenCategoryDoesNotExist()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 999);
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = "Category with ID 999 was not found."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);

            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task UpdateCategory_ReturnsConflict_WhenDuplicateNameExists()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 2, name: "Food", type: "Expense");
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           IsDuplicate = true,
                           Message = "Another category named 'Food' of type 'Expense' already exists."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);

            var conflict = Xunit.Assert.IsType<ConflictObjectResult>(result);
            Xunit.Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public async Task UpdateCategory_ReturnsBadRequest_WhenServiceFailsGeneric()
        {
            // Arrange
            var request = MakeUpdateRequest();
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           NotFound = false,
                           IsDuplicate = false,
                           Message = "Unexpected error."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);

            Xunit.Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateCategory_ServiceCalledOnceWithCorrectRequest()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 5, name: "Medical", type: "Expense");
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult { Success = true, Message = "Updated.", Data = MakeCategoryDto(5, "Medical") });

            var controller = CreateController(mockService, role: "Admin");
            await controller.UpdateCategory(request);

            mockService.Verify(s => s.UpdateCategoryAsync(request), Times.Once);
            mockService.Verify(s => s.UpdateCategoryAsync(
                It.Is<UpdateCategoryRequestDto>(r => r.CategoryId != request.CategoryId)), Times.Never);
        }

        // ===============================================================
        // DELETE /api/categories/{id}  —  DeleteCategory
        // ===============================================================

        [Fact]
        public async Task DeleteCategory_ReturnsOk_WhenCategoryIsSuccessfullyDeleted()
        {
            // Arrange
            const int categoryId = 6;
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.DeleteCategoryAsync(categoryId))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = $"Category 'Shopping' (ID: {categoryId}) deleted successfully."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.DeleteCategory(categoryId);

            var ok = Xunit.Assert.IsType<OkObjectResult>(result);
            Xunit.Assert.Equal(200, ok.StatusCode);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("deleted successfully", message);
            mockService.Verify(s => s.DeleteCategoryAsync(categoryId), Times.Once);
        }

        [Fact]
        public async Task DeleteCategory_ReturnsNotFound_WhenCategoryDoesNotExist()
        {
            // Arrange
            const int categoryId = 999;
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.DeleteCategoryAsync(categoryId))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           NotFound = true,
                           Message = $"Category with ID {categoryId} was not found."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.DeleteCategory(categoryId);

            var notFound = Xunit.Assert.IsType<NotFoundObjectResult>(result);
            Xunit.Assert.Equal(404, notFound.StatusCode);

            var body = notFound.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("999", message);
        }

        [Fact]
        public async Task DeleteCategory_ReturnsBadRequest_WhenCategoryHasLinkedRecords()
        {
            // Arrange — category is still in use by transactions or budgets
            const int categoryId = 1;
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.DeleteCategoryAsync(categoryId))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = false,
                           NotFound = false,
                           Message = "Category 'Food' cannot be deleted because it is referenced by transactions. Reassign or delete those records first."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.DeleteCategory(categoryId);

            var badRequest = Xunit.Assert.IsType<BadRequestObjectResult>(result);
            Xunit.Assert.Equal(400, badRequest.StatusCode);

            var body = badRequest.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Contains("cannot be deleted", message);
        }

        [Fact]
        public async Task DeleteCategory_CallsServiceOnceWithCorrectId()
        {
            // Arrange
            const int categoryId = 4;
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.DeleteCategoryAsync(categoryId))
                       .ReturnsAsync(new CategoryResult { Success = true, Message = "Deleted." });

            var controller = CreateController(mockService, role: "Admin");
            await controller.DeleteCategory(categoryId);

            mockService.Verify(s => s.DeleteCategoryAsync(categoryId), Times.Once);
            mockService.Verify(s => s.DeleteCategoryAsync(
                It.Is<int>(x => x != categoryId)), Times.Never);
        }

        [Fact]
        public async Task DeleteCategory_ReturnsCorrectSuccessMessage()
        {
            // Arrange
            const int categoryId = 8;
            const string catName = "Utilities";
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.DeleteCategoryAsync(categoryId))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = $"Category '{catName}' (ID: {categoryId}) deleted successfully."
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.DeleteCategory(categoryId);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal($"Category '{catName}' (ID: {categoryId}) deleted successfully.", message);
        }

        // ===============================================================
        // IsDefault flag coverage
        // ===============================================================

        [Fact]
        public async Task CreateCategory_DefaultCategory_IsPersistedCorrectly()
        {
            // Arrange
            var request = MakeCreateRequest(name: "Essential Food", type: "Expense", isDefault: true);
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.CreateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Created.",
                           Data = MakeCategoryDto(9, "Essential Food", "Expense", "utensils", isDefault: true)
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.CreateCategory(request);
            var created = Xunit.Assert.IsType<CreatedAtActionResult>(result);

            var body = created.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as CategoryResponseDto;

            Xunit.Assert.True(data?.IsDefault);
        }

        [Fact]
        public async Task UpdateCategory_CanToggleIsDefault_ToTrue()
        {
            // Arrange
            var request = MakeUpdateRequest(categoryId: 2, isDefault: true);
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.UpdateCategoryAsync(request))
                       .ReturnsAsync(new CategoryResult
                       {
                           Success = true,
                           Message = "Updated.",
                           Data = MakeCategoryDto(2, "Salary", "Income", "briefcase", isDefault: true)
                       });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.UpdateCategory(request);
            var ok = Xunit.Assert.IsType<OkObjectResult>(result);

            var body = ok.Value!;
            var data = body.GetType().GetProperty("data")?.GetValue(body) as CategoryResponseDto;
            Xunit.Assert.True(data?.IsDefault);
        }

        // ===============================================================
        // 500 error propagation
        // ===============================================================

        [Fact]
        public async Task GetAllCategories_PropagatesExactErrorMessage_On500()
        {
            // Arrange
            const string errorMsg = "EF Core connection pool exhausted.";
            var mockService = new Mock<ICategoryService>();

            mockService.Setup(s => s.GetAllCategoriesAsync())
                       .ReturnsAsync(new CategoryListResult { Success = false, Message = errorMsg });

            var controller = CreateController(mockService, role: "Admin");
            var result = await controller.GetAllCategories();
            var statusResult = Xunit.Assert.IsType<ObjectResult>(result);

            var body = statusResult.Value!;
            var message = body.GetType().GetProperty("message")?.GetValue(body) as string;
            Xunit.Assert.Equal(errorMsg, message);
        }

    }
}
