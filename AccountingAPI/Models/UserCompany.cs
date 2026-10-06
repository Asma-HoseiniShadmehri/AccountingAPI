using System; // فضای نام برای DateTime

namespace AccountingAPI.Models
{
    public class UserCompany
    {
        public int Id { get; set; } // شناسه یکتای ارتباط
        public string? Mobile { get; set; } // موبایل کاربر
        public int CompanyId { get; set; } // کلید خارجی شرکت
        public Company? Company { get; set; } // شیء شرکت مرتبط
        public int? DatabaseId { get; set; } // کلید خارجی دیتابیس (قابل null)
        public DatabaseEntity? Database { get; set; } // شیء دیتابیس مرتبط
        public DateTime RegisterDate { get; set; } // تاریخ ثبت ارتباط
        public DateTime? DisableDate { get; set; } // تاریخ غیرفعال شدن
        public DateTime? DeleteDate { get; set; } // تاریخ حذف شدن

        // ========== فیلدهای جدید برای مدیریت دسترسی ==========
        public bool IsOwner { get; set; } // آیا مدیر شرکت است؟
        public bool CanSelect { get; set; } = true; // دسترسی مشاهده (SELECT)
        public bool CanInsert { get; set; } = false; // دسترسی ثبت (INSERT)
        public bool CanUpdate { get; set; } = false; // دسترسی ویرایش (UPDATE)
        public bool CanDelete { get; set; } = false; // دسترسی حذف (DELETE)
    }
}