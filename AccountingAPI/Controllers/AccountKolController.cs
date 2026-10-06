using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Helpers;
using AccountingAPI.Services;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/kol")]
    [ApiController]
    public class AccountKolController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContextFactory _tenantFactory;

        public AccountKolController(AppDbContext context, ITenantContextFactory tenantFactory)
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

        private async Task<(int companyId, int fiscalYear)?> FindKolTenantAsync(int kolId)
        {
            var databases = await _context.Databases
                .Where(d => d.Level == "Combined" && d.DeleteDate == null && d.Number != null)
                .ToListAsync();

            foreach (var db in databases)
            {
                if (!int.TryParse(db.Number, out int fy)) continue;
                try
                {
                    using var tenant = await _tenantFactory.CreateAccountKolContextAsync(db.CompanyId, fy);
                    if (await tenant.AccountKols.AnyAsync(k => k.id_kol == kolId))
                        return (db.CompanyId, fy);
                }
                catch { }
            }
            return null;
        }

        // ============================================================
        //  CREATE — ✅ اصلاح‌شده: پیدا کردن گروه حتی اگر کد گروه تغییر کرده باشد
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateKol([FromBody] CreateKolRequest request)
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
                if (!AccountingHelper.IsValidKolCode(request.Code_kol))
                    return Ok(new { Code = -3, Message = "کد کل باید یک عدد ۳ رقمی (۰ تا ۹۹۹) باشد" });
                if (string.IsNullOrEmpty(request.Name_kol))
                    return Ok(new { Code = -1, Message = "نام کل الزامی است" });

                // ✅ کد گروه = رقم اول کد کل
                var groupCode = request.Code_kol / 100;

                // ✅ گرفتن هر دو کانتکست
                using var groupTenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id, request.Fiscal_year);

                using var tenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id, request.Fiscal_year);

                // ============================================================
                //  ✅ مرحله ۱: تلاش برای پیدا کردن گروه با Code_Group
                // ============================================================
                var group = await groupTenant.AccountGroups
                    .FirstOrDefaultAsync(g => g.Company_id == request.Company_id &&
                                               g.Fiscal_year == request.Fiscal_year &&
                                               g.Code_Group == groupCode &&
                                               g.DeletedAt == null);

                // ============================================================
                //  ✅ مرحله ۲: اگر پیدا نشد، از روی Group_id کل‌های موجود پیدا کن
                //  (برای وقتی که کد گروه توسط کاربر تغییر کرده است)
                // ============================================================
                if (group == null)
                {
                    // کل‌های موجود با همان پیشوند (100-199 برای groupCode=1)
                    var existingGroupId = await tenant.AccountKols
                        .Where(k => k.Company_id == request.Company_id &&
                                    k.Fiscal_year == request.Fiscal_year &&
                                    k.Code_kol / 100 == groupCode)
                        .Select(k => (int?)k.Group_id)
                        .FirstOrDefaultAsync();

                    if (existingGroupId.HasValue)
                    {
                        group = await groupTenant.AccountGroups
                            .FirstOrDefaultAsync(g => g.Company_id == request.Company_id &&
                                                       g.Fiscal_year == request.Fiscal_year &&
                                                       g.id_Group == existingGroupId.Value &&
                                                       g.DeletedAt == null);
                    }
                }

                // ============================================================
                //  ✅ مرحله ۳: اگر هنوز پیدا نشد، خطا برگردان
                // ============================================================
                if (group == null)
                    return Ok(new { Code = -2, Message = "گروه معتبر با این کد یافت نشد" });

                // ============================================================
                //  ادامه منطق قبلی (بدون تغییر)
                // ============================================================
                var existing = await tenant.AccountKols
                    .FirstOrDefaultAsync(k => k.Group_id == group.id_Group &&
                                               k.Code_kol == request.Code_kol &&
                                               k.Company_id == request.Company_id &&
                                               k.Fiscal_year == request.Fiscal_year);
                if (existing != null)
                    return Ok(new { Code = -2, Message = "این کد کل قبلاً در این گروه ثبت شده است" });

                var lastKolId = await tenant.AccountKols
                    .Where(k => k.Company_id == request.Company_id && k.Fiscal_year == request.Fiscal_year)
                    .OrderByDescending(k => k.id_kol)
                    .Select(k => k.id_kol)
                    .FirstOrDefaultAsync();

                var kol = new AccountKol
                {
                    Company_id = request.Company_id,
                    Fiscal_year = request.Fiscal_year,
                    Group_id = group.id_Group,
                    Code_kol = request.Code_kol,
                    Name_kol = request.Name_kol,
                    CreatedByUserId = userId.Value
                };
                tenant.AccountKols.Add(kol);
                await tenant.SaveChangesAsync();

                tenant.AccountKolHistories.Add(new AccountKolHistory
                {
                    OriginalId = kol.id_kol,
                    Company_id = kol.Company_id,
                    Fiscal_year = kol.Fiscal_year,
                    Group_id = kol.Group_id,
                    Code_kol = kol.Code_kol,
                    Name_kol = kol.Name_kol,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب کل با موفقیت ایجاد شد",
                    Data = new
                    {
                        kol.id_kol,
                        kol.Company_id,
                        kol.Fiscal_year,
                        kol.Code_kol,
                        kol.Name_kol,
                        LastKolId = lastKolId
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
        public async Task<IActionResult> SearchKols([FromBody] SearchKolRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id.Value, request.Fiscal_year.Value);

                var baseQuery = tenant.AccountKols
                    .Where(k => k.Company_id == request.Company_id.Value
                             && k.Fiscal_year == request.Fiscal_year.Value);

                if (request.Code_kol.HasValue && request.Code_kol.Value > 0)
                    baseQuery = baseQuery.Where(k => k.Code_kol == request.Code_kol.Value);

                if (!string.IsNullOrEmpty(request.Name_kol) && request.Name_kol != "string")
                    baseQuery = baseQuery.Where(k => k.Name_kol.Contains(request.Name_kol));

                var orderedQuery = baseQuery;
                switch (request.OrderBy?.ToLower())
                {
                    case "min":
                        orderedQuery = baseQuery.OrderBy(k => k.id_kol);
                        break;
                    case "max":
                        orderedQuery = baseQuery.OrderByDescending(k => k.id_kol);
                        break;
                    case "name_asc":
                        orderedQuery = baseQuery.OrderBy(k => k.Name_kol);
                        break;
                    case "name_desc":
                        orderedQuery = baseQuery.OrderByDescending(k => k.Name_kol);
                        break;
                    case "code_asc":
                        orderedQuery = baseQuery.OrderBy(k => k.Code_kol);
                        break;
                    case "code_desc":
                        orderedQuery = baseQuery.OrderByDescending(k => k.Code_kol);
                        break;
                    default:
                        orderedQuery = baseQuery.OrderByDescending(k => k.id_kol);
                        break;
                }

                var totalCount = await baseQuery.CountAsync();

                var rawKols = await orderedQuery
                    .Take(limit)
                    .ToListAsync();

                var kols = rawKols.Select(k => new
                {
                    k.id_kol,
                    k.Company_id,
                    k.Fiscal_year,
                    k.Code_kol,
                    k.Name_kol
                }).ToList();

                var returnedCount = kols.Count;
                var lastId = kols.Any() ? kols.Last().id_kol : (int?)null;
                var hasMore = totalCount > returnedCount;

                return Ok(new
                {
                    Code = 1,
                    Data = kols,
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
        public async Task<IActionResult> UpdateKol([FromBody] UpdateKolRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه کل معتبر نیست" });

                var location = await FindKolTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب کل مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountKolContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var kol = await tenant.AccountKols.FindAsync(request.Id);
                if (kol == null)
                    return Ok(new { Code = -2, Message = "حساب کل مورد نظر یافت نشد" });

                bool hasCode = request.Code_kol.HasValue && request.Code_kol.Value > 0;
                bool hasName = !string.IsNullOrEmpty(request.Name_kol);

                if (!hasCode && !hasName)
                    return Ok(new { Code = -1, Message = "هیچ فیلدی برای ویرایش ارسال نشده است" });

                tenant.AccountKolHistories.Add(new AccountKolHistory
                {
                    OriginalId = kol.id_kol,
                    Company_id = kol.Company_id,
                    Fiscal_year = kol.Fiscal_year,
                    Group_id = kol.Group_id,
                    Code_kol = kol.Code_kol,
                    Name_kol = kol.Name_kol,
                    ActionType = "Update",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                if (hasCode)
                {
                    if (!AccountingHelper.IsValidKolCode(request.Code_kol!.Value))
                        return Ok(new { Code = -3, Message = "کد کل باید یک عدد ۳ رقمی باشد" });

                    if (request.Code_kol.Value != kol.Code_kol)
                    {
                        var newGroupCode = request.Code_kol.Value / 100;

                        using var groupTenant = await _tenantFactory.CreateAccountGroupContextAsync(
                            kol.Company_id, kol.Fiscal_year);
                        var newGroup = await groupTenant.AccountGroups
                            .FirstOrDefaultAsync(g => g.Company_id == kol.Company_id &&
                                                      g.Fiscal_year == kol.Fiscal_year &&
                                                      g.Code_Group == newGroupCode);
                        if (newGroup == null)
                            return Ok(new { Code = -2, Message = "گروه معتبر با این کد یافت نشد" });

                        var existing = await tenant.AccountKols
                            .FirstOrDefaultAsync(k => k.Group_id == newGroup.id_Group &&
                                                      k.Code_kol == request.Code_kol.Value &&
                                                      k.id_kol != request.Id);
                        if (existing != null)
                            return Ok(new { Code = -2, Message = "این کد کل قبلاً در این گروه ثبت شده است" });

                        kol.Group_id = newGroup.id_Group;
                        kol.Code_kol = request.Code_kol.Value;
                    }
                }

                if (hasName)
                    kol.Name_kol = request.Name_kol!;

                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب کل با موفقیت ویرایش شد",
                    Data = new
                    {
                        kol.id_kol,
                        kol.Company_id,
                        kol.Fiscal_year,
                        kol.Code_kol,
                        kol.Name_kol
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
        public async Task<IActionResult> DeleteKol([FromBody] DeleteKolRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id, request.Fiscal_year);

                var kol = await tenant.AccountKols
                    .FirstOrDefaultAsync(k => k.id_kol == request.Id &&
                                               k.Company_id == request.Company_id &&
                                               k.Fiscal_year == request.Fiscal_year);
                if (kol == null)
                    return Ok(new { Code = -2, Message = "حساب کل مورد نظر در این شرکت و سال مالی یافت نشد" });

                using var moeinTenant = await _tenantFactory.CreateAccountMoeinContextAsync(
                    request.Company_id, request.Fiscal_year);

                var hasMoeins = await moeinTenant.AccountMoeins
                    .AnyAsync(m => m.Kol_id == kol.id_kol);

                if (hasMoeins)
                {
                    var moeinCount = await moeinTenant.AccountMoeins
                        .CountAsync(m => m.Kol_id == kol.id_kol);

                    return Ok(new
                    {
                        Code = -3,
                        Message = $"این حساب کل دارای {moeinCount} حساب معین زیرمجموعه است و قابل حذف نیست. ابتدا زیرمجموعه‌ها را حذف کنید.",
                        SubAccountType = "حساب معین",
                        SubAccountCount = moeinCount
                    });
                }

                tenant.AccountKolHistories.Add(new AccountKolHistory
                {
                    OriginalId = kol.id_kol,
                    Company_id = kol.Company_id,
                    Fiscal_year = kol.Fiscal_year,
                    Group_id = kol.Group_id,
                    Code_kol = kol.Code_kol,
                    Name_kol = kol.Name_kol,
                    ActionType = "Delete",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                tenant.AccountKols.Remove(kol);
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "حساب کل با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
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
        public async Task<IActionResult> GetHistory([FromBody] GetKolHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindKolTenantAsync(request.OriginalId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "حساب کل مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountKolContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var raw = await tenant.AccountKolHistories
                    .Where(h => h.OriginalId == request.OriginalId)
                    .OrderByDescending(h => h.ActionDate)
                    .ToListAsync();

                var histories = raw.Select(h => new
                {
                    h.Id,
                    h.OriginalId,
                    h.Company_id,
                    h.Fiscal_year,
                    h.Code_kol,
                    h.Name_kol,
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

    public class CreateKolRequest
    {
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public int Code_kol { get; set; }
        public string Name_kol { get; set; }
        public string Token { get; set; }
    }

    public class SearchKolRequest
    {
        public int? Company_id { get; set; }
        public int? Fiscal_year { get; set; }
        public int? Code_kol { get; set; }
        public string? Name_kol { get; set; }
        public string? OrderBy { get; set; }
        public string Token { get; set; }
    }
    public class UpdateKolRequest
    {
        public int Id { get; set; }
        public int? Code_kol { get; set; }
        public string? Name_kol { get; set; }
        public string? Token { get; set; }
    }

    public class DeleteKolRequest
    {
        public int Id { get; set; }
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public string Token { get; set; }
    }

    public class GetKolHistoryRequest
    {
        public int OriginalId { get; set; }
        public string Token { get; set; }
    }
}