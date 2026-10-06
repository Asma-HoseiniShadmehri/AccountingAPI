using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Models.Enums;
using AccountingAPI.Helpers;
using AccountingAPI.Services;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/group")]
    [ApiController]
    public class AccountGroupController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContextFactory _tenantFactory;

        public AccountGroupController(AppDbContext context, ITenantContextFactory tenantFactory)
        {
            _context = context;
            _tenantFactory = tenantFactory;
        }

        private async Task<int?> GetUserIdFromTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
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

        private async Task<(int companyId, int fiscalYear)?> FindGroupTenantAsync(int groupId)
        {
            var databases = await _context.Databases
                .Where(d => d.Level == "Combined" && d.DeleteDate == null && d.Number != null)
                .ToListAsync();

            foreach (var db in databases)
            {
                if (!int.TryParse(db.Number, out int fy)) continue;
                try
                {
                    using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(db.CompanyId, fy);
                    if (await tenant.AccountGroups.AnyAsync(g => g.id_Group == groupId))
                        return (db.CompanyId, fy);
                }
                catch { }
            }
            return null;
        }
        // ============================================================
        //  CREATE
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
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
                if (!AccountingHelper.IsValidGroupCode(request.Code_Group))
                    return Ok(new { Code = -3, Message = "کد گروه باید یک رقم بین ۰ تا ۹ باشد" });
                if (string.IsNullOrEmpty(request.Name_Group))
                    return Ok(new { Code = -1, Message = "نام گروه الزامی است" });
                if (!Enum.IsDefined(typeof(NatureType), request.Nature_Group))
                    return Ok(new { Code = -3, Message = "ماهیت حساب نامعتبر است (۱ تا ۵)" });
                if (!Enum.IsDefined(typeof(GroupType), request.type_Group))
                    return Ok(new { Code = -3, Message = "نوع گروه نامعتبر است (۱ یا ۲)" });

                var company = await _context.Companies.FindAsync(request.Company_id);
                if (company == null)
                    return Ok(new { Code = -2, Message = "شرکت مورد نظر یافت نشد" });

                using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id, request.Fiscal_year);

                var existing = await tenant.AccountGroups
                    .FirstOrDefaultAsync(g => g.Company_id == request.Company_id &&
                                               g.Fiscal_year == request.Fiscal_year &&
                                               g.Code_Group == request.Code_Group &&
                                               g.DeletedAt == null);
                if (existing != null)
                    return Ok(new { Code = -2, Message = "این کد گروه قبلاً در این شرکت و سال مالی ثبت شده است" });

                var lastGroupId = await tenant.AccountGroups
                    .Where(g => g.Company_id == request.Company_id && g.Fiscal_year == request.Fiscal_year)
                    .OrderByDescending(g => g.id_Group)
                    .Select(g => g.id_Group)
                    .FirstOrDefaultAsync();

                var group = new AccountGroup
                {
                    Company_id = request.Company_id,
                    Fiscal_year = request.Fiscal_year,
                    Code_Group = request.Code_Group,
                    Name_Group = request.Name_Group,
                    Nature_Group = request.Nature_Group,
                    type_Group = request.type_Group,
                    DeletedAt = null,
                    CreatedByUserId = userId.Value
                };
                tenant.AccountGroups.Add(group);
                await tenant.SaveChangesAsync();

                tenant.AccountGroupHistories.Add(new AccountGroupHistory
                {
                    OriginalId = group.id_Group,
                    Company_id = group.Company_id,
                    Fiscal_year = group.Fiscal_year,
                    Code_Group = group.Code_Group,
                    Name_Group = group.Name_Group,
                    Nature_Group = group.Nature_Group,
                    type_Group = group.type_Group,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "گروه حساب با موفقیت ایجاد شد",
                    Data = new
                    {
                        group.id_Group,
                        group.Company_id,
                        group.Fiscal_year,
                        group.Code_Group,
                        group.Name_Group,
                        Nature_Group = Enum.GetName(typeof(NatureType), group.Nature_Group),
                        type_Group = Enum.GetName(typeof(GroupType), group.type_Group),
                        LastGroupId = lastGroupId
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
        public async Task<IActionResult> SearchGroups([FromBody] SearchGroupRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id.Value, request.Fiscal_year.Value);

                var baseQuery = tenant.AccountGroups.Where(g => g.DeletedAt == null);

                if (request.Company_id.HasValue && request.Company_id.Value > 0)
                    baseQuery = baseQuery.Where(g => g.Company_id == request.Company_id.Value);

                if (request.Fiscal_year.HasValue && request.Fiscal_year.Value > 0)
                    baseQuery = baseQuery.Where(g => g.Fiscal_year == request.Fiscal_year.Value);

                if (request.Code_Group.HasValue && request.Code_Group.Value > 0)
                    baseQuery = baseQuery.Where(g => g.Code_Group == request.Code_Group.Value);

                if (!string.IsNullOrEmpty(request.Name_Group) && request.Name_Group != "string")
                    baseQuery = baseQuery.Where(g => g.Name_Group.Contains(request.Name_Group));

                if (request.Nature_Group.HasValue && request.Nature_Group.Value > 0)
                    baseQuery = baseQuery.Where(g => g.Nature_Group == request.Nature_Group.Value);

                if (request.type_Group.HasValue && request.type_Group.Value > 0)
                    baseQuery = baseQuery.Where(g => g.type_Group == request.type_Group.Value);

                var orderedQuery = baseQuery;
                switch (request.OrderBy?.ToLower())
                {
                    case "min":
                        orderedQuery = baseQuery.OrderBy(g => g.id_Group);
                        break;
                    case "max":
                        orderedQuery = baseQuery.OrderByDescending(g => g.id_Group);
                        break;
                    case "name_asc":
                        orderedQuery = baseQuery.OrderBy(g => g.Name_Group);
                        break;
                    case "name_desc":
                        orderedQuery = baseQuery.OrderByDescending(g => g.Name_Group);
                        break;
                    case "code_asc":
                        orderedQuery = baseQuery.OrderBy(g => g.Code_Group);
                        break;
                    case "code_desc":
                        orderedQuery = baseQuery.OrderByDescending(g => g.Code_Group);
                        break;
                    default:
                        orderedQuery = baseQuery.OrderByDescending(g => g.id_Group);
                        break;
                }

                var totalCount = await baseQuery.CountAsync();

                var rawGroups = await orderedQuery
                    .Take(limit)
                    .Select(g => new
                    {
                        g.id_Group,
                        g.Company_id,
                        g.Fiscal_year,
                        g.Code_Group,
                        g.Name_Group,
                        g.Nature_Group,
                        g.type_Group
                    })
                    .ToListAsync();

                var groups = rawGroups.Select(g => new
                {
                    g.id_Group,
                    g.Company_id,
                    g.Fiscal_year,
                    g.Code_Group,
                    g.Name_Group,
                    Nature_Group = Enum.GetName(typeof(NatureType), g.Nature_Group),
                    type_Group = Enum.GetName(typeof(GroupType), g.type_Group)
                }).ToList();

                var returnedCount = groups.Count;
                var lastId = groups.Any() ? groups.Last().id_Group : (int?)null;
                var hasMore = totalCount > returnedCount;

                return Ok(new
                {
                    Code = 1,
                    Data = groups,
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
        public async Task<IActionResult> UpdateGroup([FromBody] UpdateGroupRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه گروه معتبر نیست" });

                var location = await FindGroupTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "گروه مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var group = await tenant.AccountGroups.FindAsync(request.Id);
                if (group == null)
                    return Ok(new { Code = -2, Message = "گروه مورد نظر یافت نشد" });

                bool hasCode = request.Code_Group.HasValue && request.Code_Group.Value > 0;
                bool hasName = !string.IsNullOrEmpty(request.Name_Group);
                bool hasNature = request.Nature_Group.HasValue && request.Nature_Group.Value > 0;
                bool hasType = request.type_Group.HasValue && request.type_Group.Value > 0;

                if (!hasCode && !hasName && !hasNature && !hasType)
                    return Ok(new { Code = -1, Message = "هیچ فیلدی برای ویرایش ارسال نشده است" });

                tenant.AccountGroupHistories.Add(new AccountGroupHistory
                {
                    OriginalId = group.id_Group,
                    Company_id = group.Company_id,
                    Fiscal_year = group.Fiscal_year,
                    Code_Group = group.Code_Group,
                    Name_Group = group.Name_Group,
                    Nature_Group = group.Nature_Group,
                    type_Group = group.type_Group,
                    ActionType = "Update",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                if (hasCode)
                {
                    if (!AccountingHelper.IsValidGroupCode(request.Code_Group!.Value))
                        return Ok(new { Code = -3, Message = "کد گروه باید یک رقم بین ۰ تا ۹ باشد" });

                    if (request.Code_Group.Value != group.Code_Group)
                    {
                        var existing = await tenant.AccountGroups
                            .FirstOrDefaultAsync(g => g.Company_id == group.Company_id &&
                                                      g.Fiscal_year == group.Fiscal_year &&
                                                      g.Code_Group == request.Code_Group.Value &&
                                                      g.id_Group != request.Id);
                        if (existing != null)
                            return Ok(new { Code = -2, Message = "این کد گروه قبلاً در این شرکت و سال مالی ثبت شده است" });
                        group.Code_Group = request.Code_Group.Value;
                    }
                }

                if (hasName)
                    group.Name_Group = request.Name_Group!;

                if (hasNature)
                {
                    if (!Enum.IsDefined(typeof(NatureType), request.Nature_Group!.Value))
                        return Ok(new { Code = -3, Message = "ماهیت حساب نامعتبر است (۱ تا ۵)" });
                    group.Nature_Group = request.Nature_Group.Value;
                }

                if (hasType)
                {
                    if (!Enum.IsDefined(typeof(GroupType), request.type_Group!.Value))
                        return Ok(new { Code = -3, Message = "نوع گروه نامعتبر است (۱ یا ۲)" });
                    group.type_Group = request.type_Group.Value;
                }

                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "گروه حساب با موفقیت ویرایش شد",
                    Data = new
                    {
                        group.id_Group,
                        group.Company_id,
                        group.Fiscal_year,
                        group.Code_Group,
                        group.Name_Group,
                        Nature_Group = Enum.GetName(typeof(NatureType), group.Nature_Group),
                        type_Group = Enum.GetName(typeof(GroupType), group.type_Group)
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
        public async Task<IActionResult> DeleteGroup([FromBody] DeleteGroupRequest request)
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

                using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    request.Company_id, request.Fiscal_year);

                var group = await tenant.AccountGroups
                    .FirstOrDefaultAsync(g => g.id_Group == request.Id &&
                                               g.Company_id == request.Company_id &&
                                               g.Fiscal_year == request.Fiscal_year);
                if (group == null)
                    return Ok(new { Code = -2, Message = "گروه مورد نظر در این شرکت و سال مالی یافت نشد" });

                if (group.DeletedAt != null)
                    return Ok(new { Code = -2, Message = "این گروه قبلاً حذف شده است" });

                using var kolTenant = await _tenantFactory.CreateAccountKolContextAsync(
                    request.Company_id, request.Fiscal_year);

                var hasKols = await kolTenant.AccountKols
                    .AnyAsync(k => k.Group_id == group.id_Group);

                if (hasKols)
                {
                    var kolCount = await kolTenant.AccountKols
                        .CountAsync(k => k.Group_id == group.id_Group);

                    return Ok(new
                    {
                        Code = -3,
                        Message = $"این گروه دارای {kolCount} حساب کل زیرمجموعه است و قابل حذف نیست. ابتدا زیرمجموعه‌ها را حذف کنید.",
                        SubAccountType = "حساب کل",
                        SubAccountCount = kolCount
                    });
                }

                tenant.AccountGroupHistories.Add(new AccountGroupHistory
                {
                    OriginalId = group.id_Group,
                    Company_id = group.Company_id,
                    Fiscal_year = group.Fiscal_year,
                    Code_Group = group.Code_Group,
                    Name_Group = group.Name_Group,
                    Nature_Group = group.Nature_Group,
                    type_Group = group.type_Group,
                    ActionType = "Delete",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                group.DeletedAt = DateTime.Now;
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "گروه حساب با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }
        // ============================================================
        //  TYPES
        // ============================================================
        [HttpPost("types")]
        public async Task<IActionResult> GetGroupTypes([FromBody] GetTypesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var natureTypes = Enum.GetValues(typeof(NatureType))
                    .Cast<NatureType>()
                    .Select(n => new { Id = (int)n, Name = GetNatureTypeName((int)n) })
                    .ToList();

                var groupTypes = Enum.GetValues(typeof(GroupType))
                    .Cast<GroupType>()
                    .Select(g => new { Id = (int)g, Name = GetGroupTypeName((int)g) })
                    .ToList();

                return Ok(new
                {
                    Code = 1,
                    Data = new { NatureTypes = natureTypes, GroupTypes = groupTypes }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        private string GetNatureTypeName(int nature)
        {
            return nature switch
            {
                1 => "بدهکار",
                2 => "بستانکار",
                3 => "اکیدا بدهکار",
                4 => "اکیدا بستانکار",
                5 => "هم بستانکار-بدهکار",
                _ => "نامشخص"
            };
        }

        private string GetGroupTypeName(int type)
        {
            return type switch
            {
                1 => "دایم",
                2 => "موقت",
                _ => "نامشخص"
            };
        }
        // ============================================================
        //  History
        // ============================================================
        [HttpPost("history")]
        public async Task<IActionResult> GetGroupHistory([FromBody] GetHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindGroupTenantAsync(request.OriginalId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "گروه مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountGroupContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var raw = await tenant.AccountGroupHistories
                    .Where(h => h.OriginalId == request.OriginalId)
                    .OrderByDescending(h => h.ActionDate)
                    .ToListAsync();

                var histories = raw.Select(h => new
                {
                    h.Id,
                    h.OriginalId,
                    h.Company_id,
                    h.Fiscal_year,
                    h.Code_Group,
                    h.Name_Group,
                    Nature_Group = Enum.GetName(typeof(NatureType), h.Nature_Group),
                    type_Group = Enum.GetName(typeof(GroupType), h.type_Group),
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

        public class CreateGroupRequest
        {
            public int Company_id { get; set; }
            public int Fiscal_year { get; set; }
            public int Code_Group { get; set; }
            public string Name_Group { get; set; }
            public int Nature_Group { get; set; }
            public int type_Group { get; set; }
            public string Token { get; set; }
        }

        public class SearchGroupRequest
        {
            public int? Company_id { get; set; }
            public int? Fiscal_year { get; set; }
            public int? Code_Group { get; set; }
            public string? Name_Group { get; set; }
            public int? Nature_Group { get; set; }
            public int? type_Group { get; set; }
            public string? OrderBy { get; set; }
            public string Token { get; set; }
        }

        public class UpdateGroupRequest
        {
            public int Id { get; set; }
            public int? Code_Group { get; set; }
            public string? Name_Group { get; set; }
            public int? Nature_Group { get; set; }
            public int? type_Group { get; set; }
            public string? Token { get; set; }
        }

        public class DeleteGroupRequest
        {
            public int Id { get; set; }
            public int Company_id { get; set; }
            public int Fiscal_year { get; set; }
            public string Token { get; set; }
        }

        public class GetTypesRequest
        {
            public string Token { get; set; }
        }

        public class GetHistoryRequest
        {
            public int OriginalId { get; set; }
            public string Token { get; set; }
        }
    }
}