using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Server.Controllers;

// Base Route -> /stats
[ApiController]
[Route("[controller]")]
public class StatsController : ControllerBase
{
    private readonly OneSakeDbContext _dbContext;
    private readonly ILogger<StatsController> _logger;

    public StatsController(OneSakeDbContext dbContext, ILogger<StatsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // GET /stats/player/{guestId}
    // Unknown or invalid IDs return empty statistics (no 404), so a new player simply sees zeros.
    [HttpGet("player/{guestId}")]
    public async Task<ActionResult<PlayerStatsDto>> GetPlayerStats(string guestId, CancellationToken cancellationToken)
    {
        var normalizedGuestId = GuestIdValidator.Normalize(guestId);
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
