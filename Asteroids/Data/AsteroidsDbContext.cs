using Asteroids.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asteroids.Data;

public class AsteroidsDbContext : DbContext
{
    public AsteroidsDbContext(DbContextOptions<AsteroidsDbContext> options) : base(options) { }

    public DbSet<AsteroidMatch> AsteroidMatches => Set<AsteroidMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AsteroidMatch>(entity =>
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
