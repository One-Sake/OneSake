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
    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();
    public DbSet<MatchRecordingEntity> MatchRecordings => Set<MatchRecordingEntity>();
    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<Account> Accounts => Set<Account>();

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

            entity.Property(r => r.FirstPlayerGuestId).HasMaxLength(36);
            // The leaderboard and the "same pair in 24 h" rule look at the newest matches.
            entity.HasIndex(r => r.EndedAtUtc);
        });

        modelBuilder.Entity<MatchRecordingEntity>(entity =>
        {
            entity.ToTable("MatchRecordings");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.MatchResultId);
            // The retention job deletes old recordings.
            entity.HasIndex(r => r.CreatedAtUtc);
            entity.Property(r => r.RoomCode).HasMaxLength(12).IsRequired();
            entity.Property(r => r.Data).IsRequired();
        });

        modelBuilder.Entity<BugReport>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.CreatedAtUtc);
            entity.HasIndex(r => r.GuestId);
            entity.HasIndex(r => r.RecordingId);
            entity.Property(r => r.RoomCode).HasMaxLength(12).IsRequired();
            entity.Property(r => r.GuestId).HasMaxLength(36);
            entity.Property(r => r.Description).HasMaxLength(1000).IsRequired();
            entity.Property(r => r.Category).HasMaxLength(20).IsRequired();
            entity.Property(r => r.CardId).HasMaxLength(50);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);

            // Every username and every player ID exists only once.
            entity.HasIndex(a => a.NormalizedUsername).IsUnique();
            entity.HasIndex(a => a.PlayerId).IsUnique();

            entity.Property(a => a.Username).HasMaxLength(20).IsRequired();
            entity.Property(a => a.NormalizedUsername).HasMaxLength(20).IsRequired();
            entity.Property(a => a.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(a => a.RecoveryCodeHash).HasMaxLength(200).IsRequired();
            entity.Property(a => a.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.Property(a => a.PlayerId).HasMaxLength(36).IsRequired();
        });

        modelBuilder.Entity<PlayerRating>(entity =>
        {
            entity.HasKey(r => r.GuestId);
            entity.HasIndex(r => r.Rating);

            entity.Property(r => r.GuestId).HasMaxLength(36);
            entity.Property(r => r.DisplayName).HasMaxLength(64).IsRequired();
            entity.Property(r => r.FavoriteLeaderCardId).HasMaxLength(50);
        });
    }
}