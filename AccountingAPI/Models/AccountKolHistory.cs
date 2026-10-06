using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_AccountKolHistory")]
    public class AccountKolHistory
    {
        [Key]
        public int Id { get; set; }

        public int OriginalId { get; set; }

        // فیلدهای جدید با نوع int
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Group_id { get; set; }
        public int Code_kol { get; set; }
        public string Name_kol { get; set; } = string.Empty;

        public string ActionType { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public int? UserId { get; set; }
    }
}