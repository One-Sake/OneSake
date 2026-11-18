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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();

            dbContext.MatchResults.Add(ToEntity(result));
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Saved match result for room {RoomCode}: {Winner} beat {Loser} ({EndReason}).",
                result.RoomCode,
                result.Winner.DisplayName,
                result.Loser.DisplayName,
                result.EndReason);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not save match result for room {RoomCode}. The game continues without statistics.",
                result.RoomCode);
        }
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
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
