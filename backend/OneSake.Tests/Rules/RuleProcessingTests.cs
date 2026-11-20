using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Tests.Rules;

// Rule 9 (rule processing), "cannot" effects (1-3-3, 10-2-1-3) and durations.
public class RuleProcessingTests
{
    [Fact]
    public void KOByEffect_ActivatesOnKO()
    {
        // Rule 10-2-1 / 10-2-17-1: K.O. by an effect, [On K.O.] resolves from the trash.
        var game = new RuleScenario();
        var target = game.AddCharacter(game.P2, "[On K.O.] Draw 1 card.", cost: 2);
        var source = game.AddCharacter(game.P1, "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");

        game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        game.Resolve();

        Assert.Contains(target, game.P2.TrashCards);
        Assert.Equal(4, game.P2.Hand.Count);
        Assert.Contains(game.Events.History, gameEvent => gameEvent is CharacterKOd { ByEffect: true });
    }

    [Fact]
    public void OnKO_ConditionIsCheckedOnTheField()
    {
        // Rule 10-2-17-1: [DON!! x1] [On K.O.] - the DON!! is still given when the card is K.O.'d.
        var game = new RuleScenario();
        var target = game.AddCharacter(game.P2, "[DON!! x1] [On K.O.] Draw 1 card.");
        target.AttachedDon = 1;

        game.Resolver.Engine.KnockOut(target, game, byEffect: true);
        game.Resolve();

        Assert.Equal(4, game.P2.Hand.Count);
        // Rule 6-5-5-4: the given DON!! went back to the cost area rested.
        Assert.Equal(1, game.P2.RestedDon);
    }

    [Fact]
    public void CannotBeKOdByOpponentsEffects_WinsAgainstAKOEffect()
    {
        // Rule 1-3-3: the prohibiting effect takes precedence.
        var game = new RuleScenario();
        var protectedCard = game.AddCharacter(game.P2, "This Character cannot be K.O.'d by your opponent's effects.", cost: 1);
        var source = game.AddCharacter(game.P1, "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");
        game.ProcessRules();

        game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        game.Resolve();

        Assert.Contains(protectedCard, game.P2.Characters);
        Assert.DoesNotContain(game.Requests, request => request is EffectChoiceNeeded choice && choice.Request.Cards.Contains(protectedCard));
    }

    [Fact]
    public void CannotBeKOdInBattle_WinsAgainstABattleKO_ButNotAgainstAnEffect()
    {
        // Rule 10-2-1-3: "cannot be K.O.'d in battle" only protects against the battle K.O.
        var game = new RuleScenario();
        var card = game.AddCharacter(game.P2, "This Character cannot be K.O.'d in battle.");
        game.ProcessRules();

        Assert.False(game.Resolver.Engine.KnockOut(card, game, byEffect: false));
        Assert.Contains(card, game.P2.Characters);
        Assert.True(game.Resolver.Engine.KnockOut(card, game, byEffect: true));
    }

    [Fact]
    public void Victory_InTheMiddleOfAnEffectChain_EndsAtOnce()
    {
        // Rule 9-1-2 / 9-2-1-2: the deck becomes empty during the first effect -> the game ends,
        // the second pending effect is not resolved any more.
        var game = new RuleScenario(deckSize: 2);
        var first = game.AddCharacter(game.P1, "[End of Your Turn] Draw 2 cards.", name: "First");
        game.AddCharacter(game.P1, "[End of Your Turn] Draw 1 card.", name: "Second");

        game.Events.Publish(new TurnEnding("p1", 3));
        game.Resolve(order => order.Triggers.OrderBy(trigger => trigger.Source == first ? 0 : 1).Select(trigger => trigger.Id).ToList());

        Assert.True(game.IsGameOver);
        Assert.Equal("p1", Assert.Single(game.Losers).PlayerId);
        Assert.Equal(["First"], game.DrawSources());
        Assert.False(game.Resolver.IsBusy);
    }

    [Fact]
    public void Victory_InTheMiddleOfOneEffect_StopsTheRestOfIt()
    {
        // Rule 9-1-2: rule processing runs after every step, even inside one effect.
        var game = new RuleScenario(deckSize: 1);
        var source = game.AddCharacter(game.P1, "[On Play] Draw 1 card and trash 1 card from your hand.");

        game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        game.Resolve();

        Assert.True(game.IsGameOver);
        Assert.DoesNotContain(game.Requests, request => request is EffectChoiceNeeded);
        Assert.Empty(game.P1.TrashCards);
    }

    [Fact]
    public void BothPlayersMeetADefeatCondition_BothLose()
    {
        // Rule 9-2-1: "all of those players lose the game".
        var game = new RuleScenario(deckSize: 0);

        Assert.True(game.ProcessRules());
        Assert.Equal(2, game.Losers.Count);
    }

    [Fact]
    public void LeaderDamageAtZeroLife_IsADefeatCondition()
    {
        // Rule 1-2-1-1-1 / 9-2-1-1.
        var game = new RuleScenario();
        game.P2.LifeCards.Clear();

        Assert.False(game.ProcessRules());
        game.P2.LeaderDamagedAtZeroLife = true;
        Assert.True(game.ProcessRules());
        Assert.Equal("p2", Assert.Single(game.Losers).PlayerId);
    }

    [Fact]
    public void Durations_EndAtTheRightTime()
    {
        // Rule 7-1-5-3/4: "during this battle" ends with the battle. Rule 6-6-1-3: "during this turn"
        // ends with the turn. "Until the end of your opponent's next turn" lasts one more turn.
        var game = new RuleScenario();
        var card = game.AddCharacter(game.P1, power: 5000);
        card.Modifiers.Add(new CardModifier { Power = 1000, Duration = Duration.ThisBattle, CreatedOnTurn = 3 });
        card.Modifiers.Add(new CardModifier { Power = 2000, Duration = Duration.ThisTurn, CreatedOnTurn = 3 });
        card.Modifiers.Add(new CardModifier { Power = 4000, Duration = Duration.UntilEndOfOpponentsNextTurn, CreatedOnTurn = 3 });
        game.P2.TurnFlags.Add(EffectFlags.CannotBeBlocked);

        Assert.Equal(12000, card.GetPower(isOwnersTurn: false));

        EffectDurations.EndOfBattle(game);
        Assert.Equal(11000, card.GetPower(isOwnersTurn: false));

        EffectDurations.EndOfTurn(game, endingTurn: 3);
        Assert.Equal(9000, card.GetPower(isOwnersTurn: false));
        Assert.Empty(game.P2.TurnFlags);

        EffectDurations.EndOfTurn(game, endingTurn: 4);
        Assert.Equal(5000, card.GetPower(isOwnersTurn: false));
    }

    [Fact]
    public void Power_CanBecomeNegative_CostNot()
    {
        // Rule 1-3-6-1: power can become negative. Rule 1-3-6-2: a negative cost counts as 0.
        var card = new CardInstance { BasePower = 1000, Cost = 1 };
        card.Modifiers.Add(new CardModifier { Power = -3000, Cost = -3 });

        Assert.Equal(-2000, card.GetPower(isOwnersTurn: false));
        Assert.Equal(0, card.CurrentCost);
    }

    [Fact]
    public void RuleProcessing_RecalculatesStaticEffects()
    {
        // StaticEffects is the only source for values from always-on effects.
        var game = new RuleScenario();
        var card = game.AddCharacter(game.P1, "[DON!! x1] This Character gains +1000 power.", power: 4000);
        card.AttachedDon = 1;

        game.ProcessRules();

        Assert.Equal(5000, card.GetPower(isOwnersTurn: false));
    }
}
