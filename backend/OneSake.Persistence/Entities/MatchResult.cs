namespace OneSake.Persistence.Entities;

// One finished match. Guest IDs come from the browser and are not verified (no real login yet).
public class MatchResult
{
    public Guid Id { get; set; }

    public string RoomCode { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int TurnCount { get; set; }

    // LIFE, DECK_OUT, CONCEDE, DISCONNECT or TIMEOUT
    public string EndReason { get; set; } = "";

    public string? WinnerGuestId { get; set; }
    public string WinnerDisplayName { get; set; } = "";
    public string WinnerLeaderCardId { get; set; } = "";

    public string? LoserGuestId { get; set; }
    public string LoserDisplayName { get; set; } = "";
    public string LoserLeaderCardId { get; set; } = "";

    // True only for Quick Match rooms. Casual (private) tables never change the rating.
    public bool IsRanked { get; set; }

    // Turn order decided by the dice roll (null for older matches or when the match ended before it).
    public string? FirstPlayerGuestId { get; set; }
    public bool? WinnerWentFirst { get; set; }

    // Set only when the match changed the rating (see RankingRules).
    public int? WinnerRatingBefore { get; set; }
    public int? WinnerRatingAfter { get; set; }
    public int? LoserRatingBefore { get; set; }
    public int? LoserRatingAfter { get; set; }
}
