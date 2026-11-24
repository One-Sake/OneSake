using OneSake.Game.Actions;
using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: actions that change cards on the field (K.O., rest, return, power ...).
// The effects are [On Play] abilities of p1's Character "Source".
public class FieldStepBlockTests
{
    private readonly BlockGame _game = new();

    private CardInstance Source(string effect, int stepCount = 1)
    {
        var card = _game.OnField(_game.P1, $"[On Play] {effect}", name: "Source");
        Assert.Equal(stepCount, BlockGame.Ability(card).Steps.Count);
        return card;
    }

    [Fact]
    [BlockTest("Step.KO")]
    public void KO_TrashesTheChosenCharacter()
    {
        var source = Source("K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");
        Assert.Equal(StepKind.KO, BlockGame.Step(source).Kind);
        var small = _game.OnField(_game.P2, cost: 3);
        var big = _game.OnField(_game.P2, cost: 4);

        _game.Play(source);
        Assert.Equal(small, Assert.Single(_game.Offered));
        _game.Select(small);

        Assert.Contains(small, _game.P2.TrashCards);
        Assert.Contains(big, _game.P2.Characters);
        Assert.Contains(_game.Logs, line => line.StartsWith("EFFECT_KO"));
    }

    [Fact]
    [BlockTest("Step.KO")]
    public void KO_ActivatesTheOnKOEffectOfTheCharacter()
    {
        var source = Source("K.O. up to 1 of your opponent's Characters.");
        var victim = _game.OnField(_game.P2, "[On K.O.] Draw 1 card.");

        _game.Play(source);
        _game.Select(victim);

        Assert.Equal(4, _game.P2.Hand.Count);
    }

    [Fact]
    [BlockTest("Step.Rest")]
    public void Rest_RestsTheChosenCharacter()
    {
        var source = Source("Rest up to 1 of your opponent's Characters.");
        Assert.Equal(StepKind.Rest, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2);

        _game.Play(source);
        _game.Select(target);

        Assert.True(target.IsRested);
    }

    [Fact]
    [BlockTest("Step.SetActive")]
    public void SetActive_SetsTheChosenCharacterActive()
    {
        var source = Source("Set up to 1 of your Characters as active.");
        Assert.Equal(StepKind.SetActive, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P1);
        target.IsRested = true;

        _game.Play(source);
        _game.Select(target);

        Assert.False(target.IsRested);
    }

    [Fact]
    [BlockTest("Step.ReturnToHand")]
    public void ReturnToHand_GoesToTheOwnersHand()
    {
        var source = Source("Return up to 1 Character with a cost of 2 or less to the owner's hand.");
        Assert.Equal(StepKind.ReturnToHand, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2, cost: 2);
        target.AttachedDon = 1;
        target.IsRested = true;

        _game.Play(source);
        // "Character" without "your" / "your opponent's" means any Character.
        Assert.Contains(source, _game.Offered);
        _game.Select(target);

        Assert.Contains(target, _game.P2.Hand);
        Assert.Empty(_game.P2.Characters);
        // Rule 3-1-6: a card that leaves the field is a new card - no DON!!, active.
        Assert.False(target.IsRested);
        Assert.Equal(0, target.AttachedDon);
        Assert.Equal(1, _game.P2.RestedDon);
    }

    [Fact]
    [BlockTest("Step.BottomOfDeck")]
    public void BottomOfDeck_PlacesTheCharacterUnderTheOwnersDeck()
    {
        var source = Source("Place up to 1 of your opponent's Characters with a cost of 2 or less at the bottom of the owner's deck.");
        Assert.Equal(StepKind.BottomOfDeck, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2, cost: 2);

        _game.Play(source);
        _game.Select(target);

        Assert.Equal(target, _game.P2.DrawDeck[^1]);
    }

    [Fact]
    [BlockTest("Step.TopOfDeck")]
    public void TopOfDeck_PlacesTheCharacterOnTopOfTheOwnersDeck()
    {
        var source = Source("Place up to 1 of your opponent's Characters at the top of the owner's deck.");
        Assert.Equal(StepKind.TopOfDeck, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2);

        _game.Play(source);
        _game.Select(target);

        Assert.Equal(target, _game.P2.DrawDeck[0]);
    }

    [Fact]
    [BlockTest("Step.TrashCard")]
    public void TrashCard_TrashesWithoutAKO()
    {
        var source = Source("Trash up to 1 of your opponent's Characters with a cost of 1 or less.");
        Assert.Equal(StepKind.TrashCard, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2, "[On K.O.] Draw 1 card.", cost: 1);

        _game.Play(source);
        _game.Select(target);

        Assert.Contains(target, _game.P2.TrashCards);
        // Trashing is not a K.O.: no [On K.O.].
        Assert.Equal(3, _game.P2.Hand.Count);
    }

    [Fact]
    [BlockTest("Step.PowerModifier")]
    public void PowerModifier_Give_CanBeNegative()
    {
        var source = Source("Give up to 1 of your opponent's Characters -2000 power during this turn.");
        Assert.Equal(StepKind.PowerModifier, BlockGame.Step(source).Kind);
        Assert.Equal(-2000, BlockGame.Step(source).Amount);
        var target = _game.OnField(_game.P2, power: 1000);

        _game.Play(source);
        _game.Select(target);

        // Rule 1-3-6-1: power can go below 0.
        Assert.Equal(-1000, target.GetPower(isOwnersTurn: false));
    }

    [Fact]
    [BlockTest("Step.PowerModifier")]
    public void PowerModifier_Gains_AddsPower()
    {
        var source = Source("Your Leader gains +1000 power during this turn.");
        Assert.True(BlockGame.Step(source).Target!.YourLeader);

        _game.Play(source);

        Assert.Equal(6000, _game.P1.Leader!.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Step.CostModifier")]
    public void CostModifier_CannotGoBelowZero()
    {
        var source = Source("Give up to 1 of your opponent's Characters -3 cost during this turn.");
        Assert.Equal(StepKind.CostModifier, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2, cost: 2);

        _game.Play(source);
        _game.Select(target);

        Assert.Equal(0, target.CurrentCost);
    }

    [Fact]
    [BlockTest("Step.CostModifier")]
    public void CostModifier_ChangesWhatALaterFilterSees()
    {
        // -3 cost first, then "with a cost of 0" finds the card.
        var source = Source("Give up to 1 of your opponent's Characters -3 cost during this turn. Then, K.O. up to 1 of your opponent's Characters with a cost of 0.", 2);
        var target = _game.OnField(_game.P2, cost: 3);

        _game.Play(source);
        _game.Select(target);
        _game.Select(target);

        Assert.Contains(target, _game.P2.TrashCards);
    }

    [Fact]
    [BlockTest("Step.GainFlag")]
    public void GainFlag_Keyword_GivesTheKeyword()
    {
        var source = Source("Up to 1 of your Characters gains [Rush] during this turn.");
        Assert.Equal(StepKind.GainFlag, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P1);
        target.PlayedOnTurn = _game.Scenario.TurnNumber;
        // Rule 6-5-6-1: nobody attacks in their first turn.
        _game.P1.TurnsStarted = 2;
        Assert.False(AttackAction.CanAttack(target, _game.P1, _game.Scenario.TurnNumber));

        _game.Play(source);
        _game.Select(target);

        Assert.True(target.HasRush);
        Assert.True(AttackAction.CanAttack(target, _game.P1, _game.Scenario.TurnNumber));
    }

    [Fact]
    [BlockTest("Step.GiveRestedDon")]
    public void GiveRestedDon_MovesRestedDonToTheCard()
    {
        var source = Source("Give your Leader up to 1 rested DON!! card.");
        Assert.Equal(StepKind.GiveRestedDon, BlockGame.Step(source).Kind);
        _game.P1.RestedDon = 2;

        _game.Play(source);

        Assert.Equal(1, _game.P1.Leader!.AttachedDon);
        Assert.Equal(1, _game.P1.RestedDon);
    }

    [Fact]
    [BlockTest("Step.GiveRestedDon")]
    public void GiveRestedDon_WithoutRestedDon_DoesNothing()
    {
        var source = Source("Give up to 2 rested DON!! cards to your Leader.");
        _game.P1.ActiveDon = 2;

        _game.Play(source);

        Assert.Equal(0, _game.P1.Leader!.AttachedDon);
        Assert.Equal(2, _game.P1.ActiveDon);
    }

    [Fact]
    [BlockTest("Step.SkipRefresh")]
    [BlockTest("Flag.SkipNextRefresh")]
    public void SkipRefresh_TheCardStaysRestedOnce()
    {
        var source = Source("Up to 1 of your opponent's rested Characters will not become active in your opponent's next Refresh Phase.");
        Assert.Equal(StepKind.SkipRefresh, BlockGame.Step(source).Kind);
        var target = _game.OnField(_game.P2);
        target.IsRested = true;

        _game.Play(source);
        _game.Select(target);

        TurnPhases.Refresh(_game.P2, _game.Scenario);
        Assert.True(target.IsRested);

        TurnPhases.Refresh(_game.P2, _game.Scenario);
        Assert.False(target.IsRested);
    }

    [Fact]
    [BlockTest("Step.Select")]
    [BlockTest("Target.Previous")]
    public void Select_ThatCharacter_IsTheSelectedCard()
    {
        var source = Source("Select up to 1 of your opponent's Characters. That Character cannot attack during this turn.", 2);
        Assert.Equal(StepKind.Select, BlockGame.Step(source).Kind);
        Assert.True(BlockGame.Step(source, stepIndex: 1).Target!.Previous);
        var chosen = _game.OnField(_game.P2);
        var other = _game.OnField(_game.P2);

        _game.Play(source);
        _game.Select(chosen);

        Assert.True(chosen.HasFlag(EffectFlags.CannotAttack));
        Assert.False(other.HasFlag(EffectFlags.CannotAttack));
    }

    [Fact]
    [BlockTest("Target.Previous")]
    public void Previous_IsEmptyWhenNothingWasChosen()
    {
        var source = Source("Select up to 1 of your opponent's Characters. That Character cannot attack during this turn.", 2);
        var other = _game.OnField(_game.P2);

        _game.Play(source);
        _game.Done();

        Assert.False(other.HasFlag(EffectFlags.CannotAttack));
        Assert.Null(_game.Request);
    }

    [Fact]
    [BlockTest("Step.NegateBlockers")]
    public void NegateBlockers_ForTheTurn_IsAPlayerFlag()
    {
        var source = Source("Your opponent cannot activate [Blocker] during this turn.");
        Assert.Equal(StepKind.NegateBlockers, BlockGame.Step(source).Kind);

        _game.Play(source);

        Assert.Contains(EffectFlags.CannotBeBlocked, _game.P1.TurnFlags);
        _game.NextTurn(_game.P2);
        Assert.Empty(_game.P1.TurnFlags);
    }

    [Fact]
    [BlockTest("Step.NegateBlockers")]
    public void NegateBlockers_ForTheBattle_OnlyProtectsTheAttacker()
    {
        var attacker = _game.OnField(_game.P1, "[When Attacking] Your opponent cannot activate [Blocker] during this battle.");
        _game.Scenario.Battle = new BattleState { Attacker = attacker, Target = _game.P2.Leader!, AttackerPlayerId = "p1", DefenderPlayerId = "p2" };

        _game.Publish(new AttackDeclared(attacker, _game.P2.Leader!, "p1", "p2"));

        Assert.True(attacker.HasFlag(EffectFlags.CannotBeBlocked));
        Assert.Empty(_game.P1.TurnFlags);
        EffectDurations.EndOfBattle(_game.Scenario);
        Assert.False(attacker.HasFlag(EffectFlags.CannotBeBlocked));
    }
}
