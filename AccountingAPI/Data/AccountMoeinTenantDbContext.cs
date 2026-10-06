using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingAPI.Data
{
    public class AccountMoeinTenantDbContext : DbContext
    {
        public AccountMoeinTenantDbContext(DbContextOptions<AccountMoeinTenantDbContext> options)
            : base(options) { }

        public DbSet<AccountMoein> AccountMoeins { get; set; }
        public DbSet<AccountMoeinHistory> AccountMoeinHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            //  نادیده گرفتن همه موجودیت‌های دیگر
            // ============================================================
            modelBuilder.Ignore<AccountGroup>();
            modelBuilder.Ignore<AccountGroupHistory>();
            modelBuilder.Ignore<AccountKol>();
            modelBuilder.Ignore<AccountKolHistory>();
            modelBuilder.Ignore<AccountTafzili>();
            modelBuilder.Ignore<AccountTafziliHistory>();
            modelBuilder.Ignore<Document>();
            modelBuilder.Ignore<DocumentRow>();
            modelBuilder.Ignore<DocumentHistory>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Company>();
            modelBuilder.Ignore<UserToken>();
            modelBuilder.Ignore<UserCompany>();
            modelBuilder.Ignore<DatabaseEntity>();
            modelBuilder.Ignore<CompanyDatabase>();
            modelBuilder.Ignore<AuditLog>();
            modelBuilder.Ignore<CompanyHistory>();
            modelBuilder.Ignore<UserHistory>();
            modelBuilder.Ignore<DatabaseHistory>();

            // ============================================================
            //  ✅ جداول با Schema
            // ============================================================
            modelBuilder.Entity<AccountMoein>()
                .ToTable("AccountMoein", "AccountMoein");

            modelBuilder.Entity<AccountMoeinHistory>()
                .ToTable("tbl_AccountMoeinHistory", "AccountMoein");

            // ============================================================
            //  نادیده گرفتن Navigation Propertyها
            // ============================================================
            modelBuilder.Entity<AccountMoein>().Ignore(m => m.Kol);
            modelBuilder.Entity<AccountMoein>().Ignore(m => m.CreatedByUser);

            // ============================================================
            //  ایندکس‌ها
            // ============================================================
            modelBuilder.Entity<AccountMoeinHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountMoeinHistory_OriginalId");
        }
    }
}