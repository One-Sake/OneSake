using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using OneSake.Server.Auth;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Server.Controllers;

// Base Route -> /stats
[ApiController]
[Route("[controller]")]
public class StatsController : ControllerBase
{
    private readonly OneSakeDbContext _dbContext;
    private readonly IAccountStore _accounts;
    private readonly ILogger<StatsController> _logger;

    public StatsController(OneSakeDbContext dbContext, IAccountStore accounts, ILogger<StatsController> logger)
    {
        _dbContext = dbContext;
        _accounts = accounts;
        _logger = logger;
    }

    // GET /stats/me?guestId=...
    // Logged in: the statistics of the account (the guestId parameter is ignored).
    // Guest: the statistics of the guest ID, unless an account has taken that ID over.
    [HttpGet("me")]
    public async Task<ActionResult<PlayerStatsDto>> GetOwnStats([FromQuery] string? guestId, CancellationToken cancellationToken)
    {
        var playerId = await PlayerIdentity.ResolveAsync(User, guestId, id => _accounts.IsPlayerIdTakenAsync(id, cancellationToken));
        return await LoadStatsAsync(playerId, cancellationToken);
    }

    // GET /stats/player/{guestId}
    // Unknown or invalid IDs return empty statistics (no 404), so a new player simply sees zeros.
    [HttpGet("player/{guestId}")]
    public async Task<ActionResult<PlayerStatsDto>> GetPlayerStats(string guestId, CancellationToken cancellationToken)
    {
        // An ID that belongs to an account is not public as a guest ID any more.
        var playerId = await PlayerIdentity.ResolveAsync(null, guestId, id => _accounts.IsPlayerIdTakenAsync(id, cancellationToken));
        return await LoadStatsAsync(playerId, cancellationToken);
    }

    private async Task<ActionResult<PlayerStatsDto>> LoadStatsAsync(string? normalizedGuestId, CancellationToken cancellationToken)
    {
        if (normalizedGuestId is null)
        {
            return Ok(PlayerStatsCalculator.Empty);
        }

        try
        {
            var results = await _dbContext.MatchResults
                .AsNoTracking()
                .Where(result => result.WinnerGuestId == normalizedGuestId || result.LoserGuestId == normalizedGuestId)
                .ToListAsync(cancellationToken);

            return Ok(PlayerStatsCalculator.Calculate(normalizedGuestId, results));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not load statistics for guest {GuestId}.", normalizedGuestId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Statistics are not available right now." });
        }
    }
}
