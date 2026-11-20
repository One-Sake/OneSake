namespace OneSake.Persistence.Entities;

// A bug report from a player ("Fehler melden"), with the match recording up to that moment.
public class BugReport
{
    public int Id { get; set; }
    public Guid? RecordingId { get; set; }
    public string RoomCode { get; set; } = "";
    // Not verified (no real login yet), only used for the rate limit and to find reports.
    public string? GuestId { get; set; }
    public string Description { get; set; } = "";
    // EFFECT, ORDER, STUCK, DISPLAY or OTHER
    public string Category { get; set; } = "";
    public string? CardId { get; set; }
    // Number of recorded decisions when the report was sent (replay up to here).
    public int AtSeq { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
