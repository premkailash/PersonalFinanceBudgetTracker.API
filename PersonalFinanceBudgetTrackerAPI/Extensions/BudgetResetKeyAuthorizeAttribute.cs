using Microsoft.AspNetCore.Mvc;

namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
    // ── Custom API-Key attribute for the reset endpoint ────────────────────────
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class BudgetResetKeyAuthorizeAttribute
        : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter
    {
        private const string HeaderName = "X-Reset-Key";

        public async Task OnActionExecutionAsync(
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expectedKey = config["ImportSettings:ResetApiKey"];

            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                context.Result = new ObjectResult(
                    new { message = "Monthly reset endpoint is not configured." })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
                return;
            }

            if (!context.HttpContext.Request.Headers
                         .TryGetValue(HeaderName, out var provided)
                 || provided != expectedKey)
            {
                context.Result = new ObjectResult(
                    new { message = "Unauthorised. Valid X-Reset-Key header required." })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            await next();
        }
    }
}
