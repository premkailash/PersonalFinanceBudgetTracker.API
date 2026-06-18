using Microsoft.EntityFrameworkCore;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;

namespace PersonalFinanceBudgetTrackerAPI.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<SavingsGoal> SavingsGoals { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DataExport> DataExports { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<DefaultBudget> DefaultBudgets { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.UserId);

                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.HasIndex(u => u.Username)
                      .IsUnique();

                entity.Property(u => u.Role)
                      .HasDefaultValue("User");

                entity.Property(u => u.Is2FAEnabled)
                      .HasDefaultValue(false);

                entity.Property(u => u.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ---------------------------------------------------------------
            // Accounts
            // ---------------------------------------------------------------
            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(a => a.AccountId);

                entity.Property(a => a.Balance)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0.00m);

                entity.Property(a => a.IsActive).HasDefaultValue(true);
                entity.Property(a => a.LinkedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(a => a.Currency).HasDefaultValue("INR");

                entity.HasOne(a => a.User)
                      .WithMany()
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => a.UserId);
                entity.HasIndex(a => new { a.UserId, a.IsActive });
            });

            // ---------------------------------------------------------------
            // Category
            // ---------------------------------------------------------------
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("category");
                entity.HasKey(c => c.CategoryId);

                entity.Property(c => c.IsDefault).HasDefaultValue(false);

                entity.HasIndex(c => c.Type);
            });

            // ---------------------------------------------------------------
            // Budgets
            // ---------------------------------------------------------------
            modelBuilder.Entity<Budget>(entity =>
            {
                entity.ToTable("budgets");
                entity.HasKey(b => b.BudgetId);

                entity.Property(b => b.TargetAmount)
                      .HasColumnType("decimal(15,2)");

                entity.Property(b => b.CurrentAmount)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0.00m);

                entity.Property(b => b.AutoContributeAmount)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0.00m);

                entity.Property(b => b.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: Budget -> User (CASCADE delete)
                entity.HasOne(b => b.User)
                      .WithMany()
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: Budget -> Account (CASCADE delete)
                entity.HasOne(b => b.Account)
                      .WithMany()
                      .HasForeignKey(b => b.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: Budget -> Category (RESTRICT delete)
                entity.HasOne(b => b.Category)
                      .WithMany()
                      .HasForeignKey(b => b.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
               
                entity.HasIndex(b => b.UserId);
                entity.HasIndex(b => b.AccountId);
                entity.HasIndex(b => b.TargetDate);
                entity.HasIndex(b => new { b.UserId, b.TargetDate });
                entity.HasIndex(b => new { b.UserId, b.AccountId, b.CategoryId });
            });

            // ---------------------------------------------------------------
            // Logs
            // ---------------------------------------------------------------
            modelBuilder.Entity<Log>(entity =>
            {
                entity.ToTable("logs");
                entity.HasKey(l => l.LogId);

                entity.Property(l => l.Event)
                      .HasMaxLength(255)
                      .IsRequired();

                entity.Property(l => l.EventType)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(l => l.Timestamp)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: Log -> User — SET NULL when user is deleted
                // Preserves audit trail integrity
                entity.HasOne(l => l.Actor)
                      .WithMany()
                      .HasForeignKey(l => l.ActorId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                // Indexes for common audit queries
                entity.HasIndex(l => l.ActorId);
                entity.HasIndex(l => l.EventType);
                entity.HasIndex(l => l.Timestamp);
                entity.HasIndex(l => new { l.ActorId, l.EventType });
                entity.HasIndex(l => new { l.ActorId, l.Timestamp });
                entity.HasIndex(l => new { l.EventType, l.Timestamp });
            });

            // ---------------------------------------------------------------
            // SavingsGoals
            // ---------------------------------------------------------------
            modelBuilder.Entity<SavingsGoal>(entity =>
            {
                entity.ToTable("savingsgoals");
                entity.HasKey(g => g.GoalId);

                entity.Property(g => g.TargetAmount)
                      .HasColumnType("decimal(15,2)");

                entity.Property(g => g.CurrentAmount)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0.00m);

                entity.Property(g => g.AutoContributeAmount)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0.00m);

                entity.Property(g => g.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: SavingsGoal -> User (CASCADE delete)
                entity.HasOne(g => g.User)
                      .WithMany()
                      .HasForeignKey(g => g.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: SavingsGoal -> Account (CASCADE delete)
                entity.HasOne(g => g.Account)
                      .WithMany()
                      .HasForeignKey(g => g.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(g => g.UserId);
                entity.HasIndex(g => g.AccountId);
                entity.HasIndex(g => g.TargetDate);
                entity.HasIndex(g => new { g.UserId, g.TargetDate });
            });

            // ---------------------------------------------------------------
            // Notifications
            // ---------------------------------------------------------------
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notifications");
                entity.HasKey(n => n.NotificationId);

                entity.Property(n => n.Message)
                      .HasMaxLength(255)
                      .IsRequired();

                entity.Property(n => n.Type)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(n => n.IsRead)
                      .HasDefaultValue(false);

                entity.Property(n => n.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // FK: Notification -> User (CASCADE delete)
                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.IsRead);
                entity.HasIndex(n => new { n.UserId, n.IsRead });
                entity.HasIndex(n => new { n.UserId, n.CreatedAt });
            });

            // ---------------------------------------------------------------
            // DataExport
            // ---------------------------------------------------------------
            modelBuilder.Entity<DataExport>(entity =>
            {
                entity.ToTable("dataexport");
                entity.HasKey(e => e.ExportId);

                entity.Property(e => e.ReportType)
                      .HasMaxLength(30)
                      .IsRequired();

                entity.Property(e => e.ReportOptions)
                      .HasMaxLength(30)
                      .IsRequired();

                entity.Property(e => e.ReportLink)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(e => e.IsGenerated)
                      .HasDefaultValue(false);

                entity.Property(e => e.Timestamp)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");                

                // FK: DataExport -> User (CASCADE delete)
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: DataExport -> Account (CASCADE delete)
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.AccountId);
                entity.HasIndex(e => e.ReportType);
                entity.HasIndex(e => e.IsGenerated);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.UserId, e.ReportType });
                entity.HasIndex(e => new { e.UserId, e.FromDate, e.ToDate });
            });

            // ---------------------------------------------------------------
            // Transactions
            // ---------------------------------------------------------------
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.HasKey(t => t.TransactionId);

                entity.Property(t => t.Amount)
                      .HasColumnType("decimal(15,2)");

                entity.Property(t => t.Currency)
                      .HasDefaultValue("USD");

                entity.Property(t => t.IsRecurring)
                      .HasDefaultValue(false);

                entity.Property(t => t.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
               
                // FK: Transaction -> Account (CASCADE)
                entity.HasOne(t => t.Account)
                      .WithMany()
                      .HasForeignKey(t => t.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: Transaction -> User (CASCADE)
                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK: Transaction -> Category (RESTRICT)
                entity.HasOne(t => t.Category)
                      .WithMany()
                      .HasForeignKey(t => t.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.UserId);
                entity.HasIndex(t => t.AccountId);
                entity.HasIndex(t => t.CategoryId);
                entity.HasIndex(t => t.TransactionDate);
                entity.HasIndex(t => new { t.UserId, t.TransactionDate });
            });

            //Default Budgets Table

            modelBuilder.Entity<DefaultBudget>(entity =>
            {
                entity.ToTable("DefaultBudgets");

                // ── Primary key ────────────────────────────────────────────────
                entity.HasKey(e => e.DefaultBudgetId);

                // ── CHECK constraints ──────────────────────────────────────────
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "chk_defaultbudgets_targetamount_positive",
                        "\"TargetAmount\" > 0");

                    t.HasCheckConstraint(
                        "chk_defaultbudgets_autocontrib_nonneg",
                        "\"AutoContributeAmount\" >= 0");

                    t.HasCheckConstraint(
                        "chk_defaultbudgets_currency",
                        "\"CurrencyCode\" IN ('INR','USD','EUR')");

                    t.HasCheckConstraint(
                        "chk_defaultbudgets_effectivemonth_format",
                        "\"EffectiveMonth\" IS NULL " +
                        "OR \"EffectiveMonth\" ~ '^\\d{4}-(0[1-9]|1[0-2])$'");
                });
                
                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName("idx_defaultbudgets_isactive");

                entity.HasIndex(e => e.CategoryId)
                      .HasDatabaseName("idx_defaultbudgets_categoryid");

                // ── Relationships ──────────────────────────────────────────────
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedBy)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(e => e.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.UpdatedBy)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                // ── Column precision ───────────────────────────────────────────
                entity.Property(e => e.TargetAmount)
                      .HasColumnType("decimal(15,2)");

                entity.Property(e => e.AutoContributeAmount)
                      .HasColumnType("decimal(15,2)")
                      .HasDefaultValue(0m);                

                entity.Property(e => e.IsActive)
                      .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });




        }

    }
}
