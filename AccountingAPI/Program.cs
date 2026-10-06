using AccountingAPI.Data;
using AccountingAPI.Filters;
using AccountingAPI.Models;
using AccountingAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
//  ثبت سرویس‌ها (همه قبل از Build)
// ============================================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITenantContextFactory, TenantContextFactory>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<TokenRenewalFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<TokenRenewalFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "شرکت پارت حساب",
        Version = "v1",
        Description = "مدیریت حسابداری"
    });
    c.DocumentFilter<SwaggerDocumentFilter>();
});

// ============================================================
//  تنظیمات Kestrel — بستن اتصال HTTP بعد از بی‌کاری
// ============================================================
builder.WebHost.ConfigureKestrel(options =>
{
    // اگر کاربر ۲ دقیقه هیچ درخواستی نفرستد، اتصال HTTP بسته می‌شود
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);

    // حداکثر زمان برای دریافت هدرها
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

    // حداکثر تعداد اتصال همزمان
    options.Limits.MaxConcurrentConnections = 500;
});

var app = builder.Build();

// ============================================================
//  1. Developer Exception Page (اول از همه - مهم!)
// ============================================================
app.UseDeveloperExceptionPage();

// ============================================================
//  2. Middleware لاگ (ساده - فقط request + status)
// ============================================================
app.Use(async (context, next) =>
{
    string requestBody = "";
    try
    {
        if (context.Request.Method == "POST" || context.Request.Method == "PUT")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }
    }
    catch { /* نادیده گرفتن خطای خواندن بدنه */ }

    // فراخوانی Middleware بعدی
    await next();

    // ============================================================
    //  ثبت لاگ (فقط بعد از اجرای موفق یا ناموفق)
    //  بدون دستکاری Response
    // ============================================================
    try
    {
        int? userId = null;
        string? userMobile = null;

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var token = authHeader?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
            token = context.Request.Query["token"];

        if (!string.IsNullOrEmpty(token))
        {
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userToken = await db.UserTokens
                .FirstOrDefaultAsync(t => t.TokenHash == token && !t.IsRevoked && t.ExpiresAt > DateTime.Now);
            if (userToken != null)
            {
                userId = userToken.UserId;
                var user = await db.Users.FindAsync(userId);
                userMobile = user?.Mobile;
            }
        }

        var log = new AuditLog
        {
            UserId = userId,
            UserMobile = userMobile,
            Endpoint = context.Request.Path,
            Method = context.Request.Method,
            RequestBody = requestBody,
            ResponseStatus = context.Response.StatusCode.ToString(),
            ClientIP = context.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.Now
        };

        using var logScope = context.RequestServices.CreateScope();
        var logDb = logScope.ServiceProvider.GetRequiredService<AppDbContext>();
        logDb.AuditLogs.Add(log);
        await logDb.SaveChangesAsync();
    }
    catch (Exception logEx)
    {
        // اگر ثبت لاگ خطا داد، فقط در Console چاپ کن و ادامه بده
        Console.WriteLine($"⚠️ خطا در ثبت لاگ: {logEx.Message}");
    }
});

// ============================================================
//  3. Swagger
// ============================================================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "part-hesab v1");
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();