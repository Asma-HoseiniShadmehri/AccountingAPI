using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_AccountTafziliHistory")]
    public class AccountTafziliHistory
    {
        [Key]
        public int Id { get; set; }

        public int OriginalId { get; set; }

        // فیلدهای جدید (هماهنگ با مدل اصلی)
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Moein_id { get; set; }
        public int Code_tafzili { get; set; }
        public string Name_tafzili { get; set; } = string.Empty;

        // فیلدهای تاریخچه
        public string ActionType { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public int? UserId { get; set; }
    }
}