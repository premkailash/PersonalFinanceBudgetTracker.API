using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("userid")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("role")]
        [MaxLength(20)]
        public string Role { get; set; } = "User";

        [Column("is2faenabled")]
        public bool Is2FAEnabled { get; set; } = false;
        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
