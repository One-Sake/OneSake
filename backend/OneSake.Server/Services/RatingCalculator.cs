using OneSake.Persistence.Entities;
using OneSake.Server.Match;

namespace OneSake.Server.Services;

// ELO formula, pure and without database access.
public static class RatingCalculator
{
    public const int StartRating = 1000;
    public const int MinRating = 100;
    // The first 20 ranked games move the rating faster.
    public const int ProvisionalGames = 20;
    public const int ProvisionalK = 40;
    public const int EstablishedK = 24;
    // 1 rating point = 1,000 Berry, so 1000 rating = 1,000,000 Berry.
    public const long BountyPerPoint = 1000;

    // E = 1 / (1 + 10^((Rb - Ra) / 400))
    public static double ExpectedScore(int rating, int opponentRating)
    {
        return 1.0 / (1.0 + Math.Pow(10, (opponentRating - rating) / 400.0));
    }

    public static int KFactor(int rankedGamesPlayed)
    {
        return rankedGamesPlayed < ProvisionalGames ? ProvisionalK : EstablishedK;
    }

    // Each side uses its own K (based on its own number of ranked games).
    public static (int WinnerAfter, int LoserAfter) Calculate(
        int winnerRating,
        int winnerGames,
        int loserRating,
        int loserGames)
    {
        var winnerAfter = winnerRating + KFactor(winnerGames) * (1 - ExpectedScore(winnerRating, loserRating));
        var loserAfter = loserRating + KFactor(loserGames) * (0 - ExpectedScore(loserRating, winnerRating));

        return (
            Math.Max(MinRating, (int)Math.Round(winnerAfter, MidpointRounding.AwayFromZero)),
            Math.Max(MinRating, (int)Math.Round(loserAfter, MidpointRounding.AwayFromZero)));
    }

    public static long ToBounty(int rating) => rating * BountyPerPoint;
}

// When a finished match changes the rating.
public static class RankingRules
{
    public const int MinTurns = 3;
    public const int MaxGamesPerPairPerWindow = 3;
    public static readonly TimeSpan PairWindow = TimeSpan.FromHours(24);

    // Ranked room, two different valid guest IDs, and a real game
    // (at least 3 turns, or the game was won by Life/deck out).
    // The "same pair max 3 times in 24 h" rule needs the database and is checked by the caller.
    public static bool IsEligible(MatchResult result)
    {
        var winner = GuestIdValidator.Normalize(result.WinnerGuestId);
        var loser = GuestIdValidator.Normalize(result.LoserGuestId);
        var finishedProperly = result.EndReason is MatchEndReasons.Life or MatchEndReasons.DeckOut;

        return result.IsRanked
            && winner is not null
            && loser is not null
            && !string.Equals(winner, loser, StringComparison.OrdinalIgnoreCase)
            && (result.TurnCount >= MinTurns || finishedProperly);
    }

    public static bool IsEligible(MatchResult result, int recentPairGames)
    {
        return IsEligible(result) && recentPairGames < MaxGamesPerPairPerWindow;
    }

    // Changes both ratings and stores before/after on the match.
    public static void Apply(MatchResult result, PlayerRating winner, PlayerRating loser)
    {
        var (winnerAfter, loserAfter) = RatingCalculator.Calculate(
            winner.Rating,
            winner.Wins + winner.Losses,
            loser.Rating,
            loser.Wins + loser.Losses);

        result.WinnerRatingBefore = winner.Rating;
        result.WinnerRatingAfter = winnerAfter;
        result.LoserRatingBefore = loser.Rating;
        result.LoserRatingAfter = loserAfter;

        winner.Rating = winnerAfter;
        winner.Wins += 1;
        winner.Peak = Math.Max(winner.Peak, winnerAfter);
        winner.LastPlayedUtc = result.EndedAtUtc;
        winner.DisplayName = result.WinnerDisplayName;

        loser.Rating = loserAfter;
        loser.Losses += 1;
        loser.Peak = Math.Max(loser.Peak, loserAfter);
        loser.LastPlayedUtc = result.EndedAtUtc;
        loser.DisplayName = result.LoserDisplayName;
    }
}
