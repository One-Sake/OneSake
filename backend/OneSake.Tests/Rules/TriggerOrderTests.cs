using OneSake.Game.Effects;
using OneSake.Game.Rules;

namespace OneSake.Tests.Rules;

// Rule 8-6: order of effect resolution.
public class TriggerOrderTests
{
    private readonly RuleScenario _game = new();

    [Fact]
    public void TwoOwnEffectsAtTheSameTime_ThePlayerChoosesTheOrder_AndTheResultDependsOnIt()
    {
        // Rule 6-6-1-1-3: several [End of Your Turn] effects, the turn player chooses the order.
        var draw = _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "Drawer");
        var check = _game.AddCharacter(_game.P1, "[End of Your Turn] If you have 4 or more cards in your hand, draw 1 card.", name: "Checker");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve(order => order.Triggers.OrderBy(trigger => trigger.Source == draw ? 0 : 1).Select(trigger => trigger.Id).ToList());

        var order = Assert.IsType<OrderNeeded>(_game.Requests[0]);
        Assert.Equal("p1", order.PlayerId);
        Assert.Equal(2, order.Triggers.Count);
        // Drawer first: 3 -> 4 cards, then Checker draws as well.
        Assert.Equal(5, _game.P1.Hand.Count);

        var other = new RuleScenario();
        var draw2 = other.AddCharacter(other.P1, "[End of Your Turn] Draw 1 card.", name: "Drawer");
        other.AddCharacter(other.P1, "[End of Your Turn] If you have 4 or more cards in your hand, draw 1 card.", name: "Checker");
        other.Events.Publish(new TurnEnding("p1", 3));
        other.Resolve(order2 => order2.Triggers.OrderBy(trigger => trigger.Source == draw2 ? 1 : 0).Select(trigger => trigger.Id).ToList());

        // Checker first: only 3 cards, no draw. Then Drawer: 4 cards.
        Assert.Equal(4, other.P1.Hand.Count);
        Assert.NotNull(check);
    }

    [Fact]
    public void OneEffect_DoesNotAskForAnOrder()
    {
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve();

        Assert.DoesNotContain(_game.Requests, request => request is OrderNeeded);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    public void EffectsOfBothPlayers_TheTurnPlayerResolvesFirst()
    {
        // Rule 8-6-1 / 10-2-16-1: [When Attacking] of the attacker before [On Your Opponent's Attack].
        var attacker = _game.AddCharacter(_game.P1, "[When Attacking] Draw 1 card.", name: "Attacker");
        _game.AddCharacter(_game.P2, "[On Your Opponent's Attack] Draw 1 card.", name: "Defender");

        // The defender's effect is reported first - the order must still be turn player first.
        _game.Events.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));
        _game.Resolve();

        Assert.Equal(["Attacker", "Defender"], _game.DrawSources());
    }

    [Fact]
    public void EndOfTurn_TurnPlayerEffectsFirst_ThenTheOpponents()
    {
        // Rule 6-6-1-1-2: all [End of Your Turn] effects, then all [End of Your Opponent's Turn] effects.
        _game.AddCharacter(_game.P2, "[End of Your Opponent's Turn] Draw 1 card.", name: "Opponent");
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "Own");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve();

        Assert.Equal(["Own", "Opponent"], _game.DrawSources());
    }

    [Fact]
    public void NewEffectDuringResolution_WaitsBehindTheEffectsThatAreAlreadyPending()
    {
        // Rule 8-6-1 / 8-6-1-1: the turn player's A and B are pending, the opponent's D too.
        // A K.O.s a Character, its [On K.O.] (C) becomes pending -> order A, B, D, C.
        var target = _game.AddCharacter(_game.P2, "[On K.O.] Draw 1 card.", cost: 1, name: "C-KOd");
        var a = _game.AddCharacter(_game.P1, "[End of Your Turn] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.", name: "A-KO");
        var b = _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "B");
        _game.AddCharacter(_game.P2, "[End of Your Opponent's Turn] Draw 1 card.", name: "D");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve(order => order.Triggers.OrderBy(trigger => trigger.Source == a ? 0 : 1).Select(trigger => trigger.Id).ToList());

        Assert.Contains(target, _game.P2.TrashCards);
        Assert.Equal(["B", "D", "C-KOd"], _game.DrawSources());
        Assert.NotNull(b);
    }

    [Fact]
    public void EffectThatTriggersAnotherEffect_ResolvesItAfterwards()
    {
        // Rule 8-6-3: the [On K.O.] caused by an [On Play] is activated after the [On Play] resolved.
        var target = _game.AddCharacter(_game.P2, "[On K.O.] Draw 1 card.", cost: 1, name: "Target");
        var source = _game.AddCharacter(_game.P1, "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less. Then, draw 1 card.", name: "Source");

        _game.Events.Publish(new CardPlayed(source, "p1", ByEffect: false));
        _game.Resolve();

        Assert.Contains(target, _game.P2.TrashCards);
        Assert.Equal(["Source", "Target"], _game.DrawSources());
    }

    [Fact]
    public void OncePerTurn_OnlyOnceAcrossSeveralActivations()
    {
        // Rule 10-2-13-1/3: two attacks in the same turn, the [Once Per Turn] effect resolves once.
        var defender = _game.AddCharacter(_game.P2, "[On Your Opponent's Attack] [Once Per Turn] Draw 1 card.");
        var attacker = _game.AddCharacter(_game.P1);

        _game.Events.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));
        _game.Resolve();
        _game.Events.Publish(new AttackDeclared(_game.P1.Leader!, _game.P2.Leader!, "p1", "p2"));
        _game.Resolve();

        Assert.Equal(4, _game.P2.Hand.Count);
        Assert.NotNull(defender);
    }

    [Fact]
    public void OncePerTurn_TwoPendingActivations_OnlyTheFirstResolves()
    {
        // Rule 10-2-13-3: both activations are pending at the same time; after the first resolved,
        // the second cannot activate any more.
        var defender = _game.AddCharacter(_game.P2, "[On Your Opponent's Attack] [Once Per Turn] Draw 1 card.");
        var attacker = _game.AddCharacter(_game.P1);

        _game.Events.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));
        _game.Events.Publish(new AttackDeclared(_game.P1.Leader!, _game.P2.Leader!, "p1", "p2"));
        _game.Resolve();

        Assert.Equal(4, _game.P2.Hand.Count);
        Assert.Contains(_game.Logs, line => line.Contains("[Once Per Turn] was already used"));
        Assert.NotNull(defender);
    }

    [Fact]
    public void DonCondition_LostBeforeActivation_TheEffectDoesNotActivate()
    {
        // Rule 8-3-2-3 / 8-4-1-1: A needs [DON!! x1]. B is resolved first and returns that DON!!
        // (DON!! -1 takes the given DON!! because the cost area is empty).
        var a = _game.AddCharacter(_game.P1, "[DON!! x1] [End of Your Turn] Draw 1 card.", name: "A");
        var b = _game.AddCharacter(_game.P1, "[End of Your Turn] DON!! -1: Draw 2 cards.", name: "B");
        a.AttachedDon = 1;

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve(order => order.Triggers.OrderBy(trigger => trigger.Source == b ? 0 : 1).Select(trigger => trigger.Id).ToList());

        Assert.Equal(0, a.AttachedDon);
        Assert.Equal(5, _game.P1.Hand.Count);
        Assert.Equal(["B"], _game.DrawSources());
        Assert.Contains(_game.Logs, line => line.Contains("A: the conditions are no longer met"));
    }

    [Fact]
    public void CardThatLeftTheField_ItsPendingEffectDoesNotActivate()
    {
        // Rule 8-1-3-1-3: A is K.O.'d by B before A's effect activates (not an [On K.O.]).
        var a = _game.AddCharacter(_game.P2, "[End of Your Opponent's Turn] Draw 1 card.", cost: 1, name: "A");
        _game.AddCharacter(_game.P1, "[End of Your Turn] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.", name: "B");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve();

        Assert.Contains(a, _game.P2.TrashCards);
        Assert.Empty(_game.DrawSources());
        Assert.Contains(_game.Logs, line => line.Contains("A left the field"));
    }

    [Fact]
    public void OrderAnswer_MustContainEveryEffectOnce()
    {
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.");
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.");
        _game.Events.Publish(new TurnEnding("p1", 3));

        var order = Assert.IsType<OrderNeeded>(_game.Resolver.Run());

        Assert.Throws<InvalidOperationException>(() => _game.Resolver.AnswerOrder("p1", [order.Triggers[0].Id]));
        Assert.Throws<InvalidOperationException>(() => _game.Resolver.AnswerOrder("p2", order.Triggers.Select(trigger => trigger.Id).ToList()));
        Assert.Null(_game.Resolver.AnswerOrder("p1", order.Triggers.Select(trigger => trigger.Id).Reverse().ToList()));
    }

    [Fact]
    public void ResolutionOrder_IsWrittenToTheLog()
    {
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "Zoro");
        _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "Nami");

        _game.Events.Publish(new TurnEnding("p1", 3));
        _game.Resolve();

        Assert.Contains(_game.Logs, line => line == "EFFECT_ORDER: 1/2: Zoro [End of Your Turn] Draw 1 card.");
        Assert.Contains(_game.Logs, line => line == "EFFECT_ORDER: 2/2: Nami [End of Your Turn] Draw 1 card.");
    }

    [Fact]
    public void PlayerStartedEffect_IsResolvedBeforeWaitingEffects()
    {
        // Rule 8-4-1: an activated Event resolves now; effects it causes wait (rule 8-6-3).
        var waiting = _game.AddCharacter(_game.P1, "[End of Your Turn] Draw 1 card.", name: "Waiting");
        _game.Events.Publish(new TurnEnding("p1", 3));
        var eventCard = _game.Card(_game.P1, "[Main] Draw 1 card.", category: "Event", name: "Event");
        _game.Resolver.Activate(eventCard, "p1", EffectTiming.EventMain);
        _game.Resolve();

        Assert.Equal(["Event", "Waiting"], _game.DrawSources());
        Assert.NotNull(waiting);
    }
}
