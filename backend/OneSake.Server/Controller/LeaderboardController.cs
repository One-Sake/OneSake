using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneSake.Persistence;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Server.Controllers;

public sealed record LeaderboardEntryDto(
    int Rank,
    string DisplayName,
    int Rating,
    long Bounty,
    int Wins,
    int Losses,
    // Percent from 0 to 100, one decimal.
    double WinRate,
    string? FavoriteLeaderCardId,
    DateTime LastPlayedUtc);

public sealed record RatingPointDto(DateTime EndedAtUtc, int Rating);

public sealed record PlayerBountyDto(
    // Null until the player has the minimum number of ranked games.
    int? Rank,
    string? DisplayName,
    int Rating,
    long Bounty,
    int Peak,
    int Wins,
    int Losses,
    string? FavoriteLeaderCardId,
    IReadOnlyList<RatingPointDto> RatingHistory);

// Base Route -> /leaderboard. OneSake's own player ranking (ranked Quick Match games only).
[ApiController]
[Route("[controller]")]
public class LeaderboardController : ControllerBase
{
    public const string CacheKey = "leaderboard:all";
    public const int MinRankedGames = 5;
    private const int MaxLimit = 500;
    private const int HistoryLength = 30;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly OneSakeDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(OneSakeDbContext dbContext, IMemoryCache cache, ILogger<LeaderboardController> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    // GET /leaderboard?limit=100
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await GetRankedPlayersAsync(cancellationToken);
            return Ok(entries.Take(Math.Clamp(limit, 1, MaxLimit)).ToList());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not load the leaderboard.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Leaderboard is not available right now." });
        }
    }

    // GET /leaderboard/player/{guestId}
    // Unknown players get the start rating (no 404), so a new player simply sees the starting bounty.
    [HttpGet("player/{guestId}")]
    public async Task<ActionResult<PlayerBountyDto>> GetPlayer(string guestId, CancellationToken cancellationToken)
    {
        var normalizedGuestId = GuestIdValidator.Normalize(guestId);
        var startRating = RatingCalculator.StartRating;
        var empty = new PlayerBountyDto(null, null, startRating, RatingCalculator.ToBounty(startRating), startRating, 0, 0, null, []);
        if (normalizedGuestId is null)
        {
            return Ok(empty);
        }

        try
        {
            var rating = await _dbContext.PlayerRatings.AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.GuestId == normalizedGuestId, cancellationToken);
            if (rating is null)
            {
                return Ok(empty);
            }

            // Fresh from the database (not cached), so the profile updates right after a match.
            int? rank = null;
            if (rating.Wins + rating.Losses >= MinRankedGames)
            {
                rank = 1 + await _dbContext.PlayerRatings.CountAsync(
                    entry => entry.Wins + entry.Losses >= MinRankedGames
                        && (entry.Rating > rating.Rating
                            || (entry.Rating == rating.Rating && entry.Wins > rating.Wins)),
                    cancellationToken);
            }

            var history = await _dbContext.MatchResults.AsNoTracking()
                .Where(result => result.WinnerRatingAfter != null
                    && (result.WinnerGuestId == normalizedGuestId || result.LoserGuestId == normalizedGuestId))
                .OrderByDescending(result => result.EndedAtUtc)
                .Take(HistoryLength)
                .Select(result => new RatingPointDto(
                    result.EndedAtUtc,
                    result.WinnerGuestId == normalizedGuestId ? result.WinnerRatingAfter!.Value : result.LoserRatingAfter!.Value))
                .ToListAsync(cancellationToken);
            history.Reverse();

            return Ok(new PlayerBountyDto(
                rank,
                rating.DisplayName,
                rating.Rating,
                RatingCalculator.ToBounty(rating.Rating),
                rating.Peak,
                rating.Wins,
                rating.Losses,
                rating.FavoriteLeaderCardId,
                history));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not load the bounty of guest {GuestId}.", normalizedGuestId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Bounty is not available right now." });
        }
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> GetRankedPlayersAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<LeaderboardEntryDto>? cached) && cached is not null)
        {
            return cached;
        }

        var players = await _dbContext.PlayerRatings.AsNoTracking()
            .Where(entry => entry.Wins + entry.Losses >= MinRankedGames)
            .OrderByDescending(entry => entry.Rating)
            .ThenByDescending(entry => entry.Wins)
            .ThenBy(entry => entry.GuestId)
            .Take(MaxLimit)
            .ToListAsync(cancellationToken);

        var entries = players.Select((player, index) => ToEntry(player, index + 1)).ToList();
        _cache.Set(CacheKey, (IReadOnlyList<LeaderboardEntryDto>)entries, CacheDuration);
        return entries;
    }

    private static LeaderboardEntryDto ToEntry(PlayerRating player, int rank)
    {
        var games = player.Wins + player.Losses;
        return new LeaderboardEntryDto(
            rank,
            player.DisplayName,
            player.Rating,
            RatingCalculator.ToBounty(player.Rating),
            player.Wins,
            player.Losses,
            games == 0 ? 0 : Math.Round(100.0 * player.Wins / games, 1),
            player.FavoriteLeaderCardId,
            player.LastPlayedUtc);
    }
}
