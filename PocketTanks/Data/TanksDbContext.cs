using PocketTanks.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace PocketTanks.Data;

public class TanksDbContext : DbContext
{
    public TanksDbContext(DbContextOptions<TanksDbContext> options) : base(options) { }

    public DbSet<TanksMatch> TanksMatches => Set<TanksMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TanksMatch>(entity =>
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

            entity.Property(e => e.StartedAt).HasColumnType("datetime2");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime2");

            entity.HasIndex(e => e.ShortCode);
        });
    }
}
