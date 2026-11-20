using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Tests.Rules;

namespace OneSake.Tests.Simulation;

// Regression tests for problems the simulator found (docs/simulation-findings.md).
public class FindingRegressionTests
{
    [Fact]
    public void F1_LookedAtCards_StayInTheDeckWhileThePlayerChooses()
    {
        // Rule 11-3-2: "Cards remain in their original areas while being looked at."
        // Before the fix the 4 cards were in no area until the choice was answered.
        var game = new RuleScenario(deckSize: 10);
        var source = game.AddCharacter(game.P1, "[On Play] Look at 4 cards from the top of your deck; reveal up to 1 card and add it to your hand. Then, place the rest at the bottom of your deck in any order.");
        var top = game.P1.DrawDeck.Take(4).ToList();

        game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        var request = Assert.IsType<EffectChoiceNeeded>(game.Resolver.Run());

        Assert.Equal(10, game.P1.DrawDeck.Count);
        Assert.Equal(top, game.P1.DrawDeck.Take(4));

        Assert.Null(game.Resolver.AnswerEffect(EffectChoiceRequest.Select, request.Request.Cards[1].InstanceId));

        Assert.Equal(9, game.P1.DrawDeck.Count);
        Assert.Contains(top[1], game.P1.Hand);
        // The other three are at the bottom, in the same order.
        Assert.Equal([top[0], top[2], top[3]], game.P1.DrawDeck.TakeLast(3));
    }

    [Fact]
    public void F1_ACardThatLeftTheDeckDuringTheChoice_IsNotPlacedAgain()
    {
        var game = new RuleScenario(deckSize: 10);
        var source = game.AddCharacter(game.P1, "[On Play] Look at 3 cards from the top of your deck; reveal up to 1 card and add it to your hand. Then, place the rest at the bottom of your deck in any order.");

        game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        game.Resolver.Run();
        // Meanwhile the top card is drawn (e.g. by a manual action).
        game.P1.DrawCards(1);
        game.Resolver.AnswerEffect(EffectChoiceRequest.Done, null);

        var all = game.P1.DrawDeck.Concat(game.P1.Hand).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(13, all.Count);
    }
}
