using Microsoft.EntityFrameworkCore;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Data;

public class LogQueueDbContext : DbContext
{
    private readonly string _databasePath;

    public LogQueueDbContext(string databasePath)
    {
        _databasePath = databasePath;
    }

    public DbSet<QueuedLog> QueuedLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueuedLog>(entity =>
        {
            entity.ToTable("QueuedLogs");
            
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QueuedAt);
            entity.HasIndex(e => new { e.Status, e.RetryCount });
            
            entity.Property(e => e.EmployeeId)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.BranchId)
                .IsRequired();
            
            entity.Property(e => e.LastError)
                .HasMaxLength(500);
        });
    }
}
