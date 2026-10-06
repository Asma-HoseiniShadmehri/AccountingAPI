using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingAPI.Models
{
    [Table("tbl_PasswordResetToken")]
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(11)]
        public string Mobile { get; set; } = string.Empty;

        // کد ۶ رقمی OTP — به صورت SHA256 ذخیره می‌شود (نه متن ساده)
        [Required]
        [MaxLength(128)]
        public string OtpCodeHash { get; set; } = string.Empty;

        // توکن بلند برای مرحله ۳ — فقط بعد از verify-otp پر می‌شود
        [MaxLength(128)]
        public string? ResetTokenHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? UsedAt { get; set; }

        // تعداد تلاش‌های ناموفق برای وارد کردن OTP
        public int Attempts { get; set; } = 0;

        public bool IsRevoked { get; set; } = false;

        [MaxLength(45)]
        public string? ClientIP { get; set; }
    }
}