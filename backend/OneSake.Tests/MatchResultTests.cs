using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneSake.Domain;
using OneSake.Persistence;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Tests;

public class MatchResultTests
{
    private const string AliceGuestId = "11111111-1111-4111-8111-111111111111";
    private const string BobGuestId = "22222222-2222-4222-8222-222222222222";

    #region MATCH ROOM

    [Fact]
    public void EndGame_ReportsResultExactlyOnceWithWinnerAndLoser()
    {
        // Alice's deck is too small: after the mulligan she has no cards left and loses by deck out.
        var room = CreateStartedRoom(aliceDeckSize: 6, out var startUpdate);
        Assert.Null(startUpdate.MatchResult);

        Submit(room, GetPrompt(startUpdate, "p1"), "KEEP");
        var endUpdate = Submit(room, GetPrompt(startUpdate, "p2"), "KEEP");

        Assert.Equal(MatchPhase.GameOver, endUpdate.StateSnapshot.Phase);
        var result = Assert.IsType<MatchResultData>(endUpdate.MatchResult);
        Assert.Equal("ABCD", result.RoomCode);
        Assert.Equal(MatchEndReasons.DeckOut, result.EndReason);
        Assert.Equal(new MatchPlayerResult(BobGuestId, "Bob", "OP01-060"), result.Winner);
        Assert.Equal(new MatchPlayerResult(AliceGuestId, "Alice", "OP01-001"), result.Loser);
        Assert.Equal(1, result.TurnCount);
        Assert.True(result.EndedAtUtc >= result.StartedAtUtc);

        // Later updates of the same match (chat, leaving after game over) never report it again.
        Assert.Null(room.JoinPlayer("p2", "Bob", BobGuestId).MatchResult);
        Assert.Null(room.RemovePlayer("p1")!.MatchResult);
    }

    [Fact]
    public void RemovePlayer_DuringRunningMatch_ReportsRemainingPlayerAsWinner()
    {
        var room = CreateStartedRoom(aliceDeckSize: 50, out _);

        var update = room.RemovePlayer("p2", MatchEndReasons.Concede);

        var result = Assert.IsType<MatchResultData>(update!.MatchResult);
        Assert.Equal(MatchEndReasons.Concede, result.EndReason);
        Assert.Equal("Alice", result.Winner.DisplayName);
        Assert.Equal("Bob", result.Loser.DisplayName);
        Assert.Null(room.RemovePlayer("p1")!.MatchResult);
    }

    [Fact]
    public void RemovePlayer_InLobby_DoesNotReportAResult()
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice", AliceGuestId);
        room.JoinPlayer("p2", "Bob", BobGuestId);

        Assert.Null(room.RemovePlayer("p1")!.MatchResult);
    }

    [Fact]
    public void Rematch_AfterGameOver_ReportsTheNextResultAgain()
    {
        var room = CreateStartedRoom(aliceDeckSize: 6, out var startUpdate);
        Submit(room, GetPrompt(startUpdate, "p1"), "KEEP");
        Assert.NotNull(Submit(room, GetPrompt(startUpdate, "p2"), "KEEP").MatchResult);

        var rematchUpdate = room.StartMatch();
        Submit(room, GetPrompt(rematchUpdate, "p1"), "KEEP");
        var secondEnd = Submit(room, GetPrompt(rematchUpdate, "p2"), "KEEP");

        Assert.NotNull(secondEnd.MatchResult);
    }

    #endregion

    #region SAVING

    [Fact]
    public async Task SaveAsync_DoesNotThrowWhenDatabaseIsUnreachable()
    {
        var services = new ServiceCollection();
        services.AddDbContext<OneSakeDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1"));
        using var provider = services.BuildServiceProvider();
        var service = new MatchResultService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MatchResultService>.Instance);

        var exception = await Record.ExceptionAsync(() => service.SaveAsync(CreateResultData()));

        Assert.Null(exception);
    }

    [Fact]
    public void ToEntity_CopiesAllFieldsAndShortensLongNames()
    {
        var data = CreateResultData() with
        {
            Winner = new MatchPlayerResult(AliceGuestId, new string('A', 100), "OP01-001")
        };

        var entity = MatchResultService.ToEntity(data);

        Assert.Equal(64, entity.WinnerDisplayName.Length);
        Assert.Equal(AliceGuestId, entity.WinnerGuestId);
        Assert.Equal(BobGuestId, entity.LoserGuestId);
        Assert.Equal("OP01-060", entity.LoserLeaderCardId);
        Assert.Equal(MatchEndReasons.Life, entity.EndReason);
        Assert.Equal(DateTimeKind.Utc, entity.EndedAtUtc.Kind);
    }

    #endregion

    #region STATISTICS

    [Fact]
    public void Calculate_UnknownPlayer_ReturnsZeros()
    {
        var stats = PlayerStatsCalculator.Calculate(AliceGuestId, []);

        Assert.Equal(0, stats.Matches);
        Assert.Equal(0, stats.WinRate);
        Assert.Null(stats.FavoriteLeaderCardId);
        Assert.Empty(stats.Leaders);
        Assert.Empty(stats.Recent);
    }

    [Fact]
    public void Calculate_CountsWinsLossesLeadersAndRecentMatches()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var results = new[]
        {
            // Alice wins twice with Zoro, loses once with Zoro, loses once with Kaido.
            CreateResult(now.AddMinutes(1), AliceGuestId, "OP01-001", BobGuestId, "OP01-060"),
            CreateResult(now.AddMinutes(2), AliceGuestId, "OP01-001", BobGuestId, "OP01-060"),
            CreateResult(now.AddMinutes(3), BobGuestId, "OP01-060", AliceGuestId, "OP01-001"),
            CreateResult(now.AddMinutes(4), BobGuestId, "OP01-060", AliceGuestId, "OP01-061"),
            // Someone else's match must be ignored.
            CreateResult(now.AddMinutes(5), BobGuestId, "OP01-060", null, "OP01-002"),
        };

        var stats = PlayerStatsCalculator.Calculate(AliceGuestId, results);

        Assert.Equal(4, stats.Matches);
        Assert.Equal(2, stats.Wins);
        Assert.Equal(2, stats.Losses);
        Assert.Equal(50.0, stats.WinRate);
        Assert.Equal("OP01-001", stats.FavoriteLeaderCardId);
        Assert.Equal(new LeaderStatsDto("OP01-001", 3, 2), stats.Leaders[0]);
        Assert.Equal(new LeaderStatsDto("OP01-061", 1, 0), stats.Leaders[1]);

        // Newest first, from Alice's point of view.
        Assert.Equal(now.AddMinutes(4), stats.Recent[0].PlayedAtUtc);
        Assert.False(stats.Recent[0].Won);
        Assert.Equal("Bob", stats.Recent[0].OpponentName);
        Assert.Equal("OP01-061", stats.Recent[0].OwnLeaderCardId);
        Assert.Equal("OP01-060", stats.Recent[0].OpponentLeaderCardId);
        Assert.True(stats.Recent[3].Won);
    }

    [Fact]
    public void Calculate_LimitsRecentMatchesToTenAndRoundsWinRate()
    {
        var now = DateTime.UtcNow;
        var results = Enumerable.Range(0, 12)
            .Select(index => index % 3 == 0
                ? CreateResult(now.AddMinutes(index), AliceGuestId, "OP01-001", BobGuestId, "OP01-060")
                : CreateResult(now.AddMinutes(index), BobGuestId, "OP01-060", AliceGuestId, "OP01-001"))
            .ToList();

        var stats = PlayerStatsCalculator.Calculate(AliceGuestId, results);

        Assert.Equal(12, stats.Matches);
        Assert.Equal(4, stats.Wins);
        Assert.Equal(33.3, stats.WinRate);
        Assert.Equal(10, stats.Recent.Count);
    }

    #endregion

    #region HELPERS

    private static MatchRoom CreateStartedRoom(int aliceDeckSize, out MatchUpdate startUpdate)
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice", AliceGuestId);
        room.JoinPlayer("p2", "Bob", BobGuestId);
        room.SetPlayerDeck("p1", CreateDeck("OP01-001", aliceDeckSize));
        room.SetPlayerDeck("p2", CreateDeck("OP01-060", 50));
        startUpdate = room.StartMatch();
        return room;
    }

    private static MatchUpdate Submit(MatchRoom room, ChoicePromptDto prompt, string option)
    {
        return room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = prompt.ChoiceId,
            PlayerId = prompt.PlayerId,
            SelectedOption = option
        });
    }

    private static ChoicePromptDto GetPrompt(MatchUpdate update, string playerId)
    {
        return update.ChoicePrompts.Single(prompt => prompt.PlayerId == playerId);
    }

    private static PlayerDeckSubmissionDto CreateDeck(string leaderCardId, int quantity)
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = $"deck-{leaderCardId}",
            DeckName = "Test Deck",
            LeaderCardId = leaderCardId,
            LeaderName = "Test Leader",
            LeaderPower = 5000,
            LeaderLife = 5,
            Cards =
            [
                new PlayerDeckCardDto
                {
                    CardId = "OP01-016",
                    Name = "Test Character",
                    Quantity = quantity,
                    Category = "Character",
                    Cost = 1,
                    Power = 3000
                }
            ]
        };
    }

    private static MatchResultData CreateResultData()
    {
        return new MatchResultData(
            "ABCD",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow,
            7,
            MatchEndReasons.Life,
            new MatchPlayerResult(AliceGuestId, "Alice", "OP01-001"),
            new MatchPlayerResult(BobGuestId, "Bob", "OP01-060"));
    }

    private static MatchResult CreateResult(
        DateTime endedAtUtc,
        string? winnerGuestId,
        string winnerLeader,
        string? loserGuestId,
        string loserLeader)
    {
        return new MatchResult
        {
            Id = Guid.NewGuid(),
            RoomCode = "ABCD",
            StartedAtUtc = endedAtUtc.AddMinutes(-10),
            EndedAtUtc = endedAtUtc,
            TurnCount = 6,
            EndReason = MatchEndReasons.Life,
            WinnerGuestId = winnerGuestId,
            WinnerDisplayName = winnerGuestId == AliceGuestId ? "Alice" : "Bob",
            WinnerLeaderCardId = winnerLeader,
            LoserGuestId = loserGuestId,
            LoserDisplayName = loserGuestId == AliceGuestId ? "Alice" : "Bob",
            LoserLeaderCardId = loserLeader
        };
    }

    #endregion
}
