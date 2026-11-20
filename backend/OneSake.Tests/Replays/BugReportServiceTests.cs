using Microsoft.Extensions.Logging.Abstractions;
using OneSake.Game.Replay;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Tests.Replays;

public class BugReportServiceTests
{
    private sealed class FakeStore : IBugReportStore
    {
        public List<(BugReport Report, MatchRecording? Recording)> Saved { get; } = [];
        public bool Fail { get; set; }

        public Task<int> SaveAsync(BugReport report, MatchRecording? recording, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("database down");
            }

            report.Id = Saved.Count + 100;
            Saved.Add((report, recording));
            return Task.FromResult(report.Id);
        }

        public Task<IReadOnlyList<BugReportListItem>> ListAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BugReportListItem>>([]);
        }
    }

    private readonly FakeStore _store = new();
    private readonly MatchRoomManager _rooms = new();
    private DateTime _now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private BugReportService CreateService() => new(_rooms, _store, NullLogger<BugReportService>.Instance, () => _now);

    private static BugReportRequest Request(string roomCode = "ROOM1", string description = "Zoro got no power", string category = "effect", string? cardId = null, int atSeq = 0) => new()
    {
        RoomCode = roomCode,
        GuestId = "11111111-1111-4111-8111-111111111111",
        Description = description,
        Category = category,
        CardId = cardId,
        AtSeq = atSeq
    };

    [Fact]
    public async Task RunningMatch_IsSavedWithTheRecordingUpToNow()
    {
        var room = _rooms.Create("ROOM1");
        room.JoinPlayer("a", "Alice");
        room.JoinPlayer("b", "Bob");
        room.SetPlayerDeck("a", MatchBot.Deck());
        room.SetPlayerDeck("b", MatchBot.Deck());
        var bot = new MatchBot(room, "a", "b").Play(maxDecisions: 15);
        Assert.False(bot.IsGameOver);

        var result = await CreateService().SubmitAsync(Request(cardId: "op01-016", atSeq: 99), "guest");

        Assert.Equal(100, result.Id);
        var (report, recording) = Assert.Single(_store.Saved);
        Assert.NotNull(recording);
        Assert.False(recording!.Finished);
        Assert.Equal(15, recording.Inputs.Count);
        // atSeq is limited to what was recorded.
        Assert.Equal(15, report.AtSeq);
        Assert.Equal("EFFECT", report.Category);
        Assert.Equal("OP01-016", report.CardId);
        Assert.Equal("ROOM1", report.RoomCode);

        // The saved recording can be played up to the reported point.
        ReplayRunner.Run(recording, MatchRoomReplay.Start(recording), report.AtSeq);
    }

    [Fact]
    public async Task UnknownRoom_IsSavedWithoutRecording()
    {
        var result = await CreateService().SubmitAsync(Request("GONE1"), "guest");

        Assert.NotNull(result.Id);
        Assert.Null(_store.Saved[0].Recording);
        Assert.Equal(0, _store.Saved[0].Report.AtSeq);
    }

    [Theory]
    [InlineData("", "text", "EFFECT", null)]
    [InlineData("BAD CODE", "text", "EFFECT", null)]
    [InlineData("ROOM1", "   ", "EFFECT", null)]
    [InlineData("ROOM1", "text", "WHATEVER", null)]
    [InlineData("ROOM1", "text", "OTHER", "<script>")]
    public async Task InvalidInput_IsRejected(string roomCode, string description, string category, string? cardId)
    {
        var result = await CreateService().SubmitAsync(Request(roomCode, description, category, cardId), "guest");

        Assert.Null(result.Id);
        Assert.NotNull(result.Error);
        Assert.False(result.RateLimited);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task LongDescription_IsShortened()
    {
        await CreateService().SubmitAsync(Request(description: new string('x', 1500)), "guest");

        Assert.Equal(BugReportService.MaxDescriptionLength, _store.Saved[0].Report.Description.Length);
    }

    [Fact]
    public async Task RateLimit_FivePerHourAndClient()
    {
        var service = CreateService();
        for (var index = 0; index < 5; index += 1)
        {
            Assert.NotNull((await service.SubmitAsync(Request(), "guest")).Id);
        }

        var sixth = await service.SubmitAsync(Request(), "guest");
        Assert.True(sixth.RateLimited);
        Assert.NotNull((await service.SubmitAsync(Request(), "other-guest")).Id);

        _now = _now.AddMinutes(61);
        Assert.NotNull((await service.SubmitAsync(Request(), "guest")).Id);
    }

    [Fact]
    public async Task StorageError_DoesNotThrow()
    {
        _store.Fail = true;

        var result = await CreateService().SubmitAsync(Request(), "guest");

        Assert.Null(result.Id);
        Assert.Contains("could not be saved", result.Error);
    }
}
