using OneSake.Game.Replay;
using OneSake.Persistence;
using OneSake.Persistence.Entities;

namespace OneSake.Server.Services;

// Stores match recordings (gzip JSON). Like the match results: saving never breaks a match,
// every error is only logged.
public sealed class MatchRecordingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MatchRecordingService> _logger;

    public MatchRecordingService(IServiceScopeFactory scopeFactory, ILogger<MatchRecordingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void SaveInBackground(MatchRecording recording, Guid? matchResultId = null)
    {
        _ = Task.Run(() => SaveAsync(recording, matchResultId));
    }

    // Returns the ID of the stored recording, or null when saving failed.
    public async Task<Guid?> SaveAsync(MatchRecording recording, Guid? matchResultId = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();
            var entity = new MatchRecordingEntity
            {
                Id = Guid.NewGuid(),
                MatchResultId = matchResultId,
                RoomCode = recording.RoomCode.Length > 12 ? recording.RoomCode[..12] : recording.RoomCode,
                Data = MatchRecordingJson.Compress(recording),
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.MatchRecordings.Add(entity);
            await dbContext.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not save the match recording for room {RoomCode}.", recording.RoomCode);
            return null;
        }
    }
}
