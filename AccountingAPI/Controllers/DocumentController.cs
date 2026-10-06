using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingAPI.Data;
using AccountingAPI.Models;
using AccountingAPI.Helpers;
using AccountingAPI.Services;

namespace AccountingAPI.Controllers
{
    [Route("api/accounting/document")]
    [ApiController]
    public class DocumentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContextFactory _tenantFactory;

        public DocumentController(AppDbContext context, ITenantContextFactory tenantFactory)
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

        private async Task<(int companyId, int fiscalYear)?> FindDocumentTenantAsync(int documentId)
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
                    if (await tenant.Documents.AnyAsync(d => d.Id == documentId))
                        return (db.CompanyId, fy);
                }
                catch { }
            }
            return null;
        }

        private async Task<int> GenerateDocumentNumberAsync(int companyId, int fiscalYear)
        {
            using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(companyId, fiscalYear);

            var lastDoc = await tenant.Documents
                .Where(d => d.Company_id == companyId && d.Fiscal_year == fiscalYear)
                .OrderByDescending(d => d.DocumentNumber)
                .FirstOrDefaultAsync();

            return lastDoc == null ? 1 : lastDoc.DocumentNumber + 1;
        }

        // ============================================================
        //  ✅ جدید: پیدا کردن یا ساخت خودکار واحد پول
        //  - اگر ورودی خالی باشد → IRR (ریال)
        //  - ابتدا با Code جستجو می‌کند (مثل IRR, USD)
        //  - اگر پیدا نشد، با Name جستجو می‌کند (مثل «ریال», «دلار»)
        //  - اگر ارز استاندارد باشد و پیدا نشد → خودکار می‌سازد
        //  - اگر هیچ ارزی در دیتابیس نباشد → IRR می‌سازد
        // ============================================================
        private async Task<Currency> ResolveCurrencyAsync(string? input)
        {
            string key = string.IsNullOrEmpty(input) ? "IRR" : input.Trim();

            // ۱) جستجو با Code (بدون حساسیت به حروف)
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == key.ToUpper() && c.IsActive);
            if (currency != null) return currency;

            // ۲) جستجو با Name (فارسی)
            currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Name == key && c.IsActive);
            if (currency != null) return currency;

            // ۳) اگر ارز استاندارد است، خودکار بساز
            var standard = GetStandardCurrencyInfo(key.ToUpper());
            if (standard.HasValue)
            {
                var newCurrency = new Currency
                {
                    Code = standard.Value.Code,
                    Name = standard.Value.Name,
                    Symbol = standard.Value.Symbol,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _context.Currencies.Add(newCurrency);
                await _context.SaveChangesAsync();
                return newCurrency;
            }

            // ۴) اگر جدول خالی باشد، IRR بساز
            var anyCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsActive);
            if (anyCurrency != null) return anyCurrency;

            var irr = new Currency
            {
                Code = "IRR",
                Name = "ریال",
                Symbol = "﷼",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Currencies.Add(irr);
            await _context.SaveChangesAsync();
            return irr;
        }

        private static (string Code, string Name, string Symbol)? GetStandardCurrencyInfo(string code)
        {
            return code switch
            {
                "IRR" => ("IRR", "ریال", "﷼"),
                "IRT" => ("IRT", "تومان", "تومان"),
                "USD" => ("USD", "دلار", "$"),
                "EUR" => ("EUR", "یورو", "€"),
                "GBP" => ("GBP", "پوند", "£"),
                "AED" => ("AED", "درهم امارات", "د.إ"),
                "TRY" => ("TRY", "لیر ترکیه", "₺"),
                "CNY" => ("CNY", "یوان چین", "¥"),
                "JPY" => ("JPY", "ین ژاپن", "¥"),
                "RUB" => ("RUB", "روبل روسیه", "₽"),
                _ => null
            };
        }

        private string GetStatusName(string status)
        {
            return status switch
            {
                "Temp" => "موقت (یادداشت - تراز لازم نیست، جزو حساب‌ها نیست)",
                "Save" => "ذخیره‌شده (تراز، جزو حساب‌ها، قابل ویرایش)",
                "Lock" => "قطعی (تراز، جزو حساب‌ها، غیرقابل ویرایش و حذف)",
                _ => "نامشخص"
            };
        }

        private bool IsEditableStatus(string status) => status == "Temp" || status == "Save";
        private bool IsCountedInAccounts(string status) => status == "Save" || status == "Lock";
        private bool RequiresBalance(string status) => status == "Save" || status == "Lock";

        private static bool IsValidDocumentType(string? type)
        {
            if (string.IsNullOrEmpty(type) || type == "string")
                return false;
            return type is "Permanent" or "Temporary" or "Correction";
        }

        private static bool IsValidStatus(string? status)
        {
            if (string.IsNullOrEmpty(status) || status == "string")
                return false;
            return status is "Temp" or "Save" or "Lock";
        }

        // ============================================================
        //  CREATE
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentRequest request)
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
                if (string.IsNullOrEmpty(request.Document_date))
                    return Ok(new { Code = -1, Message = "تاریخ سند الزامی است" });
                if (request.Rows == null || request.Rows.Count == 0)
                    return Ok(new { Code = -1, Message = "سند حداقل باید یک ردیف داشته باشد" });

                string status = string.IsNullOrEmpty(request.Status) ? "Temp" : request.Status;

                if (status != "Temp" && status != "Save" && status != "Lock")
                    return Ok(new
                    {
                        Code = -3,
                        Message = "وضعیت سند نامعتبر است. مقادیر مجاز: Temp یا Save یا Lock"
                    });

                try
                {
                    var _ = DateHelper.PersianToGregorian(request.Document_date);
                }
                catch (Exception ex)
                {
                    return Ok(new { Code = -3, Message = $"فرمت تاریخ شمسی نامعتبر است: {ex.Message}" });
                }

                var company = await _context.Companies.FindAsync(request.Company_id);
                if (company == null)
                    return Ok(new { Code = -2, Message = "شرکت مورد نظر یافت نشد" });

                var totalDebit = request.Rows.Sum(r => r.Debit);
                var totalCredit = request.Rows.Sum(r => r.Credit);

                if (RequiresBalance(status) && totalDebit != totalCredit)
                    return Ok(new
                    {
                        Code = -2,
                        Message = $"سند در وضعیت {status} باید تراز باشد (جمع بدهکار = جمع بستانکار)"
                    });

                // ✅ دریافت/ساخت خودکار ارز
                var currency = await ResolveCurrencyAsync(request.Currency);

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id, request.Fiscal_year);

                foreach (var row in request.Rows)
                {
                    var tafzili = await tenant.AccountTafzilis.FindAsync(row.Tafzili_id);
                    if (tafzili == null)
                        return Ok(new { Code = -2, Message = $"حساب تفصیلی با شناسه {row.Tafzili_id} وجود ندارد" });
                }

                var lastDocInfo = await tenant.Documents
                    .Where(d => d.Company_id == request.Company_id &&
                                d.Fiscal_year == request.Fiscal_year &&
                                d.DeletedAt == null)
                    .OrderByDescending(d => d.DocumentNumber)
                    .Select(d => new { d.Id, d.DocumentNumber })
                    .FirstOrDefaultAsync();

                var lastDocNumber = lastDocInfo?.DocumentNumber ?? 0;
                var lastDocumentId = lastDocInfo?.Id ?? 0;

                var docNumber = await GenerateDocumentNumberAsync(request.Company_id, request.Fiscal_year);

                var document = new Document
                {
                    Company_id = request.Company_id,
                    Fiscal_year = request.Fiscal_year,
                    DocumentNumber = docNumber,
                    DocumentDate = request.Document_date,
                    Description = request.Description,
                    DocumentType = request.Document_type ?? "Permanent",
                    IsBalanced = (totalDebit == totalCredit),
                    Status = status,
                    CurrencyId = currency.Id,
                    CreatedAt = DateTime.Now,
                    DeletedAt = null,
                    CreatedByUserId = userId.Value,
                    OriginalDocumentId = null
                };
                tenant.Documents.Add(document);
                await tenant.SaveChangesAsync();

                int rowNumber = 1;
                foreach (var row in request.Rows)
                {
                    var docRow = new DocumentRow
                    {
                        DocumentId = document.Id,
                        tafzili_id = row.Tafzili_id,
                        Debit = row.Debit,
                        Credit = row.Credit,
                        RowDescription = row.Row_description ?? "",
                        RowNumber = rowNumber++
                    };
                    tenant.DocumentRows.Add(docRow);
                }
                await tenant.SaveChangesAsync();

                tenant.DocumentHistories.Add(new DocumentHistory
                {
                    OriginalId = document.Id,
                    Company_id = document.Company_id,
                    Fiscal_year = document.Fiscal_year,
                    DocumentNumber = document.DocumentNumber,
                    DocumentDate = document.DocumentDate,
                    Description = document.Description,
                    DocumentType = document.DocumentType,
                    IsBalanced = document.IsBalanced,
                    Status = document.Status,
                    CreatedAt = document.CreatedAt,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "سند با موفقیت ثبت شد",
                    Data = new
                    {
                        DocumentId = document.Id,
                        DocumentNumber = document.DocumentNumber,
                        LastDocumentId = lastDocumentId,
                        LastDocumentNumber = lastDocNumber == 0 ? "اولین سند" : lastDocNumber.ToString(),
                        DocumentDate = document.DocumentDate,
                        Status = document.Status,
                        StatusName = GetStatusName(document.Status),
                        IsEditable = IsEditableStatus(document.Status),
                        IsCountedInAccounts = IsCountedInAccounts(document.Status),
                        RequiresBalance = RequiresBalance(document.Status),
                        TotalDebit = totalDebit,
                        TotalCredit = totalCredit,
                        IsBalanced = document.IsBalanced,
                        Currency = currency.Name,
                        CurrencySymbol = currency.Symbol
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
        public async Task<IActionResult> SearchDocuments([FromBody] SearchDocumentRequest request)
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

                const int limit = 100;

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    request.Company_id.Value, request.Fiscal_year.Value);

                var baseQuery = tenant.Documents
                    .Where(d => d.DeletedAt == null
                             && d.Company_id == request.Company_id.Value
                             && d.Fiscal_year == request.Fiscal_year.Value);

                if (request.DocumentNumber is > 0)
                    baseQuery = baseQuery.Where(d => d.DocumentNumber == request.DocumentNumber!.Value);

                if (!string.IsNullOrEmpty(request.Document_date) && request.Document_date != "string")
                    baseQuery = baseQuery.Where(d => d.DocumentDate.Contains(request.Document_date));

                if (!string.IsNullOrEmpty(request.Description) && request.Description != "string")
                    baseQuery = baseQuery.Where(d => d.Description.Contains(request.Description));

                if (IsValidDocumentType(request.Document_type))
                    baseQuery = baseQuery.Where(d => d.DocumentType == request.Document_type);

                if (IsValidStatus(request.Status))
                    baseQuery = baseQuery.Where(d => d.Status == request.Status);

                if (request.IsBalanced.HasValue && request.IsBalanced.Value == false)
                    baseQuery = baseQuery.Where(d => d.IsBalanced == false);

                // ✅ فیلتر ارز — هم با Code و هم با Name
                if (!string.IsNullOrEmpty(request.Currency))
                {
                    var key = request.Currency.Trim();
                    var currencyIdFilter = await _context.Currencies
                        .Where(c => c.Code.ToUpper() == key.ToUpper() || c.Name == key)
                        .Select(c => (int?)c.Id)
                        .FirstOrDefaultAsync();
                    if (currencyIdFilter.HasValue)
                        baseQuery = baseQuery.Where(d => d.CurrencyId == currencyIdFilter.Value);
                }

                var orderedQuery = baseQuery;
                switch (request.OrderBy?.ToLower())
                {
                    case "min":
                        orderedQuery = baseQuery.OrderBy(d => d.Id);
                        break;
                    case "max":
                        orderedQuery = baseQuery.OrderByDescending(d => d.Id);
                        break;
                    case "name_asc":
                        orderedQuery = baseQuery.OrderBy(d => d.Description);
                        break;
                    case "name_desc":
                        orderedQuery = baseQuery.OrderByDescending(d => d.Description);
                        break;
                    case "code_asc":
                        orderedQuery = baseQuery.OrderBy(d => d.DocumentNumber);
                        break;
                    case "code_desc":
                        orderedQuery = baseQuery.OrderByDescending(d => d.DocumentNumber);
                        break;
                    case "date_asc":
                        orderedQuery = baseQuery.OrderBy(d => d.DocumentDate);
                        break;
                    case "date_desc":
                        orderedQuery = baseQuery.OrderByDescending(d => d.DocumentDate);
                        break;
                    default:
                        orderedQuery = baseQuery.OrderByDescending(d => d.Id);
                        break;
                }

                var totalCount = await baseQuery.CountAsync();

                var rawDocuments = await orderedQuery
                    .Take(limit)
                    .Select(d => new
                    {
                        d.Id,
                        d.Company_id,
                        d.Fiscal_year,
                        d.DocumentNumber,
                        d.DocumentDate,
                        d.Description,
                        d.DocumentType,
                        d.Status,
                        d.IsBalanced,
                        d.CurrencyId,
                        RowCount = tenant.DocumentRows.Count(r => r.DocumentId == d.Id)
                    })
                    .ToListAsync();

                var currencyIds = rawDocuments
                    .Where(d => d.CurrencyId.HasValue)
                    .Select(d => d.CurrencyId!.Value)
                    .Distinct()
                    .ToList();

                var currencies = await _context.Currencies
                    .Where(c => currencyIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Symbol });

                var result = rawDocuments.Select(d => new
                {
                    d.Id,
                    d.Company_id,
                    d.Fiscal_year,
                    d.DocumentNumber,
                    d.DocumentDate,
                    d.Description,
                    d.DocumentType,
                    d.Status,
                    StatusName = GetStatusName(d.Status),
                    IsEditable = IsEditableStatus(d.Status),
                    IsCountedInAccounts = IsCountedInAccounts(d.Status),
                    RequiresBalance = RequiresBalance(d.Status),
                    d.IsBalanced,
                    d.RowCount,
                    Currency = d.CurrencyId.HasValue && currencies.ContainsKey(d.CurrencyId.Value)
                        ? currencies[d.CurrencyId.Value].Name : null,
                    CurrencySymbol = d.CurrencyId.HasValue && currencies.ContainsKey(d.CurrencyId.Value)
                        ? currencies[d.CurrencyId.Value].Symbol : null
                }).ToList();

                var returnedCount = result.Count;
                var nextLastId = returnedCount > 0 ? result[^1].Id : (int?)null;
                var hasMore = totalCount > returnedCount;

                return Ok(new
                {
                    Code = 1,
                    Data = result,
                    Limit = limit,
                    ReturnedCount = returnedCount,
                    TotalCount = totalCount,
                    HasMore = hasMore,
                    NextLastId = hasMore ? nextLastId : null,
                    IsLimited = totalCount > returnedCount
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  FINALIZE
        // ============================================================
        [HttpPost("finalize")]
        public async Task<IActionResult> FinalizeDocument([FromBody] FinalizeDocumentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindDocumentTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "سند یافت نشد یا حذف شده است" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var document = await tenant.Documents.FindAsync(request.Id);
                if (document == null || document.DeletedAt != null)
                    return Ok(new { Code = -2, Message = "سند یافت نشد یا حذف شده است" });

                if (document.Status == "Lock")
                    return Ok(new { Code = -3, Message = "سند قبلاً قطعی شده است" });

                string newStatus;
                if (document.Status == "Temp") newStatus = "Save";
                else if (document.Status == "Save") newStatus = "Lock";
                else return Ok(new { Code = -3, Message = "وضعیت فعلی سند نامعتبر است" });

                var rows = await tenant.DocumentRows
                    .Where(r => r.DocumentId == document.Id)
                    .ToListAsync();

                var totalDebit = rows.Sum(r => r.Debit);
                var totalCredit = rows.Sum(r => r.Credit);

                if (totalDebit != totalCredit)
                    return Ok(new
                    {
                        Code = -3,
                        Message = $"سند برای انتقال به وضعیت {newStatus} باید تراز باشد. جمع بدهکار: {totalDebit}، جمع بستانکار: {totalCredit}"
                    });

                document.Status = newStatus;
                document.IsBalanced = true;

                tenant.DocumentHistories.Add(new DocumentHistory
                {
                    OriginalId = document.Id,
                    Company_id = document.Company_id,
                    Fiscal_year = document.Fiscal_year,
                    DocumentNumber = document.DocumentNumber,
                    DocumentDate = document.DocumentDate,
                    Description = document.Description,
                    DocumentType = document.DocumentType,
                    IsBalanced = document.IsBalanced,
                    Status = document.Status,
                    CreatedAt = document.CreatedAt,
                    ActionType = "Finalize",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                var currencyInfo = document.CurrencyId.HasValue
                    ? await _context.Currencies.FindAsync(document.CurrencyId.Value)
                    : null;

                return Ok(new
                {
                    Code = 1,
                    Message = $"سند با موفقیت به وضعیت {GetStatusName(document.Status)} منتقل شد",
                    Data = new
                    {
                        document.Id,
                        document.DocumentNumber,
                        document.DocumentDate,
                        Status = document.Status,
                        StatusName = GetStatusName(document.Status),
                        IsEditable = IsEditableStatus(document.Status),
                        IsCountedInAccounts = IsCountedInAccounts(document.Status),
                        RequiresBalance = RequiresBalance(document.Status),
                        Currency = currencyInfo?.Name,
                        CurrencySymbol = currencyInfo?.Symbol
                    }
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
        public async Task<IActionResult> UpdateDocument([FromBody] UpdateDocumentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.Id <= 0)
                    return Ok(new { Code = -1, Message = "شناسه سند معتبر نیست" });

                var location = await FindDocumentTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "سند مورد نظر یافت نشد یا حذف شده است" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var document = await tenant.Documents
                    .FirstOrDefaultAsync(d => d.Id == request.Id && d.DeletedAt == null);
                if (document == null)
                    return Ok(new { Code = -2, Message = "سند مورد نظر یافت نشد یا حذف شده است" });

                if (document.Status == "Lock")
                    return Ok(new
                    {
                        Code = -3,
                        Message = "سند قطعی (Lock) قابل ویرایش نیست. برای اصلاح از سند اصلاحی استفاده کنید.",
                        Suggestion = "برای ایجاد سند اصلاحی از مسیر /api/accounting/document/create-correction استفاده کنید."
                    });

                bool hasDate = !string.IsNullOrEmpty(request.Document_date);
                bool hasDescription = request.Description != null;
                bool hasType = !string.IsNullOrEmpty(request.Document_type);
                bool hasStatus = !string.IsNullOrEmpty(request.Status);
                bool hasCurrency = !string.IsNullOrEmpty(request.Currency);
                bool hasRows = request.Rows != null && request.Rows.Count > 0;

                if (!hasDate && !hasDescription && !hasType && !hasStatus && !hasCurrency && !hasRows)
                    return Ok(new { Code = -1, Message = "هیچ فیلدی برای ویرایش ارسال نشده است" });

                string newStatus = document.Status;
                if (hasStatus)
                {
                    newStatus = request.Status!;
                    if (newStatus != "Temp" && newStatus != "Save")
                        return Ok(new
                        {
                            Code = -3,
                            Message = "وضعیت در ویرایش فقط می‌تواند Temp یا Save باشد. برای انتقال به Lock از finalize استفاده کنید."
                        });
                }

                if (hasDate)
                {
                    try { var _ = DateHelper.PersianToGregorian(request.Document_date!); }
                    catch (Exception ex)
                    {
                        return Ok(new { Code = -3, Message = $"فرمت تاریخ شمسی نامعتبر است: {ex.Message}" });
                    }
                }

                // ✅ دریافت/ساخت خودکار ارز
                int? newCurrencyId = null;
                if (hasCurrency)
                {
                    var resolvedCurrency = await ResolveCurrencyAsync(request.Currency);
                    newCurrencyId = resolvedCurrency.Id;
                }

                if (hasRows)
                {
                    foreach (var row in request.Rows!)
                    {
                        if (row.Tafzili_id <= 0)
                            return Ok(new { Code = -3, Message = "شناسه تفصیلی در یکی از ردیف‌ها نامعتبر است" });

                        var tafzili = await tenant.AccountTafzilis.FindAsync(row.Tafzili_id);
                        if (tafzili == null)
                            return Ok(new { Code = -2, Message = $"حساب تفصیلی با شناسه {row.Tafzili_id} وجود ندارد" });
                    }
                }

                if (hasDate) document.DocumentDate = request.Document_date!;
                if (hasDescription) document.Description = request.Description!;
                if (hasType) document.DocumentType = request.Document_type!;
                if (hasCurrency && newCurrencyId.HasValue) document.CurrencyId = newCurrencyId.Value;
                document.Status = newStatus;

                decimal finalDebit;
                decimal finalCredit;
                int finalRowCount;

                if (hasRows)
                {
                    var oldRows = await tenant.DocumentRows
                        .Where(r => r.DocumentId == document.Id)
                        .ToListAsync();
                    tenant.DocumentRows.RemoveRange(oldRows);

                    int rowNumber = 1;
                    finalDebit = 0;
                    finalCredit = 0;

                    foreach (var row in request.Rows!)
                    {
                        tenant.DocumentRows.Add(new DocumentRow
                        {
                            DocumentId = document.Id,
                            tafzili_id = row.Tafzili_id,
                            Debit = row.Debit,
                            Credit = row.Credit,
                            RowDescription = row.Row_description ?? "",
                            RowNumber = rowNumber++
                        });
                        finalDebit += row.Debit;
                        finalCredit += row.Credit;
                    }
                    finalRowCount = request.Rows.Count;
                }
                else
                {
                    var existingRows = await tenant.DocumentRows
                        .Where(r => r.DocumentId == document.Id)
                        .ToListAsync();
                    finalDebit = existingRows.Sum(r => r.Debit);
                    finalCredit = existingRows.Sum(r => r.Credit);
                    finalRowCount = existingRows.Count;
                }

                document.IsBalanced = (finalDebit == finalCredit);

                if (newStatus == "Save" && !document.IsBalanced)
                    return Ok(new
                    {
                        Code = -3,
                        Message = $"سند در وضعیت Save باید تراز باشد. جمع بدهکار: {finalDebit}، جمع بستانکار: {finalCredit}"
                    });

                tenant.DocumentHistories.Add(new DocumentHistory
                {
                    OriginalId = document.Id,
                    Company_id = document.Company_id,
                    Fiscal_year = document.Fiscal_year,
                    DocumentNumber = document.DocumentNumber,
                    DocumentDate = document.DocumentDate,
                    Description = document.Description,
                    DocumentType = document.DocumentType,
                    IsBalanced = document.IsBalanced,
                    Status = document.Status,
                    CreatedAt = document.CreatedAt,
                    ActionType = "Update",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                await tenant.SaveChangesAsync();

                var currencyInfo = document.CurrencyId.HasValue
                    ? await _context.Currencies.FindAsync(document.CurrencyId.Value)
                    : null;

                return Ok(new
                {
                    Code = 1,
                    Message = "سند با موفقیت ویرایش شد",
                    Data = new
                    {
                        DocumentId = document.Id,
                        DocumentNumber = document.DocumentNumber,
                        DocumentDate = document.DocumentDate,
                        Description = document.Description,
                        DocumentType = document.DocumentType,
                        Status = document.Status,
                        StatusName = GetStatusName(document.Status),
                        IsEditable = IsEditableStatus(document.Status),
                        IsCountedInAccounts = IsCountedInAccounts(document.Status),
                        RequiresBalance = RequiresBalance(document.Status),
                        TotalDebit = finalDebit,
                        TotalCredit = finalCredit,
                        IsBalanced = document.IsBalanced,
                        RowCount = finalRowCount,
                        Currency = currencyInfo?.Name,
                        CurrencySymbol = currencyInfo?.Symbol
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
        public async Task<IActionResult> DeleteDocument([FromBody] DeleteDocumentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindDocumentTenantAsync(request.Id);
                if (location == null)
                    return Ok(new { Code = -2, Message = "سند مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var document = await tenant.Documents.FindAsync(request.Id);
                if (document == null)
                    return Ok(new { Code = -2, Message = "سند مورد نظر یافت نشد" });

                if (document.DeletedAt != null)
                    return Ok(new { Code = -2, Message = "این سند قبلاً حذف شده است" });

                if (document.Status == "Lock")
                {
                    return Ok(new
                    {
                        Code = -3,
                        Message = "سند قطعی (Lock) قابل حذف نیست. لطفاً یک سند اصلاحی ایجاد کنید.",
                        Suggestion = "برای ایجاد سند اصلاحی از مسیر /api/accounting/document/create-correction استفاده کنید.",
                        OriginalDocumentId = document.Id,
                        OriginalDocumentNumber = document.DocumentNumber
                    });
                }

                document.DeletedAt = DateTime.Now;

                tenant.DocumentHistories.Add(new DocumentHistory
                {
                    OriginalId = document.Id,
                    Company_id = document.Company_id,
                    Fiscal_year = document.Fiscal_year,
                    DocumentNumber = document.DocumentNumber,
                    DocumentDate = document.DocumentDate,
                    Description = document.Description,
                    DocumentType = document.DocumentType,
                    IsBalanced = document.IsBalanced,
                    Status = document.Status,
                    CreatedAt = document.CreatedAt,
                    ActionType = "Delete",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });

                await tenant.SaveChangesAsync();

                return Ok(new
                {
                    Code = 1,
                    Message = "سند با موفقیت حذف شد (کپی در تاریخچه ذخیره شد)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        
        // ============================================================
        //  TYPES — ✅ فقط Name و Code (به‌جای symbol، کد اختصاری)
        // ============================================================
        [HttpPost("types")]
        public async Task<IActionResult> GetDocumentTypes([FromBody] GetDocumentTypesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var documentTypes = new List<object>
        {
            new { Value = "Permanent"  },
            new { Value = "Temporary"  },
            new { Value = "Correction" }
        };

                var documentStatuses = new List<object>
        {
            new { Value = "Temp", IsEditable = true,  RequiresBalance = false, CountedInAccounts = false },
            new { Value = "Save", IsEditable = true,  RequiresBalance = true,  CountedInAccounts = true  },
            new { Value = "Lock", IsEditable = false, RequiresBalance = true,  CountedInAccounts = true  }
        };

                // ✅ خروجی ارزها: name = نام فارسی، symbol = کد اختصاری (مثل IRR, USD)
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Id)
                    .Select(c => new
                    {
                        Name = c.Name,
                        Symbol = c.Code
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Code = 1,
                    Data = new
                    {
                        DocumentTypes = documentTypes,
                        DocumentStatuses = documentStatuses,
                        Currencies = currencies
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }

        // ============================================================
        //  HISTORY
        // ============================================================
        [HttpPost("history")]
        public async Task<IActionResult> GetDocumentHistory([FromBody] GetDocumentHistoryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                var location = await FindDocumentTenantAsync(request.OriginalId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "سند مورد نظر یافت نشد" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var raw = await tenant.DocumentHistories
                    .Where(h => h.OriginalId == request.OriginalId)
                    .OrderByDescending(h => h.ActionDate)
                    .ToListAsync();

                var histories = raw.Select(h => new
                {
                    h.Id,
                    h.OriginalId,
                    h.Company_id,
                    h.Fiscal_year,
                    h.DocumentNumber,
                    h.DocumentDate,
                    h.Description,
                    h.DocumentType,
                    h.Status,
                    StatusName = GetStatusName(h.Status),
                    IsEditable = IsEditableStatus(h.Status),
                    IsCountedInAccounts = IsCountedInAccounts(h.Status),
                    RequiresBalance = RequiresBalance(h.Status),
                    h.IsBalanced,
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
        //  CREATE CORRECTION
        // ============================================================
        [HttpPost("create-correction")]
        public async Task<IActionResult> CreateCorrectionDocument([FromBody] CreateCorrectionDocumentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                    return Ok(new { Code = -2, Message = "توکن ارائه نشده است" });

                var userId = await GetUserIdFromTokenAsync(request.Token);
                if (userId == null)
                    return Ok(new { Code = -2, Message = "توکن نامعتبر یا منقضی شده است" });

                if (request.OriginalDocumentId <= 0)
                    return Ok(new { Code = -1, Message = "شناسه سند اصلی معتبر نیست" });

                var location = await FindDocumentTenantAsync(request.OriginalDocumentId);
                if (location == null)
                    return Ok(new { Code = -2, Message = "سند اصلی یافت نشد یا قبلاً حذف شده است" });

                if (!await HasCompanyAccessAsync(userId.Value, location.Value.companyId))
                    return Ok(new { Code = -3, Message = "شما به این شرکت دسترسی ندارید" });

                using var tenant = await _tenantFactory.CreateAccountTafziliContextAsync(
                    location.Value.companyId, location.Value.fiscalYear);

                var originalDocument = await tenant.Documents
                    .FirstOrDefaultAsync(d => d.Id == request.OriginalDocumentId && d.DeletedAt == null);

                if (originalDocument == null)
                    return Ok(new { Code = -2, Message = "سند اصلی یافت نشد یا قبلاً حذف شده است" });

                var isPermanent = originalDocument.DocumentType == "Permanent"
                                  || originalDocument.DocumentType == "دائم";

                if (!isPermanent || originalDocument.Status != "Lock")
                    return Ok(new
                    {
                        Code = -3,
                        Message = "فقط اسناد دائم قطعی (Permanent + Lock) قابلیت ایجاد سند اصلاحی دارند"
                    });

                var existingCorrection = await tenant.Documents
                    .AnyAsync(d => d.OriginalDocumentId == originalDocument.Id && d.DeletedAt == null);

                if (existingCorrection)
                    return Ok(new { Code = -3, Message = "قبلاً برای این سند اصلاحی ایجاد شده است" });

                var originalRows = await tenant.DocumentRows
                    .Where(r => r.DocumentId == originalDocument.Id)
                    .OrderBy(r => r.RowNumber)
                    .ToListAsync();

                if (originalRows == null || originalRows.Count == 0)
                    return Ok(new { Code = -2, Message = "سند اصلی فاقد ردیف است" });

                string correctionDate;
                if (!string.IsNullOrEmpty(request.Document_date))
                {
                    try
                    {
                        var _ = DateHelper.PersianToGregorian(request.Document_date);
                        correctionDate = request.Document_date;
                    }
                    catch (Exception ex)
                    {
                        return Ok(new { Code = -3, Message = $"فرمت تاریخ شمسی نامعتبر است: {ex.Message}" });
                    }
                }
                else
                {
                    correctionDate = DateHelper.GregorianToPersian(DateTime.Now);
                }

                var docNumber = await GenerateDocumentNumberAsync(
                    originalDocument.Company_id, originalDocument.Fiscal_year);

                var correctionDocument = new Document
                {
                    Company_id = originalDocument.Company_id,
                    Fiscal_year = originalDocument.Fiscal_year,
                    DocumentNumber = docNumber,
                    DocumentDate = correctionDate,
                    Description = request.Description ?? $"اصلاحیه سند شماره {originalDocument.DocumentNumber}",
                    DocumentType = "Correction",
                    IsBalanced = true,
                    Status = "Temp",
                    CurrencyId = originalDocument.CurrencyId,
                    CreatedAt = DateTime.Now,
                    DeletedAt = null,
                    CreatedByUserId = userId.Value,
                    OriginalDocumentId = originalDocument.Id
                };

                tenant.Documents.Add(correctionDocument);
                await tenant.SaveChangesAsync();

                int rowNumber = 1;
                foreach (var row in originalRows)
                {
                    var correctionRow = new DocumentRow
                    {
                        DocumentId = correctionDocument.Id,
                        tafzili_id = row.tafzili_id,
                        Debit = row.Credit,
                        Credit = row.Debit,
                        RowDescription = $"اصلاحیه: {row.RowDescription}",
                        RowNumber = rowNumber++
                    };
                    tenant.DocumentRows.Add(correctionRow);
                }
                await tenant.SaveChangesAsync();

                tenant.DocumentHistories.Add(new DocumentHistory
                {
                    OriginalId = correctionDocument.Id,
                    Company_id = correctionDocument.Company_id,
                    Fiscal_year = correctionDocument.Fiscal_year,
                    DocumentNumber = correctionDocument.DocumentNumber,
                    DocumentDate = correctionDocument.DocumentDate,
                    Description = correctionDocument.Description,
                    DocumentType = correctionDocument.DocumentType,
                    IsBalanced = correctionDocument.IsBalanced,
                    Status = correctionDocument.Status,
                    CreatedAt = correctionDocument.CreatedAt,
                    ActionType = "Insert",
                    ActionDate = DateTime.Now,
                    UserId = userId
                });
                await tenant.SaveChangesAsync();

                var currencyInfo = correctionDocument.CurrencyId.HasValue
                    ? await _context.Currencies.FindAsync(correctionDocument.CurrencyId.Value)
                    : null;

                return Ok(new
                {
                    Code = 1,
                    Message = "سند اصلاحی با موفقیت ایجاد شد",
                    Data = new
                    {
                        CorrectionDocumentId = correctionDocument.Id,
                        CorrectionDocumentNumber = correctionDocument.DocumentNumber,
                        OriginalDocumentId = originalDocument.Id,
                        OriginalDocumentNumber = originalDocument.DocumentNumber,
                        DocumentDate = correctionDocument.DocumentDate,
                        Description = correctionDocument.Description,
                        Status = correctionDocument.Status,
                        StatusName = GetStatusName(correctionDocument.Status),
                        IsEditable = IsEditableStatus(correctionDocument.Status),
                        TotalRows = originalRows.Count,
                        Currency = currencyInfo?.Name,
                        CurrencySymbol = currencyInfo?.Symbol
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Code = -1, Message = $"خطا: {ex.Message}" });
            }
        }
    }

    // ============================================================
    //  کلاس‌های Request
    // ============================================================

    public class CreateDocumentRequest
    {
        public int Company_id { get; set; }
        public int Fiscal_year { get; set; }
        public string Document_date { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Document_type { get; set; }
        public string Status { get; set; } = "Temp";

        /// <summary>
        /// واحد پول — هم با Code (IRR, USD) هم با Name («ریال», «دلار») قابل قبول است. پیش‌فرض: ریال
        /// </summary>
        public string? Currency { get; set; }

        public List<DocumentRowRequest> Rows { get; set; }
        public string Token { get; set; }
    }

    public class DocumentRowRequest
    {
        public int Tafzili_id { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Row_description { get; set; }
    }

    public class SearchDocumentRequest
    {
        public int? Company_id { get; set; }
        public int? Fiscal_year { get; set; }
        public int? DocumentNumber { get; set; }
        public string? Document_date { get; set; }
        public string? Description { get; set; }
        public string? Document_type { get; set; }
        public string? Status { get; set; }
        public bool? IsBalanced { get; set; }

        /// <summary>
        /// فیلتر ارز — با Code یا Name
        /// </summary>
        public string? Currency { get; set; }

        public string? OrderBy { get; set; }
        public string Token { get; set; }
    }

    public class GetTafziliByCodeRequest
    {
        public int CompanyId { get; set; }
        public int FiscalYear { get; set; }
        public int Code_tafzili { get; set; }
        public string Token { get; set; }
    }

    public class GetDocumentHistoryRequest
    {
        public int OriginalId { get; set; }
        public string Token { get; set; }
    }

    public class FinalizeDocumentRequest
    {
        public int Id { get; set; }
        public string Token { get; set; }
    }

    public class UpdateDocumentRequest
    {
        public int Id { get; set; }
        public string? Document_date { get; set; }
        public string? Description { get; set; }
        public string? Document_type { get; set; }
        public string? Status { get; set; }

        /// <summary>
        /// واحد پول — با Code یا Name
        /// </summary>
        public string? Currency { get; set; }

        public List<DocumentRowRequest>? Rows { get; set; }
        public string Token { get; set; }
    }

    public class DeleteDocumentRequest
    {
        public int Id { get; set; }
        public string Token { get; set; }
    }

    public class CreateCorrectionDocumentRequest
    {
        public int OriginalDocumentId { get; set; }
        public string? Description { get; set; }
        public string? Document_date { get; set; }
        public string Token { get; set; }
    }

    public class GetDocumentTypesRequest
    {
        public string Token { get; set; }
    }
}