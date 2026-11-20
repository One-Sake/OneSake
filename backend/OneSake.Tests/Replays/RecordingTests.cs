using OneSake.Domain;
using OneSake.Game.Replay;
using OneSake.Server.Match;

namespace OneSake.Tests.Replays;

public class RecordingTests
{
    [Fact]
    public void FinishedMatch_DeliversTheRecordingOnce_WithEveryDecision()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(77, "a", "b"), "a", "b").Play();

        var recording = Assert.Single(bot.DeliveredRecordings);
        Assert.True(recording.Finished);
        Assert.Equal("77", recording.Seed);
        Assert.Equal(bot.Decisions, recording.Inputs.Count);
        Assert.Equal(Enumerable.Range(1, recording.Inputs.Count), recording.Inputs.Select(input => input.Seq));
        Assert.Equal(bot.Room.ComputeStateHash(), recording.FinalStateHash);
        Assert.Equal(bot.Hashes, recording.Inputs.Select(input => input.StateHash));
        Assert.Equal([1, 2], recording.Players.Select(player => player.Seat));
        Assert.Equal("BOT-LEADER", recording.Players[0].Deck.LeaderCardId);
        Assert.NotEmpty(recording.DiceRolls);
        Assert.Contains(recording.Inputs, input => input.Kind == RecordedInputKinds.Manual);
        // No connection IDs in the recording.
        Assert.DoesNotContain("\"playerId\": \"a\"", MatchRecordingJson.Serialize(recording));
    }

    [Fact]
    public void Recording_SurvivesGzip()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(78, "a", "b"), "a", "b").Play();
        var recording = bot.DeliveredRecordings[0];

        var copy = MatchRecordingJson.Decompress(MatchRecordingJson.Compress(recording));

        Assert.Equal(MatchRecordingJson.Serialize(recording), MatchRecordingJson.Serialize(copy));
    }

    [Fact]
    public void SeedIsNeverInTheStateSentToClients()
    {
        var room = MatchBot.CreateRoom(123456789012345UL, "a", "b");
        var update = room.StartMatch();

        foreach (var snapshot in update.StateSnapshots.Values.Append(update.StateSnapshot))
        {
            Assert.DoesNotContain("123456789012345", System.Text.Json.JsonSerializer.Serialize(snapshot));
        }

        Assert.Null(update.Recording);
    }

    [Fact]
    public void LeavingTheMatch_IsRecorded_AndFinishesTheRecording()
    {
        var room = MatchBot.CreateRoom(5, "a", "b");
        room.StartMatch();

        var update = room.RemovePlayer("b", MatchEndReasons.Concede)!;

        var recording = Assert.IsType<MatchRecording>(update.Recording);
        var leave = Assert.Single(recording.Inputs);
        Assert.Equal(RecordedInputKinds.Leave, leave.Kind);
        Assert.Equal(2, leave.PlayerSlot);
    }
}
