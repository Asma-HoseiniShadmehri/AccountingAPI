using System; // استفاده از فضای نام پایه

namespace AccountingAPI.Models
{
    public class CompanyDatabase
    {
        public int Id { get; set; } // شناسه یکتای دیتابیس شرکت
        public int CompanyId { get; set; } // کلید خارجی به جدول Companies
        public Company? Company { get; set; } // شیء شرکت مرتبط
        public string? ServerName { get; set; } // نام سرور SQL Server
        public string? DatabaseName { get; set; } // نام دیتابیس شرکت
        public string? UserId { get; set; } // نام کاربری SQL Server (اختیاری)
        public string? Password { get; set; } // رمز عبور SQL Server (اختیاری)
        public string? ConnectionString { get; set; } // رشته اتصال کامل به دیتابیس
        public DateTime CreatedDate { get; set; } // تاریخ ایجاد رکورد
        public bool IsActive { get; set; } = true; // فعال بودن دیتابیس (پیش‌فرض true)
    }
}