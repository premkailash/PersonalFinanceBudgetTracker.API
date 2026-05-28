using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.DataExport
{
    public class CreateDataExportRequestDto
    {
        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "AccountId is required.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "ReportType is required.")]
        [RegularExpression("^(Budget|Transaction)$",
            ErrorMessage = "ReportType must be 'Budget' or 'Transaction'.")]
        public string ReportType { get; set; } = string.Empty;

        [Required(ErrorMessage = "FromDate is required.")]
        public DateTime FromDate { get; set; }

        [Required(ErrorMessage = "ToDate is required.")]
        public DateTime ToDate { get; set; }

        [Required(ErrorMessage = "ReportOptions is required.")]
        [RegularExpression("^(CSV|PDF)$",
            ErrorMessage = "ReportOptions must be 'CSV' or 'PDF'.")]
        public string ReportOptions { get; set; } = string.Empty;

    }
}
