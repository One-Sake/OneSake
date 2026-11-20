using OneSake.Game.Randomness;

namespace OneSake.Tests.Replays;

// The same seed and the same decisions must always give the same match.
public class DeterminismTests
{
    [Fact]
    public void SameSeedAndDecisions_GiveTheSameStateAfterEveryStep()
    {
        // Different connection IDs on purpose: they must not change anything.
        var first = new MatchBot(MatchBot.CreateRoom(4242, "conn-a", "conn-b"), "conn-a", "conn-b").Play();
        var second = new MatchBot(MatchBot.CreateRoom(4242, "x", "y"), "x", "y").Play();

        Assert.True(first.Decisions > 40, $"Only {first.Decisions} decisions - the match should be longer.");
        Assert.True(first.IsGameOver);
        Assert.Equal(first.Hashes, second.Hashes);
        Assert.Equal(first.Room.ComputeStateHash(), second.Room.ComputeStateHash());
    }

    [Fact]
    public void AnotherSeed_GivesAnotherMatch()
    {
        var first = new MatchBot(MatchBot.CreateRoom(1, "a", "b"), "a", "b").Play();
        var second = new MatchBot(MatchBot.CreateRoom(2, "a", "b"), "a", "b").Play();

        Assert.NotEqual(first.Room.ComputeStateHash(), second.Room.ComputeStateHash());
    }

    [Fact]
    public void Pcg32_IsDeterministic_AndStreamsAreIndependent()
    {
        var a = Pcg32.FromSeed(7, "deck:1");
        var b = Pcg32.FromSeed(7, "deck:1");
        var other = Pcg32.FromSeed(7, "deck:2");

        var first = Enumerable.Range(0, 20).Select(_ => a.Next(1000)).ToList();
        Assert.Equal(first, Enumerable.Range(0, 20).Select(_ => b.Next(1000)).ToList());
        Assert.NotEqual(first, Enumerable.Range(0, 20).Select(_ => other.Next(1000)).ToList());
    }

    [Fact]
    public void Pcg32_NextStaysInRange_AndUsesAllValues()
    {
        var random = Pcg32.FromSeed(99, "range");
        var counts = new int[6];
        for (var index = 0; index < 6000; index += 1)
        {
            counts[random.Next(6)] += 1;
        }

        Assert.All(counts, count => Assert.InRange(count, 850, 1150));
    }
}
