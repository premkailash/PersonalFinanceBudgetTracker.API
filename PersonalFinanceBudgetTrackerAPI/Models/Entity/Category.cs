using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinanceBudgetTrackerAPI.Models.Entity
{
    [Table("category")]
    public class Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("categoryid")]
        public int CategoryId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("type")]
        [MaxLength(10)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Column("icon")]
        [MaxLength(50)]
        public string Icon { get; set; } = string.Empty;
               
        [Column("isdefault")]
        public bool IsDefault { get; set; } = false;       
    }
}
