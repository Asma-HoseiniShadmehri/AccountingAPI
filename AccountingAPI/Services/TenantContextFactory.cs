using AccountingAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingAPI.Services
{
    public interface ITenantContextFactory
    {
        Task<AccountGroupTenantDbContext> CreateAccountGroupContextAsync(int companyId, int fiscalYear);
        Task<AccountKolTenantDbContext> CreateAccountKolContextAsync(int companyId, int fiscalYear);
        Task<AccountMoeinTenantDbContext> CreateAccountMoeinContextAsync(int companyId, int fiscalYear);
        Task<AccountTafziliTenantDbContext> CreateAccountTafziliContextAsync(int companyId, int fiscalYear);
    }

    public class TenantContextFactory : ITenantContextFactory
    {
        private readonly AppDbContext _appContext;
        private readonly IConfiguration _configuration;

        public TenantContextFactory(AppDbContext appContext, IConfiguration configuration)
        {
            _appContext = appContext;
            _configuration = configuration;
        }

        // ============================================================
        //  ✅ اصلاح‌شده: اول Combined را چک می‌کند، بعد Level را
        //  این باعث می‌شود هم با DBهای قدیمی (۴ تایی) و هم با
        //  DBهای جدید (Combined) کار کند.
        // ============================================================
        private async Task<string> GetConnectionStringAsync(int companyId, int fiscalYear, string level)
        {
            // ۱) اول دنبال Combined بگرد (دیتابیس‌های جدید)
            var combinedDb = await _appContext.Databases
                .FirstOrDefaultAsync(d => d.CompanyId == companyId
                                       && d.Number == fiscalYear.ToString()
                                       && d.Level == "Combined"
                                       && d.DeleteDate == null);

            if (combinedDb != null && !string.IsNullOrEmpty(combinedDb.DbName))
            {
                return BuildConnectionString(combinedDb.DbName);
            }

            // ۲) اگر Combined نبود، دنبال Level مشخص بگرد (دیتابیس‌های قدیمی)
            var levelDb = await _appContext.Databases
                .FirstOrDefaultAsync(d => d.CompanyId == companyId
                                       && d.Number == fiscalYear.ToString()
                                       && d.Level == level
                                       && d.DeleteDate == null);

            if (levelDb != null && !string.IsNullOrEmpty(levelDb.DbName))
            {
                return BuildConnectionString(levelDb.DbName);
            }

            throw new Exception(
                $"دیتابیس برای شرکت {companyId} سال {fiscalYear} سطح {level} یافت نشد.");
        }

        private string BuildConnectionString(string databaseName)
        {
            var mainConnStr = _configuration.GetConnectionString("DefaultConnection");
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(mainConnStr)
            {
                InitialCatalog = databaseName
            };
            return builder.ConnectionString;
        }

        public async Task<AccountGroupTenantDbContext> CreateAccountGroupContextAsync(int companyId, int fiscalYear)
        {
            var connStr = await GetConnectionStringAsync(companyId, fiscalYear, "AccountGroup");
            var opts = new DbContextOptionsBuilder<AccountGroupTenantDbContext>()
                .UseSqlServer(connStr).Options;
            return new AccountGroupTenantDbContext(opts);
        }

        public async Task<AccountKolTenantDbContext> CreateAccountKolContextAsync(int companyId, int fiscalYear)
        {
            var connStr = await GetConnectionStringAsync(companyId, fiscalYear, "AccountKol");
            var opts = new DbContextOptionsBuilder<AccountKolTenantDbContext>()
                .UseSqlServer(connStr).Options;
            return new AccountKolTenantDbContext(opts);
        }

        public async Task<AccountMoeinTenantDbContext> CreateAccountMoeinContextAsync(int companyId, int fiscalYear)
        {
            var connStr = await GetConnectionStringAsync(companyId, fiscalYear, "AccountMoein");
            var opts = new DbContextOptionsBuilder<AccountMoeinTenantDbContext>()
                .UseSqlServer(connStr).Options;
            return new AccountMoeinTenantDbContext(opts);
        }

        public async Task<AccountTafziliTenantDbContext> CreateAccountTafziliContextAsync(int companyId, int fiscalYear)
        {
            var connStr = await GetConnectionStringAsync(companyId, fiscalYear, "AccountTafzili");
            var opts = new DbContextOptionsBuilder<AccountTafziliTenantDbContext>()
                .UseSqlServer(connStr).Options;
            return new AccountTafziliTenantDbContext(opts);
        }
    }
}