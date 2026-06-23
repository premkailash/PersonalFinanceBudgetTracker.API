using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Account;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;

namespace PersonalFinanceBudgetTrackerAPI.Repository.DataExport
{
    public class DataExportService : IDataExportService
    {
        private readonly AppDbContext _db;

        public DataExportService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // REQUEST EXPORT — creates a pending export entry in the DB
        // ---------------------------------------------------------------
        public async Task<DataExportResult> RequestExportAsync(CreateDataExportRequestDto request)
        {
            try
            {
                var export = new Models.Entity.DataExport
                {
                    ReportType = request.ReportType,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    UserId = request.UserId,
                    AccountId = request.AccountId,
                    IsGenerated = false,
                    ReportOptions = request.ReportOptions,
                    ReportLink = null,
                    Timestamp = DateTime.UtcNow
                };

                _db.DataExports.Add(export);
                await _db.SaveChangesAsync();

                return new DataExportResult
                {
                    Success = true,
                    Message = $"Export request submitted successfully. " +
                              $"Export ID: {export.ExportId}. " +
                              $"Your {export.ReportOptions} report will be available for download shortly.",
                    Data = MapToDto(export)
                };
            }
            catch (Exception ex)
            {
                return new DataExportResult
                {
                    Success = false,
                    Message = $"An error occurred while submitting the export request: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // GET EXPORT DOWNLOAD — returns the ReportLink if ready
        // ---------------------------------------------------------------
        public async Task<DataExportDownloadResult> GetExportDownloadAsync(int exportId, int callerId)
        {
            var export = await _db.DataExports
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ExportId == exportId);

            if (export == null)
                return new DataExportDownloadResult
                {
                    Success = false,
                    NotFound = true,
                    Message = $"Export with ID {exportId} was not found."
                };

            // Ownership check
            if (export.UserId != callerId)
                return new DataExportDownloadResult
                {
                    Success = false,
                    NotFound = false,
                    NotReady = false,
                    Message = "You are not authorized to access this export."
                };

            // Export not yet processed
            if (!export.IsGenerated || string.IsNullOrWhiteSpace(export.ReportLink))
                return new DataExportDownloadResult
                {
                    Success = false,
                    NotFound = false,
                    NotReady = true,
                    Message = "Export is still being processed. Please try again shortly.",
                    Data = new DataExportDownloadDto
                    {
                        ExportId = export.ExportId,
                        IsGenerated = export.IsGenerated,
                        ReportLink = null
                    }
                };

            return new DataExportDownloadResult
            {
                Success = true,
                Message = "Export is ready for download.",
                Data = new DataExportDownloadDto
                {
                    ExportId = export.ExportId,
                    IsGenerated = export.IsGenerated,
                    ReportLink = export.ReportLink
                }
            };
        }

        public async Task<DataExportListResult> GetExportRequestAsync(int callerId)
        {
            var export = await _db.DataExports
               .AsNoTracking()
               .Where(a => a.UserId == callerId)
               .OrderByDescending(x => x.ExportId)
               .Select(a => new DataExportResponseDto
               {
                  ExportId = a.ExportId,
                  ReportType = a.ReportType,
                  FromDate = a.FromDate,
                  ToDate = a.ToDate,
                  UserId = a.UserId,
                  AccountId = a.AccountId,
                  IsGenerated = a.IsGenerated,
                  ReportOptions = a.ReportOptions,
                  ReportLink = a.ReportLink,
                  Timestamp = a.Timestamp

               })
                .ToListAsync();
               

            if (export == null)
                return new DataExportListResult
                {
                    Success = false,
                    Message = "No Records found",
                    Data = null
                };
                     
            return new DataExportListResult
            {
                Success = true,
                Message = "Export is ready for download.",
                Data = export
            };
        }


        // ---------------------------------------------------------------
        // Private helper
        // ---------------------------------------------------------------
        private static DataExportResponseDto MapToDto(Models.Entity.DataExport e) =>
            new DataExportResponseDto
            {
                ExportId = e.ExportId,
                ReportType = e.ReportType,
                FromDate = e.FromDate,
                ToDate = e.ToDate,
                UserId = e.UserId,
                AccountId = e.AccountId,
                IsGenerated = e.IsGenerated,
                ReportOptions = e.ReportOptions,
                ReportLink = e.ReportLink,
                Timestamp = e.Timestamp
            };
    }

}
