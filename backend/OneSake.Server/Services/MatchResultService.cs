using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;

namespace OneSake.Server.Services;

// Saves finished matches. Registered as singleton (like MatchRoomManager), so it
// creates its own scope for the scoped DbContext.
// Saving must never break a match: every error is only logged as a warning.
public sealed class MatchResultService
{
    private const int MaxNameLength = 64;
    private const int MaxLeaderIdLength = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MatchResultService> _logger;
    // One save at a time, so two matches of the same player cannot overwrite each other's rating.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public MatchResultService(IServiceScopeFactory scopeFactory, ILogger<MatchResultService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Runs in the background, so a slow or missing database does not delay the game.
    public void SaveInBackground(MatchResultData result)
    {
        _ = Task.Run(() => SaveAsync(result));
    }

    public async Task SaveAsync(MatchResultData result)
    {
        await _saveLock.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();

            // Match result and both ratings are saved in ONE transaction.
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var entity = ToEntity(result);
            var ratingChanged = await TryApplyRatingAsync(dbContext, entity);
            dbContext.MatchResults.Add(entity);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Saved match result for room {RoomCode}: {Winner} beat {Loser} ({EndReason}, rating changed: {RatingChanged}).",
                result.RoomCode,
                result.Winner.DisplayName,
                result.Loser.DisplayName,
                result.EndReason,
                ratingChanged);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not save match result for room {RoomCode}. The game continues without statistics.",
                result.RoomCode);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Updates both PlayerRatings and the rating fields of the match when the match counts.
    private static async Task<bool> TryApplyRatingAsync(OneSakeDbContext dbContext, MatchResult entity)
    {
        if (!RankingRules.IsEligible(entity))
        {
            return false;
        }

        var winnerId = entity.WinnerGuestId!;
        var loserId = entity.LoserGuestId!;
        var since = entity.EndedAtUtc - RankingRules.PairWindow;
        var recentPairGames = await dbContext.MatchResults.CountAsync(result =>
            result.WinnerRatingAfter != null
            && result.EndedAtUtc >= since
            && ((result.WinnerGuestId == winnerId && result.LoserGuestId == loserId)
                || (result.WinnerGuestId == loserId && result.LoserGuestId == winnerId)));

        if (recentPairGames >= RankingRules.MaxGamesPerPairPerWindow)
        {
            return false;
        }

        var winner = await LoadOrCreateRatingAsync(dbContext, winnerId, entity.WinnerDisplayName);
        var loser = await LoadOrCreateRatingAsync(dbContext, loserId, entity.LoserDisplayName);
        RankingRules.Apply(entity, winner, loser);
        winner.FavoriteLeaderCardId = await FindFavoriteLeaderAsync(dbContext, winnerId, entity.WinnerLeaderCardId);
        loser.FavoriteLeaderCardId = await FindFavoriteLeaderAsync(dbContext, loserId, entity.LoserLeaderCardId);
        return true;
    }

    private static async Task<PlayerRating> LoadOrCreateRatingAsync(OneSakeDbContext dbContext, string guestId, string displayName)
    {
        var rating = await dbContext.PlayerRatings.FindAsync(guestId);
        if (rating is not null)
        {
            return rating;
        }

        rating = new PlayerRating
        {
            GuestId = guestId,
            DisplayName = displayName,
            Rating = RatingCalculator.StartRating,
            Peak = RatingCalculator.StartRating
        };
        dbContext.PlayerRatings.Add(rating);
        return rating;
    }

    // Most played leader in rated matches (including the match that is being saved).
    private static async Task<string?> FindFavoriteLeaderAsync(OneSakeDbContext dbContext, string guestId, string currentLeader)
    {
        var asWinner = await dbContext.MatchResults
            .Where(result => result.WinnerRatingAfter != null && result.WinnerGuestId == guestId)
            .Select(result => result.WinnerLeaderCardId)
            .ToListAsync();
        var asLoser = await dbContext.MatchResults
            .Where(result => result.WinnerRatingAfter != null && result.LoserGuestId == guestId)
            .Select(result => result.LoserLeaderCardId)
            .ToListAsync();

        return asWinner
            .Concat(asLoser)
            .Append(currentLeader)
            .Where(leader => !string.IsNullOrWhiteSpace(leader))
            .GroupBy(leader => leader)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    public static MatchResult ToEntity(MatchResultData result)
    {
        return new MatchResult
        {
            Id = Guid.NewGuid(),
            RoomCode = result.RoomCode,
            StartedAtUtc = result.StartedAtUtc.UtcDateTime,
            EndedAtUtc = result.EndedAtUtc.UtcDateTime,
            TurnCount = result.TurnCount,
            EndReason = result.EndReason,
            WinnerGuestId = result.Winner.GuestId,
            WinnerDisplayName = Truncate(result.Winner.DisplayName, MaxNameLength),
            WinnerLeaderCardId = Truncate(result.Winner.LeaderCardId, MaxLeaderIdLength),
            LoserGuestId = result.Loser.GuestId,
            LoserDisplayName = Truncate(result.Loser.DisplayName, MaxNameLength),
            LoserLeaderCardId = Truncate(result.Loser.LeaderCardId, MaxLeaderIdLength),
            IsRanked = result.IsRanked,
            FirstPlayerGuestId = result.FirstPlayerGuestId,
            WinnerWentFirst = result.WinnerWentFirst,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
