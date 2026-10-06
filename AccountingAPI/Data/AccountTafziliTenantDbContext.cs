using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingAPI.Data
{
    public class AccountTafziliTenantDbContext : DbContext
    {
        public AccountTafziliTenantDbContext(DbContextOptions<AccountTafziliTenantDbContext> options)
            : base(options) { }

        public DbSet<AccountTafzili> AccountTafzilis { get; set; }
        public DbSet<AccountTafziliHistory> AccountTafziliHistories { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentRow> DocumentRows { get; set; }
        public DbSet<DocumentHistory> DocumentHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ============================================================
            //  نادیده گرفتن همه موجودیت‌های دیگر
            // ============================================================
            modelBuilder.Ignore<AccountGroup>();
            modelBuilder.Ignore<AccountGroupHistory>();
            modelBuilder.Ignore<AccountKol>();
            modelBuilder.Ignore<AccountKolHistory>();
            modelBuilder.Ignore<AccountMoein>();
            modelBuilder.Ignore<AccountMoeinHistory>();
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
            modelBuilder.Entity<AccountTafzili>()
                .ToTable("AccountTafzili", "AccountTafzili");

            modelBuilder.Entity<AccountTafziliHistory>()
                .ToTable("tbl_AccountTafziliHistory", "AccountTafzili");

            modelBuilder.Entity<Document>()
                .ToTable("tbl_Document", "AccountTafzili");

            modelBuilder.Entity<DocumentRow>()
                .ToTable("tbl_DocumentRow", "AccountTafzili");

            modelBuilder.Entity<DocumentHistory>()
                .ToTable("tbl_DocumentHistory", "AccountTafzili");

            // ============================================================
            //  نادیده گرفتن Navigation Propertyها
            // ============================================================
            modelBuilder.Entity<AccountTafzili>().Ignore(t => t.Moein);
            modelBuilder.Entity<AccountTafzili>().Ignore(t => t.CreatedByUser);

            modelBuilder.Entity<Document>().Ignore(d => d.Company);
            modelBuilder.Entity<Document>().Ignore(d => d.CreatedByUser);
            modelBuilder.Entity<Document>().Ignore(d => d.OriginalDocument);

            // ============================================================
            //  تنظیمات اضافی
            // ============================================================
            modelBuilder.Entity<DocumentRow>(entity =>
            {
                entity.Property(e => e.Debit).HasPrecision(18, 2);
                entity.Property(e => e.Credit).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Document>()
                .HasIndex(d => new { d.Company_id, d.Fiscal_year, d.DocumentNumber })
                .IsUnique()
                .HasDatabaseName("IX_Document_Company_Fiscal_Number");

            modelBuilder.Entity<AccountTafziliHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountTafziliHistory_OriginalId");

            modelBuilder.Entity<DocumentHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_DocumentHistory_OriginalId");
        }
    }
}