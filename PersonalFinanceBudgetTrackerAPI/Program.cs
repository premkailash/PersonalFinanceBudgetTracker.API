using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Extensions;
using PersonalFinanceBudgetTrackerAPI.Repository.Account;
using PersonalFinanceBudgetTrackerAPI.Repository.Auth;
using PersonalFinanceBudgetTrackerAPI.Repository.Budget;
using PersonalFinanceBudgetTrackerAPI.Repository.Category;
using PersonalFinanceBudgetTrackerAPI.Repository.DataExport;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;
using PersonalFinanceBudgetTrackerAPI.Repository.Notification;
using PersonalFinanceBudgetTrackerAPI.Repository.Report;
using PersonalFinanceBudgetTrackerAPI.Repository.SavingsGoal;
using PersonalFinanceBudgetTrackerAPI.Repository.Transaction;
using PersonalFinanceBudgetTrackerAPI.Repository.User;
using System.Text;
using System.Text.Json;

namespace PersonalFinanceBudgetTrackerAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options =>
            {
                options.FormatterName = "json";
            });

            builder.Logging.AddJsonConsole(options =>
            {
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
                options.IncludeScopes = true;
                options.TimestampFormat = "o";
            });
           
            builder.Configuration.AddAwsSecretsManager(builder.Environment);
            // ---------------------------------------------------------------
            // Database
            // ---------------------------------------------------------------
            builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddFinanceRateLimiting();

            // ---------------------------------------------------------------
            // JWT Authentication
            // ---------------------------------------------------------------
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]
                              ?? throw new InvalidOperationException(
                                  "JwtSettings:SecretKey is not configured. " +
                                  "Ensure the AWS Secrets Manager secret contains 'JwtSettings__SecretKey'.");

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidAudience = jwtSettings["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                                                       Encoding.UTF8.GetBytes(secretKey)),
                        ClockSkew = TimeSpan.Zero
                    };

                    // Token blacklist check on every authenticated request
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var blacklist = context.HttpContext.RequestServices
                                                   .GetRequiredService<ITokenBlacklist>();
                            var userIdClaim = context.Principal?.FindFirst("userId")?.Value;
                            var issuedAtClaim = context.Principal?.FindFirst("issuedAt")?.Value;
                            if (userIdClaim != null && issuedAtClaim != null)
                            {
                                int userId = int.Parse(userIdClaim);
                                long tokenIssued = long.Parse(issuedAtClaim);
                                bool isInvalidated = await blacklist.IsUserInvalidatedAsync(userId, tokenIssued);
                                if (isInvalidated)
                                    context.Fail("Token has been invalidated. Please log in again.");
                            }
                        }
                    };
                });


            // ---------------------------------------------------------------
            // Authorization
            // ---------------------------------------------------------------
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
            });
         
            // ---------------------------------------------------------------
            // Application Services
            // ---------------------------------------------------------------
            builder.Services.AddSingleton<ITokenBlacklist, InMemoryTokenBlacklist>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IBudgetService, BudgetService>();
            builder.Services.AddScoped<IBudgetAlertService, BudgetAlertService>();
            builder.Services.AddScoped<ILogService, LogService>();
            builder.Services.AddScoped<ISavingsGoalService, SavingsGoalService>();
            builder.Services.AddScoped<ISavingsGoalAlertService, SavingsGoalAlertService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IDataExportService, DataExportService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<ITransactionService, TransactionService>();
            builder.Services.AddScoped<ITransactionImportService, TransactionImportService>();
            builder.Services.AddScoped<IPlaidBankService, PlaidBankService>();
            builder.Services.AddScoped<ApiKeyAuthorizeAttribute>();
            builder.Services.AddScoped<ICategoryService,CategoryService>();
            builder.Services.AddScoped<IDefaultBudgetService, DefaultBudgetService>();
            // ---------------------------------------------------------------
            // Controllers & Swagger
            // ---------------------------------------------------------------
            builder.Services.AddFinanceHealthChecks();
            builder.Services.AddControllers()
            .AddJsonOptions(options =>
             {
                 options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
             });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Personal Finance Budget Tracker API", Version = "v1" });

                // Add JWT Bearer to Swagger UI
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id   = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            
            var app = builder.Build();

            // ---------------------------------------------------------------
            // Middleware Pipeline
            // ---------------------------------------------------------------
            app.UseGlobalExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            app.UseHttpsRedirection();
            app.UseRouting();

            // ?? Health endpoints — mapped BEFORE rate limiting so ALB probes are
            //    never throttled and never require authentication headers.
            //    /health/live  ? liveness  (ALB target-group health-check path)
            //    /health/ready ? readiness (deep check including DB)
            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                // Liveness: only checks that survive with zero external calls (no "ready"/"db" tags)
                Predicate = check => !check.Tags.Contains("ready") && !check.Tags.Contains("db"),
                ResponseWriter = WriteHealthResponse,
                AllowCachingResponses = false
            });

            app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                // Readiness: run all registered checks
                Predicate = _ => true,
                ResponseWriter = WriteHealthResponse,
                AllowCachingResponses = false
            }).WithMetadata(new AllowAnonymousAttribute());

            app.UseRateLimiter();       // before auth — throttles brute-force by IP
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }

        // ?????????????????????????????????????????????????????????????????????????????
        //  Health-check response writer
        //  Returns a JSON body instead of the default plain-text "Healthy" / "Unhealthy"
        //  so ALB access logs and monitoring tools receive structured data.
        // ?????????????????????????????????????????????????????????????????????????????
        static Task WriteHealthResponse(
            HttpContext context,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow.ToString("o"),
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    error = e.Value.Exception?.Message
                })
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

            return context.Response.WriteAsync(result);
        }
    }

    


}
