using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_Currency")]
    public class Currency
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;      // USD, IRT, EUR

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;      // دلار، تومان، یورو

        [MaxLength(10)]
        public string Symbol { get; set; } = string.Empty;    // $, ﷼, €

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}