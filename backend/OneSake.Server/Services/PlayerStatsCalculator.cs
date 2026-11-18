using OneSake.Persistence.Entities;

namespace OneSake.Server.Services;

public sealed record LeaderStatsDto(string LeaderCardId, int Matches, int Wins);

public sealed record RecentMatchDto(
    DateTime PlayedAtUtc,
    string OpponentName,
    string OwnLeaderCardId,
    string OpponentLeaderCardId,
    bool Won,
    string EndReason,
    int TurnCount);

public sealed record PlayerStatsDto(
    int Matches,
    int Wins,
    int Losses,
    // Percent from 0 to 100, rounded to one decimal.
    double WinRate,
    string? FavoriteLeaderCardId,
    IReadOnlyList<LeaderStatsDto> Leaders,
    IReadOnlyList<RecentMatchDto> Recent);

// Pure calculation without database access, so it is easy to test.
public static class PlayerStatsCalculator
{
    public const int RecentMatchCount = 10;

    public static PlayerStatsDto Empty { get; } = new(0, 0, 0, 0, null, [], []);

    public static PlayerStatsDto Calculate(string guestId, IEnumerable<MatchResult> results)
    {
        // One row per match from the view of this player.
        var ownMatches = results
            .Select(result => ToOwnView(guestId, result))
            .Where(match => match is not null)
            .Select(match => match!)
            .OrderByDescending(match => match.PlayedAtUtc)
            .ToList();

        if (ownMatches.Count == 0)
        {
            return Empty;
        }

        var wins = ownMatches.Count(match => match.Won);
        var leaders = ownMatches
            .Where(match => !string.IsNullOrWhiteSpace(match.OwnLeaderCardId))
            .GroupBy(match => match.OwnLeaderCardId)
            .Select(group => new LeaderStatsDto(group.Key, group.Count(), group.Count(match => match.Won)))
            .OrderByDescending(leader => leader.Matches)
            .ThenByDescending(leader => leader.Wins)
            .ThenBy(leader => leader.LeaderCardId, StringComparer.Ordinal)
            .ToList();

        return new PlayerStatsDto(
            Matches: ownMatches.Count,
            Wins: wins,
            Losses: ownMatches.Count - wins,
            WinRate: Math.Round(100.0 * wins / ownMatches.Count, 1),
            FavoriteLeaderCardId: leaders.FirstOrDefault()?.LeaderCardId,
            Leaders: leaders,
            Recent: ownMatches.Take(RecentMatchCount).ToList());
    }

    private static RecentMatchDto? ToOwnView(string guestId, MatchResult result)
    {
        var won = string.Equals(result.WinnerGuestId, guestId, StringComparison.OrdinalIgnoreCase);
        var lost = string.Equals(result.LoserGuestId, guestId, StringComparison.OrdinalIgnoreCase);
        if (won == lost)
        {
            // Not this player's match (or both sides used the same ID, which should not count).
            return null;
        }

        return new RecentMatchDto(
            PlayedAtUtc: result.EndedAtUtc,
            OpponentName: won ? result.LoserDisplayName : result.WinnerDisplayName,
            OwnLeaderCardId: won ? result.WinnerLeaderCardId : result.LoserLeaderCardId,
            OpponentLeaderCardId: won ? result.LoserLeaderCardId : result.WinnerLeaderCardId,
            Won: won,
            EndReason: result.EndReason,
            TurnCount: result.TurnCount);
    }
}
