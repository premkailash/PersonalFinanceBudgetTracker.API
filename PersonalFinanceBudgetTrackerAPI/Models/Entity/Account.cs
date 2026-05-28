using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("accounts")]
    public class Account
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("accountid")]
        public int AccountId { get; set; }

        [Required]
        [Column("userid")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("accountname")]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("accounttype")]
        public string AccountType { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        [Column("currency")]
        public string Currency { get; set; } = "USD";

        [Column("balance",TypeName = "decimal(15,2)")]
        public decimal Balance { get; set; } = 0.00m;
        [Column("linkedat")]
        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
        [Column("isactive")]
        public bool IsActive { get; set; } = true;

        // Navigation Property
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

    }
}
