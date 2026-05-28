using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonalFinanceBudgetTrackerAPI.Context;
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
            // ---------------------------------------------------------------
            // Database
            // ---------------------------------------------------------------
            builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


            // ---------------------------------------------------------------
            // JWT Authentication
            // ---------------------------------------------------------------
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]
                              ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

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
                        ClockSkew = TimeSpan.Zero   // No grace period on expiry
                    };

                    // Check token blacklist on every authenticated request
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

            builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

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
            // ---------------------------------------------------------------
            // Controllers & Swagger
            // ---------------------------------------------------------------
            builder.Services.AddControllers()
            .AddJsonOptions(options =>
             {
                 options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
             });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "FinanceApp Auth API", Version = "v1" });

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
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseCors("Frontend");
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }

}
