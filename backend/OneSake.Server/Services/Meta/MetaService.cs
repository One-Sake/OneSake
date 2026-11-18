using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OneSake.Persistence;

namespace OneSake.Server.Services.Meta;

public sealed record MetaResult(MetaSnapshot? Snapshot, bool IsStale);

// Loads meta snapshots from the configured provider and caches them (6 hours).
// If the provider fails, the last successful snapshot is returned with IsStale = true.
public sealed class MetaService
{
    // After a failure, wait a bit before asking the provider again.
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly MetaOptions _options;
    private readonly ILogger<MetaService> _logger;
    private readonly ConcurrentDictionary<string, MetaSnapshot> _lastGood = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public MetaService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        IOptions<MetaOptions> options,
        ILogger<MetaService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public MetaOptions Options => _options;

    public async Task<MetaResult> GetAsync(string? format, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!_options.Enabled)
        {
            return new MetaResult(null, false);
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? _options.Format : format.Trim().ToLowerInvariant();
        var cacheKey = $"meta:{normalizedFormat}";

        if (!forceRefresh && _cache.TryGetValue(cacheKey, out MetaResult? cached) && cached is not null)
        {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IMetaProvider>();
            var snapshot = await provider.GetLatestAsync(normalizedFormat, cancellationToken);

            MetaResult result;
            if (snapshot is not null)
            {
                snapshot = await ResolveMissingCardIdsAsync(scope.ServiceProvider, snapshot, cancellationToken);
                _lastGood[normalizedFormat] = snapshot;
                result = new MetaResult(snapshot, false);
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_options.CacheHours));
            }
            else
            {
                result = new MetaResult(_lastGood.GetValueOrDefault(normalizedFormat), true);
                _cache.Set(cacheKey, result, FailureRetryDelay);
            }

            return result;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    // LeaderCardId: taken from the provider when it has one. Otherwise the leader name is looked
    // up in our card database (Leader cards only). Not unique -> null (shown without image).
    private async Task<MetaSnapshot> ResolveMissingCardIdsAsync(
        IServiceProvider services,
        MetaSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var missingNames = snapshot.Leaders
            .Where(leader => leader.LeaderCardId is null)
            .Select(leader => leader.LeaderName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingNames.Count == 0)
        {
            return snapshot;
        }

        Dictionary<string, string?> resolved;
        try
        {
            var dbContext = services.GetRequiredService<OneSakeDbContext>();
            var matches = await dbContext.Cards.AsNoTracking()
                .Where(card => card.Type == "Leader" && missingNames.Contains(card.CardName))
                .Select(card => new { card.CardName, card.CardId })
                .ToListAsync(cancellationToken);

            resolved = matches
                .GroupBy(card => card.CardName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var ids = group.Select(card => card.CardId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        return ids.Count == 1 ? ids[0] : null;
                    },
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not resolve meta leader names against the card database.");
            return snapshot;
        }

        string? Lookup(string name, string? current) => current ?? resolved.GetValueOrDefault(name);
        var idsByKey = snapshot.Leaders.ToDictionary(
            leader => leader.LeaderKey,
            leader => Lookup(leader.LeaderName, leader.LeaderCardId),
            StringComparer.Ordinal);

        return snapshot with
        {
            Leaders = snapshot.Leaders.Select(leader => leader with
            {
                LeaderCardId = idsByKey[leader.LeaderKey],
                Matchups = leader.Matchups.Select(matchup => matchup with
                {
                    OpponentCardId = matchup.OpponentCardId ?? idsByKey.GetValueOrDefault(matchup.OpponentKey)
                }).ToList()
            }).ToList()
        };
    }
}
