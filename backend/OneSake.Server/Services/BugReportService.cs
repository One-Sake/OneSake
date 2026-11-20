using Microsoft.EntityFrameworkCore;
using OneSake.Game.Replay;
using OneSake.Persistence;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;

namespace OneSake.Server.Services;

public sealed record BugReportRequest
{
    public string RoomCode { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? CardId { get; init; }
    public int AtSeq { get; init; }
}

public sealed record BugReportListItem(
    int Id,
    string Category,
    string? CardId,
    string Description,
    string RoomCode,
    int AtSeq,
    bool HasRecording,
    DateTime CreatedAtUtc);

// Saving and listing bug reports (EF in the server, a fake in the tests).
public interface IBugReportStore
{
    // Saves the report together with the recording (if any). Returns the report number.
    Task<int> SaveAsync(BugReport report, MatchRecording? recording, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BugReportListItem>> ListAsync(int take, CancellationToken cancellationToken = default);
}

public sealed class EfBugReportStore : IBugReportStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfBugReportStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<int> SaveAsync(BugReport report, MatchRecording? recording, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (recording is not null)
        {
            var entity = new MatchRecordingEntity
            {
                Id = Guid.NewGuid(),
                RoomCode = report.RoomCode,
                Data = MatchRecordingJson.Compress(recording),
                CreatedAtUtc = report.CreatedAtUtc
            };
            dbContext.MatchRecordings.Add(entity);
            report.RecordingId = entity.Id;
        }

        dbContext.BugReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return report.Id;
    }

    public async Task<IReadOnlyList<BugReportListItem>> ListAsync(int take, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();
        return await dbContext.BugReports.AsNoTracking()
            .OrderByDescending(report => report.CreatedAtUtc)
            .Take(take)
            .Select(report => new BugReportListItem(
                report.Id,
                report.Category,
                report.CardId,
                report.Description,
                report.RoomCode,
                report.AtSeq,
                report.RecordingId != null,
                report.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public sealed record BugReportResult(int? Id, string? Error, bool RateLimited = false)
{
    public static BugReportResult Invalid(string error) => new(null, error);
}

// "Fehler melden": validates the input, applies the rate limit and stores the report with the
// recording of the match up to now. Nothing is sent to other services.
public sealed class BugReportService
{
    public const int MaxDescriptionLength = 1000;
    public const int MaxReportsPerHour = 5;
    public static readonly IReadOnlyList<string> Categories = ["EFFECT", "ORDER", "STUCK", "DISPLAY", "OTHER"];

    private readonly MatchRoomManager _rooms;
    private readonly IBugReportStore _store;
    private readonly ILogger<BugReportService> _logger;
    private readonly Func<DateTime> _clock;
    private readonly Dictionary<string, Queue<DateTime>> _recentReports = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public BugReportService(MatchRoomManager rooms, IBugReportStore store, ILogger<BugReportService> logger, Func<DateTime>? clock = null)
    {
        _rooms = rooms;
        _store = store;
        _logger = logger;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    // clientKey: the guest ID, or the IP address for clients without one.
    public async Task<BugReportResult> SubmitAsync(BugReportRequest request, string clientKey, CancellationToken cancellationToken = default)
    {
        var roomCode = (request.RoomCode ?? string.Empty).Trim().ToUpperInvariant();
        if (roomCode.Length is 0 or > 12 || !roomCode.All(char.IsLetterOrDigit))
        {
            return BugReportResult.Invalid("roomCode is invalid.");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            return BugReportResult.Invalid("Please describe the problem.");
        }

        if (description.Length > MaxDescriptionLength)
        {
            description = description[..MaxDescriptionLength];
        }

        var category = (request.Category ?? string.Empty).Trim().ToUpperInvariant();
        if (!Categories.Contains(category))
        {
            return BugReportResult.Invalid($"category must be one of {string.Join(", ", Categories)}.");
        }

        var cardId = string.IsNullOrWhiteSpace(request.CardId) ? null : request.CardId.Trim().ToUpperInvariant();
        if (cardId is not null && (cardId.Length > 50 || !cardId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')))
        {
            return BugReportResult.Invalid("cardId is invalid.");
        }

        var now = _clock();
        if (!TryCountReport(clientKey, now))
        {
            return new BugReportResult(null, $"Too many reports - at most {MaxReportsPerHour} per hour.", RateLimited: true);
        }

        // The match up to now (still running, or the last match of this room).
        var recording = _rooms.Get(roomCode)?.CreateRecordingSnapshot();
        var atSeq = Math.Clamp(request.AtSeq, 0, recording?.Inputs.Count ?? 0);

        var report = new BugReport
        {
            RoomCode = roomCode,
            GuestId = GuestIdValidator.Normalize(request.GuestId),
            Description = description,
            Category = category,
            CardId = cardId,
            AtSeq = atSeq,
            CreatedAtUtc = now
        };

        try
        {
            var id = await _store.SaveAsync(report, recording, cancellationToken);
            _logger.LogInformation("Bug report #{Id} ({Category}) for room {RoomCode} at seq {AtSeq}.", id, category, roomCode, atSeq);
            return new BugReportResult(id, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not save a bug report for room {RoomCode}.", roomCode);
            return new BugReportResult(null, "The report could not be saved. Please try again later.");
        }
    }

    // Sliding window: at most MaxReportsPerHour per client and hour.
    private bool TryCountReport(string clientKey, DateTime now)
    {
        lock (_sync)
        {
            if (!_recentReports.TryGetValue(clientKey, out var times))
            {
                times = new Queue<DateTime>();
                _recentReports[clientKey] = times;
            }

            while (times.Count > 0 && now - times.Peek() >= TimeSpan.FromHours(1))
            {
                times.Dequeue();
            }

            if (times.Count >= MaxReportsPerHour)
            {
                return false;
            }

            times.Enqueue(now);
            return true;
        }
    }
}
