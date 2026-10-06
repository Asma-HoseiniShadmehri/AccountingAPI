namespace AccountingAPI.Services
{
    /// <summary>
    /// پیاده‌سازی موقت — به جای ارسال واقعی، کد را در Console چاپ می‌کند.
    /// برای اتصال به پنل واقعی (Kavenegar / Melipayamak / ...) فقط متد
    /// SendOtpAsync را تغییر دهید — بقیه کد دست نمی‌خورد.
    /// </summary>
    public class SmsService : ISmsService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmsService> _logger;

        public SmsService(IConfiguration configuration, ILogger<SmsService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpAsync(string mobile, string otpCode)
        {
            try
            {
                // 📌 TODO: اینجا کد پنل پیامکی واقعی را قرار دهید
                // مثال با Kavenegar:
                // var api = new KavenegarApi(apiKey);
                // await api.Send(apiKey, "OTP", mobile, $"کد شما: {otpCode}");

                _logger.LogWarning("📱 [DEV] OTP for {Mobile}: {Code}", mobile, otpCode);
                Console.WriteLine($"📱 [DEV] OTP for {mobile}: {otpCode}");

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال SMS به {Mobile}", mobile);
                return false;
            }
        }
    }
}