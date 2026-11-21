namespace OneSake.Server.Match;

public static class MatchEndReasons
{
    public const string Life = "LIFE";
    public const string DeckOut = "DECK_OUT";
    public const string Concede = "CONCEDE";
    public const string Disconnect = "DISCONNECT";
    // Two own turns in a row ran out of time (turn time and time bank).
    public const string Timeout = "TIMEOUT";
}

public sealed record MatchPlayerResult(string? GuestId, string DisplayName, string LeaderCardId);

// Everything needed to store one finished match. Created by MatchRoom, saved by MatchResultService.
public sealed record MatchResultData(
    string RoomCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    int TurnCount,
    string EndReason,
    MatchPlayerResult Winner,
    MatchPlayerResult Loser,
    // Quick Match rooms are ranked, private tables are casual.
    bool IsRanked = false,
    string? FirstPlayerGuestId = null,
    bool? WinnerWentFirst = null);
