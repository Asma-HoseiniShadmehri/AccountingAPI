using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Services;
using System.Security.Cryptography;
using System.Text;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/auth")]
    [ApiController]
    [Tags("PasswordReset")]
    public class PasswordResetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        // ─── تنظیمات امنیتی ───
        private const int OtpExpirySeconds = 120;   // ۲ دقیقه
        private const int OtpLength = 6;
        private const int MaxOtpAttempts = 5;     // حداکثر تلاش برای OTP
        private const int ResetTokenExpiryMin = 10;    // ۱۰ دقیقه
        private const int OtpRequestCooldownSec = 60;    // ۱ دقیقه بین دو درخواست
        private const int MaxRequestsPerHour = 5;     // سقف ساعتی

        // rate-limit سبک (in-memory) — مثل الگوی _loginAttempts موجود
        private static readonly Dictionary<string, List<DateTime>> _otpRequests = new();

        public PasswordResetController(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        // ============================================================
        //  ابزارهای کمکی
        // ============================================================
        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        private static string GenerateOtp()
        {
            // امن‌تر از Random معمولی
            var buffer = new byte[4];
            RandomNumberGenerator.Fill(buffer);
            var value = BitConverter.ToUInt32(buffer, 0) % 1_000_000;
            return value.ToString($"D{OtpLength}");
        }

        private static string GenerateResetToken()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);
            return Convert.ToBase64String(buffer);
        }

        private bool IsRateLimited(string mobile, out string reason)
        {
            reason = string.Empty;

            if (!_otpRequests.ContainsKey(mobile))
                _otpRequests[mobile] = new List<DateTime>();

            var history = _otpRequests[mobile];
            var now = DateTime.Now;

            // پاکسازی رکوردهای قدیمی‌تر از ۱ ساعت
            history.RemoveAll(t => (now - t).TotalHours > 1);

            // چک cooldown ۶۰ ثانیه
            var last = history.LastOrDefault();
            if (last != default && (now - last).TotalSeconds < OtpRequestCooldownSec)
            {
                var remaining = OtpRequestCooldownSec - (int)(now - last).TotalSeconds;
                reason = $"لطفاً {remaining} ثانیه دیگر تلاش کنید";
                return true;
            }

            // چک سقف ساعتی
            if (history.Count >= MaxRequestsPerHour)
            {
                reason = "تعداد درخواست‌های شما بیش از حد مجاز است. لطفاً بعداً تلاش کنید";
                return true;
            }

            history.Add(now);
            return false;
        }

        // ============================================================
        //  مرحله ۱: درخواست OTP
        // ============================================================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Mobile))
                    return Ok(new { Code = -1, Message = "شماره موبایل الزامی است" });

                if (request.Mobile.Length != 11 || !request.Mobile.All(char.IsDigit))
                    return Ok(new { Code = -1, Message = "شماره موبایل نامعتبر است" });

                // 🔒 rate-limit
                if (IsRateLimited(request.Mobile, out var rateReason))
                    return Ok(new { Code = -3, Message = rateReason });

                // 🔒 نکته امنیتی: چه کاربر وجود داشته باشد چه نه، پیام یکسان است
                //    تا مهاجم نتواند وجود/عدم وجود موبایل را کشف کند
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Mobile == request.Mobile);

                if (user == null ||
                    (user.DeleteDate.HasValue && user.DeleteDate.Value <= DateTime.Now))
                {
                    // پاسخ یکسان ولی بدون ارسال واقعی
                    return Ok(new
                    {
                        Code = 1,
                        Message = "اگر این شماره در سیستم ثبت شده باشد، کد بازیابی ارسال خواهد شد",
                        ExpiresInSeconds = OtpExpirySeconds
                    });
                }

                if (user.DisableDate.HasValue && user.DisableDate.Value <= DateTime.Now)
                    return Ok(new { Code = -2, Message = "حساب کاربری شما غیرفعال شده است" });

                // باطل کردن کدهای قبلی این موبایل
                var oldTokens = await _context.PasswordResetTokens
                    .Where(t => t.Mobile == request.Mobile && !t.IsRevoked && t.UsedAt == null)
                    .ToListAsync();

                foreach (var t in oldTokens)
                    t.IsRevoked = true;

                // تولید و ذخیره کد جدید
                var otpCode = GenerateOtp();
                var resetToken = new PasswordResetToken
                {
                    Mobile = request.Mobile,
                    OtpCodeHash = ComputeSha256(otpCode),
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddSeconds(OtpExpirySeconds),
                    Attempts = 0,
                    IsRevoked = false,
                    ClientIP = HttpContext.Connection.RemoteIpAddress?.ToString()
                };
                _context.PasswordResetTokens.Add(resetToken);
                await _context.SaveChangesAsync();

                // ارسال SMS
                var sent = await _smsService.SendOtpAsync(request.Mobile, otpCode);
                if (!sent)
                    return Ok(new { Code = -1, Message = "خطا در ارسال پیامک. لطفاً دوباره تلاش کنید" });

                return Ok(new
                {
                    Code = 1,
                    Message = "اگر این شماره در سیستم ثبت شده باشد، کد بازیابی ارسال خواهد شد",
                    ExpiresInSeconds = OtpExpirySeconds
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  مرحله ۲: تأیید OTP
        // ============================================================
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Mobile) || string.IsNullOrEmpty(request.OtpCode))
                    return Ok(new { Code = -1, Message = "موبایل و کد تأیید الزامی است" });

                if (request.OtpCode.Length != OtpLength || !request.OtpCode.All(char.IsDigit))
                    return Ok(new { Code = -1, Message = "کد تأیید باید ۶ رقم باشد" });

                // آخرین توکن فعال این موبایل
                var token = await _context.PasswordResetTokens
                    .Where(t => t.Mobile == request.Mobile
                             && !t.IsRevoked
                             && t.UsedAt == null)
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefaultAsync();

                if (token == null)
                    return Ok(new { Code = -2, Message = "کد بازیابی یافت نشد. لطفاً دوباره درخواست دهید" });

                if (token.ExpiresAt <= DateTime.Now)
                    return Ok(new { Code = -2, Message = "کد بازیابی منقضی شده است" });

                if (token.Attempts >= MaxOtpAttempts)
                {
                    token.IsRevoked = true;
                    await _context.SaveChangesAsync();
                    return Ok(new { Code = -3, Message = "تعداد تلاش‌های ناموفق بیش از حد مجاز. لطفاً کد جدید درخواست دهید" });
                }

                // بررسی کد
                var inputHash = ComputeSha256(request.OtpCode);
                if (inputHash != token.OtpCodeHash)
                {
                    token.Attempts++;
                    await _context.SaveChangesAsync();

                    var remaining = MaxOtpAttempts - token.Attempts;
                    return Ok(new
                    {
                        Code = -2,
                        Message = $"کد تأیید اشتباه است. {remaining} تلاش باقی‌مانده",
                        RemainingAttempts = remaining
                    });
                }

                // ✅ کد صحیح → تولید ResetToken
                var resetTokenPlain = GenerateResetToken();
                token.ResetTokenHash = ComputeSha256(resetTokenPlain);
                token.VerifiedAt = DateTime.Now;
                token.ExpiresAt = DateTime.Now.AddMinutes(ResetTokenExpiryMin);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "کد تأیید صحیح است",
                    Data = new
                    {
                        ResetToken = resetTokenPlain,
                        ExpiresInMinutes = ResetTokenExpiryMin
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  مرحله ۳: تغییر رمز عبور
        // ============================================================
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResetToken) || string.IsNullOrEmpty(request.NewPassword))
                    return Ok(new { Code = -1, Message = "توکن و رمز جدید الزامی است" });

                if (request.NewPassword.Length < 6)
                    return Ok(new { Code = -3, Message = "رمز عبور باید حداقل ۶ کاراکتر باشد" });

                var tokenHash = ComputeSha256(request.ResetToken);

                var token = await _context.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.ResetTokenHash == tokenHash
                                           && !t.IsRevoked
                                           && t.UsedAt == null);

                if (token == null)
                    return Ok(new { Code = -2, Message = "توکن بازیابی نامعتبر است" });

                if (token.ExpiresAt <= DateTime.Now)
                    return Ok(new { Code = -2, Message = "توکن بازیابی منقضی شده است" });

                if (token.VerifiedAt == null)
                    return Ok(new { Code = -2, Message = "ابتدا کد تأیید را وارد کنید" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Mobile == token.Mobile);
                if (user == null)
                    return Ok(new { Code = -2, Message = "کاربر یافت نشد" });

                // تغییر رمز
                user.Password = request.NewPassword;

                // باطل کردن توکن بازیابی
                token.UsedAt = DateTime.Now;
                token.IsRevoked = true;

                // 🔒 خروج از همه دستگاه‌ها — همه توکن‌های فعال کاربر باطل می‌شوند
                var activeTokens = await _context.UserTokens
                    .Where(t => t.UserId == user.Id && !t.IsRevoked)
                    .ToListAsync();

                foreach (var ut in activeTokens)
                    ut.IsRevoked = true;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "رمز عبور با موفقیت تغییر کرد. لطفاً دوباره وارد شوید"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  کلاس‌های Request
        // ============================================================
        public class ForgotPasswordRequest
        {
            public string Mobile { get; set; }
        }

        public class VerifyOtpRequest
        {
            public string Mobile { get; set; }
            public string OtpCode { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string ResetToken { get; set; }
            public string NewPassword { get; set; }
        }
    }
}