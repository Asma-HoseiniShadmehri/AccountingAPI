using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_DocumentHistory")]
    public class DocumentHistory
    {
        [Key]
        public int Id { get; set; }

        public int OriginalId { get; set; }

        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int DocumentNumber { get; set; }

        // ============================================================
        //  تاریخ سند به صورت شمسی (رشته‌ای)
        //  مثال: "1405/06/14"
        // ============================================================
        public string DocumentDate { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // نوع سند: "دائم" / "موقت" / "اصلاحی"
        public string DocumentType { get; set; } = string.Empty;

        public bool IsBalanced { get; set; }

        // ============================================================
        //  وضعیت سند در تاریخچه (رشته‌ای)
        //  مقادیر مجاز:
        //  - "Temp" = موقت (قابل ویرایش و حذف)
        //  - "Lock" = قطعی (غیرقابل ویرایش و حذف - فقط با سند اصلاحی)
        // ============================================================
        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // ============================================================
        //  نوع عملیات انجام‌شده روی سند
        //  مقادیر: "Insert" / "Finalize" / "Delete" / "Update"
        // ============================================================
        public string ActionType { get; set; } = string.Empty;

        public DateTime ActionDate { get; set; }

        public int? UserId { get; set; }
    }
}