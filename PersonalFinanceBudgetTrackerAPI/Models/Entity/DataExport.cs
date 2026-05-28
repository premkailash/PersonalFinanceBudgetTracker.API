using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("dataexport")]
    public class DataExport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("exportid")]
        public int ExportId { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("reporttype")]
        public string ReportType { get; set; } = string.Empty;   // Budget | Transaction

        [Required]
        [Column("fromdate")]
        public DateTime FromDate { get; set; }

        [Required]
        [Column("todate")]
        public DateTime ToDate { get; set; }


        [Required]
        [Column("userid")]
        public int UserId { get; set; }

        [Required]
        [Column("accountid")]
        public int AccountId { get; set; }

        [Column("isgenerated")]
        public bool IsGenerated { get; set; } = false;

        [Required]
        [MaxLength(30)]
        [Column("reportoptions")]
        public string ReportOptions { get; set; } = string.Empty;   // CSV | PDF

        [MaxLength(500)]
        [Column("reportlink")]
        public string? ReportLink { get; set; }                    // NULL until generated
        
        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

    }
}
