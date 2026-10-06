using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_AccountGroup")]
    public class AccountGroup
    {
        [Key]
        public int id_Group { get; set; }

        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Code_Group { get; set; }
        public string Name_Group { get; set; } = string.Empty;
        public int Nature_Group { get; set; }
        public int type_Group { get; set; }

        public DateTime? DeletedAt { get; set; }
        public int? CreatedByUserId { get; set; }

        [ForeignKey("Company_id")]
        public virtual Company? Company { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }

        [NotMapped]
        public string? Token { get; set; }
    }
}