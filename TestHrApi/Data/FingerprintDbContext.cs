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
    }
}
