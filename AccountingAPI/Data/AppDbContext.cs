using Microsoft.EntityFrameworkCore;
using AccountingAPI.Models;

namespace AccountingAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ========== DbSet های موجود ==========
        public DbSet<User> Users { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<DatabaseEntity> Databases { get; set; }
        public DbSet<UserCompany> UserCompanies { get; set; }
        public DbSet<CompanyDatabase> CompanyDatabases { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CompanyHistory> CompanyHistories { get; set; }
        public DbSet<UserHistory> UserHistories { get; set; }
        public DbSet<DatabaseHistory> DatabaseHistories { get; set; }

        // ========== DbSet های جدید ماژول حسابداری ==========
        public DbSet<AccountGroup> AccountGroups { get; set; }
        public DbSet<AccountKol> AccountKols { get; set; }
        public DbSet<AccountMoein> AccountMoeins { get; set; }
        public DbSet<AccountTafzili> AccountTafzilis { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<DocumentRow> DocumentRows { get; set; }

        // تاریخچه‌ها
        public DbSet<AccountGroupHistory> AccountGroupHistories { get; set; }
        public DbSet<AccountKolHistory> AccountKolHistories { get; set; }
        public DbSet<AccountMoeinHistory> AccountMoeinHistories { get; set; }
        public DbSet<AccountTafziliHistory> AccountTafziliHistories { get; set; }
        public DbSet<DocumentHistory> DocumentHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ========== تغییر نام جداول به tbl_ ==========
            modelBuilder.Entity<User>().ToTable("tbl_User");
            modelBuilder.Entity<PasswordResetToken>().ToTable("tbl_PasswordResetToken");
            modelBuilder.Entity<Company>().ToTable("tbl_Company");
            modelBuilder.Entity<DatabaseEntity>().ToTable("tbl_Databases");
            modelBuilder.Entity<UserCompany>().ToTable("tbl_UserCompany");
            modelBuilder.Entity<UserToken>().ToTable("tbl_UserToken");
            modelBuilder.Entity<CompanyDatabase>().ToTable("tbl_CompanyDatabases");
            modelBuilder.Entity<AuditLog>().ToTable("tbl_AuditLogs");
            modelBuilder.Entity<CompanyHistory>().ToTable("tbl_CompanyHistories");
            modelBuilder.Entity<UserHistory>().ToTable("tbl_UserHistories");
            modelBuilder.Entity<DatabaseHistory>().ToTable("tbl_DatabaseHistories");

            // ========== جداول حسابداری (اصلاح‌شده) ==========
            modelBuilder.Entity<AccountGroup>().ToTable("tbl_AccountGroup");
            modelBuilder.Entity<AccountKol>().ToTable("AccountKol");             // ← اصلاح شد
            modelBuilder.Entity<AccountMoein>().ToTable("AccountMoein");         // ← اصلاح شد
            modelBuilder.Entity<AccountTafzili>().ToTable("AccountTafzili");     // ← اصلاح شد

            modelBuilder.Entity<Document>().ToTable("tbl_Document");
            modelBuilder.Entity<Currency>().ToTable("tbl_Currency");
            modelBuilder.Entity<DocumentRow>().ToTable("tbl_DocumentRow");

            // ========== تاریخچه‌ها ==========
            modelBuilder.Entity<AccountGroupHistory>().ToTable("tbl_AccountGroupHistory");
            modelBuilder.Entity<AccountKolHistory>().ToTable("tbl_AccountKolHistory");
            modelBuilder.Entity<AccountMoeinHistory>().ToTable("tbl_AccountMoeinHistory");
            modelBuilder.Entity<AccountTafziliHistory>().ToTable("tbl_AccountTafziliHistory");
            modelBuilder.Entity<DocumentHistory>().ToTable("tbl_DocumentHistory");

            // ========== مقدار پیش‌فرض RegisterDate ==========
            modelBuilder.Entity<User>()
                .Property(u => u.RegisterDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Company>()
                .Property(c => c.RegisterDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<DatabaseEntity>()
                .Property(d => d.RegisterDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<UserCompany>()
                .Property(uc => uc.RegisterDate)
                .HasDefaultValueSql("GETDATE()");

            // ========== تنظیم جدول CompanyDatabase ==========
            modelBuilder.Entity<CompanyDatabase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ServerName).HasMaxLength(200);
                entity.Property(e => e.DatabaseName).HasMaxLength(200);
                entity.Property(e => e.ConnectionString).HasMaxLength(1000);
                entity.HasOne(cd => cd.Company)
                      .WithMany()
                      .HasForeignKey(cd => cd.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            //  تنظیمات جدید ماژول حسابداری (با فیلدهای اصلاح‌شده)
            // ============================================================

            // 1. روابط سلسله‌مراتب گروه ← کل ← معین ← تفصیلی

            // AccountKol → AccountGroup (با Group_id)
            modelBuilder.Entity<AccountKol>()
                .HasOne(k => k.Group)
                .WithMany()
                .HasForeignKey(k => k.Group_id)
                .OnDelete(DeleteBehavior.Restrict);

            // AccountMoein → AccountKol (با Kol_id - اصلاح شده)
            modelBuilder.Entity<AccountMoein>()
                .HasOne(m => m.Kol)
                .WithMany()
                .HasForeignKey(m => m.Kol_id)    // ← اصلاح: Kol_id
                .OnDelete(DeleteBehavior.Restrict);

            // AccountTafzili → AccountMoein (با Moein_id - اصلاح شده)
            modelBuilder.Entity<AccountTafzili>()
                .HasOne(t => t.Moein)
                .WithMany()
                .HasForeignKey(t => t.Moein_id)  // ← اصلاح: Moein_id
                .OnDelete(DeleteBehavior.Restrict);

            // 2. روابط برای CreatedByUserId (اختیاری اما برای جلوگیری از خطا)
            modelBuilder.Entity<AccountGroup>()
                .HasOne(ag => ag.CreatedByUser)
                .WithMany()
                .HasForeignKey(ag => ag.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccountKol>()
                .HasOne(ak => ak.CreatedByUser)
                .WithMany()
                .HasForeignKey(ak => ak.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccountMoein>()
                .HasOne(am => am.CreatedByUser)
                .WithMany()
                .HasForeignKey(am => am.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccountTafzili>()
                .HasOne(at => at.CreatedByUser)
                .WithMany()
                .HasForeignKey(at => at.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. رابطه Document با Company (با Company_id - اصلاح شده)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Company)
                .WithMany()
                .HasForeignKey(d => d.Company_id)   // ← اصلاح: Company_id
                .OnDelete(DeleteBehavior.Restrict);

            // 4. رابطه Document با OriginalDocument (برای سند اصلاحی)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.OriginalDocument)
                .WithMany()
                .HasForeignKey(d => d.OriginalDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. رابطه DocumentRow با TafziliAccount (با tafzili_id)
            modelBuilder.Entity<DocumentRow>()
                .HasOne(dr => dr.Tafzili)
                .WithMany()
                .HasForeignKey(dr => dr.tafzili_id)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. ایندکس یکتا برای شماره سند در هر شرکت و سال مالی (اصلاح شده)
            modelBuilder.Entity<Document>()
                .HasIndex(d => new { d.Company_id, d.Fiscal_year, d.DocumentNumber })
                .IsUnique()
                .HasDatabaseName("IX_Document_Company_Fiscal_Number");

            // ============================================================
            // 7. تنظیم دقت (Precision) برای فیلدهای decimal در DocumentRow
            // ============================================================
            modelBuilder.Entity<DocumentRow>(entity =>
            {
                entity.Property(e => e.Debit).HasPrecision(18, 2);
                entity.Property(e => e.Credit).HasPrecision(18, 2);
                
            });

            // ============================================================
            // 8. تنظیمات اضافی برای فیلدهای تاریخچه (در صورت نیاز)
            // ============================================================
            // اگر تاریخچه‌ها نیاز به ایندکس دارند، می‌توان اضافه کرد
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