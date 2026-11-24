using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: when an ability is used, and the parts of the ability header.
public class TimingBlockTests
{
    private readonly BlockGame _game = new();

    [Fact]
    [BlockTest("Timing.OnPlay")]
    public void OnPlay_RunsWhenTheCardIsPlayed()
    {
        var card = _game.OnField(_game.P1, "[On Play] Draw 1 card.");
        Assert.Equal(EffectTiming.OnPlay, BlockGame.Ability(card).Timing);

        _game.Play(card);

        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.WhenAttacking")]
    public void WhenAttacking_RunsForTheAttackerOnly()
    {
        var attacker = _game.OnField(_game.P1, "[When Attacking] Draw 1 card.");
        _game.OnField(_game.P1, "[When Attacking] Draw 1 card.");
        Assert.Equal(EffectTiming.WhenAttacking, BlockGame.Ability(attacker).Timing);

        _game.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));

        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.OnOpponentsAttack")]
    public void OnYourOpponentsAttack_RunsForTheDefendersCards()
    {
        var defender = _game.OnField(_game.P2, "[On Your Opponent's Attack] Draw 1 card.");
        var attacker = _game.OnField(_game.P1, "[On Your Opponent's Attack] Draw 1 card.");
        Assert.Equal(EffectTiming.OnOpponentsAttack, BlockGame.Ability(defender).Timing);

        _game.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));

        Assert.Equal(4, _game.P2.Hand.Count);
        Assert.Equal(3, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.OnBlock")]
    public void OnBlock_RunsWhenTheBlockerIsActivated()
    {
        var blocker = _game.OnField(_game.P2, "[Blocker] [On Block] Draw 1 card.");
        Assert.Single(blocker.Script.WithTiming(EffectTiming.OnBlock));
        Assert.True(blocker.HasBlocker);

        _game.Publish(new BlockerDeclared(blocker, "p2"));

        Assert.Equal(4, _game.P2.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.OnKO")]
    public void OnKO_IsCheckedOnTheField_AndResolvedFromTheTrash()
    {
        var card = _game.OnField(_game.P2, "[On K.O.] Draw 1 card.");
        Assert.Equal(EffectTiming.OnKO, BlockGame.Ability(card).Timing);

        Assert.True(_game.Scenario.Resolver.Engine.KnockOut(card, _game.Scenario, byEffect: true));
        _game.Run();

        Assert.Contains(card, _game.P2.TrashCards);
        Assert.Equal(4, _game.P2.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.EndOfYourTurn")]
    [BlockTest("Timing.EndOfOpponentsTurn")]
    public void EndOfTurn_RunsForTheTurnPlayer_AndForTheOpponent()
    {
        var own = _game.OnField(_game.P1, "[End of Your Turn] Draw 1 card.");
        _game.OnField(_game.P1, "[End of Your Opponent's Turn] Draw 1 card.");
        var other = _game.OnField(_game.P2, "[End of Your Opponent's Turn] Draw 2 cards.");
        _game.OnField(_game.P2, "[End of Your Turn] Draw 2 cards.");
        Assert.Equal(EffectTiming.EndOfYourTurn, BlockGame.Ability(own).Timing);
        Assert.Equal(EffectTiming.EndOfOpponentsTurn, BlockGame.Ability(other).Timing);

        // p1's turn ends: p1's [End of Your Turn] and p2's [End of Your Opponent's Turn].
        _game.Publish(new TurnEnding("p1", 3));

        Assert.Equal(4, _game.P1.Hand.Count);
        Assert.Equal(5, _game.P2.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.ActivateMain")]
    public void ActivateMain_IsStartedByThePlayer_WithoutAUseQuestion()
    {
        var card = _game.OnField(_game.P1, "[Activate: Main] Draw 1 card.");
        var ability = BlockGame.Ability(card);
        Assert.Equal(EffectTiming.ActivateMain, ability.Timing);
        Assert.True(EffectEngine.CanActivate(ability, card, "p1", _game.Scenario));

        // Playing the card does not use it.
        _game.Play(card);
        Assert.Equal(3, _game.P1.Hand.Count);

        Assert.Null(_game.ActivateMain(card));
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.EventMain")]
    public void EventMain_RunsWhenTheEventIsActivated()
    {
        var card = _game.InHand(_game.P1, "[Main] Draw 2 cards.", category: CardInstance.EventCategory);
        Assert.Equal(EffectTiming.EventMain, BlockGame.Ability(card).Timing);

        _game.Activate(card, EffectTiming.EventMain);

        Assert.Equal(6, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.EventCounter")]
    public void EventCounter_RunsInTheCounterStep()
    {
        var card = _game.InHand(_game.P2, "[Counter] Up to 1 of your Leader or Character cards gains +2000 power during this battle.",
            category: CardInstance.EventCategory);
        Assert.Equal(EffectTiming.EventCounter, BlockGame.Ability(card).Timing);

        _game.Activate(card, EffectTiming.EventCounter);
        _game.Select(_game.P2.Leader!);

        Assert.Equal(7000, _game.P2.Leader!.GetPower(isOwnersTurn: false));
    }

    [Fact]
    [BlockTest("Timing.Trigger")]
    public void Trigger_RunsWhenThePlayerActivatesTheLifeCard()
    {
        var card = _game.InHand(_game.P1, "[Trigger] Draw 1 card.");
        Assert.Equal(EffectTiming.Trigger, BlockGame.Ability(card).Timing);
        Assert.True(card.HasTrigger);

        _game.Activate(card, EffectTiming.Trigger);

        Assert.Equal(5, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Timing.Static")]
    [BlockTest("Ability.RequiredDon")]
    public void Static_DonRequirement_IsCheckedAllTheTime()
    {
        var card = _game.OnField(_game.P1, "[DON!! x1] This Character gains +1000 power.");
        var ability = BlockGame.Ability(card);
        Assert.Equal(EffectTiming.Static, ability.Timing);
        Assert.Equal(1, ability.RequiredDon);

        Assert.Equal(2000, card.GetPower(isOwnersTurn: true));

        card.AttachedDon = 1;
        _game.Recalculate();
        // 2000 + 1000 (given DON!!) + 1000 (effect)
        Assert.Equal(4000, card.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Ability.RequiredDon")]
    public void DonRequirement_OfATriggeredAbility_IsCheckedWhenItActivates()
    {
        var card = _game.OnField(_game.P1, "[DON!! x2] [When Attacking] Draw 1 card.");
        card.AttachedDon = 1;

        _game.Publish(new AttackDeclared(card, _game.P2.Leader!, "p1", "p2"));
        Assert.Equal(3, _game.P1.Hand.Count);

        card.AttachedDon = 2;
        _game.Publish(new AttackDeclared(card, _game.P2.Leader!, "p1", "p2"));
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Ability.OncePerTurn")]
    public void OncePerTurn_CanBeUsedAgainInTheNextTurn()
    {
        var card = _game.OnField(_game.P1, "[Activate: Main] [Once Per Turn] Draw 1 card.");
        var ability = BlockGame.Ability(card);
        Assert.True(ability.OncePerTurn);

        _game.ActivateMain(card);
        Assert.False(EffectEngine.CanActivate(ability, card, "p1", _game.Scenario));

        _game.NextTurn(_game.P2);
        _game.NextTurn(_game.P1);
        Assert.True(EffectEngine.CanActivate(ability, card, "p1", _game.Scenario));
    }

    [Fact]
    [BlockTest("Ability.OnlyYourTurn")]
    [BlockTest("Ability.OnlyOpponentsTurn")]
    public void YourTurn_AndOpponentsTurn_OnlyWorkInThatTurn()
    {
        var yourTurn = _game.OnField(_game.P1, "[Your Turn] This Character gains +1000 power.");
        var opponentsTurn = _game.OnField(_game.P1, "[Opponent's Turn] This Character gains +2000 power.");
        Assert.True(BlockGame.Ability(yourTurn).OnlyYourTurn);
        Assert.True(BlockGame.Ability(opponentsTurn).OnlyOpponentsTurn);

        _game.Recalculate();
        Assert.Equal(3000, yourTurn.GetPower(isOwnersTurn: true));
        Assert.Equal(2000, opponentsTurn.GetPower(isOwnersTurn: true));

        _game.NextTurn(_game.P2);
        Assert.Equal(2000, yourTurn.GetPower(isOwnersTurn: false));
        Assert.Equal(4000, opponentsTurn.GetPower(isOwnersTurn: false));
    }

    [Fact]
    [BlockTest("Ability.Conditions")]
    public void LeadingIf_OfAStaticAbility_IsAConditionOfTheWholeAbility()
    {
        var card = _game.OnField(_game.P1, "If you have 2 or less Life cards, this Character gains +2000 power.");
        var ability = BlockGame.Ability(card);
        Assert.Equal(ConditionKind.LifeAtMost, Assert.Single(ability.Conditions).Kind);

        _game.Recalculate();
        Assert.Equal(2000, card.GetPower(isOwnersTurn: true));

        _game.P1.LifeCards.RemoveRange(0, 3);
        _game.Recalculate();
        Assert.Equal(4000, card.GetPower(isOwnersTurn: true));
    }
}
