using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class MariaDbContext : DbContext
{
    public MariaDbContext(DbContextOptions<MariaDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TimeRecord> TimeRecords => Set<TimeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // === Users ===
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // === TimeRecords ===
        modelBuilder.Entity<TimeRecord>(entity =>
        {
            entity.ToTable("time_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.RecordedAt).HasColumnName("recorded_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("char(36)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.CreatedBy);
        });
    }
}