using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_AccountGroupHistory")]
    public class AccountGroupHistory
    {
        [Key]
        public int Id { get; set; }

        public int OriginalId { get; set; }

        // فیلدهای جدید با نوع int
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Code_Group { get; set; }
        public string Name_Group { get; set; } = string.Empty;
        public int Nature_Group { get; set; }
        public int type_Group { get; set; }

        public string ActionType { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public int? UserId { get; set; }
    }
}