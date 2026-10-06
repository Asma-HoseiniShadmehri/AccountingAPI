using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Helpers;
using AccountingAPI.Services;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/tafzili")]
    [ApiController]
    public class AccountTafziliController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContextFactory _tenantFactory;

        public AccountTafziliController(AppDbContext context, ITenantContextFactory tenantFactory)
        {
            _context = context;
            _tenantFactory = tenantFactory;
        }

        private async Task<int?> GetUserIdFromTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var userToken = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.TokenHash == token && !t.IsRevoked && t.ExpiresAt > DateTime.Now);
            return userToken?.UserId;
        }

        private async Task<bool> HasCompanyAccessAsync(int userId, int companyId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Mobile))
                return false;

            return await _context.UserCompanies
                .AnyAsync(uc => uc.Mobile == user.Mobile
                             && uc.CompanyId == companyId
                             && uc.DeleteDate == null
                             && uc.DisableDate == null);
        }

        private async Task<(int companyId, int fiscalYear)?> FindTafziliTenantAsync(int tafziliId)
        {
            var databases = await _context.Databases
                .Where(d => d.Level == "Combined" && d.DeleteDate == null && d.Number != null)
                .ToListAsync();

            foreach (var db in databases)
            {
                if (!int.TryParse(db.Number, out int fy)) continue;
                try
                {
                    using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(db.CompanyId, fy);
                    if (await tenant.AccountTafzilis.AnyAsync(t => t.id_tafzili == tafziliId))
                        return (db.CompanyId, fy);
                }
                catch { }
            }
            return null;
        }

        // ============================================================
        //  CREATE — اصلاح شد: Kol و Moein را مستقیم از کد ۹ رقمی پیدا می‌کنیم
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateTafzili([FromBody] CreateTafziliRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Company_id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });

                if (!await HasCompanyAccessAsync(userId.Value, request.Company_id))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                if (request.Fiscal_year <= 0)
                    return Ok(new { Code = -1, Message = "سال مالی معتبر نیست" });
                if (!AccountingHelper.IsValidTafziliCode(request.Code_tafzili))
                    return Ok(new { Code = -3, Message = "کد تفصیلی باید یک عدد ۹ رقمی باشد" });
                if (string.IsNullOrEmpty(request.Name_tafzili))
                    return Ok(new { Code = -1, Message = "نام تفصیلی الزامی است" });

                // ✅ استخراج کد کل (۳ رقم اول) و کد معین (۶ رقم اول)
                var kolCode = request.Code_tafzili / 1000000;   // مثال: 101202001 → 101
                var moeinCode = request.Code_tafzili / 1000;      // مثال: 101202001 → 101202

                // ✅ پیدا کردن کل مستقیماً
                using var kolTenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id, request.Fiscal_year);
                var kol = await kolTenant.AccountKols
                    .FirstOrDefaultAsync(k => k.Company_id == request.Company_id &&
                                               k.Fiscal_year == request.Fiscal_year &&
                                               k.Code_kol == kolCode);
                if (kol == null)
                    return Ok(new { Code = -2, Message = "حساب کل معتبر با این کد یافت نشد" });

                // ✅ پیدا کردن معین مستقیماً
                using var moeinTenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    request.Company_id, request.Fiscal_year);
                var moein = await moeinTenant.AccountMoeins
                    .FirstOrDefaultAsync(m => m.Company_id == request.Company_id &&
                                               m.Fiscal_year == request.Fiscal_year &&
                                               m.Code_moein == moeinCode);
                if (moein == null)
                    return Ok(new { Code = -2, Message = "حساب معین معتبر با این کد یافت نشد" });

                // ✅ چک گروه از روی Group_id خود کل
                using var groupTenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id, request.Fiscal_year);
                var group = await groupTenant.AccountGroups
                    .FirstOrDefaultAsync(g => g.id_Group == kol.Group_id && g.DeletedAt == null);
                if (group == null)
                    return Ok(new { Code = -2, Message = "گروه معتبر برای این کل یافت نشد" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id, request.Fiscal_year);

                var existing = await tenant.AccountTafzilis
                    .FirstOrDefaultAsync(t => t.Moein_id == moein.id_moein &&
                                               t.Code_tafzili == request.Code_tafzili &&
                                               t.Company_id == request.Company_id &&
                                               t.Fiscal_year == request.Fiscal_year);
                if (existing != null)
                    return Ok(new { Code = -2, Message = "این کد تفصیلی قبلاً در این معین ثبت شده است" });

                var lastTafziliId = await tenant.AccountTafzilis
                    .Where(t => t.Company_id == request.Company_id && t.Fiscal_year == request.Fiscal_year)
                    .OrderByDescending(t => t.id_tafzili)
                    .Select(t => t.id_tafzili)
                    .FirstOrDefaultAsync();

                var tafzili = new AccountTafzili
                {
                    Company_id = request.Company_id,
                    Fiscal_year = request.Fiscal_year,
                    Moein_id = moein.id_moein,
                    Code_tafzili = request.Code_tafzili,
                    Name_tafzili = request.Name_tafzili,
                    CreatedByUserId = userId.Value
                };
                tenant.AccountTafzilis.Add(tafzili);
                await tenant.SaveChangesAsync();

                tenant.AccountTafziliHistories.Add(new AccountTafziliHistory
                {
                    OriginalId = tafzili.id_tafzili,
                    Company_id = tafzili.Company_id,
                    Fiscal_year = tafzili.Fiscal_year,
                    Moein_id = tafzili.Moein_id,
                    Code_tafzili = tafzili.Code_tafzili,
                    Name_tafzili = tafzili.Name_tafzili,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب تفصیلی با موفقیت ایجاد شد",
                    Data = new
                    {
                        tafzili.id_tafzili,
                        tafzili.Company_id,
                        tafzili.Fiscal_year,
                        tafzili.Code_tafzili,
                        tafzili.Name_tafzili,
                        LastTafziliId = lastTafziliId
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  SEARCH
        // ============================================================
        [HttpPost("search")]
        public async Task<IActionResult> SearchTafzilis([FromBody] SearchTafziliRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (!request.Company_id.HasValue || request.Company_id.Value <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت الزامی است" });
                if (!request.Fiscal_year.HasValue || request.Fiscal_year.Value <= 0)
                    return Ok(new { Code = -1, Message = "سال مالی الزامی است" });

                if (!await HasCompanyAccessAsync(userId.Value, request.Company_id.Value))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                int limit = 100;

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id.Value, request.Fiscal_year.Value);

                var baseQuery = tenant.AccountTafzilis
                    .Where(t => t.Company_id == request.Company_id.Value
                             && t.Fiscal_year == request.Fiscal_year.Value);

                if (request.Code_tafzili.HasValue && request.Code_tafzili.Value > 0)
                    baseQuery = baseQuery.Where(t => t.Code_tafzili == request.Code_tafzili.Value);

                if (!string.IsNullOrEmpty(request.Name_tafzili) && request.Name_tafzili != "string")
                    baseQuery = baseQuery.Where(t => t.Name_tafzili.Contains(request.Name_tafzili));

                var orderedQuery = baseQuery;
                switch (request.OrderBy?.ToLower())
                {
                    case "min":
                        orderedQuery = baseQuery.OrderBy(t => t.id_tafzili);
                        break;
                    case "max":
                        orderedQuery = baseQuery.OrderByDescending(t => t.id_tafzili);
                        break;
                    case "name_asc":
                        orderedQuery = baseQuery.OrderBy(t => t.Name_tafzili);
                        break;
                    case "name_desc":
                        orderedQuery = baseQuery.OrderByDescending(t => t.Name_tafzili);
                        break;
                    case "code_asc":
                        orderedQuery = baseQuery.OrderBy(t => t.Code_tafzili);
                        break;
                    case "code_desc":
                        orderedQuery = baseQuery.OrderByDescending(t => t.Code_tafzili);
                        break;
                    default:
                        orderedQuery = baseQuery.OrderByDescending(t => t.id_tafzili);
                        break;
                }

                var totalCount = await baseQuery.CountAsync();

                var tafzilis = await orderedQuery
                    .Take(limit)
                    .ToListAsync();

                var result = tafzilis.Select(t => new
                {
                    t.id_tafzili,
                    t.Company_id,
                    t.Fiscal_year,
                    t.Code_tafzili,
                    t.Name_tafzili
                }).ToList();

                var returnedCount = result.Count;
                var lastId = result.Any() ? result.Last().id_tafzili : (int?)null;
                var hasMore = totalCount > returnedCount;

                return Ok(new
                {
                    Code = 1,
                    Data = result,
                    Limit = limit,
                    ReturnedCount = returnedCount,
                    TotalCount = totalCount,
                    HasMore = hasMore,
                    NextLastId = hasMore ? lastId : null,
                    IsLimited = totalCount > returnedCount
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }
        // ============================================================
        //  UPDATE
        // ============================================================
        [HttpPost("update")]
        public async Task<IActionResult> UpdateTafzili([FromBody] UpdateTafziliRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه تفصیلی معتبر نیست" });

                var location = await FindTafziliTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب تفصیلی مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var tafzili = await tenant.AccountTafzilis.FindAsync(request.Id);
                if (tafzili == null)
                    return Ok(new { Code = -2, Message = "حساب تفصیلی مورد نظر یافت نشد" });

                bool hasCode = request.Code_tafzili.HasValue && request.Code_tafzili.Value > 0;
                bool hasName = !string.IsNullOrEmpty(request.Name_tafzili);

                if (!hasCode && !hasName)
                    return Ok(new { Code = -1, Message = "هیچ فیلدی برای ویرایش ارسال نشده است" });

                tenant.AccountTafziliHistories.Add(new AccountTafziliHistory
                {
                    OriginalId = tafzili.id_tafzili,
                    Company_id = tafzili.Company_id,
                    Fiscal_year = tafzili.Fiscal_year,
                    Moein_id = tafzili.Moein_id,
                    Code_tafzili = tafzili.Code_tafzili,
                    Name_tafzili = tafzili.Name_tafzili,
                    ActionType = "Update",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                if (hasCode)
                {
                    if (!AccountingHelper.IsValidTafziliCode(request.Code_tafzili!.Value))
                        return Ok(new { Code = -3, Message = "کد تفصیلی باید یک عدد ۹ رقمی باشد" });

                    if (request.Code_tafzili.Value != tafzili.Code_tafzili)
                    {
                        var newMoeinCode = request.Code_tafzili.Value / 1000;

                        using var moeinTenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                            tafzili.Company_id, tafzili.Fiscal_year);
                        var newMoein = await moeinTenant.AccountMoeins
                            .FirstOrDefaultAsync(m => m.Company_id == tafzili.Company_id &&
                                                      m.Fiscal_year == tafzili.Fiscal_year &&
                                                      m.Code_moein == newMoeinCode);
                        if (newMoein == null)
                            return Ok(new { Code = -2, Message = "حساب معین معتبر با این کد یافت نشد" });

                        var existing = await tenant.AccountTafzilis
                            .FirstOrDefaultAsync(t => t.Moein_id == newMoein.id_moein &&
                                                      t.Code_tafzili == request.Code_tafzili.Value &&
                                                      t.id_tafzili != request.Id);
                        if (existing != null)
                            return Ok(new { Code = -2, Message = "این کد تفصیلی قبلاً در این معین ثبت شده است" });

                        tafzili.Moein_id = newMoein.id_moein;
                        tafzili.Code_tafzili = request.Code_tafzili.Value;
                    }
                }

                if (hasName)
                    tafzili.Name_tafzili = request.Name_tafzili!;

                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب تفصیلی با موفقیت ویرایش شد",
                    Data = new
                    {
                        tafzili.id_tafzili,
                        tafzili.Company_id,
                        tafzili.Fiscal_year,
                        tafzili.Code_tafzili,
                        tafzili.Name_tafzili
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  DELETE
        // ============================================================
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteTafzili([FromBody] DeleteTafziliRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Company_id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت معتبر نیست" });
                if (request.Fiscal_year <= 0)
                    return Ok(new { Code = -1, Message = "سال مالی معتبر نیست" });

                if (!await HasCompanyAccessAsync(userId.Value, request.Company_id))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id, request.Fiscal_year);

                var tafzili = await tenant.AccountTafzilis
                    .FirstOrDefaultAsync(t => t.id_tafzili == request.Id &&
                                               t.Company_id == request.Company_id &&
                                               t.Fiscal_year == request.Fiscal_year);
                if (tafzili == null)
                    return Ok(new { Code = -2, Message = "حساب تفصیلی مورد نظر در این شرکت و سال مالی یافت نشد" });

                var usedInDocuments = await tenant.DocumentRows
                    .AnyAsync(r => r.tafzili_id == tafzili.id_tafzili);

                if (usedInDocuments)
                {
                    var rowCount = await tenant.DocumentRows
                        .CountAsync(r => r.tafzili_id == tafzili.id_tafzili);

                    return Ok(new
                    {
                        Code = -3,
                        Message = $"این حساب تفصیلی در {rowCount} ردیف سند استفاده شده است و قابل حذف نیست. ابتدا اسناد مرتبط را حذف کنید.",
                        SubAccountType = "ردیف سند",
                        SubAccountCount = rowCount
                    });
                }

                tenant.AccountTafziliHistories.Add(new AccountTafziliHistory
                {
                    OriginalId = tafzili.id_tafzili,
                    Company_id = tafzili.Company_id,
                    Fiscal_year = tafzili.Fiscal_year,
                    Moein_id = tafzili.Moein_id,
                    Code_tafzili = tafzili.Code_tafzili,
                    Name_tafzili = tafzili.Name_tafzili,
                    ActionType = "Delete",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                tenant.AccountTafzilis.Remove(tafzili);
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب تفصیلی با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }
        // ============================================================
        //  History
        // ============================================================
        [HttpPost("history")]
        public async Task<IActionResult> GetHistory([FromBody] GetTafziliHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindTafziliTenantAsync(request.OriginalId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب تفصیلی مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var raw = await tenant.AccountTafziliHistories
                    .Where(h => h.OriginalId == request.OriginalId)
                    .OrderByDescending(h => h.ActionDate)
                    .ToListAsync();

                var histories = raw.Select(h => new
                {
                    h.Id,
                    h.OriginalId,
                    h.Company_id,
                    h.Fiscal_year,
                    h.Code_tafzili,
                    h.Name_tafzili,
                    h.ActionType,
                    h.ActionDate,
                    h.UserId
                }).ToList();

                return Ok(new { Code = 1, Data = histories });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  LAST-RECORD — اصلاح شد: فقط آخرین رکورد، بدون لیست
        // ============================================================
        [HttpPost("last-record")]
        public async Task<IActionResult> GetLastRecord([FromBody] LastRecordTafziliRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Company_id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه شرکت الزامی است" });
                if (request.Fiscal_year <= 0)
                    return Ok(new { Code = -1, Message = "سال مالی الزامی است" });

                if (!await HasCompanyAccessAsync(userId.Value, request.Company_id))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id, request.Fiscal_year);

                var lastRecord = await tenant.AccountTafzilis
                    .Where(t => t.Company_id == request.Company_id &&
                                t.Fiscal_year == request.Fiscal_year)
                    .OrderByDescending(t => t.id_tafzili)
                    .FirstOrDefaultAsync();

                if (lastRecord == null)
                {
                    return Ok(new
                    {
                        Code = 1,
                        LastRecord = (object?)null
                    });
                }

                return Ok(new
                {
                    Code = 1,
                    LastRecord = new
                    {
                        lastRecord.id_tafzili,
                        lastRecord.Code_tafzili,
                        lastRecord.Name_tafzili,
                        NextCode_tafzili = lastRecord.Code_tafzili + 1
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        public class CreateTafziliRequest
        {
            public int Company_id { get; set; }
            public int Fiscal_year { get; set; }
            public int Code_tafzili { get; set; }
            public string Name_tafzili { get; set; }
            public string Token { get; set; }
        }

        public class SearchTafziliRequest
        {
            public int? Company_id { get; set; }
            public int? Fiscal_year { get; set; }
            public int? Code_tafzili { get; set; }
            public string? Name_tafzili { get; set; }
            public string? OrderBy { get; set; }
            public string Token { get; set; }
        }

        public class UpdateTafziliRequest
        {
            public int Id { get; set; }
            public int? Code_tafzili { get; set; }
            public string? Name_tafzili { get; set; }
            public string? Token { get; set; }
        }

        public class DeleteTafziliRequest
        {
            public int Id { get; set; }
            public int Company_id { get; set; }
            public int Fiscal_year { get; set; }
            public string Token { get; set; }
        }

        public class GetTafziliHistoryRequest
        {
            public int OriginalId { get; set; }
            public string Token { get; set; }
        }

        public class LastRecordTafziliRequest
        {
            public int Company_id { get; set; }
            public int Fiscal_year { get; set; }
            public string Token { get; set; }
        }
    }
}