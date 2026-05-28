using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Report;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Report
{
    public interface IReportService
    {
        Task<ReportResult<IEnumerable<MonthlyReportDto>>> GetMonthlyReportAsync(int userId, string month);
        Task<ReportResult<IEnumerable<YearlyReportDto>>> GetYearlyReportAsync(int userId, string year);
        Task<ReportResult<IEnumerable<CategoryBreakdownDto>>> GetCategoryBreakdownAsync(int userId, string month);
        Task<ReportResult<NetWorthDto>> GetNetWorthAsync(int userId);

    }
}
