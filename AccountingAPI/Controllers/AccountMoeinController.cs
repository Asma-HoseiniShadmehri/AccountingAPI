using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Helpers;
using AccountingAPI.Services;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/moein")]
    [ApiController]
    public class AccountMoeinController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContextFactory _tenantFactory;

        public AccountMoeinController(AppDbContext context, ITenantContextFactory tenantFactory)
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

        private async Task<(int companyId, int fiscalYear)?> FindMoeinTenantAsync(int moeinId)
        {
            var databases = await _context.Databases
                .Where(d => d.Level == "Combined" && d.DeleteDate == null && d.Number != null)
                .ToListAsync();

            foreach (var db in databases)
            {
                if (!int.TryParse(db.Number, out int fy)) continue;
                try
                {
                    using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(db.CompanyId, fy);
                    if (await tenant.AccountMoeins.AnyAsync(m => m.id_moein == moeinId))
                        return (db.CompanyId, fy);
                }
                catch { }
            }
            return null;
        }

        // ============================================================
        //  CREATE — اصلاح شد: Kol را مستقیم از پیشوند ۳ رقمی پیدا می‌کنیم
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateMoein([FromBody] CreateMoeinRequest request)
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
                if (!AccountingHelper.IsValidMoeinCode(request.Code_moein))
                    return Ok(new { Code = -3, Message = "کد معین باید یک عدد ۶ رقمی باشد" });
                if (string.IsNullOrEmpty(request.Name_moein))
                    return Ok(new { Code = -1, Message = "نام معین الزامی است" });

                // ✅ استخراج کد کل از ۳ رقم اول کد معین (SKK)
                var kolCode = request.Code_moein / 1000;   // مثال: 101202 → 101

                // ✅ پیدا کردن کل مستقیماً
                using var kolTenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id, request.Fiscal_year);
                var kol = await kolTenant.AccountKols
                    .FirstOrDefaultAsync(k => k.Company_id == request.Company_id &&
                                               k.Fiscal_year == request.Fiscal_year &&
                                               k.Code_kol == kolCode);
                if (kol == null)
                    return Ok(new { Code = -2, Message = "حساب کل معتبر با این کد یافت نشد" });

                // ✅ چک گروه از روی Group_id خود کل (نه از کد)
                using var groupTenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id, request.Fiscal_year);
                var group = await groupTenant.AccountGroups
                    .FirstOrDefaultAsync(g => g.id_Group == kol.Group_id && g.DeletedAt == null);
                if (group == null)
                    return Ok(new { Code = -2, Message = "گروه معتبر برای این کل یافت نشد" });

                using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    request.Company_id, request.Fiscal_year);

                var existing = await tenant.AccountMoeins
                    .FirstOrDefaultAsync(m => m.Kol_id == kol.id_kol &&
                                               m.Code_moein == request.Code_moein &&
                                               m.Company_id == request.Company_id &&
                                               m.Fiscal_year == request.Fiscal_year);
                if (existing != null)
                    return Ok(new { Code = -2, Message = "این کد معین قبلاً در این کل ثبت شده است" });

                var lastMoeinId = await tenant.AccountMoeins
                    .Where(m => m.Company_id == request.Company_id && m.Fiscal_year == request.Fiscal_year)
                    .OrderByDescending(m => m.id_moein)
                    .Select(m => m.id_moein)
                    .FirstOrDefaultAsync();

                var moein = new AccountMoein
                {
                    Company_id = request.Company_id,
                    Fiscal_year = request.Fiscal_year,
                    Kol_id = kol.id_kol,
                    Code_moein = request.Code_moein,
                    Name_moein = request.Name_moein,
                    CreatedByUserId = userId.Value
                };
                tenant.AccountMoeins.Add(moein);
                await tenant.SaveChangesAsync();

                tenant.AccountMoeinHistories.Add(new AccountMoeinHistory
                {
                    OriginalId = moein.id_moein,
                    Company_id = moein.Company_id,
                    Fiscal_year = moein.Fiscal_year,
                    Kol_id = moein.Kol_id,
                    Code_moein = moein.Code_moein,
                    Name_moein = moein.Name_moein,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب معین با موفقیت ایجاد شد",
                    Data = new
                    {
                        moein.id_moein,
                        moein.Company_id,
                        moein.Fiscal_year,
                        moein.Code_moein,
                        moein.Name_moein,
                        LastMoeinId = lastMoeinId
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
        public async Task<IActionResult> SearchMoeins([FromBody] SearchMoeinRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    request.Company_id.Value, request.Fiscal_year.Value);

                var baseQuery = tenant.AccountMoeins
                    .Where(m => m.Company_id == request.Company_id.Value
                             && m.Fiscal_year == request.Fiscal_year.Value);

                if (request.Code_moein.HasValue && request.Code_moein.Value > 0)
                    baseQuery = baseQuery.Where(m => m.Code_moein == request.Code_moein.Value);

                if (!string.IsNullOrEmpty(request.Name_moein) && request.Name_moein != "string")
                    baseQuery = baseQuery.Where(m => m.Name_moein.Contains(request.Name_moein));

                var orderedQuery = baseQuery;
                switch (request.OrderBy?.ToLower())
                {
                    case "min":
                        orderedQuery = baseQuery.OrderBy(m => m.id_moein);
                        break;
                    case "max":
                        orderedQuery = baseQuery.OrderByDescending(m => m.id_moein);
                        break;
                    case "name_asc":
                        orderedQuery = baseQuery.OrderBy(m => m.Name_moein);
                        break;
                    case "name_desc":
                        orderedQuery = baseQuery.OrderByDescending(m => m.Name_moein);
                        break;
                    case "code_asc":
                        orderedQuery = baseQuery.OrderBy(m => m.Code_moein);
                        break;
                    case "code_desc":
                        orderedQuery = baseQuery.OrderByDescending(m => m.Code_moein);
                        break;
                    default:
                        orderedQuery = baseQuery.OrderByDescending(m => m.id_moein);
                        break;
                }

                var totalCount = await baseQuery.CountAsync();

                var moeins = await orderedQuery
                    .Take(limit)
                    .ToListAsync();

                var result = moeins.Select(m => new
                {
                    m.id_moein,
                    m.Company_id,
                    m.Fiscal_year,
                    m.Code_moein,
                    m.Name_moein
                }).ToList();

                var returnedCount = result.Count;
                var lastId = result.Any() ? result.Last().id_moein : (int?)null;
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
        public async Task<IActionResult> UpdateMoein([FromBody] UpdateMoeinRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه معین معتبر نیست" });

                var location = await FindMoeinTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب معین مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var moein = await tenant.AccountMoeins.FindAsync(request.Id);
                if (moein == null)
                    return Ok(new { Code = -2, Message = "حساب معین مورد نظر یافت نشد" });

                bool hasCode = request.Code_moein.HasValue && request.Code_moein.Value > 0;
                bool hasName = !string.IsNullOrEmpty(request.Name_moein);

                if (!hasCode && !hasName)
                    return Ok(new { Code = -1, Message = "هیچ فیلدی برای ویرایش ارسال نشده است" });

                tenant.AccountMoeinHistories.Add(new AccountMoeinHistory
                {
                    OriginalId = moein.id_moein,
                    Company_id = moein.Company_id,
                    Fiscal_year = moein.Fiscal_year,
                    Kol_id = moein.Kol_id,
                    Code_moein = moein.Code_moein,
                    Name_moein = moein.Name_moein,
                    ActionType = "Update",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                if (hasCode)
                {
                    if (!AccountingHelper.IsValidMoeinCode(request.Code_moein!.Value))
                        return Ok(new { Code = -3, Message = "کد معین باید یک عدد ۶ رقمی باشد" });

                    if (request.Code_moein.Value != moein.Code_moein)
                    {
                        var newKolCode = request.Code_moein.Value / 1000;

                        using var kolTenant = await _tenantFactory.CreateAccountKolContextAsync(
                            moein.Company_id, moein.Fiscal_year);
                        var newKol = await kolTenant.AccountKols
                            .FirstOrDefaultAsync(k => k.Company_id == moein.Company_id &&
                                                      k.Fiscal_year == moein.Fiscal_year &&
                                                      k.Code_kol == newKolCode);
                        if (newKol == null)
                            return Ok(new { Code = -2, Message = "حساب کل معتبر با این کد یافت نشد" });

                        var existing = await tenant.AccountMoeins
                            .FirstOrDefaultAsync(m => m.Kol_id == newKol.id_kol &&
                                                      m.Code_moein == request.Code_moein.Value &&
                                                      m.id_moein != request.Id);
                        if (existing != null)
                            return Ok(new { Code = -2, Message = "این کد معین قبلاً در این کل ثبت شده است" });

                        moein.Kol_id = newKol.id_kol;
                        moein.Code_moein = request.Code_moein.Value;
                    }
                }

                if (hasName)
                    moein.Name_moein = request.Name_moein!;

                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب معین با موفقیت ویرایش شد",
                    Data = new
                    {
                        moein.id_moein,
                        moein.Company_id,
                        moein.Fiscal_year,
                        moein.Code_moein,
                        moein.Name_moein
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
        public async Task<IActionResult> DeleteMoein([FromBody] DeleteMoeinRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    request.Company_id, request.Fiscal_year);

                var moein = await tenant.AccountMoeins
                    .FirstOrDefaultAsync(m => m.id_moein == request.Id &&
                                               m.Company_id == request.Company_id &&
                                               m.Fiscal_year == request.Fiscal_year);
                if (moein == null)
                    return Ok(new { Code = -2, Message = "حساب معین مورد نظر در این شرکت و سال مالی یافت نشد" });

                using var tafziliTenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id, request.Fiscal_year);

                var hasTafzilis = await tafziliTenant.AccountTafzilis
                    .AnyAsync(t => t.Moein_id == moein.id_moein);

                if (hasTafzilis)
                {
                    var tafziliCount = await tafziliTenant.AccountTafzilis
                        .CountAsync(t => t.Moein_id == moein.id_moein);

                    return Ok(new
                    {
                        Code = -3,
                        Message = $"این حساب معین دارای {tafziliCount} حساب تفصیلی زیرمجموعه است و قابل حذف نیست. ابتدا زیرمجموعه‌ها را حذف کنید.",
                        SubAccountType = "حساب تفصیلی",
                        SubAccountCount = tafziliCount
                    });
                }

                tenant.AccountMoeinHistories.Add(new AccountMoeinHistory
                {
                    OriginalId = moein.id_moein,
                    Company_id = moein.Company_id,
                    Fiscal_year = moein.Fiscal_year,
                    Kol_id = moein.Kol_id,
                    Code_moein = moein.Code_moein,
                    Name_moein = moein.Name_moein,
                    ActionType = "Delete",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                tenant.AccountMoeins.Remove(moein);
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب معین با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
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
        public async Task<IActionResult> GetHistory([FromBody] GetMoeinHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindMoeinTenantAsync(request.OriginalId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب معین مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var raw = await tenant.AccountMoeinHistories
                    .Where(h => h.OriginalId == request.OriginalId)
                    .OrderByDescending(h => h.ActionDate)
                    .ToListAsync();

                var histories = raw.Select(h => new
                {
                    h.Id,
                    h.OriginalId,
                    h.Company_id,
                    h.Fiscal_year,
                    h.Code_moein,
                    h.Name_moein,
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
    }

    public class CreateMoeinRequest
    {
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Code_moein { get; set; }
        public string Name_moein { get; set; }
        public string Token { get; set; }
    }

    public class SearchMoeinRequest
    {
        public int? Company_id { get; set; }
        public int? Fiscal_year { get; set; }
        public int? Code_moein { get; set; }
        public string? Name_moein { get; set; }
        public string? OrderBy { get; set; }
        public string Token { get; set; }
    }
    public class UpdateMoeinRequest
    {
        public int Id { get; set; }
        public int? Code_moein { get; set; }
        public string? Name_moein { get; set; }
        public string? Token { get; set; }
    }

    public class DeleteMoeinRequest
    {
        public int Id { get; set; }
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public string Token { get; set; }
    }

    public class GetMoeinHistoryRequest
    {
        public int OriginalId { get; set; }
        public string Token { get; set; }
    }
}