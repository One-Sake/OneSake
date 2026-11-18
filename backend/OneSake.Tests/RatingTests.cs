using OneSake.Domain;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Tests;

public class RatingTests
{
    private const string AliceGuestId = "11111111-1111-4111-8111-111111111111";
    private const string BobGuestId = "22222222-2222-4222-8222-222222222222";

    #region FORMULA

    [Fact]
    public void ExpectedScore_EqualRatingsIsOneHalf_AndFollowsTheFormula()
    {
        Assert.Equal(0.5, RatingCalculator.ExpectedScore(1000, 1000), 6);
        // 400 points more -> 10 : 1 -> 10/11.
        Assert.Equal(10.0 / 11.0, RatingCalculator.ExpectedScore(1400, 1000), 6);
        Assert.Equal(1.0 / 11.0, RatingCalculator.ExpectedScore(1000, 1400), 6);
    }

    [Fact]
    public void Calculate_EqualPlayersInProvisionalPhase_MoveByHalfOfK40()
    {
        var (winner, loser) = RatingCalculator.Calculate(1000, 0, 1000, 0);

        Assert.Equal(1020, winner);
        Assert.Equal(980, loser);
    }

    [Fact]
    public void KFactor_Is40ForTheFirst20Games_Then24()
    {
        Assert.Equal(40, RatingCalculator.KFactor(0));
        Assert.Equal(40, RatingCalculator.KFactor(19));
        Assert.Equal(24, RatingCalculator.KFactor(20));
        Assert.Equal(24, RatingCalculator.KFactor(500));

        var (winner, loser) = RatingCalculator.Calculate(1000, 20, 1000, 20);
        Assert.Equal(1012, winner);
        Assert.Equal(988, loser);
    }

    [Fact]
    public void Calculate_EachSideUsesItsOwnK()
    {
        // Winner is new (K 40), loser is established (K 24).
        var (winner, loser) = RatingCalculator.Calculate(1000, 3, 1000, 30);

        Assert.Equal(1020, winner);
        Assert.Equal(988, loser);
    }

    [Fact]
    public void Calculate_UpsetGivesMorePointsThanTheExpectedWin()
    {
        var (underdog, _) = RatingCalculator.Calculate(1000, 30, 1400, 30);
        var (favorite, _) = RatingCalculator.Calculate(1400, 30, 1000, 30);

        // 24 * (1 - 1/11) = 21.8 -> 22; 24 * (1 - 10/11) = 2.2 -> 2
        Assert.Equal(1022, underdog);
        Assert.Equal(1402, favorite);
    }

    [Fact]
    public void Calculate_NeverGoesBelowTheMinimumRating()
    {
        var (_, loser) = RatingCalculator.Calculate(500, 0, 100, 0);

        Assert.Equal(RatingCalculator.MinRating, loser);
    }

    [Fact]
    public void Bounty_IsRatingTimesThousand()
    {
        Assert.Equal(1_000_000, RatingCalculator.ToBounty(1000));
        Assert.Equal(1_250_000, RatingCalculator.ToBounty(1250));
    }

    #endregion

    #region WHEN A MATCH COUNTS

    [Fact]
    public void IsEligible_RankedMatchWithTwoGuestsAndEnoughTurns_Counts()
    {
        Assert.True(RankingRules.IsEligible(CreateResult(), recentPairGames: 0));
    }

    [Fact]
    public void IsEligible_CasualTable_DoesNotCount()
    {
        Assert.False(RankingRules.IsEligible(CreateResult(isRanked: false), 0));
    }

    [Theory]
    [InlineData(null, BobGuestId)]
    [InlineData(AliceGuestId, null)]
    [InlineData("not-a-guid", BobGuestId)]
    [InlineData(AliceGuestId, "00000000-0000-0000-0000-000000000000")]
    public void IsEligible_MissingOrInvalidGuestId_DoesNotCount(string? winner, string? loser)
    {
        Assert.False(RankingRules.IsEligible(CreateResult(winner: winner, loser: loser), 0));
    }

    [Fact]
    public void IsEligible_SameGuestOnBothSides_DoesNotCount()
    {
        Assert.False(RankingRules.IsEligible(CreateResult(winner: AliceGuestId, loser: AliceGuestId.ToUpperInvariant()), 0));
    }

    [Theory]
    [InlineData(2, MatchEndReasons.Concede, false)]
    [InlineData(1, MatchEndReasons.Disconnect, false)]
    [InlineData(3, MatchEndReasons.Concede, true)]
    [InlineData(1, MatchEndReasons.DeckOut, true)]
    [InlineData(2, MatchEndReasons.Life, true)]
    public void IsEligible_NeedsThreeTurnsOrARealWin(int turns, string endReason, bool expected)
    {
        Assert.Equal(expected, RankingRules.IsEligible(CreateResult(turns: turns, endReason: endReason), 0));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    public void IsEligible_SamePairAtMostThreeRatedGamesIn24Hours(int recentPairGames, bool expected)
    {
        Assert.Equal(expected, RankingRules.IsEligible(CreateResult(), recentPairGames));
    }

    [Fact]
    public void Apply_UpdatesBothRatingsAndStoresBeforeAndAfter()
    {
        var result = CreateResult();
        var winner = new PlayerRating { GuestId = AliceGuestId, Rating = 1000, Peak = 1000 };
        var loser = new PlayerRating { GuestId = BobGuestId, Rating = 1000, Peak = 1100, Wins = 20, Losses = 5 };

        RankingRules.Apply(result, winner, loser);

        Assert.Equal(1000, result.WinnerRatingBefore);
        Assert.Equal(1020, result.WinnerRatingAfter);
        Assert.Equal(1000, result.LoserRatingBefore);
        Assert.Equal(988, result.LoserRatingAfter);
        Assert.Equal(1020, winner.Rating);
        Assert.Equal(1020, winner.Peak);
        Assert.Equal(1, winner.Wins);
        Assert.Equal(6, loser.Losses);
        // The peak never goes down.
        Assert.Equal(1100, loser.Peak);
        Assert.Equal("Alice", winner.DisplayName);
        Assert.Equal(result.EndedAtUtc, loser.LastPlayedUtc);
    }

    #endregion

    #region MATCH ROOM DATA

    [Fact]
    public void QuickMatchResult_IsRankedAndRemembersTheFirstPlayer()
    {
        var room = new MatchRoom("ABCD", isQuickMatch: true, diceRoller: new FixedDiceRoller([2, 6]));
        room.JoinPlayer("p1", "Alice", AliceGuestId);
        room.JoinPlayer("p2", "Bob", BobGuestId);
        room.SetPlayerDeck("p1", CreateDeck());
        room.SetPlayerDeck("p2", CreateDeck());
        var prompt = Assert.Single(room.StartMatch().ChoicePrompts);
        // Bob won the dice and lets Alice begin.
        room.SubmitChoice(new ChoiceSubmissionDto { ChoiceId = prompt.ChoiceId, PlayerId = "p2", SelectedOption = "GO_SECOND" });

        var update = room.RemovePlayer("p2", MatchEndReasons.Concede);

        var result = update!.MatchResult!;
        Assert.True(result.IsRanked);
        Assert.Equal(AliceGuestId, result.FirstPlayerGuestId);
        Assert.True(result.WinnerWentFirst);
        var entity = MatchResultService.ToEntity(result);
        Assert.True(entity.IsRanked);
        Assert.Equal(AliceGuestId, entity.FirstPlayerGuestId);
        Assert.True(entity.WinnerWentFirst);
    }

    [Fact]
    public void PrivateTableResult_IsNotRanked()
    {
        var room = new MatchRoom("ABCD", diceRoller: new FixedDiceRoller([6, 1]));
        room.JoinPlayer("p1", "Alice", AliceGuestId);
        room.JoinPlayer("p2", "Bob", BobGuestId);
        room.SetPlayerDeck("p1", CreateDeck());
        room.SetPlayerDeck("p2", CreateDeck());
        room.StartMatch();

        var result = room.RemovePlayer("p1", MatchEndReasons.Concede)!.MatchResult!;

        Assert.False(result.IsRanked);
        // The match ended before the turn order was decided.
        Assert.Null(result.FirstPlayerGuestId);
        Assert.Null(result.WinnerWentFirst);
    }

    [Fact]
    public void PlayerStats_ShowTheRatingChangeOfRankedMatches()
    {
        var rated = CreateResult();
        rated.WinnerRatingBefore = 1000;
        rated.WinnerRatingAfter = 1024;
        rated.LoserRatingBefore = 1000;
        rated.LoserRatingAfter = 976;
        var casual = CreateResult(isRanked: false);
        casual.EndedAtUtc = rated.EndedAtUtc.AddMinutes(-30);

        var alice = PlayerStatsCalculator.Calculate(AliceGuestId, [rated, casual]);
        var bob = PlayerStatsCalculator.Calculate(BobGuestId, [rated, casual]);

        Assert.Equal(24, alice.Recent[0].RatingChange);
        Assert.Equal(24_000, alice.Recent[0].BountyChange);
        Assert.True(alice.Recent[0].IsRanked);
        Assert.Null(alice.Recent[1].RatingChange);
        Assert.False(alice.Recent[1].IsRanked);
        Assert.Equal(-24, bob.Recent[0].RatingChange);
    }

    #endregion

    private static MatchResult CreateResult(
        bool isRanked = true,
        string? winner = AliceGuestId,
        string? loser = BobGuestId,
        int turns = 6,
        string endReason = MatchEndReasons.Life)
    {
        return new MatchResult
        {
            Id = Guid.NewGuid(),
            RoomCode = "ABCD",
            StartedAtUtc = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc),
            EndedAtUtc = new DateTime(2026, 11, 18, 10, 20, 0, DateTimeKind.Utc),
            TurnCount = turns,
            EndReason = endReason,
            IsRanked = isRanked,
            WinnerGuestId = winner,
            WinnerDisplayName = "Alice",
            WinnerLeaderCardId = "OP01-001",
            LoserGuestId = loser,
            LoserDisplayName = "Bob",
            LoserLeaderCardId = "OP01-060"
        };
    }

    private static PlayerDeckSubmissionDto CreateDeck()
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = "deck",
            DeckName = "Test Deck",
            LeaderCardId = "OP01-001",
            LeaderLife = 5,
            Cards = [new PlayerDeckCardDto { CardId = "OP01-016", Name = "Test", Quantity = 50, Category = "Character", Cost = 1, Power = 3000 }]
        };
    }
}
