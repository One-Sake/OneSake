namespace OneSake.Server.Match;

// The clock of the match rooms. Tests use a fake clock to let deadlines run out.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

// Settings of every match room (see "Match" in appsettings.json and docs/deployment.md).
public sealed record MatchRoomOptions
{
    // How long a disconnected player keeps the seat in a running match.
    public TimeSpan ReconnectGrace { get; init; } = TimeSpan.FromSeconds(90);

    // Total disconnected time per player and match, so nobody wins time by disconnecting again and again.
    public TimeSpan ReconnectBudget { get; init; } = TimeSpan.FromSeconds(180);

    // How long a disconnected player keeps the seat in the lobby (match not started).
    public TimeSpan LobbyReconnectGrace { get; init; } = TimeSpan.FromSeconds(30);
}

// Configuration section "Match" (seconds, so the values are easy to set as environment variables).
public sealed class MatchSettings
{
    public const string SectionName = "Match";

    public int ReconnectGraceSeconds { get; set; } = 90;
    public int ReconnectBudgetSeconds { get; set; } = 180;
    public int LobbyReconnectGraceSeconds { get; set; } = 30;

    public MatchRoomOptions ToOptions()
    {
        return new MatchRoomOptions
        {
            ReconnectGrace = TimeSpan.FromSeconds(Math.Max(1, ReconnectGraceSeconds)),
            ReconnectBudget = TimeSpan.FromSeconds(Math.Max(0, ReconnectBudgetSeconds)),
            LobbyReconnectGrace = TimeSpan.FromSeconds(Math.Max(1, LobbyReconnectGraceSeconds))
        };
    }
}
