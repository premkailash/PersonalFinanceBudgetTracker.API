💰 Personal Finance Budget Tracker API
A production-ready ASP.NET Core 8 REST API for personal finance management.
Track accounts, transactions, budgets, savings goals, notifications, and generate
financial reports — all secured with JWT authentication and protected by
per-user rate limiting.
---
Table of Contents
Tech Stack
Architecture Overview
Project Structure
Prerequisites
Getting Started
Configuration
Database Setup
Running the API
API Modules
Authentication
Rate Limiting
Alert Notifications
Running Tests
Environment Variables Reference
Deployment Notes
Contributing
---
Tech Stack
Layer	Technology
Runtime	.NET 8 (C# 12)
Web framework	ASP.NET Core 8 minimal-hosting model
ORM	Entity Framework Core 8
Database	PostgreSQL 15+ (via `Npgsql.EntityFrameworkCore.PostgreSQL`)
Authentication	JWT Bearer (HMAC-SHA256, `Microsoft.AspNetCore.Authentication.JwtBearer`)
Password hashing	BCrypt.Net-Next (work factor 12, auto-salt)
Rate limiting	`Microsoft.AspNetCore.RateLimiting` (built-in, .NET 7+)
API docs	Swagger / OpenAPI (`Swashbuckle.AspNetCore`)
Testing	xUnit + Moq + EF Core InMemory
---
Architecture Overview
```
┌───────────────────────────────────────────────────────────┐
│                    ASP.NET Core Pipeline                  │
│                                                           │
│  HTTPS → Routing → RateLimiter → Auth → Authorization    │
│                          │                               │
│              ┌───────────▼──────────┐                    │
│              │     Controllers      │                    │
│              │  (thin — validates   │                    │
│              │   & delegates)       │                    │
│              └───────────┬──────────┘                    │
│                          │                               │
│              ┌───────────▼──────────┐                    │
│              │      Services        │                    │
│              │  (business logic,    │                    │
│              │   alert evaluation)  │                    │
│              └───────────┬──────────┘                    │
│                          │                               │
│              ┌───────────▼──────────┐                    │
│              │    AppDbContext       │                    │
│              │    (EF Core)         │                    │
│              └───────────┬──────────┘                    │
│                          │                               │
│                    PostgreSQL                            │
└───────────────────────────────────────────────────────────┘
```
Design principles:
Controllers are thin — they validate input, call a service, and return HTTP responses.
Services encapsulate all business logic and DB access via EF Core.
Every service is registered behind an interface for easy mocking in tests.
Alert services (`IBudgetAlertService`, `ISavingsGoalAlertService`) fire
automatically inside the relevant services after every write — no extra
endpoint or background job required.
---
Project Structure
```
PersonalFinanceBudgetTrackerAPI/
│
├── Controllers/                  # HTTP endpoints (one controller per domain)
│   ├── AuthController.cs
│   ├── AccountsController.cs
│   ├── BudgetsController.cs
│   ├── CategoriesController.cs
│   ├── DataExportController.cs
│   ├── LogsController.cs
│   ├── NotificationsController.cs
│   ├── ReportsController.cs
│   ├── SavingsGoalsController.cs
│   ├── TransactionsController.cs
│   └── UsersController.cs
│
├── Services/                     # Business logic & DB access
│   ├── AuthService.cs / IAuthService.cs
│   ├── AccountService.cs / IAccountService.cs
│   ├── BudgetService.cs / IBudgetService.cs
│   ├── BudgetAlertService.cs / IBudgetAlertService.cs
│   ├── CategoryService.cs / ICategoryService.cs
│   ├── DataExportService.cs / IDataExportService.cs
│   ├── LogService.cs / ILogService.cs
│   ├── NotificationService.cs / INotificationService.cs
│   ├── ReportService.cs / IReportService.cs
│   ├── SavingsGoalService.cs / ISavingsGoalService.cs
│   ├── SavingsGoalAlertService.cs / ISavingsGoalAlertService.cs
│   ├── TransactionService.cs / ITransactionService.cs
│   ├── TransactionImportService.cs / ITransactionImportService.cs
│   ├── PlaidBankServiceStub.cs / IPlaidBankService.cs
│   ├── UserService.cs / IUserService.cs
│   └── TokenBlacklist.cs / ITokenBlacklist.cs
│
├── Models/                       # EF Core entities
│   ├── User.cs
│   ├── Account.cs
│   ├── Budget.cs
│   ├── Category.cs
│   ├── DataExport.cs
│   ├── Log.cs
│   ├── Notification.cs
│   ├── SavingsGoal.cs
│   └── Transaction.cs
│
├── DTOs/                         # Request / response data transfer objects
│   ├── AccountDtos.cs
│   ├── AuthDtos.cs
│   ├── BudgetDtos.cs
│   ├── CategoryDtos.cs
│   ├── DataExportDtos.cs
│   ├── LogDtos.cs
│   ├── NotificationDtos.cs
│   ├── ReportDtos.cs
│   ├── SavingsGoalDtos.cs
│   ├── TransactionDtos.cs
│   └── UserDtos.cs
│
├── Infrastructure/
│   ├── AppDbContext.cs            # EF Core DbContext (10 DbSets)
│   ├── ApiKeyAuthorizeAttribute.cs# Secures /api/transactions/import
│   └── RateLimiting/
│       ├── RateLimitPolicies.cs
│       ├── RateLimitingExtensions.cs
│       └── RateLimitingUsageGuide.cs
│
├── Migrations/                   # EF Core migrations (auto-generated)
│
├── Tests/                        # xUnit test projects
│   ├── AccountsControllerTests.cs
│   ├── BudgetsControllerTests.cs
│   ├── BudgetAlertServiceTests.cs
│   ├── CategoriesControllerTests.cs
│   ├── DataExportControllerTests.cs
│   ├── LogsControllerTests.cs
│   ├── NotificationsControllerTests.cs
│   ├── RateLimitingTests.cs
│   ├── ReportControllerTests.cs
│   ├── SavingsGoalsControllerTests.cs
│   ├── SavingsGoalAlertServiceTests.cs
│   ├── TransactionControllerTests.cs
│   └── UsersControllerTests.cs
│
├── appsettings.json               # Base configuration (no secrets)
├── appsettings.Development.json   # ⚠ NOT committed — add to .gitignore
├── Program.cs                     # App bootstrap & DI registration
└── README.md
```
---
Prerequisites
Tool	Minimum Version	Notes
.NET SDK	8.0	`dotnet --version` to verify
PostgreSQL	15.0	Local install or Docker
Git	2.40+	
(Optional) Docker Desktop	4.x	For containerised Postgres
---
Getting Started
1 — Clone the repository
```bash
git clone https://github.com/<your-org>/PersonalFinanceBudgetTrackerAPI.git
cd PersonalFinanceBudgetTrackerAPI
```
2 — Restore NuGet packages
```bash
dotnet restore
```
3 — Create `appsettings.Development.json`
> ⚠️ This file is in `.gitignore` and must **never** be committed.
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FinanceAppDb;Username=postgres;Password=<your_local_password>"
  },
  "JwtSettings": {
    "SecretKey":      "<minimum-32-character-random-secret>",
    "Issuer":         "FinanceApp",
    "Audience":       "FinanceAppUsers",
    "ExpiryMinutes":  "60"
  },
  "ImportSettings": {
    "ApiKey": "<random-api-key-for-lambda-import-endpoint>"
  },
  "Logging": {
    "LogLevel": {
      "Default":               "Debug",
      "Microsoft.AspNetCore":  "Information"
    }
  }
}
```
4 — Apply database migrations
```bash
dotnet ef database update
```
> If you haven't created migrations yet:
> ```bash
> dotnet ef migrations add InitialCreate
> dotnet ef database update
> ```
5 — Run the API
```bash
dotnet run
```
Swagger UI is available at: https://localhost:7001/swagger
---
Configuration
All configuration lives in `appsettings.json` (committed, no secrets) with
environment-specific overrides in `appsettings.{Environment}.json` (not committed).
`appsettings.json` — safe defaults
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FinanceAppDb;Username=postgres;Password=your_password"
  },
  "JwtSettings": {
    "SecretKey":     "YOUR_SUPER_SECRET_KEY_MIN_32_CHARACTERS_LONG!",
    "Issuer":        "FinanceApp",
    "Audience":      "FinanceAppUsers",
    "ExpiryMinutes": "60"
  },
  "ImportSettings": {
    "ApiKey": "REPLACE_WITH_SECURE_RANDOM_KEY"
  }
}
```
> Replace all placeholder values before running in any non-development environment.
Generating a secure JWT secret
```bash
# PowerShell
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# Bash
openssl rand -base64 32
```
---
Database Setup
Schema (9 tables)
Table	Description
`Users`	Registered users; Role = `User` | `Admin`
`Accounts`	Linked bank/wallet/credit/investment accounts (soft-delete via `IsActive`)
`Category`	Income/Expense categories; shared lookup table
`Transactions`	Financial transactions linked to accounts and categories
`Budgets`	Monthly category budgets; one per (UserId, CategoryId)
`SavingsGoals`	User savings targets with optional auto-contribute
`Notifications`	In-app alerts including budget and goal threshold alerts
`Logs`	Immutable audit trail; `ActorId` SET NULL preserves logs on user delete
`DataExport`	Export requests (CSV / PDF); polled for completion via `IsGenerated`
EF Core migrations
```bash
# Create a new migration after model changes
dotnet ef migrations add <MigrationName> --project src/PersonalFinanceBudgetTrackerAPI

# Apply pending migrations
dotnet ef database update

# Roll back one migration
dotnet ef database update <PreviousMigrationName>

# Generate SQL script (for production deployments)
dotnet ef script --idempotent --output migrate.sql
```
---
Running the API
Development
```bash
dotnet run --environment Development
# API: https://localhost:7001
# Swagger: https://localhost:7001/swagger
```
Docker (optional — Postgres only)
```bash
# Start a local Postgres container
docker run -d \
  --name financeapp-postgres \
  -e POSTGRES_DB=FinanceAppDb \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=localpassword \
  -p 5432:5432 \
  postgres:15

# Then run the API normally
dotnet run
```
Production build
```bash
dotnet publish -c Release -o ./publish
```
---
API Modules
The base URL for all endpoints is `/api`.
Module	Base Route	Roles	Description
Auth	`/api/auth`	Public	Register, Login, Logout
Users	`/api/users`	Admin / User*	Profile management
Accounts	`/api/accounts`	User	CRUD — Bank/Wallet/Credit/Investment

Categories	`/api/categories`	User+Admin (GET), Admin (write)	Income/Expense categories
Transactions	`/api/transactions`	User	CRUD + CSV/Batch import
Budgets	`/api/budgets`	User	Monthly category budgets
Savings Goals	`/api/goals`	User	Savings targets + contributions
Notifications	`/api/notifications`	User	In-app alerts
Reports	`/api/reports`	User	Monthly, yearly, category, net-worth
Data Export	`/api/export`	User	CSV/PDF export requests
Logs	`/api/logs`	Admin (GET), Any Auth (POST)	Audit trail
> \* A `User` can only access their own profile; `Admin` can access any.
Key endpoint examples
```
# Auth
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/logout

# Accounts
GET    /api/accounts?userId={id}
POST   /api/accounts
PUT    /api/accounts/{id}
DELETE /api/accounts/{id}

# Budgets
GET    /api/budgets?userId={id}&month=2024-05
GET    /api/budgets/{id}
POST   /api/budgets
PUT    /api/budgets/{id}
DELETE /api/budgets/{id}
GET    /api/budgets/utilization?userId={id}&month=2024-05

# Savings Goals
GET    /api/goals?userId={id}
POST   /api/goals
PUT    /api/goals/{id}
DELETE /api/goals/{id}
POST   /api/goals/{id}/contribute

# Transactions
GET    /api/transactions?account_id={id}&from={ISO8601}&to={ISO8601}
POST   /api/transactions
PUT    /api/transactions/{id}
DELETE /api/transactions/{id}
POST   /api/transactions/import          ← Lambda/Batch Job only (X-Import-Key)

# Reports
GET    /api/reports/monthly?month=2024-05
GET    /api/reports/yearly?year=2024
GET    /api/reports/category-breakdown?month=2024-05
GET    /api/reports/net-worth
```
Full details are in the API Specification document (`FinanceApp_API_Specification.docx`).
---
Authentication
All protected endpoints require a Bearer JWT token in the `Authorization` header:
```
Authorization: Bearer <jwt_token>
```
Token lifecycle
Register → `POST /api/auth/register` — creates user (BCrypt-hashed password).
Login → `POST /api/auth/login` — returns `{ token, userId, role }`.
Use token — attach to every subsequent request header.
Logout → `POST /api/auth/logout` — invalidates the token via the in-memory
blacklist (keyed on `(userId, issuedAt)` from JWT claims).
JWT Claims
Claim	Value
`userId`	Numeric user ID
`email`	User's email address
`role`	`User` or `Admin`
`issuedAt`	Unix timestamp — used by the token blacklist
`jti`	Unique token ID
> **Production note:** The default `ITokenBlacklist` is in-memory. For
> multi-instance deployments (load balancer / Kubernetes) replace it with a
> Redis-backed implementation.
---
Rate Limiting
Uses .NET 8 built-in fixed-window rate limiting (`Microsoft.AspNetCore.RateLimiting`).
Policy	Limit	Window	Applies To
`auth_fixed_window`	10 req/min	60 s	`/api/auth/*`
`data_fixed_window`	300 req/min	60 s	All other `/api/*`
Partition key resolution:
```
Authenticated request   → "user:{userId}"    (JWT claim)
Unauthenticated         → "ip:{X-Forwarded-For}"  or  "ip:{RemoteIpAddress}"
```
HTTP 429 response shape:
```json
{
  "message": "Too many requests. Please slow down and try again."
}
```
The `Retry-After` header indicates the number of seconds until the window resets.
Middleware order — `UseRateLimiter()` is placed before `UseAuthentication()`
so unauthenticated brute-force attempts against `/api/auth/login` are throttled
by IP before the JWT pipeline runs.
---
Alert Notifications
Two alert services fire automatically inside the relevant write operations.
No extra endpoint or scheduled job is required.
Budget Alerts (`IBudgetAlertService`)
Fires when `Budget.CurrentAmount / Budget.TargetAmount` crosses a threshold.
Threshold	Type	Message
≥ 80 %	`BudgetAlert80`	`Budget "{name}" (Budget ID {id}) has reached 80% of its target.`
≥ 100 %	`BudgetAlert100`	`Budget "{name}" (Budget ID {id}) has reached 100% of its target.`
Fire points: `BudgetService.CreateBudgetAsync`, `BudgetService.UpdateBudgetAsync`,
`TransactionService.ApplyBudgetDeltaAsync` (positive delta only).
Savings Goal Alerts (`ISavingsGoalAlertService`)
Progress = `(CurrentAmount + AutoContributeAmount) / TargetAmount`.
Threshold	Type	Message
≥ 50 %	`GoalAlert50`	`Goal "{name}" (Goal ID {id}) has reached 50% of its target.`
≥ 100 %	`GoalAlert100`	`Goal "{name}" (Goal ID {id}) has reached 100% of its target.`
Fire points: `SavingsGoalService.CreateGoalAsync`, `SavingsGoalService.UpdateGoalAsync`,
`SavingsGoalService.ContributeAsync`.
Duplicate-prevention
Both alert services perform a DB check before inserting:
```
(UserId, Type, CreatedAt.Year == TargetDate.Year, CreatedAt.Month == TargetDate.Month,
 Message contains "Budget/Goal ID {id}")
```
One alert per (user, entity, type, calendar month) — guaranteed.
---
Running Tests
The test project uses xUnit with Moq for mocked dependencies and
EF Core InMemory for data-layer unit tests.
```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "ClassName=BudgetAlertServiceTests"

# Run with code coverage (requires coverlet)
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage report (requires reportgenerator)
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```
Test projects
Test File	Covers	Approach
`AccountsControllerTests.cs`	`AccountsController`	Mocked service
`BudgetsControllerTests.cs`	`BudgetsController`	Mocked service
`BudgetAlertServiceTests.cs`	`BudgetAlertService` — 22 tests	In-memory EF Core
`CategoriesControllerTests.cs`	`CategoriesController` — 30 tests	Mocked service
`DataExportControllerTests.cs`	`DataExportController`	Mocked service
`LogsControllerTests.cs`	`LogsController`	Mocked service
`NotificationsControllerTests.cs`	`NotificationsController`	Mocked service
`RateLimitingTests.cs`	Rate-limit policies & partition keys	TestServer integration
`ReportControllerTests.cs`	`ReportsController`	Mocked service
`SavingsGoalsControllerTests.cs`	`SavingsGoalsController`	Mocked service
`SavingsGoalAlertServiceTests.cs`	`SavingsGoalAlertService` — 28 tests	In-memory EF Core
`TransactionControllerTests.cs`	`TransactionsController` — 37 tests	Mocked service
`UsersControllerTests.cs`	`UsersController`	Mocked service
---
Environment Variables Reference
These can be set via `appsettings.{Environment}.json`, system environment
variables, or a secrets manager.
Key	Required	Description
`ConnectionStrings:DefaultConnection`	✅	Full Npgsql connection string
`JwtSettings:SecretKey`	✅	HMAC-SHA256 signing key — minimum 32 characters
`JwtSettings:Issuer`	✅	JWT `iss` claim — e.g. `FinanceApp`
`JwtSettings:Audience`	✅	JWT `aud` claim — e.g. `FinanceAppUsers`
`JwtSettings:ExpiryMinutes`	✅	Token lifetime in minutes — e.g. `60`
`ImportSettings:ApiKey`	✅	Static key for `X-Import-Key` header on `/api/transactions/import`
`Logging:LogLevel:Default`	—	`Information` (prod) / `Debug` (dev)
`AllowedHosts`	—	`*` for development; restrict in production
---
Deployment Notes
CORS
The Angular frontend (`http://localhost:4200` in dev) must be listed in CORS:
```csharp
// Program.cs
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("https://your-frontend-domain.com")
              .AllowAnyHeader()
              .AllowAnyMethod()));

app.UseCors("Frontend");
```
Production checklist
[ ] Replace `InMemoryTokenBlacklist` with a Redis-backed implementation
[ ] Set `Logging:LogLevel:Default` to `Warning` or `Error`
[ ] Restrict `AllowedHosts` to your domain
[ ] Use a secrets manager (AWS Secrets Manager / Azure Key Vault) for
`SecretKey`, `DefaultConnection`, and `ImportSettings:ApiKey`
[ ] Run `dotnet ef script --idempotent` and apply migrations via CI/CD
[ ] Replace `PlaidBankServiceStub` with the real `PlaidBankService` implementation
[ ] Set up HTTPS termination at the reverse proxy (Nginx / ALB)
[ ] Configure health-check endpoint for load-balancer probes
Useful commands
```bash
# Production publish
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish

# EF Core idempotent migration script for CI/CD
dotnet ef migrations script --idempotent --output migrations.sql

# Check for outdated NuGet packages
dotnet list package --outdated
```
---
Contributing
Fork the repository and create a feature branch:
```bash
   git checkout -b feature/your-feature-name
   ```
Write tests for any new business logic — target ≥ 80 % coverage.
Follow the existing patterns — thin controller, interface-backed service,
xUnit tests with mocked or in-memory dependencies.
Never commit secrets — use `appsettings.Development.json` locally and
ensure it remains in `.gitignore`.
Open a Pull Request against `main` with a clear description of the change.
Branch naming convention
Prefix	Usage
`feature/`	New feature
`fix/`	Bug fix
`refactor/`	Code refactoring without behaviour change
`test/`	Adding or updating tests only
`docs/`	Documentation updates
`chore/`	Dependency updates, CI/CD changes
---
License
This project is proprietary. All rights reserved.
---
Built with .NET 8 · PostgreSQL · EF Core · JWT · BCrypt