using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingAPI.Data
{
    public class AccountKolTenantDbContext : DbContext
    {
        public AccountKolTenantDbContext(DbContextOptions<AccountKolTenantDbContext> options)
            : base(options) { }

        public DbSet<AccountKol> AccountKols { get; set; }
        public DbSet<AccountKolHistory> AccountKolHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            //  نادیده گرفتن همه موجودیت‌های دیگر
            // ============================================================
            modelBuilder.Ignore<AccountGroup>();
            modelBuilder.Ignore<AccountGroupHistory>();
            modelBuilder.Ignore<AccountMoein>();
            modelBuilder.Ignore<AccountMoeinHistory>();
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
            modelBuilder.Entity<AccountKol>()
                .ToTable("AccountKol", "AccountKol");

            modelBuilder.Entity<AccountKolHistory>()
                .ToTable("tbl_AccountKolHistory", "AccountKol");

            // ============================================================
            //  نادیده گرفتن Navigation Propertyها
            // ============================================================
            modelBuilder.Entity<AccountKol>().Ignore(k => k.Group);
            modelBuilder.Entity<AccountKol>().Ignore(k => k.CreatedByUser);

            // ============================================================
            //  ایندکس‌ها
            // ============================================================
            modelBuilder.Entity<AccountKolHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountKolHistory_OriginalId");
        }
    }
}