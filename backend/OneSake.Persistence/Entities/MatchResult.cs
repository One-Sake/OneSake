namespace OneSake.Persistence.Entities;

// One finished match. Guest IDs come from the browser and are not verified (no real login yet).
public class MatchResult
{
    public Guid Id { get; set; }

    public string RoomCode { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int TurnCount { get; set; }

    // LIFE, DECK_OUT, CONCEDE or DISCONNECT
    public string EndReason { get; set; } = "";

    public string? WinnerGuestId { get; set; }
    public string WinnerDisplayName { get; set; } = "";
    public string WinnerLeaderCardId { get; set; } = "";

    public string? LoserGuestId { get; set; }
    public string LoserDisplayName { get; set; } = "";
    public string LoserLeaderCardId { get; set; } = "";
}
