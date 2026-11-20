namespace OneSake.Persistence.Entities;

// The recording of one match (seed, decks, every decision) as gzip-compressed JSON.
// Deleted after 30 days unless a bug report points to it (MatchRecordingRetentionService).
public class MatchRecordingEntity
{
    public Guid Id { get; set; }
    // Null for matches without a result (draw) and for snapshots of a running match (bug report).
    public Guid? MatchResultId { get; set; }
    public string RoomCode { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
