using Microsoft.EntityFrameworkCore;
using TestHrApi.Models;

namespace TestHrApi.Data;

public class FingerprintDbContext : DbContext
{
    public FingerprintDbContext(DbContextOptions<FingerprintDbContext> options)
        : base(options)
    {
    }

    public DbSet<AttendanceLog> AttendanceLogs { get; set; }
    public DbSet<InstallationKey> InstallationKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AttendanceLog>(entity =>
        {
            entity.ToTable("AttendanceLogs");
            
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.PunchTime);
            
            entity.Property(e => e.EmployeeId)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.PunchTime)
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<InstallationKey>(entity =>
        {
            entity.ToTable("InstallationKeys");
            
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.Used);
            
            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.BranchId)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.BranchName)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(e => e.UsedByIp)
                .HasMaxLength(45);
            
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100);
        });
    }
}
