using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("AccountKol")]
    public class AccountKol
    {
        [Key]
        public int id_kol { get; set; }

        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }

        // کلید خارجی مخفی (برای ارتباط با گروه)
        public int Group_id { get; set; }

        public int Code_kol { get; set; }          // ۳ رقمی (SKK)
        public string Name_kol { get; set; } = string.Empty;

        public int? CreatedByUserId { get; set; }

        [ForeignKey("Group_id")]
        public virtual AccountGroup? Group { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }

        [NotMapped]
        public string? Token { get; set; }
    }
}