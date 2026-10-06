using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Helpers;

namespace AccountingAPI.Filters
{
    public class TokenRenewalFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TokenRenewalFilter> _logger;

        public TokenRenewalFilter(AppDbContext context, ILogger<TokenRenewalFilter> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultContext = await next();

            if (resultContext.HttpContext.Response.StatusCode >= 200 &&
                resultContext.HttpContext.Response.StatusCode < 300)
            {
                // استخراج توکن
                string token = ExtractTokenFromHeadersOrQuery(resultContext.HttpContext.Request);

                if (string.IsNullOrEmpty(token))
                    token = ExtractTokenFromActionArguments(context.ActionArguments);

                if (!string.IsNullOrEmpty(token))
                {
                    var userToken = await _context.UserTokens
                        .FirstOrDefaultAsync(t => t.TokenHash == token &&
                                                   !t.IsRevoked &&
                                                   t.ExpiresAt > DateTime.Now);
                    if (userToken != null)
                    {
                        // تمدید ۱۵ دقیقه‌ای
                        userToken.ExpiresAt = DateTime.Now.AddMinutes(15);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Token renewed. New expiry: {userToken.ExpiresAt}");

                        // ✅ افزودن هدرها به پاسخ
                        resultContext.HttpContext.Response.Headers["X-Token-Created-At"] =
                            DateHelper.GregorianToPersianWithTime(userToken.CreatedAt);
                        resultContext.HttpContext.Response.Headers["X-Token-Expires-At"] =
                            DateHelper.GregorianToPersianWithTime(userToken.ExpiresAt);
                        resultContext.HttpContext.Response.Headers["X-System-Now"] =
                            DateHelper.GregorianToPersianWithTime(DateTime.Now);
                    }
                }
            }
        }

        private string ExtractTokenFromHeadersOrQuery(HttpRequest request)
        {
            if (request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var parts = authHeader.ToString().Split(' ');
                if (parts.Length == 2 && parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                    return parts[1];
            }

            if (request.Query.TryGetValue("token", out var tokenQuery))
                return tokenQuery.ToString();

            return null;
        }

        private string ExtractTokenFromActionArguments(IDictionary<string, object> actionArguments)
        {
            foreach (var arg in actionArguments.Values)
            {
                if (arg == null) continue;

                var tokenProp = arg.GetType().GetProperty("Token");
                if (tokenProp != null)
                {
                    var tokenValue = tokenProp.GetValue(arg) as string;
                    if (!string.IsNullOrEmpty(tokenValue))
                        return tokenValue;
                }
            }
            return null;
        }
    }
}