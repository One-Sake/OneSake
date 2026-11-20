using OneSake.Game.Randomness;
using OneSake.Game.Simulation;
using OneSake.Server.Simulation;
using OneSake.Tests.Replays;
using Xunit.Abstractions;

namespace OneSake.Tests.Simulation;

public class SimulatorTests
{
    private readonly ITestOutputHelper _output;

    public SimulatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IBotPlayer Random(IRandom random) => new RandomBot(random);
    public static IBotPlayer Greedy(IRandom random) => new GreedyBot(random);

    [Fact]
    public void FiftyMatches_WithFixedSeeds_BreakNoInvariant()
    {
        var failures = new List<string>();
        var finished = 0;
        for (var index = 0; index < 50; index += 1)
        {
            var seed = 1000UL + (ulong)index;
            Func<IRandom, IBotPlayer> first = index % 2 == 0 ? Random : Greedy;
            Func<IRandom, IBotPlayer> second = index % 3 == 0 ? Greedy : Random;
            var result = MatchSimulator.Play(seed, MatchBot.Deck(), MatchBot.Deck(), first, second);

            finished += result.Finished ? 1 : 0;
            if (result.Failed)
            {
                failures.Add($"seed {seed} ({result.Bots}): {string.Join(" | ", result.Violations)}{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", result.LastLog)}");
            }
        }

        _output.WriteLine($"{finished}/50 finished");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(50, finished);
    }

    [Fact]
    public void SameSeed_GivesTheSameMatch()
    {
        var result = MatchSimulator.Play(77, MatchBot.Deck(), MatchBot.Deck(), Random, Greedy, new SimulationOptions { CheckDeterminism = true });

        Assert.False(result.Failed, string.Join(" | ", result.Violations));
    }

    [Fact]
    public void FailedMatch_HasARecordingThatReplays()
    {
        // A tiny step limit makes the match "hang" on purpose.
        var result = MatchSimulator.Play(5, MatchBot.Deck(), MatchBot.Deck(), Random, Random, new SimulationOptions { MaxSteps = 30 });

        var violation = Assert.Single(result.Violations);
        Assert.Equal(InvariantNames.Hang, violation.Invariant);
        Assert.NotNull(result.Recording);
        Assert.Equal(30, result.FailedAtSeq);
        Assert.Equal(20, result.LastLog.Count);
        OneSake.Game.Replay.ReplayRunner.Run(result.Recording!, OneSake.Server.Match.MatchRoomReplay.Start(result.Recording!));
    }
}
