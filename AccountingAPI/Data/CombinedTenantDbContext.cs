using AccountingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingAPI.Data
{
    /// <summary>
    /// این Context فقط برای EnsureCreated روی یک DB واحد استفاده می‌شود
    /// تا همه جداول ۴ سطح در یک DB ساخته شوند.
    /// </summary>
    public class CombinedTenantDbContext : DbContext
    {
        public CombinedTenantDbContext(DbContextOptions<CombinedTenantDbContext> options)
            : base(options) { }

        // همه DbSetها
        public DbSet<AccountGroup> AccountGroups { get; set; }
        public DbSet<AccountGroupHistory> AccountGroupHistories { get; set; }
        public DbSet<AccountKol> AccountKols { get; set; }
        public DbSet<AccountKolHistory> AccountKolHistories { get; set; }
        public DbSet<AccountMoein> AccountMoeins { get; set; }
        public DbSet<AccountMoeinHistory> AccountMoeinHistories { get; set; }
        public DbSet<AccountTafzili> AccountTafzilis { get; set; }
        public DbSet<AccountTafziliHistory> AccountTafziliHistories { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentRow> DocumentRows { get; set; }
        public DbSet<DocumentHistory> DocumentHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // نادیده گرفتن همه موجودیت‌های بیرونی
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

            // ═══════════════════════════════════════════════════════════
            //  جدول‌ها با SCHEMA
            // ═══════════════════════════════════════════════════════════

            // Group
            modelBuilder.Entity<AccountGroup>()
                .ToTable("tbl_AccountGroup", "AccountGroup");
            modelBuilder.Entity<AccountGroupHistory>()
                .ToTable("tbl_AccountGroupHistory", "AccountGroup");

            // Kol
            modelBuilder.Entity<AccountKol>()
                .ToTable("AccountKol", "AccountKol");
            modelBuilder.Entity<AccountKolHistory>()
                .ToTable("tbl_AccountKolHistory", "AccountKol");

            // Moein
            modelBuilder.Entity<AccountMoein>()
                .ToTable("AccountMoein", "AccountMoein");
            modelBuilder.Entity<AccountMoeinHistory>()
                .ToTable("tbl_AccountMoeinHistory", "AccountMoein");

            // Tafzili + Documents
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

            // Navigation ignores
            modelBuilder.Entity<AccountGroup>().Ignore(ag => ag.Company);
            modelBuilder.Entity<AccountGroup>().Ignore(ag => ag.CreatedByUser);

            modelBuilder.Entity<AccountKol>().Ignore(k => k.Group);
            modelBuilder.Entity<AccountKol>().Ignore(k => k.CreatedByUser);

            modelBuilder.Entity<AccountMoein>().Ignore(m => m.Kol);
            modelBuilder.Entity<AccountMoein>().Ignore(m => m.CreatedByUser);

            modelBuilder.Entity<AccountTafzili>().Ignore(t => t.Moein);
            modelBuilder.Entity<AccountTafzili>().Ignore(t => t.CreatedByUser);

            modelBuilder.Entity<Document>().Ignore(d => d.Company);
            modelBuilder.Entity<Document>().Ignore(d => d.CreatedByUser);
            modelBuilder.Entity<Document>().Ignore(d => d.OriginalDocument);

            // DocumentRow
            modelBuilder.Entity<DocumentRow>(entity =>
            {
                entity.Property(e => e.Debit).HasPrecision(18, 2);
                entity.Property(e => e.Credit).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Document>()
                .HasIndex(d => new { d.Company_id, d.Fiscal_year, d.DocumentNumber })
                .IsUnique()
                .HasDatabaseName("IX_Document_Company_Fiscal_Number");

            // ایندکس‌های تاریخچه
            modelBuilder.Entity<AccountGroupHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountGroupHistory_OriginalId");
            modelBuilder.Entity<AccountKolHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountKolHistory_OriginalId");
            modelBuilder.Entity<AccountMoeinHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountMoeinHistory_OriginalId");
            modelBuilder.Entity<AccountTafziliHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_AccountTafziliHistory_OriginalId");
            modelBuilder.Entity<DocumentHistory>()
                .HasIndex(h => h.OriginalId)
                .HasDatabaseName("IX_DocumentHistory_OriginalId");
        }
    }
}