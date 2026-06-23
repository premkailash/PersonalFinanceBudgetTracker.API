using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport;

namespace PersonalFinanceBudgetTrackerAPI.Repository.DataExport
{
    public interface IDataExportService
    {
        Task<DataExportResult> RequestExportAsync(CreateDataExportRequestDto request);
        Task<DataExportDownloadResult> GetExportDownloadAsync(int exportId, int callerId);

        Task <DataExportListResult> GetExportRequestAsync(int callerId);

    }
}
