using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_Document")]
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int Company_id { get; set; }

        [Required]
        public int Fiscal_year { get; set; }

        [Required]
        public int DocumentNumber { get; set; }

        // ============================================================
        //  تاریخ سند به صورت شمسی (رشته‌ای)
        //  مثال: "1405/06/14"
        // ============================================================
        public string DocumentDate { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // نوع سند: "دائم" / "موقت" / "اصلاحی"
        public string DocumentType { get; set; } = "دائم";

        public bool IsBalanced { get; set; }

        // ============================================================
        //  وضعیت سند (رشته‌ای)
        //  مقادیر مجاز:
        //  - "Temp" = موقت (قابل ویرایش و حذف)
        //  - "Lock" = قطعی (غیرقابل ویرایش و حذف - فقط با سند اصلاحی)
        //  پیش‌فرض: "Temp"
        // ============================================================
        public string Status { get; set; } = "Temp";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DeletedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public int? OriginalDocumentId { get; set; }
        public int? CurrencyId { get; set; }   // ← این خط را اضافه کن (nullable برای سازگاری)

        [ForeignKey("Company_id")]
        public virtual Company? Company { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedByUser { get; set; }

        [ForeignKey("OriginalDocumentId")]
        public virtual Document? OriginalDocument { get; set; }

        [NotMapped]
        public string? Token { get; set; }
    }
}