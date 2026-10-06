using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("AccountMoein")]
    public class AccountMoein
    {
        [Key]
        public int id_moein { get; set; }

        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }

        // کلید خارجی مخفی (برای ارتباط با کل)
        public int Kol_id { get; set; }

        public int Code_moein { get; set; }          // ۶ رقمی (SKKMMM)
        public string Name_moein { get; set; } = string.Empty;

        public int? CreatedByUserId { get; set; }

        [ForeignKey("Kol_id")]
        public virtual AccountKol? Kol { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }

        [NotMapped]
        public string? Token { get; set; }
    }
}