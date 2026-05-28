using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Transaction
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class ApiKeyAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        private const string HeaderName = "X-Import-Key";

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var config = context.HttpContext.RequestServices
                                .GetRequiredService<IConfiguration>();

            var expectedKey = config["ImportSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                // Fail closed — if the key is not configured, deny access
                context.Result = new ObjectResult(
                    new { message = "Import endpoint is not configured." })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
                return;
            }

            if (!context.HttpContext.Request.Headers
                         .TryGetValue(HeaderName, out var providedKey)
                 || providedKey != expectedKey)
            {
                context.Result = new ObjectResult(
                    new { message = "Unauthorised. Valid API key required for import endpoint." })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            await next();
        }
    }

}
