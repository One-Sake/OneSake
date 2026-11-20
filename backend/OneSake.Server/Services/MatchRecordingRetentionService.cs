using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;

namespace OneSake.Server.Services;

// Deletes match recordings older than 30 days, except the ones a bug report points to.
public sealed class MatchRecordingRetentionService : BackgroundService
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MatchRecordingRetentionService> _logger;

    public MatchRecordingRetentionService(IServiceScopeFactory scopeFactory, ILogger<MatchRecordingRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DeleteOldRecordingsAsync(DateTime.UtcNow, stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task<int> DeleteOldRecordingsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OneSakeDbContext>();
            var cutoff = nowUtc - MaxAge;
            var deleted = await dbContext.MatchRecordings
                .Where(recording => recording.CreatedAtUtc < cutoff
                    && !dbContext.BugReports.Any(report => report.RecordingId == recording.Id))
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} match recordings older than {Days} days.", deleted, MaxAge.TotalDays);
            }

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A missing table (migration not applied yet) or database must not stop the server.
            _logger.LogWarning(exception, "Could not delete old match recordings.");
            return 0;
        }
    }
}
