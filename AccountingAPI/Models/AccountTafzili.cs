using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("AccountTafzili")]
    public class AccountTafzili
    {
        [Key]
        public int id_tafzili { get; set; }

        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }

        // کلید خارجی مخفی (برای ارتباط با معین)
        public int Moein_id { get; set; }

        public int Code_tafzili { get; set; }          // ۹ رقمی (SKKMMMTTT)
        public string Name_tafzili { get; set; } = string.Empty;

        public int? CreatedByUserId { get; set; }

        [ForeignKey("Moein_id")]
        public virtual AccountMoein? Moein { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }

        [NotMapped]
        public string? Token { get; set; }
    }
}