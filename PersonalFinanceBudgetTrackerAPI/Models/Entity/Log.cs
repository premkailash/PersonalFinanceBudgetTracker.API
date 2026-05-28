using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("logs")]
    public class Log
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("logid")]
        public int LogId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("event")]
        public string Event { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("eventtype")]
        public string EventType { get; set; } = string.Empty;

        // Nullable — system events may have no actor
        [Column("actorid")]
        public int? ActorId { get; set; }
        
        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property (SET NULL on user delete)
        [ForeignKey(nameof(ActorId))]
        public User? Actor { get; set; }
    }

}
