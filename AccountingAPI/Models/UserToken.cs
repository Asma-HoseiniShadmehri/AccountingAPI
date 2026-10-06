using System; // فضای نام برای DateTime

namespace AccountingAPI.Models
{
    public class UserToken
    {
        public int Id { get; set; } // شناسه یکتای توکن
        public int UserId { get; set; } // کلید خارجی به کاربر
        public User? User { get; set; } // شیء کاربر مرتبط
        public string? TokenHash { get; set; } // هش توکن (SHA256)
        public DateTime CreatedAt { get; set; } // زمان ایجاد توکن
        public DateTime ExpiresAt { get; set; } // زمان انقضای توکن
        public bool IsRevoked { get; set; } // آیا توکن باطل شده است؟
    }
}