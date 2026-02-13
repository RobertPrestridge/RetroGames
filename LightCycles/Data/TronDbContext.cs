using LightCycles.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LightCycles.Data;

public class TronDbContext : DbContext
{
    public TronDbContext(DbContextOptions<TronDbContext> options) : base(options) { }

    public DbSet<TronMatch> TronMatches => Set<TronMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TronMatch>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ShortCode)
                .HasMaxLength(8)
                .IsRequired();

            entity.Property(e => e.Player1Name)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Player2Name)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.WinnerName)
                .HasMaxLength(20);

            entity.Property(e => e.StartedAt).HasColumnType("datetime2");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime2");

            entity.HasIndex(e => e.ShortCode);
        });
    }
}
