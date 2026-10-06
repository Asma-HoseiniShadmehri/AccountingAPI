using System;
using System.Threading.Tasks;
using AccountingAPI.Data;
using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AccountingAPI.Services
{
    // ⚠️ interface از این فایل حذف شد — در ITenantService.cs است

    public class TenantService : ITenantService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public TenantService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string BuildConnectionString(string databaseName)
        {
            var mainConnStr = _configuration.GetConnectionString("DefaultConnection");
            var builder = new SqlConnectionStringBuilder(mainConnStr)
            {
                InitialCatalog = databaseName,
                MultipleActiveResultSets = true
            };
            return builder.ConnectionString;
        }

        private string BuildMasterConnectionString()
        {
            var mainConnStr = _configuration.GetConnectionString("DefaultConnection");
            var builder = new SqlConnectionStringBuilder(mainConnStr)
            {
                InitialCatalog = "master"
            };
            return builder.ConnectionString;
        }

        public async Task<string> GetCompanyConnectionString(int companyId)
        {
            var companyDb = await _context.CompanyDatabases
                .FirstOrDefaultAsync(cd => cd.CompanyId == companyId && cd.IsActive);

            if (companyDb == null || string.IsNullOrEmpty(companyDb.ConnectionString))
                throw new Exception("اطلاعات دیتابیس شرکت یافت نشد");

            return companyDb.ConnectionString;
        }

        public async Task<string> GetConnectionString(int companyId, string level)
        {
            // ✅ حالا Level مهم نیست — همه در یک DB هستند
            var database = await _context.Databases
                .FirstOrDefaultAsync(d => d.CompanyId == companyId
                                       && d.DeleteDate == null);

            if (database == null || string.IsNullOrEmpty(database.DbName))
                throw new Exception($"دیتابیس برای شرکت {companyId} یافت نشد");

            return BuildConnectionString(database.DbName);
        }

        // ============================================================
        //  ایجاد یک دیتابیس فیزیکی + همه جداول با Schema
        // ============================================================
        public async Task<bool> CreateCompanyDatabase(int companyId, string serverName, string databaseName, string level)
        {
            try
            {
                Console.WriteLine($"🔵 [1/3] Creating DB: {databaseName}");

                // ─── ۱) ایجاد DB فیزیکی
                var masterConnStr = BuildMasterConnectionString();

                using (var connection = new SqlConnection(masterConnStr))
                {
                    await connection.OpenAsync();

                    var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @dbName";
                    checkCmd.Parameters.AddWithValue("@dbName", databaseName);
                    var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!exists)
                    {
                        var createCmd = connection.CreateCommand();
                        createCmd.CommandText = $"CREATE DATABASE [{databaseName}]";
                        await createCmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"✅ [2/3] DB '{databaseName}' created.");
                    }
                    else
                    {
                        Console.WriteLine($"ℹ️ [2/3] DB '{databaseName}' already exists.");
                    }
                }

                // ─── ۲) منتظر ONLINE شدن
                var isOnline = await WaitForDatabaseOnlineAsync(masterConnStr, databaseName);
                if (!isOnline)
                {
                    Console.WriteLine($"❌ DB '{databaseName}' never became ONLINE.");
                    return false;
                }

                // ─── ۳) ساخت همه جداول با Schema
                var connStr = BuildConnectionString(databaseName);
                var success = await CreateAllTablesAsync(connStr);
                if (!success)
                {
                    Console.WriteLine($"❌ Table creation failed for '{databaseName}'.");
                    return false;
                }
                Console.WriteLine($"✅ [3/3] All tables created in '{databaseName}'.");

                // ─── ۴) ثبت CompanyDatabases
                var existing = await _context.CompanyDatabases
                    .FirstOrDefaultAsync(cd => cd.CompanyId == companyId
                                            && cd.DatabaseName == databaseName);

                if (existing == null)
                {
                    _context.CompanyDatabases.Add(new CompanyDatabase
                    {
                        CompanyId = companyId,
                        ServerName = serverName,
                        DatabaseName = databaseName,
                        ConnectionString = connStr,
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    });
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                return false;
            }
        }

        // ============================================================
        //  منتظر ONLINE شدن DB
        // ============================================================
        private async Task<bool> WaitForDatabaseOnlineAsync(string masterConnStr, string dbName, int maxRetries = 20)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var conn = new SqlConnection(masterConnStr);
                    await conn.OpenAsync();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @name";
                    cmd.Parameters.AddWithValue("@name", dbName);
                    var state = (string?)await cmd.ExecuteScalarAsync();
                    if (state == "ONLINE") return true;
                }
                catch { }
                await Task.Delay(500);
            }
            return false;
        }

        // ============================================================
        //  ساخت همه جداول با CombinedTenantDbContext
        // ============================================================
        private async Task<bool> CreateAllTablesAsync(string connectionString, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // اول Schemaها را بساز
                    await EnsureSchemasAsync(connectionString);

                    var opts = new DbContextOptionsBuilder<CombinedTenantDbContext>()
                        .UseSqlServer(connectionString).Options;

                    using var db = new CombinedTenantDbContext(opts);
                    await db.Database.EnsureCreatedAsync();

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Attempt {i + 1}/{maxRetries}: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                    if (i < maxRetries - 1)
                        await Task.Delay(1000);
                }
            }
            return false;
        }

        // ============================================================
        //  اطمینان از وجود Schemaها
        // ============================================================
        private async Task EnsureSchemasAsync(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                var schemas = new[] { "AccountGroup", "AccountKol", "AccountMoein", "AccountTafzili" };

                foreach (var schema in schemas)
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
                        IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{schema}')
                        BEGIN
                            EXEC('CREATE SCHEMA [{schema}]')
                        END";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   EnsureSchemas warning: {ex.Message}");
            }
        }

        public async Task<bool> UpdateCompanyConnectionString(int companyId, string connectionString)
        {
            var companyDb = await _context.CompanyDatabases
                .FirstOrDefaultAsync(cd => cd.CompanyId == companyId);

            if (companyDb == null) return false;

            companyDb.ConnectionString = connectionString;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}