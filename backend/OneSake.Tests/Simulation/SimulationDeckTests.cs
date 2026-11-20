using OneSake.Domain;
using OneSake.Game.Randomness;
using OneSake.Server.Simulation;

namespace OneSake.Tests.Simulation;

public class SimulationDeckTests
{
    public static string DeckFolder => Path.Combine(AppContext.BaseDirectory, "Decks");
    public static CardPool Fixture { get; } = CardPool.Load(Path.Combine(DeckFolder, "cards-fixture.json"));
    public static IReadOnlyList<PlayerDeckSubmissionDto> MetaDecks { get; } = DeckList.LoadFolder(DeckFolder, Fixture);

    [Fact]
    public void EveryMetaDeck_HasALeaderAnd50Cards_FromTheFixture()
    {
        Assert.InRange(MetaDecks.Count, 10, 20);
        Assert.All(MetaDecks, deck =>
        {
            Assert.Equal(50, deck.Cards.Sum(card => card.Quantity));
            Assert.All(deck.Cards, card => Assert.InRange(card.Quantity, 1, 4));
            Assert.False(string.IsNullOrEmpty(deck.LeaderText));
        });
    }

    [Fact]
    public void RandomDeck_IsValid_AndDependsOnlyOnTheSeed()
    {
        var first = RandomDeckBuilder.Build(Fixture, Pcg32.FromSeed(9, "deck"));
        var second = RandomDeckBuilder.Build(Fixture, Pcg32.FromSeed(9, "deck"));

        Assert.Equal(50, first.Cards.Sum(card => card.Quantity));
        Assert.All(first.Cards, card => Assert.InRange(card.Quantity, 1, 4));
        var leaderColors = first.LeaderColor.Split(' ');
        Assert.All(first.Cards, card => Assert.All(card.Color.Split(' '), color => Assert.Contains(color, leaderColors)));
        Assert.Equal(first.Cards.Select(card => (card.CardId, card.Quantity)), second.Cards.Select(card => (card.CardId, card.Quantity)));
    }

    [Fact]
    public void WrongDeckFile_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"onesake-deck-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "1xOP13-001\n4xST16-005\n");
        try
        {
            Assert.Throws<FormatException>(() => DeckList.Load(path, Fixture));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
