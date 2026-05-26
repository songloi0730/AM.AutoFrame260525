// -------------------------------------------------------
// File:    AutoMachineDbContext.cs
// Project: AM.Data
// Purpose: EF Core DbContext cho SQLite — AutoMachine database
// -------------------------------------------------------

using AM.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AM.Data;

/// <summary>
/// EF Core DbContext cho AutoMachine.
/// Database: SQLite (không phải SQL Server).
/// Lifetime: Scoped — tạo mới cho mỗi operation.
/// KHÔNG inject trực tiếp vào Service — dùng qua Repository interface.
/// </summary>
public sealed class AutoMachineDbContext : DbContext
{
    public AutoMachineDbContext(DbContextOptions<AutoMachineDbContext> options)
        : base(options)
    { }

    public DbSet<AlarmHistoryEntity> AlarmHistory => Set<AlarmHistoryEntity>();
    public DbSet<ProductionRecordEntity> ProductionRecords => Set<ProductionRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── AlarmHistory ─────────────────────────────────────────────────────
        modelBuilder.Entity<AlarmHistoryEntity>(e =>
        {
            e.ToTable("AlarmHistory");
            e.HasKey(p => p.Id);
            e.Property(p => p.Station).HasMaxLength(100).IsRequired();
            e.Property(p => p.Message).HasMaxLength(500);
            e.Property(p => p.AcknowledgedBy).HasMaxLength(100);

            // Indexes bắt buộc theo R-DB-03
            e.HasIndex(p => p.Timestamp);
            e.HasIndex(p => new { p.AlarmCode, p.Station });
        });

        // ─── ProductionRecords ────────────────────────────────────────────────
        modelBuilder.Entity<ProductionRecordEntity>(e =>
        {
            e.ToTable("ProductionRecords");
            e.HasKey(p => p.Id);
            e.Property(p => p.SerialNumber).HasMaxLength(100).IsRequired();
            e.Property(p => p.RecipeName).HasMaxLength(100);
            e.Property(p => p.OperatorId).HasMaxLength(100);
            e.Property(p => p.FailReason).HasMaxLength(300);

            // Indexes bắt buộc theo R-DB-03
            e.HasIndex(p => p.Timestamp);
            e.HasIndex(p => p.SerialNumber);
        });
    }
}
