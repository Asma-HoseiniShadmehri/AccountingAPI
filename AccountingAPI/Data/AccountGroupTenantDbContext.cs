using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingAPI.Data
{
    public class AccountGroupTenantDbContext : DbContext
    {
        public AccountGroupTenantDbContext(DbContextOptions<AccountGroupTenantDbContext> options)
            : base(options) { }

        public DbSet<AccountGroup> AccountGroups { get; set; }
        public DbSet<AccountGroupHistory> AccountGroupHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            //  نادیده گرفتن همه موجودیت‌های دیگر
            // ============================================================
            modelBuilder.Ignore<AccountKol>();
            modelBuilder.Ignore<AccountKolHistory>();
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
            modelBuilder.Entity<AccountGroup>()
                .ToTable("tbl_AccountGroup", "AccountGroup");

            modelBuilder.Entity<AccountGroupHistory>()
                .ToTable("tbl_AccountGroupHistory", "AccountGroup");

            // ============================================================
            //  نادیده گرفتن Navigation Propertyها
            // ============================================================
            modelBuilder.Entity<AccountGroup>().Ignore(ag => ag.Company);
            modelBuilder.Entity<AccountGroup>().Ignore(ag => ag.CreatedByUser);

            // ============================================================
            //  ایندکس‌ها
            // ============================================================
            modelBuilder.Entity<AccountGroupHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountGroupHistory_OriginalId");
        }
    }
}