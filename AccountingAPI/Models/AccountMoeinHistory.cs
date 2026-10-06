using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_AccountMoeinHistory")]
    public class AccountMoeinHistory
    {
        [Key]
        public int Id { get; set; }

        public int OriginalId { get; set; }

        // ========== فیلدهای جدید (هماهنگ با مدل اصلی) ==========
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Kol_id { get; set; }
        public int Code_moein { get; set; }
        public string Name_moein { get; set; } = string.Empty;

        // ========== فیلدهای تاریخچه ==========
        public string ActionType { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public int? UserId { get; set; }
    }
}