using System; // فضای نام برای DateTime

namespace AccountingAPI.Models
{
    public class User
    {
        public int Id { get; set; } // شناسه یکتای کاربر
        public string? Mobile { get; set; } // شماره موبایل کاربر (نام کاربری)
        public string? Password { get; set; } // رمز عبور کاربر
        public string? FullName { get; set; } // نام کامل کاربر
        public DateTime RegisterDate { get; set; } // تاریخ ثبت‌نام کاربر
        public DateTime? DisableDate { get; set; } // تاریخ غیرفعال شدن خودکار
        public DateTime? DeleteDate { get; set; } // تاریخ حذف خودکار
    }
}