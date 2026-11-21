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

    // Turn timer values for the "Standard" mode ("Relaxed" doubles them).
    public MatchTimerSettings Timer { get; init; } = new();
}

// Timer of a room: Quick Match always Standard, private tables choose when they are created.
public enum TimerMode
{
    Standard,
    // All times doubled.
    Relaxed,
    Off
}

// Configuration section "Match:Timer" (seconds).
public sealed record MatchTimerSettings
{
    // Time per own turn; after that the time bank is used.
    public int TurnSeconds { get; set; } = 90;
    // Time for a decision outside the own turn (block, counter, trigger, opponent's effect...).
    public int ResponseSeconds { get; set; } = 25;
    // Mulligan and the turn order decision have no time bank.
    public int MulliganSeconds { get; set; } = 30;
    public int TurnOrderSeconds { get; set; } = 20;
    public int TimeBankStartSeconds { get; set; } = 180;
    public int TimeBankBonusSeconds { get; set; } = 10;
    public int TimeBankMaxSeconds { get; set; } = 240;
    // Lag/animation tolerance: a deadline only counts as over this much later.
    public int ToleranceSeconds { get; set; } = 2;
}

// Configuration section "Match" (seconds, so the values are easy to set as environment variables).
public sealed class MatchSettings
{
    public const string SectionName = "Match";

    public int ReconnectGraceSeconds { get; set; } = 90;
    public int ReconnectBudgetSeconds { get; set; } = 180;
    public int LobbyReconnectGraceSeconds { get; set; } = 30;
    public MatchTimerSettings Timer { get; set; } = new();

    public MatchRoomOptions ToOptions()
    {
        return new MatchRoomOptions
        {
            ReconnectGrace = TimeSpan.FromSeconds(Math.Max(1, ReconnectGraceSeconds)),
            ReconnectBudget = TimeSpan.FromSeconds(Math.Max(0, ReconnectBudgetSeconds)),
            LobbyReconnectGrace = TimeSpan.FromSeconds(Math.Max(1, LobbyReconnectGraceSeconds)),
            Timer = Timer
        };
    }
}
