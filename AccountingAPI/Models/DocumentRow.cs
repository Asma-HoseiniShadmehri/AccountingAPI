using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_DocumentRow")]
    public class DocumentRow
    {
        [Key]
        public int Id { get; set; }

        public int DocumentId { get; set; }

        public int tafzili_id { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }

        public string RowDescription { get; set; } = string.Empty;

        public int RowNumber { get; set; }

        [ForeignKey("DocumentId")]
        public virtual Document? Document { get; set; }

        [ForeignKey("tafzili_id")]
        public virtual AccountTafzili? Tafzili { get; set; }
    }
}