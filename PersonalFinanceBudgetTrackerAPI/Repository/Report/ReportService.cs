using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Report
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _db;

        private static readonly string[] AssetTypes = { "Bank", "Investment", "Wallet" };
        private static readonly string[] LiabilityTypes = { "Credit" };

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // MONTHLY INCOME VS EXPENSE
        // ---------------------------------------------------------------
        public async Task<ReportResult<IEnumerable<MonthlyReportDto>>> GetMonthlyReportAsync(
            int userId, string month)
        {
            try
            {
                if (!TryParseMonth(month, out int year, out int mon))
                    return Fail<IEnumerable<MonthlyReportDto>>("Invalid month format.");

                var transactions = await _db.Transactions
                    .AsNoTracking()
                    .Include(t => t.Account)
                    .Where(t => t.UserId == userId
                             && t.TransactionDate.Year == year
                             && t.TransactionDate.Month == mon
                             && t.Account!.IsActive)
                    .ToListAsync();

                var report = transactions
                    .GroupBy(t => new { t.AccountId, t.Account!.AccountName })
                    .Select(g => new MonthlyReportDto
                    {
                        AccountId = g.Key.AccountId,
                        AccountName = g.Key.AccountName,
                        Month = month,
                        TotalIncome = g.Where(t => t.Type == "Income").Sum(t => t.Amount),
                        TotalExpense = g.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                        NetAmount = g.Where(t => t.Type == "Income").Sum(t => t.Amount)
                                     - g.Where(t => t.Type == "Expense").Sum(t => t.Amount)
                    })
                    .OrderBy(r => r.AccountName)
                    .ToList();

                return Ok(report.AsEnumerable(),
                    $"{report.Count} account(s) returned for {month}.");
            }
            catch (Exception ex)
            {
                return Fail<IEnumerable<MonthlyReportDto>>(ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // YEARLY FINANCIAL REPORT
        // ---------------------------------------------------------------
        public async Task<ReportResult<IEnumerable<YearlyReportDto>>> GetYearlyReportAsync(
            int userId, string year)
        {
            try
            {
                if (!int.TryParse(year, out int yearInt))
                    return Fail<IEnumerable<YearlyReportDto>>("Invalid year format.");

                var transactions = await _db.Transactions
                    .AsNoTracking()
                    .Include(t => t.Account)
                    .Where(t => t.UserId == userId
                             && t.TransactionDate.Year == yearInt
                             && t.Account!.IsActive)
                    .ToListAsync();

                var report = transactions
                    .GroupBy(t => new
                    {
                        t.AccountId,
                        t.Account!.AccountName,
                        t.TransactionDate.Year,
                        t.TransactionDate.Month
                    })
                    .Select(g => new YearlyReportDto
                    {
                        AccountId = g.Key.AccountId,
                        AccountName = g.Key.AccountName,
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = new DateTime(g.Key.Year, g.Key.Month, 1)
                                           .ToString("MMMM"),
                        TotalIncome = g.Where(t => t.Type == "Income").Sum(t => t.Amount),
                        TotalExpense = g.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                        NetAmount = g.Where(t => t.Type == "Income").Sum(t => t.Amount)
                                     - g.Where(t => t.Type == "Expense").Sum(t => t.Amount)
                    })
                    .OrderBy(r => r.AccountName)
                    .ThenBy(r => r.Month)
                    .ToList();

                return Ok(report.AsEnumerable(),
                    $"{report.Count} record(s) returned for {year}.");
            }
            catch (Exception ex)
            {
                return Fail<IEnumerable<YearlyReportDto>>(ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // CATEGORY BREAKDOWN
        // ---------------------------------------------------------------
        public async Task<ReportResult<IEnumerable<CategoryBreakdownDto>>> GetCategoryBreakdownAsync(
            int userId, string month)
        {
            try
            {
                if (!TryParseMonth(month, out int year, out int mon))
                    return Fail<IEnumerable<CategoryBreakdownDto>>("Invalid month format.");

                var transactions = await _db.Transactions
                    .AsNoTracking()
                    .Include(t => t.Account)
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId
                             && t.TransactionDate.Year == year
                             && t.TransactionDate.Month == mon
                             && t.Account!.IsActive)
                    .ToListAsync();

                var report = transactions
                    .GroupBy(t => new
                    {
                        t.AccountId,
                        t.Account!.AccountName,
                        t.CategoryId,
                        CategoryName = t.Category!.Name,
                        t.Type
                    })
                    .Select(g => new CategoryBreakdownDto
                    {
                        AccountId = g.Key.AccountId,
                        AccountName = g.Key.AccountName,
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName,
                        Type = g.Key.Type,
                        Total = g.Sum(t => t.Amount),
                        Count = g.Count()
                    })
                    .OrderBy(r => r.AccountName)
                    .ThenByDescending(r => r.Total)
                    .ToList();

                return Ok(report.AsEnumerable(),
                    $"{report.Count} category breakdown(s) returned for {month}.");
            }
            catch (Exception ex)
            {
                return Fail<IEnumerable<CategoryBreakdownDto>>(ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // NET WORTH
        // Assets (Bank + Investment + Wallet) minus Liabilities (Credit)
        // ---------------------------------------------------------------
        public async Task<ReportResult<NetWorthDto>> GetNetWorthAsync(int userId)
        {
            try
            {
                var accounts = await _db.Accounts
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && a.IsActive)
                    .ToListAsync();

                var accountDetails = accounts
                    .Select(a => new AccountNetWorthDto
                    {
                        AccountId = a.AccountId,
                        AccountName = a.AccountName,
                        AccountType = a.AccountType,
                        Balance = a.Balance,
                        IsAsset = AssetTypes.Contains(a.AccountType)
                    })
                    .OrderByDescending(a => a.IsAsset)
                    .ThenBy(a => a.AccountName)
                    .ToList();

                decimal totalAssets = accountDetails.Where(a => a.IsAsset).Sum(a => a.Balance);
                decimal totalLiabilities = accountDetails.Where(a => !a.IsAsset).Sum(a => a.Balance);

                var netWorth = new NetWorthDto
                {
                    SnapshotDate = DateTime.UtcNow,
                    TotalAssets = totalAssets,
                    TotalLiabilit = totalLiabilities,
                    NetWorth = totalAssets - totalLiabilities,
                    Accounts = accountDetails
                };

                return Ok(netWorth, "Net worth calculated successfully.");
            }
            catch (Exception ex)
            {
                return Fail<NetWorthDto>(ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // Private helpers
        // ---------------------------------------------------------------
        private static bool TryParseMonth(string month, out int year, out int mon)
        {
            year = 0; mon = 0;
            if (string.IsNullOrWhiteSpace(month) || !month.Contains('-')) return false;
            var parts = month.Split('-');
            return parts.Length == 2
                && int.TryParse(parts[0], out year)
                && int.TryParse(parts[1], out mon)
                && mon >= 1 && mon <= 12;
        }

        private static ReportResult<T> Ok<T>(T data, string message) =>
            new ReportResult<T> { Success = true, Message = message, Data = data };

        private static ReportResult<T> Fail<T>(string message) =>
            new ReportResult<T> { Success = false, Message = message };
    }

}
