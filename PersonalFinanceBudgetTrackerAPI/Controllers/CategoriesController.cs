using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Category;
using PersonalFinanceBudgetTrackerAPI.Repository.Category;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [Authorize]    
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // ---------------------------------------------------------------
        // GET /api/categories
        // Accessible by both User and Admin roles
        // ---------------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> GetAllCategories()
        {
            var result = await _categoryService.GetAllCategoriesAsync();

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/categories
        // Admin only — add a custom category
        // ---------------------------------------------------------------
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory(
            [FromBody] CreateCategoryRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateCategoryAsync(request);

            if (!result.Success)
                return result.IsDuplicate
                    ? Conflict(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return CreatedAtAction(
                nameof(GetAllCategories),
                new { },
                new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // PUT /api/categories
        // Admin only — update a category
        // ---------------------------------------------------------------
        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(
            [FromBody] UpdateCategoryRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.UpdateCategoryAsync(request);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                if (result.IsDuplicate) return Conflict(new { message = result.Message });
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // DELETE /api/categories/{id}
        // Admin only — delete a category
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var result = await _categoryService.DeleteCategoryAsync(id);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
    }

}
