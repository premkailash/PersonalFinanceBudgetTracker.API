using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("transactionid")]
        public int TransactionId { get; set; }

        [Required]
        [Column("accountid")]
        public int AccountId { get; set; }

        [Required]
        [Column("userid")]
        public int UserId { get; set; }

        [Required]
        [Column("amount",TypeName = "decimal(15,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        [Column("currency")]
        public string Currency { get; set; } = "INR";

        [Required]
        [MaxLength(10)]
        [Column("type")]
        public string Type { get; set; } = string.Empty;  // Income | Expense

        [Required]
        [Column("categoryid")]
        public int CategoryId { get; set; }

        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("transactiondate")]
        public DateTime TransactionDate { get; set; }
        
        [Column("isrecurring")]
        public bool IsRecurring { get; set; } = false;

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }
    }

}
