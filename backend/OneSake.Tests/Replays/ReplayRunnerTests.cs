using OneSake.Game.Replay;
using OneSake.Server.Match;

namespace OneSake.Tests.Replays;

// Recording -> replay must give the same state after every input.
public class ReplayRunnerTests
{
    [Theory]
    [InlineData(11UL)]
    [InlineData(2026UL)]
    [InlineData(987654321UL)]
    public void Replay_ReachesTheSameStateAsTheRecordedMatch(ulong seed)
    {
        var bot = new MatchBot(MatchBot.CreateRoom(seed, "conn-1", "conn-2"), "conn-1", "conn-2").Play();
        var recording = Assert.Single(bot.DeliveredRecordings);

        var replay = MatchRoomReplay.Start(recording);
        var steps = ReplayRunner.Run(recording, replay);

        Assert.Equal(recording.Inputs.Count, steps.Count);
        Assert.Equal(bot.Room.ComputeStateHash(), replay.StateHash());
    }

    [Fact]
    public void Replay_CanStopAtASeq()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(3, "a", "b"), "a", "b").Play();
        var recording = bot.DeliveredRecordings[0];

        var replay = MatchRoomReplay.Start(recording);
        var steps = ReplayRunner.Run(recording, replay, untilSeq: 20);

        Assert.Equal(20, steps.Count);
        Assert.Equal(bot.Hashes[19], replay.StateHash());
    }

    [Fact]
    public void ChangedInput_ReportsTheSeqWhereTheEngineDiffers()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(4, "a", "b"), "a", "b").Play();
        var recording = bot.DeliveredRecordings[0];
        var inputs = recording.Inputs.ToList();
        // Answer seq 12 with an option the prompt does not have.
        var broken = inputs[11] with { Payload = MatchRecordingJson.ToPayload(new { choiceId = "c999", selectedOption = "NOPE" }) };
        inputs[11] = broken;

        var error = Assert.Throws<ReplayDivergenceException>(
            () => ReplayRunner.Run(recording with { Inputs = inputs }, MatchRoomReplay.Start(recording)));

        Assert.Equal(12, error.Seq);
        Assert.StartsWith("Engine weicht ab bei seq 12", error.Message);
    }

    [Fact]
    public void DifferentState_IsReportedEvenWhenTheInputIsStillValid()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(6, "a", "b"), "a", "b").Play();
        var recording = bot.DeliveredRecordings[0];
        var inputs = recording.Inputs.ToList();
        inputs[4] = inputs[4] with { StateHash = "0000" };

        var error = Assert.Throws<ReplayDivergenceException>(
            () => ReplayRunner.Run(recording with { Inputs = inputs }, MatchRoomReplay.Start(recording)));

        Assert.Equal(5, error.Seq);
    }

    [Fact]
    public void ReplayFile_RoundTrip()
    {
        var bot = new MatchBot(MatchBot.CreateRoom(8, "a", "b"), "a", "b").Play();
        var file = new ReplayFile { Report = new BugReportInfo { Id = 1, Category = "EFFECT", AtSeq = 5 }, Recording = bot.DeliveredRecordings[0] };
        var path = Path.Combine(Path.GetTempPath(), $"onesake-replay-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, file.ToJson());

        try
        {
            var loaded = ReplayFile.Load(path);
            Assert.Equal(5, loaded.Report!.AtSeq);
            Assert.Equal(file.Recording.Inputs.Count, loaded.Recording.Inputs.Count);
            ReplayRunner.Run(loaded.Recording, MatchRoomReplay.Start(loaded.Recording));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
