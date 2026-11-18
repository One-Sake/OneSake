using Microsoft.AspNetCore.Mvc;
using OneSake.Server.Services.Meta;

namespace OneSake.Server.Controllers;

public sealed record MetaStatusDto(
    bool Enabled,
    string Provider,
    string AttributionName,
    string AttributionUrl,
    DateOnly? SnapshotDate,
    DateTimeOffset? FetchedAtUtc,
    bool IsStale,
    IReadOnlyList<string> Formats);

public sealed record MetaLeaderSummaryDto(
    string LeaderKey,
    string LeaderName,
    string? LeaderCardId,
    int Games,
    double? WinRate,
    double? FirstWinRate,
    double? SecondWinRate,
    int MatchupCount);

public sealed record MetaSourceDto(
    string Provider,
    string AttributionName,
    string AttributionUrl,
    DateOnly SnapshotDate,
    DateTimeOffset FetchedAtUtc,
    bool IsStale);

public sealed record MetaLeadersDto(bool Enabled, MetaSourceDto? Source, IReadOnlyList<MetaLeaderSummaryDto> Leaders);

public sealed record MetaLeaderDetailDto(bool Enabled, MetaSourceDto? Source, MetaLeader? Leader);

public sealed record MetaMatrixLeaderDto(string LeaderKey, string LeaderName, string? LeaderCardId, int Games);

public sealed record MetaMatrixCellDto(
    string LeaderKey,
    string OpponentKey,
    int Games,
    int FirstGames,
    int SecondGames,
    double? WinRate,
    double? FirstWinRate,
    double? SecondWinRate);

public sealed record MetaMatchupsDto(
    bool Enabled,
    MetaSourceDto? Source,
    int MinGames,
    IReadOnlyList<MetaMatrixLeaderDto> Leaders,
    IReadOnlyList<MetaMatrixCellDto> Cells);

// Base Route -> /meta. ONLY data of the external provider, never our own match results.
// Disabled meta returns 200 with enabled = false and empty data.
[ApiController]
[Route("[controller]")]
public class MetaController : ControllerBase
{
    private readonly MetaService _metaService;

    public MetaController(MetaService metaService)
    {
        _metaService = metaService;
    }

    // GET /meta/status
    [HttpGet("status")]
    public async Task<ActionResult<MetaStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var options = _metaService.Options;
        if (!options.Enabled)
        {
            return Ok(new MetaStatusDto(false, options.Provider, options.AttributionName, options.AttributionUrl, null, null, false, []));
        }

        var result = await _metaService.GetAsync(options.Format, cancellationToken);
        return Ok(new MetaStatusDto(
            true,
            options.Provider,
            options.AttributionName,
            options.AttributionUrl,
            result.Snapshot?.SnapshotDate,
            result.Snapshot?.FetchedAtUtc,
            result.IsStale,
            options.Formats));
    }

    // GET /meta/leaders?format=...
    [HttpGet("leaders")]
    public async Task<ActionResult<MetaLeadersDto>> GetLeaders([FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (enabled, result, error) = await LoadAsync(format, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (!enabled)
        {
            return Ok(new MetaLeadersDto(false, null, []));
        }

        var leaders = result!.Snapshot!.Leaders
            .OrderByDescending(leader => leader.Games)
            .Select(leader => new MetaLeaderSummaryDto(
                leader.LeaderKey,
                leader.LeaderName,
                leader.LeaderCardId,
                leader.Games,
                leader.WinRate,
                leader.FirstWinRate,
                leader.SecondWinRate,
                leader.Matchups.Count))
            .ToList();

        return Ok(new MetaLeadersDto(true, ToSource(result), leaders));
    }

    // GET /meta/leader/{leaderKey}?format=...
    [HttpGet("leader/{leaderKey}")]
    public async Task<ActionResult<MetaLeaderDetailDto>> GetLeader(
        string leaderKey,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var (enabled, result, error) = await LoadAsync(format, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (!enabled)
        {
            return Ok(new MetaLeaderDetailDto(false, null, null));
        }

        var leader = result!.Snapshot!.Leaders.FirstOrDefault(
            entry => string.Equals(entry.LeaderKey, leaderKey, StringComparison.OrdinalIgnoreCase));
        if (leader is null)
        {
            return NotFound(new { message = $"Leader {leaderKey} is not in the snapshot." });
        }

        var sorted = leader with { Matchups = leader.Matchups.OrderByDescending(matchup => matchup.Games).ToList() };
        return Ok(new MetaLeaderDetailDto(true, ToSource(result), sorted));
    }

    // GET /meta/matchups?format=...&minGames=20&limit=20
    // Rows/columns: the most played leaders. Cells below minGames are left out.
    [HttpGet("matchups")]
    public async Task<ActionResult<MetaMatchupsDto>> GetMatchups(
        [FromQuery] string? format,
        [FromQuery] int minGames = 20,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var (enabled, result, error) = await LoadAsync(format, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var safeMinGames = Math.Clamp(minGames, 0, 100_000);
        if (!enabled)
        {
            return Ok(new MetaMatchupsDto(false, null, safeMinGames, [], []));
        }

        var leaders = result!.Snapshot!.Leaders
            .OrderByDescending(leader => leader.Games)
            .Take(Math.Clamp(limit, 2, 60))
            .ToList();
        var keys = leaders.Select(leader => leader.LeaderKey).ToHashSet(StringComparer.Ordinal);

        var cells = leaders
            .SelectMany(leader => leader.Matchups
                .Where(matchup => keys.Contains(matchup.OpponentKey) && matchup.Games >= safeMinGames)
                .Select(matchup => new MetaMatrixCellDto(
                    leader.LeaderKey,
                    matchup.OpponentKey,
                    matchup.Games,
                    matchup.FirstGames,
                    matchup.SecondGames,
                    matchup.WinRate,
                    matchup.FirstWinRate,
                    matchup.SecondWinRate)))
            .ToList();

        return Ok(new MetaMatchupsDto(
            true,
            ToSource(result),
            safeMinGames,
            leaders.Select(leader => new MetaMatrixLeaderDto(leader.LeaderKey, leader.LeaderName, leader.LeaderCardId, leader.Games)).ToList(),
            cells));
    }

    private async Task<(bool Enabled, MetaResult? Result, ActionResult? Error)> LoadAsync(
        string? format,
        CancellationToken cancellationToken)
    {
        if (!_metaService.Options.Enabled)
        {
            return (false, null, null);
        }

        var result = await _metaService.GetAsync(format, cancellationToken);
        if (result.Snapshot is null)
        {
            return (true, result, StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta data is not available right now." }));
        }

        return (true, result, null);
    }

    private MetaSourceDto ToSource(MetaResult result)
    {
        var options = _metaService.Options;
        var snapshot = result.Snapshot!;
        return new MetaSourceDto(
            snapshot.Provider,
            options.AttributionName,
            options.AttributionUrl,
            snapshot.SnapshotDate,
            snapshot.FetchedAtUtc,
            result.IsStale);
    }
}
