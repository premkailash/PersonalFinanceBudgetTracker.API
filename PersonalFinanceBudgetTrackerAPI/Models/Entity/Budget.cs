using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("budgets")]
    public class Budget
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("budgetid")]
        public int BudgetId { get; set; }

        [Required]
        [Column("userid")]
        public int UserId { get; set; }

        [Required]
        [Column("accountid")]
        public int AccountId { get; set; }

        [Required]
        [Column("categoryid")]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("targetamount",TypeName = "decimal(15,2)")]
        public decimal TargetAmount { get; set; }

        [Column("currentamount",TypeName = "decimal(15,2)")]
        public decimal CurrentAmount { get; set; } = 0.00m;

        [Column("targetdate")]
        public DateTime TargetDate { get; set; }

        [Column("autocontributeamount",TypeName = "decimal(15,2)")]
        public decimal AutoContributeAmount { get; set; } = 0.00m;

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }
    }

}
