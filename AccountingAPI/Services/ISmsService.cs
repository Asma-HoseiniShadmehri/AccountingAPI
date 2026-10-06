namespace AccountingAPI.Services
{
    public interface ISmsService
    {
        Task<bool> SendOtpAsync(string mobile, string otpCode);
    }
}