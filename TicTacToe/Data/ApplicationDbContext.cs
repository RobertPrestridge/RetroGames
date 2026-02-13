using Microsoft.EntityFrameworkCore;
using TicTacToe.Data.Entities;

namespace TicTacToe.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ShortCode)
                .HasMaxLength(8)
                .IsRequired();

            entity.Property(e => e.BoardState)
                .HasColumnType("nchar(9)")
                .IsRequired();

            entity.Property(e => e.CurrentTurn)
                .HasColumnType("nchar(1)")
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired();

            entity.Property(e => e.PlayerXConnectionId).HasMaxLength(100);
            entity.Property(e => e.PlayerOConnectionId).HasMaxLength(100);
            entity.Property(e => e.PlayerXSessionId).HasMaxLength(50);
            entity.Property(e => e.PlayerOSessionId).HasMaxLength(50);

            entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime2");

            entity.Property(e => e.RowVersion)
                .IsRowVersion();

            // Indexes
            entity.HasIndex(e => e.ShortCode).IsUnique();
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => e.PlayerXSessionId);
            entity.HasIndex(e => e.PlayerOSessionId);
        });
    }
}
