using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("DefaultBudgets")]
    public class DefaultBudget
    {
        [Key]
        [Column("DefaultBudgetId")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DefaultBudgetId { get; set; }

        // ── FK: Category ──────────────────────────────────────────────────────
        [Required]
        [Column("CategoryId")]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        // ── Core fields ───────────────────────────────────────────────────────
        [Required]
        [MaxLength(100)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("TargetAmount", TypeName = "decimal(15,2)")]
        public decimal TargetAmount { get; set; }

        [Required]
        [Column("AutoContributeAmount", TypeName = "decimal(15,2)")]
        public decimal AutoContributeAmount { get; set; } = 0;        

        /// <summary>
        /// NULL = applies every month (global default).
        /// Non-null = YYYY-MM month-specific override (e.g. "2024-12").
        /// </summary>
        [MaxLength(7)]
        [Column("EffectiveMonth", TypeName = "char(7)")]
        public string? EffectiveMonth { get; set; }

        [MaxLength(255)]
        [Column("Description")]
        public string? Description { get; set; }

        [Required]
        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        // ── Audit: Admin who created / last updated ────────────────────────────
        [Column("CreatedBy")]
        public int? CreatedBy { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public User? CreatedByUser { get; set; }

        [Column("UpdatedBy")]
        public int? UpdatedBy { get; set; }

        [ForeignKey(nameof(UpdatedBy))]
        public User? UpdatedByUser { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

}
