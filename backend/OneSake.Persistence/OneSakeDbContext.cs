using Microsoft.EntityFrameworkCore;
using OneSake.Persistence.Entities;

namespace OneSake.Persistence;

public class OneSakeDbContext : DbContext
{
    public OneSakeDbContext(DbContextOptions<OneSakeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.HasIndex(c => c.CardId);
            entity.HasIndex(c => c.SetId);
            entity.HasIndex(c => c.CardName);

            entity.Property(c => c.CardId).HasMaxLength(50).IsRequired();
            entity.Property(c => c.CardName).HasMaxLength(200).IsRequired();

            entity.Property(c => c.SetId).HasMaxLength(50);
            entity.Property(c => c.SetName).HasMaxLength(200);
            entity.Property(c => c.Rarity).HasMaxLength(50);
            entity.Property(c => c.Color).HasMaxLength(50);
            entity.Property(c => c.Type).HasMaxLength(50);
            entity.Property(c => c.Attribute).HasMaxLength(50);

            entity.Property(c => c.InventoryPrice).HasColumnType("numeric(10,2)");
            entity.Property(c => c.MarketPrice).HasColumnType("numeric(10,2)");
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.HasKey(r => r.Id);

            // Profile statistics look up matches by guest ID.
            entity.HasIndex(r => r.WinnerGuestId);
            entity.HasIndex(r => r.LoserGuestId);

            entity.Property(r => r.RoomCode).HasMaxLength(12).IsRequired();
            entity.Property(r => r.EndReason).HasMaxLength(20).IsRequired();

            entity.Property(r => r.WinnerGuestId).HasMaxLength(36);
            entity.Property(r => r.WinnerDisplayName).HasMaxLength(64).IsRequired();
            entity.Property(r => r.WinnerLeaderCardId).HasMaxLength(50).IsRequired();

            entity.Property(r => r.LoserGuestId).HasMaxLength(36);
            entity.Property(r => r.LoserDisplayName).HasMaxLength(64).IsRequired();
            entity.Property(r => r.LoserLeaderCardId).HasMaxLength(50).IsRequired();
        });
    }
}