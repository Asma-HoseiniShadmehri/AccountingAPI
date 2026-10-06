using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/auth")]
    [ApiController]
    public class AccountingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private static Dictionary<string, int> _loginAttempts = new Dictionary<string, int>();

        public AccountingController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ========== توابع کمکی ==========
        private string GenerateSecureToken(int userId, string fullName, string mobile)
        {
            var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = new Random();
            var nonce = random.Next(100000, 999999);
            var rawToken = $"{userId}:{fullName}:{mobile}:{milliseconds}:{nonce}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(rawToken);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private async Task<int?> GetUserIdFromTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            var userToken = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.TokenHash == token && !t.IsRevoked && t.ExpiresAt > DateTime.Now);
            return userToken?.UserId;
        }

        private void RecordFailedAttempt(string key)
        {
            if (_loginAttempts.ContainsKey(key))
                _loginAttempts[key]++;
            else
                _loginAttempts[key] = 1;
        }

        private string NormalizeCompanyName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var patterns = new[] { "شرکت ", "شرکت" };
            var result = input.Trim();

            foreach (var pattern in patterns)
            {
                if (result.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(pattern.Length).TrimStart();
                    break;
                }
            }

            return result;
        }

        // ============================================================
        //  1. LOGIN
        // ============================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Mobile) || string.IsNullOrEmpty(request.Password))
                    return Ok(new { Code = -1, Message = "موبایل و رمز عبور الزامی است" });

                var attemptKey = request.Mobile;
                if (_loginAttempts.ContainsKey(attemptKey) && _loginAttempts[attemptKey] >= 5)
                    return Ok(new { Code = -3, Message = "تلاش بیش از حد. لطفاً 5 دقیقه دیگر تلاش کنید" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Mobile == request.Mobile);
                if (user == null)
                {
                    RecordFailedAttempt(attemptKey);
                    return Ok(new { Code = -2, Message = "کاربری با این مشخصات وجود ندارد" });
                }

                if (user.DisableDate.HasValue && user.DisableDate.Value <= DateTime.Now)
                    return Ok(new { Code = -2, Message = "حساب کاربری شما غیرفعال شده است" });
                if (user.DeleteDate.HasValue && user.DeleteDate.Value <= DateTime.Now)
                    return Ok(new { Code = -2, Message = "حساب کاربری شما حذف شده است" });

                if (user.Password != request.Password)
                {
                    RecordFailedAttempt(attemptKey);
                    return Ok(new { Code = -2, Message = "رمز عبور اشتباه است" });
                }

                var expiredTokens = _context.UserTokens.Where(t => t.UserId == user.Id && t.ExpiresAt <= DateTime.Now);
                _context.UserTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();

                _loginAttempts.Remove(attemptKey);

                var tokenHash = GenerateSecureToken(user.Id, user.FullName, user.Mobile);
                var userToken = new UserToken
                {
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMinutes(15),
                    IsRevoked = false
                };
                _context.UserTokens.Add(userToken);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "ورود موفق",
                    Token = tokenHash,
                    CreatedAtShamsi = Helpers.DateHelper.GregorianToPersianWithTime(userToken.CreatedAt),
                    ExpiresAtShamsi = Helpers.DateHelper.GregorianToPersianWithTime(userToken.ExpiresAt),
                    NowShamsi = Helpers.DateHelper.GregorianToPersianWithTime(DateTime.Now),
                    Now = DateTime.Now,
                    ExpiresIn = 15,
                    Waiting = 0
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا در ورود: {ex.Message}" });
            }
        }

        // ============================================================
        //  TOKEN INFO
        // ============================================================
        [HttpPost("token-info")]
        public async Task<IActionResult> TokenInfo([FromBody] TokenInfoRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userToken = await _context.UserTokens
                    .FirstOrDefaultAsync(t => t.TokenHash == request.Token);

                if (userToken == null)
                    return Ok(new { Code = -2, Message = "توکن یافت نشد" });

                return Ok(new
                {
                    Code = 1,
                    Data = new
                    {
                        CreatedAtShamsi = Helpers.DateHelper.GregorianToPersianWithTime(userToken.CreatedAt),
                        ExpiresAtShamsi = Helpers.DateHelper.GregorianToPersianWithTime(userToken.ExpiresAt),
                        NowShamsi = Helpers.DateHelper.GregorianToPersianWithTime(DateTime.Now)
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  2. REGISTER USER
        // ============================================================
        [HttpPost("register-user")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Mobile))
                    return Ok(new { Code = -1, Message = "موبایل الزامی است" });
                if (string.IsNullOrEmpty(request.Password))
                    return Ok(new { Code = -1, Message = "رمز عبور الزامی است" });
                if (string.IsNullOrEmpty(request.FullName))
                    return Ok(new { Code = -1, Message = "نام کامل الزامی است" });
                if (request.Mobile.Length < 11)
                    return Ok(new { Code = -3, Message = "شماره موبایل نامعتبر است" });
                if (request.Password.Length < 6)
                    return Ok(new { Code = -3, Message = "رمز عبور باید حداقل 6 کاراکتر باشد" });

                if (await _context.Users.AnyAsync(u => u.Mobile == request.Mobile))
                    return Ok(new { Code = -2, Message = "این موبایل قبلاً ثبت شده است" });

                var user = new User
                {
                    Mobile = request.Mobile,
                    Password = request.Password,
                    FullName = request.FullName,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { Code = 1, Message = "کاربر با موفقیت ثبت شد" });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا در ثبت کاربر: {ex.Message}" });
            }
        }

        // ============================================================
        //  3. REGISTER COMPANY
        // ============================================================
        [HttpPost("register-company")]
        public async Task<IActionResult> RegisterCompany([FromBody] RegisterCompanyRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (string.IsNullOrEmpty(request.NationalId))
                    return Ok(new { Code = -1, Message = "شناسه ملی الزامی است" });
                if (string.IsNullOrEmpty(request.Name))
                    return Ok(new { Code = -1, Message = "نام شرکت الزامی است" });
                if (string.IsNullOrEmpty(request.Tel))
                    return Ok(new { Code = -1, Message = "تلفن الزامی است" });
                if (request.NationalId.Length != 11 || !request.NationalId.All(char.IsDigit))
                    return Ok(new { Code = -3, Message = "شناسه ملی باید 11 رقم باشد" });

                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                    return Ok(new { Code = -2, Message = "کاربر ثبت‌کننده وجود ندارد" });

                if (await _context.Companies.AnyAsync(c => c.Name == request.Name && c.DeleteDate == null))
                    return Ok(new { Code = -2, Message = "این شرکت قبلاً ثبت شده است" });
                if (await _context.Companies.AnyAsync(c => c.NationalId == request.NationalId && c.DeleteDate == null))
                    return Ok(new { Code = -2, Message = "این شناسه ملی قبلاً ثبت شده است" });

                var company = new Company
                {
                    NationalId = request.NationalId,
                    Name = request.Name,
                    X = request.X,
                    Y = request.Y,
                    Tel = request.Tel,
                    RegisteredByUserId = userId.Value,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                var existingUC = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == user.Mobile && uc.CompanyId == company.Id);
                if (existingUC == null)
                {
                    var userCompany = new UserCompany
                    {
                        Mobile = user.Mobile,
                        CompanyId = company.Id,
                        DatabaseId = null,
                        IsOwner = true,
                        CanSelect = true,
                        CanInsert = true,
                        CanUpdate = true,
                        CanDelete = true,
                        RegisterDate = DateTime.Now,
                        DisableDate = null,
                        DeleteDate = null
                    };
                    _context.UserCompanies.Add(userCompany);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "شرکت با موفقیت ثبت شد",
                    RegisteredBy = new { user.Id, user.FullName, user.Mobile },
                    Company = new
                    {
                        company.Id,
                        company.Name,
                        company.NationalId,
                        company.X,
                        company.Y,
                        company.Tel,
                        company.RegisterDate
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Ok(new { Code = -1, Message = $"خطا در ثبت شرکت: {ex.Message}" });
            }
        }

        // ============================================================
        //  4. REGISTER DATABASE
        // ============================================================
        [HttpPost("register-database")]
        public async Task<IActionResult> RegisterDatabase([FromBody] RegisterDatabaseRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.CompanyId <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });

                if (string.IsNullOrEmpty(request.CompanyNameEN))
                    return Ok(new { Code = -1, Message = "نام انگلیسی شرکت الزامی است" });

                if (string.IsNullOrEmpty(request.FiscalYear))
                    return Ok(new { Code = -1, Message = "سال مالی الزامی است" });

                var requester = await _context.Users.FindAsync(userId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == request.CompanyId && c.DeleteDate == null);
                if (company == null)
                    return Ok(new { Code = -2, Message = $"شرکتی با Id '{request.CompanyId}' یافت نشد" });

                var isOwner = await _context.UserCompanies
                    .AnyAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id && uc.IsOwner == true);
                if (!isOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند دیتابیس ثبت کند" });

                var pattern = _configuration["DatabaseSettings:DbNamePattern"];
                var dbName = Helpers.DatabaseNameGenerator.Generate(
                    pattern,
                    request.CompanyNameEN,
                    request.FiscalYear,
                    "AccountGroup"
                );

                if (await _context.Databases.AnyAsync(d => d.DbName == dbName && d.DeleteDate == null))
                    return Ok(new { Code = -2, Message = $"نام دیتابیس '{dbName}' قبلاً ثبت شده است" });

                var serverName = _configuration["DatabaseSettings:ServerName"] ?? "(localdb)\\MSSQLLocalDB";
                var tenantService = HttpContext.RequestServices.GetService<Services.ITenantService>();
                if (tenantService == null)
                    return Ok(new { Code = -2, Message = "سرویس TenantService در دسترس نیست" });

                var dbResult = await tenantService.CreateCompanyDatabase(
                    company.Id, serverName, dbName, "Combined");
                if (!dbResult)
                    return Ok(new { Code = -3, Message = $"ایجاد دیتابیس '{dbName}' با خطا مواجه شد" });

                var database = new DatabaseEntity
                {
                    DbName = dbName,
                    Number = request.FiscalYear,
                    Level = "Combined",
                    Memo = request.Memo,
                    CompanyId = company.Id,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Databases.Add(database);
                await _context.SaveChangesAsync();

                var userCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id);
                if (userCompany != null && userCompany.DatabaseId == null)
                {
                    userCompany.DatabaseId = database.Id;
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    Code = 1,
                    Message = $"دیتابیس '{dbName}' با موفقیت ثبت و ایجاد شد",
                    Data = new
                    {
                        database.Id,
                        database.DbName,
                        database.Level,
                        FiscalYear = database.Number,
                        database.Memo,
                        Company = new { company.Id, company.Name }
                    }
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : "";
                return Ok(new { Code = -1, Message = $"خطا در ثبت دیتابیس: {ex.Message} - Inner: {innerMessage}" });
            }
        }

        // ============================================================
        //  5. REGISTER USER-COMPANY
        // ============================================================
        [HttpPost("register-user-company")]
        public async Task<IActionResult> RegisterUserCompany([FromBody] RegisterUserCompanyRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Mobile))
                    return Ok(new { Code = -1, Message = "موبایل الزامی است" });
                if (request.CompanyId == null)
                    return Ok(new { Code = -1, Message = "شناسه شرکت الزامی است" });
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var requesterUserId = await GetUserIdFromTokenAsync(request.Token);
                if (requesterUserId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var requester = await _context.Users.FindAsync(requesterUserId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == request.CompanyId.Value && c.DeleteDate == null);
                if (company == null)
                    return Ok(new { Code = -2, Message = $"شرکتی با Id '{request.CompanyId}' یافت نشد" });

                var requesterCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id);
                if (requesterCompany == null || !requesterCompany.IsOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند کاربر جدید اضافه کند" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Mobile == request.Mobile);
                if (user == null)
                    return Ok(new { Code = -2, Message = "کاربر وجود ندارد" });

                var database = await _context.Databases
                    .FirstOrDefaultAsync(d => d.Id == request.DatabaseId && d.DeleteDate == null);
                if (database == null)
                    return Ok(new { Code = -2, Message = "دیتابیس وجود ندارد" });

                if (await _context.UserCompanies.AnyAsync(uc =>
                    uc.Mobile == request.Mobile && uc.CompanyId == company.Id && uc.DeleteDate == null))
                {
                    return Ok(new { Code = -2, Message = "این کاربر قبلاً به این شرکت متصل شده است" });
                }

                var userCompany = new UserCompany
                {
                    Mobile = user.Mobile,
                    CompanyId = company.Id,
                    DatabaseId = null,
                    IsOwner = false,
                    CanSelect = true,
                    CanInsert = false,
                    CanUpdate = false,
                    CanDelete = false,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.UserCompanies.Add(userCompany);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "ارتباط کاربر با شرکت و دیتابیس ثبت شد",
                    Data = new
                    {
                        Company = new { company.Id, company.Name },
                        User = new { user.Id, user.Mobile, user.FullName }
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message} - Inner: {ex.InnerException?.Message}" });
            }
        }

        // ============================================================
        //  6. ASSIGN PERMISSION
        // ============================================================
        [HttpPost("assign-permission")]
        public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var requesterUserId = await GetUserIdFromTokenAsync(request.Token);
                if (requesterUserId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var requester = await _context.Users.FindAsync(requesterUserId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                var requesterUserCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == request.CompanyId);
                if (requesterUserCompany == null)
                    return Ok(new { Code = -2, Message = "شما به این شرکت دسترسی ندارید" });

                if (!requesterUserCompany.IsOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند دسترسی بدهد" });

                var targetUserCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == request.TargetMobile && uc.CompanyId == request.CompanyId);
                if (targetUserCompany == null)
                    return Ok(new { Code = -2, Message = "این کاربر به این شرکت متصل نیست" });

                targetUserCompany.CanSelect = request.CanSelect;
                targetUserCompany.CanInsert = request.CanInsert;
                targetUserCompany.CanUpdate = request.CanUpdate;
                targetUserCompany.CanDelete = request.CanDelete;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "دسترسی‌ها با موفقیت به‌روزرسانی شد",
                    TargetUser = request.TargetMobile,
                    Permissions = new { request.CanSelect, request.CanInsert, request.CanUpdate, request.CanDelete }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  7. REGISTER ALL
        // ============================================================
        [HttpPost("register-all")]
        public async Task<IActionResult> RegisterAll([FromBody] RegisterAllRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrEmpty(request.Mobile)) return Ok(new { Code = -1, Message = "موبایل الزامی است" });
                if (string.IsNullOrEmpty(request.Password)) return Ok(new { Code = -1, Message = "رمز عبور الزامی است" });
                if (string.IsNullOrEmpty(request.FullName)) return Ok(new { Code = -1, Message = "نام کامل الزامی است" });
                if (request.Mobile.Length < 11) return Ok(new { Code = -3, Message = "شماره موبایل نامعتبر است" });
                if (request.Password.Length < 6) return Ok(new { Code = -3, Message = "رمز عبور باید حداقل 6 کاراکتر باشد" });

                if (string.IsNullOrEmpty(request.NationalId)) return Ok(new { Code = -1, Message = "شناسه ملی الزامی است" });
                if (string.IsNullOrEmpty(request.CompanyName)) return Ok(new { Code = -1, Message = "نام شرکت الزامی است" });
                if (string.IsNullOrEmpty(request.CompanyTel)) return Ok(new { Code = -1, Message = "تلفن شرکت الزامی است" });
                if (request.NationalId.Length != 11 || !request.NationalId.All(char.IsDigit))
                    return Ok(new { Code = -3, Message = "شناسه ملی باید 11 رقم باشد" });

                if (string.IsNullOrEmpty(request.CompanyNameEN))
                    return Ok(new { Code = -1, Message = "نام انگلیسی شرکت الزامی است" });
                if (string.IsNullOrEmpty(request.FiscalYear))
                    return Ok(new { Code = -1, Message = "سال مالی الزامی است" });

                if (await _context.Users.AnyAsync(u => u.Mobile == request.Mobile))
                    return Ok(new { Code = -2, Message = "این موبایل قبلاً ثبت شده است" });
                if (await _context.Companies.AnyAsync(c => c.Name == request.CompanyName && c.DeleteDate == null))
                    return Ok(new { Code = -2, Message = "این شرکت قبلاً ثبت شده است" });
                if (await _context.Companies.AnyAsync(c => c.NationalId == request.NationalId && c.DeleteDate == null))
                    return Ok(new { Code = -2, Message = "این شناسه ملی قبلاً ثبت شده است" });

                var user = new User
                {
                    Mobile = request.Mobile,
                    Password = request.Password,
                    FullName = request.FullName,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var company = new Company
                {
                    NationalId = request.NationalId,
                    Name = request.CompanyName,
                    X = request.X,
                    Y = request.Y,
                    Tel = request.CompanyTel,
                    RegisteredByUserId = user.Id,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                var serverName = _configuration["DatabaseSettings:ServerName"] ?? "(localdb)\\MSSQLLocalDB";
                var tenantService = HttpContext.RequestServices.GetService<Services.ITenantService>();
                if (tenantService == null)
                {
                    await transaction.RollbackAsync();
                    return Ok(new { Code = -2, Message = "سرویس TenantService در دسترس نیست" });
                }

                var pattern = _configuration["DatabaseSettings:DbNamePattern"];

                var dbName = Helpers.DatabaseNameGenerator.Generate(
                    pattern,
                    request.CompanyNameEN,
                    request.FiscalYear,
                    "AccountGroup"
                );

                if (await _context.Databases.AnyAsync(d => d.DbName == dbName && d.DeleteDate == null))
                {
                    await transaction.RollbackAsync();
                    return Ok(new { Code = -2, Message = $"نام دیتابیس '{dbName}' قبلاً ثبت شده است" });
                }

                var dbResult = await tenantService.CreateCompanyDatabase(
                    company.Id, serverName, dbName, "Combined");
                if (!dbResult)
                {
                    await transaction.RollbackAsync();
                    return Ok(new
                    {
                        Code = -3,
                        Message = $"ایجاد دیتابیس '{dbName}' با خطا مواجه شد. لطفاً با پشتیبانی تماس بگیرید."
                    });
                }

                var database = new DatabaseEntity
                {
                    DbName = dbName,
                    Number = request.FiscalYear,
                    Level = "Combined",
                    Memo = request.Memo,
                    CompanyId = company.Id,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.Databases.Add(database);
                await _context.SaveChangesAsync();

                var userCompany = new UserCompany
                {
                    Mobile = user.Mobile,
                    CompanyId = company.Id,
                    DatabaseId = database.Id,
                    IsOwner = true,
                    CanSelect = true,
                    CanInsert = true,
                    CanUpdate = true,
                    CanDelete = true,
                    RegisterDate = DateTime.Now,
                    DisableDate = null,
                    DeleteDate = null
                };
                _context.UserCompanies.Add(userCompany);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var tokenHash = GenerateSecureToken(user.Id, user.FullName, user.Mobile);
                var userToken = new UserToken
                {
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMinutes(15),
                    IsRevoked = false
                };
                _context.UserTokens.Add(userToken);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "ثبت‌نام کامل با موفقیت انجام شد و دیتابیس شرکت ایجاد گردید",
                    Token = tokenHash,
                    User = new { user.Id, user.Mobile, user.FullName, user.RegisterDate },
                    Company = new
                    {
                        company.Id,
                        company.Name,
                        company.NationalId,
                        company.X,
                        company.Y,
                        company.Tel,
                        company.RegisterDate
                    },
                    Databases = new[]
                    {
                        new
                        {
                            database.Id,
                            database.DbName,
                            database.Level,
                            FiscalYear = database.Number,
                            database.Memo,
                            database.RegisterDate
                        }
                    },
                    UserCompany = new
                    {
                        userCompany.Id,
                        userCompany.Mobile,
                        userCompany.CompanyId,
                        userCompany.DatabaseId,
                        userCompany.IsOwner,
                        userCompany.CanSelect,
                        userCompany.CanInsert,
                        userCompany.CanUpdate,
                        userCompany.CanDelete,
                        userCompany.RegisterDate
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Ok(new { Code = -1, Message = $"خطا در ثبت یکجا: {ex.Message}" });
            }
        }

        // ============================================================
        //  8. GET FINANCIAL YEARS — ✅ اصلاح‌شده: فقط سال‌های کاربر
        // ============================================================
        [HttpPost("get-financial-years")]
        public async Task<IActionResult> GetFinancialYears([FromBody] GetFinancialYearsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                // ✅ کاربر را پیدا کن
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null || string.IsNullOrEmpty(user.Mobile))
                    return Ok(new { Code = -2, Message = "کاربر یافت نشد" });

                // ✅ فقط دیتابیس‌هایی که این کاربر به آن‌ها دسترسی دارد
                var userDatabaseIds = await _context.UserCompanies
                    .Where(uc => uc.Mobile == user.Mobile
                              && uc.DeleteDate == null
                              && uc.DatabaseId != null)
                    .Select(uc => uc.DatabaseId!.Value)
                    .Distinct()
                    .ToListAsync();

                var years = await _context.Databases
                    .Where(d => d.DeleteDate == null
                             && !string.IsNullOrEmpty(d.Number)
                             && userDatabaseIds.Contains(d.Id))
                    .Select(d => d.Number)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToListAsync();

                return Ok(new { Code = 1, Data = years });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  9. GET COMPANIES BY YEAR — ✅ اصلاح‌شده: فقط شرکت‌های کاربر
        // ============================================================
        [HttpPost("get-companies-by-year")]
        public async Task<IActionResult> GetCompaniesByYear([FromBody] GetCompaniesByYearRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (string.IsNullOrEmpty(request.Year))
                    return Ok(new { Code = -1, Message = "سال مالی الزامی است" });

                // ✅ کاربر را پیدا کن
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null || string.IsNullOrEmpty(user.Mobile))
                    return Ok(new { Code = -2, Message = "کاربر یافت نشد" });

                var databases = await _context.Databases
                    .Where(d => d.Number == request.Year && d.DeleteDate == null)
                    .Select(d => d.Id)
                    .ToListAsync();

                if (databases.Count == 0)
                    return Ok(new { Code = 1, Data = new List<object>(), Message = "هیچ دیتابیسی برای این سال یافت نشد" });

                // ✅ فقط شرکت‌های همین کاربر
                var companyIds = await _context.UserCompanies
                    .Where(uc => databases.Contains(uc.DatabaseId.GetValueOrDefault())
                              && uc.DeleteDate == null
                              && uc.Mobile == user.Mobile)
                    .Select(uc => uc.CompanyId)
                    .Distinct()
                    .ToListAsync();

                if (companyIds.Count == 0)
                    return Ok(new { Code = 1, Data = new List<object>(), Message = "هیچ شرکتی برای این سال یافت نشد" });

                var companies = await _context.Companies
                    .Where(c => companyIds.Contains(c.Id) && c.DeleteDate == null)
                    .Select(c => new
                    {
                        c.Id,
                        c.NationalId,
                        c.Name,
                        c.X,
                        c.Y,
                        c.Tel,
                        c.RegisterDate
                    })
                    .ToListAsync();

                return Ok(new { Code = 1, Data = companies });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  10. GET COMPANY FINANCIAL LIST — ✅ اصلاح‌شده: فقط شرکت‌های کاربر
        // ============================================================
        [HttpPost("get-company-financial-list")]
        public async Task<IActionResult> GetCompanyFinancialList([FromBody] GetCompanyFinancialListRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                // ✅ کاربر را پیدا کن
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null || string.IsNullOrEmpty(user.Mobile))
                    return Ok(new { Code = -2, Message = "کاربر یافت نشد" });

                // ✅ فقط شرکت‌هایی که این کاربر به آن‌ها دسترسی دارد
                var userCompanies = await _context.UserCompanies
                    .Where(uc => uc.DeleteDate == null
                              && uc.DatabaseId != null
                              && uc.Mobile == user.Mobile)
                    .Include(uc => uc.Company)
                    .ToListAsync();

                var databases = await _context.Databases
                    .Where(d => d.DeleteDate == null && !string.IsNullOrEmpty(d.Number))
                    .ToDictionaryAsync(d => d.Id, d => new { d.Number, d.Level });

                var result = new List<object>();

                foreach (var uc in userCompanies)
                {
                    if (uc.Company != null && uc.DatabaseId.HasValue && databases.ContainsKey(uc.DatabaseId.Value))
                    {
                        var dbInfo = databases[uc.DatabaseId.Value];
                        result.Add(new
                        {
                            CompanyId = uc.Company.Id,
                            CompanyName = uc.Company.Name,
                            FinancialYear = dbInfo.Number,
                            Level = dbInfo.Level
                        });
                    }
                }

                return Ok(new { Code = 1, Data = result });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  11. GET COMPANY HISTORY
        // ============================================================
        [HttpPost("get-company-history")]
        public async Task<IActionResult> GetCompanyHistory([FromBody] GetCompanyHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.CompanyId <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });

                var histories = await _context.CompanyHistories
                    .Where(h => h.OriginalId == request.CompanyId)
                    .OrderByDescending(h => h.DeletedDate)
                    .ToListAsync();

                return Ok(new { Code = 1, Data = histories });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  12. GET USER HISTORY
        // ============================================================
        [HttpPost("get-user-history")]
        public async Task<IActionResult> GetUserHistory([FromBody] GetUserHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.CompanyId <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });

                var histories = await _context.UserHistories
                    .Where(h => h.CompanyId == request.CompanyId)
                    .OrderByDescending(h => h.DeletedAt)
                    .ToListAsync();

                return Ok(new { Code = 1, Data = histories });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  13. GET DATABASE HISTORY
        // ============================================================
        [HttpPost("get-database-history")]
        public async Task<IActionResult> GetDatabaseHistory([FromBody] GetDatabaseHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.CompanyId <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });

                var histories = await _context.DatabaseHistories
                    .Where(h => h.CompanyId == request.CompanyId)
                    .OrderByDescending(h => h.DeletedAt)
                    .ToListAsync();

                return Ok(new { Code = 1, Data = histories });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  14. DELETE COMPANY
        // ============================================================
        [HttpPost("delete-company")]
        public async Task<IActionResult> DeleteCompany([FromBody] DeleteCompanyRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var requester = await _context.Users.FindAsync(userId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                Company? company = null;

                if (request.CompanyId.HasValue)
                {
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Id == request.CompanyId.Value && c.DeleteDate == null);
                }
                else if (!string.IsNullOrEmpty(request.CompanyName))
                {
                    var normalizedName = NormalizeCompanyName(request.CompanyName);
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Name == normalizedName && c.DeleteDate == null);
                    if (company == null)
                        company = await _context.Companies
                            .FirstOrDefaultAsync(c => c.Name == request.CompanyName && c.DeleteDate == null);
                }

                if (company == null)
                    return Ok(new { Code = -2, Message = "شرکت مورد نظر یافت نشد یا قبلاً حذف شده است" });

                var isOwner = await _context.UserCompanies
                    .AnyAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id && uc.IsOwner == true);
                if (!isOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند شرکت را حذف کند" });

                var history = new CompanyHistory
                {
                    OriginalId = company.Id,
                    NationalId = company.NationalId,
                    Name = company.Name,
                    X = company.X,
                    Y = company.Y,
                    Tel = company.Tel,
                    DeletedByUserId = userId,
                    DeletedDate = DateTime.Now,
                    OperationType = "Delete"
                };
                _context.CompanyHistories.Add(history);

                company.DeleteDate = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = $"شرکت '{company.Name}' با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  15. DELETE USER-COMPANY
        // ============================================================
        [HttpPost("delete-user-company")]
        public async Task<IActionResult> DeleteUserCompany([FromBody] DeleteUserCompanyRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var requester = await _context.Users.FindAsync(userId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == request.CompanyId && c.DeleteDate == null);
                if (company == null)
                    return Ok(new { Code = -2, Message = "شرکت وجود ندارد" });

                var isOwner = await _context.UserCompanies
                    .AnyAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id && uc.IsOwner == true);
                if (!isOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند این عملیات را انجام دهد" });

                var userCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.Mobile == request.Mobile && uc.CompanyId == request.CompanyId && uc.DeleteDate == null);
                if (userCompany == null)
                    return Ok(new { Code = -2, Message = "این کاربر به این شرکت متصل نیست" });

                var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Mobile == request.Mobile);
                if (targetUser != null)
                {
                    var history = new UserHistory
                    {
                        OriginalId = targetUser.Id,
                        Mobile = targetUser.Mobile,
                        Password = targetUser.Password,
                        FullName = targetUser.FullName,
                        RegisterDate = targetUser.RegisterDate,
                        DisableDate = targetUser.DisableDate,
                        DeleteDate = targetUser.DeleteDate,
                        DeletedByUserId = userId,
                        DeletedAt = DateTime.Now,
                        OperationType = "DeleteUserCompany",
                        CompanyId = company.Id,
                        CompanyName = company.Name
                    };
                    _context.UserHistories.Add(history);
                }

                userCompany.DeleteDate = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = $"ارتباط کاربر '{request.Mobile}' با شرکت '{company.Name}' با موفقیت حذف شد (تاریخچه ذخیره شد)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  16. DELETE DATABASE
        // ============================================================
        [HttpPost("delete-database")]
        public async Task<IActionResult> DeleteDatabase([FromBody] DeleteDatabaseRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var requester = await _context.Users.FindAsync(userId.Value);
                if (requester == null)
                    return Ok(new { Code = -2, Message = "کاربر درخواست‌کننده وجود ندارد" });

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == request.CompanyId && c.DeleteDate == null);
                if (company == null)
                    return Ok(new { Code = -2, Message = "شرکت وجود ندارد" });

                var isOwner = await _context.UserCompanies
                    .AnyAsync(uc => uc.Mobile == requester.Mobile && uc.CompanyId == company.Id && uc.IsOwner == true);
                if (!isOwner)
                    return Ok(new { Code = -3, Message = "فقط مدیر شرکت می‌تواند دیتابیس را حذف کند" });

                var database = await _context.Databases
                    .FirstOrDefaultAsync(d => d.Number == request.Year && d.CompanyId == request.CompanyId && d.DeleteDate == null);
                if (database == null)
                    return Ok(new { Code = -2, Message = $"دیتابیسی با سال مالی '{request.Year}' برای این شرکت یافت نشد" });

                var history = new DatabaseHistory
                {
                    OriginalId = database.Id,
                    DbName = database.DbName,
                    Number = database.Number,
                    Memo = database.Memo,
                    RegisterDate = database.RegisterDate,
                    DisableDate = database.DisableDate,
                    DeleteDate = database.DeleteDate,
                    DeletedByUserId = userId,
                    DeletedAt = DateTime.Now,
                    OperationType = "Delete",
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    FinancialYear = request.Year
                };
                _context.DatabaseHistories.Add(history);

                database.DeleteDate = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = $"دیتابیس سال مالی '{request.Year}' با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
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
        public class LoginRequest
        {
            public string? Mobile { get; set; }
            public string? Password { get; set; }
        }
        public class TokenInfoRequest
        {
            public string Token { get; set; }
        }
        public class RegisterUserRequest
        {
            public string? Mobile { get; set; }
            public string? Password { get; set; }
            public string? FullName { get; set; }
        }

        public class RegisterCompanyRequest
        {
            public string? NationalId { get; set; }
            public string? Name { get; set; }
            public string? X { get; set; }
            public string? Y { get; set; }
            public string? Tel { get; set; }
            public string? Token { get; set; }
        }

        public class RegisterDatabaseRequest
        {
            public int CompanyId { get; set; }
            public string? CompanyNameEN { get; set; }
            public string? FiscalYear { get; set; }
            public string? Level { get; set; }
            public string? Memo { get; set; }
            public string? Token { get; set; }
        }

        public class RegisterUserCompanyRequest
        {
            public string? Mobile { get; set; }
            public int? CompanyId { get; set; }
            public int DatabaseId { get; set; }
            public string? Token { get; set; }
        }

        public class AssignPermissionRequest
        {
            public string? RequesterMobile { get; set; }
            public string? TargetMobile { get; set; }
            public int CompanyId { get; set; }
            public bool CanSelect { get; set; } = true;
            public bool CanInsert { get; set; } = false;
            public bool CanUpdate { get; set; } = false;
            public bool CanDelete { get; set; } = false;
            public string? Token { get; set; }
        }

        public class RegisterAllRequest
        {
            public string? Mobile { get; set; }
            public string? Password { get; set; }
            public string? FullName { get; set; }
            public string? NationalId { get; set; }
            public string? CompanyName { get; set; }
            public string? CompanyNameEN { get; set; }
            public string? FiscalYear { get; set; }
            public string? X { get; set; }
            public string? Y { get; set; }
            public string? CompanyTel { get; set; }
            public string? Memo { get; set; }
        }

        public class GetFinancialYearsRequest
        {
            public string Token { get; set; }
        }

        public class GetCompaniesByYearRequest
        {
            public string Year { get; set; }
            public string Token { get; set; }
        }

        public class GetCompanyFinancialListRequest
        {
            public string Token { get; set; }
        }

        public class GetCompanyHistoryRequest
        {
            public int CompanyId { get; set; }
            public string Token { get; set; }
        }

        public class GetUserHistoryRequest
        {
            public int CompanyId { get; set; }
            public string Token { get; set; }
        }

        public class GetDatabaseHistoryRequest
        {
            public int CompanyId { get; set; }
            public string Token { get; set; }
        }

        public class DeleteCompanyRequest
        {
            public int? CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string Token { get; set; }
        }

        public class DeleteUserCompanyRequest
        {
            public string Mobile { get; set; }
            public int CompanyId { get; set; }
            public string Token { get; set; }
        }

        public class DeleteDatabaseRequest
        {
            public int CompanyId { get; set; }
            public string Year { get; set; }
            public string Token { get; set; }
        }
    }
}