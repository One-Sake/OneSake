using OneSake.Domain;
using OneSake.Game.Actions;
using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: how long an effect lasts, and the flags ("cannot ...", "can also ...") a card can gain.
public class DurationAndFlagBlockTests
{
    private readonly BlockGame _game = new();

    public DurationAndFlagBlockTests()
    {
        // Both players had their first turn, so attacks are allowed.
        _game.P1.TurnsStarted = 2;
        _game.P2.TurnsStarted = 2;
    }

    private CardInstance PlayLeaderBuff(string duration)
    {
        var source = _game.OnField(_game.P1, $"[On Play] Your Leader gains +1000 power{duration}.");
        _game.Play(source);
        return _game.P1.Leader!;
    }

    // ---------- Durations ----------

    [Fact]
    [BlockTest("Duration.ThisTurn")]
    public void ThisTurn_EndsAtTheEndOfTheTurn()
    {
        var leader = PlayLeaderBuff(" during this turn");
        Assert.Equal(6000, leader.GetPower(isOwnersTurn: true));

        EffectDurations.EndOfBattle(_game.Scenario);
        Assert.Equal(6000, leader.GetPower(isOwnersTurn: true));

        _game.NextTurn(_game.P2);
        Assert.Equal(5000, leader.GetPower(isOwnersTurn: false));
    }

    [Fact]
    [BlockTest("Duration.ThisBattle")]
    public void ThisBattle_EndsAtTheEndOfTheBattle()
    {
        var leader = PlayLeaderBuff(" during this battle");
        Assert.Equal(6000, leader.GetPower(isOwnersTurn: true));

        EffectDurations.EndOfBattle(_game.Scenario);

        Assert.Equal(5000, leader.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Duration.UntilEndOfOpponentsNextTurn")]
    public void UntilTheEndOfYourOpponentsNextTurn_LastsOneMoreTurn()
    {
        var leader = PlayLeaderBuff(" until the end of your opponent's next turn");

        _game.NextTurn(_game.P2);
        Assert.Equal(6000, leader.GetPower(isOwnersTurn: false));

        _game.NextTurn(_game.P1);
        Assert.Equal(5000, leader.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Duration.UntilEndOfOpponentsNextTurn")]
    public void UntilTheStartOfYourNextTurn_IsTheSameDuration()
    {
        var source = _game.OnField(_game.P1, "[On Play] Your Leader gains +1000 power until the start of your next turn.");

        Assert.Equal(Duration.UntilEndOfOpponentsNextTurn, BlockGame.Step(source).Duration);
    }

    [Fact]
    [BlockTest("Duration.Permanent")]
    public void Permanent_LastsWhileTheCardIsOnTheField()
    {
        // Without a duration, an effect lasts until the card leaves the field.
        var source = _game.OnField(_game.P1, "[On Play] This Character gains +1000 power.");
        Assert.Equal(Duration.Permanent, BlockGame.Step(source).Duration);

        _game.Play(source);
        _game.NextTurn(_game.P2);
        _game.NextTurn(_game.P1);
        Assert.Equal(3000, source.GetPower(isOwnersTurn: true));

        _game.P1.RemoveFromField(source);
        source.ResetFieldState();
        Assert.Equal(2000, source.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Duration.Permanent")]
    public void StaticAbilities_ArePermanent()
    {
        var card = _game.OnField(_game.P1, "This Character gains +1000 power.");

        Assert.Equal(Duration.Permanent, BlockGame.Step(card).Duration);
        _game.NextTurn(_game.P2);
        Assert.Equal(3000, card.GetPower(isOwnersTurn: false));
    }

    // ---------- Flags ----------

    [Fact]
    [BlockTest("Flag.CannotAttack")]
    public void CannotAttack_WinsOverRush()
    {
        var source = _game.OnField(_game.P2, "[On Play] Up to 1 of your opponent's Characters cannot attack during this turn.");
        var target = _game.OnField(_game.P1, "[Rush]");
        Assert.Equal(EffectFlags.CannotAttack, BlockGame.Step(source).Flag);

        _game.Play(source);
        _game.Select(target);

        Assert.False(Prohibitions.CanAttack(target));
        Assert.False(AttackAction.CanAttack(target, _game.P1, _game.Scenario.TurnNumber));
    }

    [Fact]
    [BlockTest("Flag.CannotBeKOdInBattle")]
    public void CannotBeKOdInBattle_ButByEffects()
    {
        var card = _game.OnField(_game.P2, "This Character cannot be K.O.'d in battle.");
        Assert.Equal(EffectFlags.CannotBeKOdInBattle, BlockGame.Step(card).Flag);
        var engine = _game.Scenario.Resolver.Engine;

        Assert.False(engine.KnockOut(card, _game.Scenario, byEffect: false));
        Assert.Contains(card, _game.P2.Characters);

        Assert.True(engine.KnockOut(card, _game.Scenario, byEffect: true));
    }

    [Fact]
    [BlockTest("Flag.CannotBeKOdByEffects")]
    public void CannotBeKOdByEffects_OnlyProtectsAgainstTheOpponent()
    {
        var card = _game.OnField(_game.P2, "This Character cannot be K.O.'d by your opponent's effects.");
        Assert.Equal(EffectFlags.CannotBeKOdByEffects, BlockGame.Step(card).Flag);
        var other = _game.OnField(_game.P2);
        var opponentsKo = _game.OnField(_game.P1, "[On Play] K.O. up to 1 of your opponent's Characters.");

        _game.Play(opponentsKo);
        Assert.Equal(other, Assert.Single(_game.Offered));
        _game.Done();

        // Its owner's own effects can still K.O. it.
        var ownKo = _game.OnField(_game.P2, "[On Play] K.O. up to 1 of your Characters.");
        _game.Play(ownKo);
        Assert.Contains(card, _game.Offered);
    }

    [Fact]
    [BlockTest("Flag.CannotBeRestedByEffects")]
    public void CannotBeRestedByEffects()
    {
        var card = _game.OnField(_game.P2, "This Character cannot be rested by your opponent's effects.");
        Assert.Equal(EffectFlags.CannotBeRestedByEffects, BlockGame.Step(card).Flag);
        var rest = _game.OnField(_game.P1, "[On Play] Rest up to 1 of your opponent's Characters.");

        // The only Character cannot be chosen: nothing to ask.
        Assert.Null(_game.Play(rest));
        Assert.False(card.IsRested);
    }

    [Fact]
    [BlockTest("Flag.CannotBeBlocked")]
    public void Unblockable_IsCannotBeBlocked()
    {
        var card = _game.OnField(_game.P1, "[DON!! x1] This Character gains [Unblockable].");
        Assert.Equal(EffectFlags.CannotBeBlocked, BlockGame.Step(card).Flag);

        card.AttachedDon = 1;
        _game.Recalculate();

        // The MatchRoom does not offer a block against cards with this flag.
        Assert.True(card.HasFlag(EffectFlags.CannotBeBlocked));
    }

    [Fact]
    [BlockTest("Flag.CannotBeBlocked")]
    public void CannotActivateBlockerWhenThisAttacks_IsCannotBeBlocked()
    {
        var card = _game.OnField(_game.P1, "[When Attacking] Your opponent cannot activate [Blocker] when this Character attacks during this battle.");

        Assert.Equal(EffectFlags.CannotBeBlocked, BlockGame.Step(card).Flag);
        Assert.True(BlockGame.Step(card).Target!.Self);
    }

    [Fact]
    [BlockTest("Flag.CanAttackActiveCharacters")]
    public void CanAlsoAttackActiveCharacters()
    {
        var attacker = _game.OnField(_game.P1, "[On Play] This Character can also attack your opponent's active Characters during this turn.");
        var target = _game.OnField(_game.P2);
        var action = new AttackAction();

        Assert.Throws<InvalidOperationException>(() =>
            action.Execute(_game.P1, _game.P2, MatchPhase.Main, "p1", attacker.InstanceId, target.InstanceId, _game.Scenario.TurnNumber));

        _game.Play(attacker);
        var battle = action.Execute(_game.P1, _game.P2, MatchPhase.Main, "p1", attacker.InstanceId, target.InstanceId, _game.Scenario.TurnNumber);

        Assert.Equal(target, battle.Target);
    }

    [Fact]
    [BlockTest("Flag.CannotBlock")]
    public void CannotBeRested_MeansNoAttackAndNoBlock()
    {
        var source = _game.OnField(_game.P1, "[On Play] Up to 1 of your opponent's Characters cannot be rested during this turn.");
        Assert.Equal(2, BlockGame.Ability(source).Steps.Count);
        var blocker = _game.OnField(_game.P2, "[Blocker]");
        Assert.True(blocker.HasBlocker);

        _game.Play(source);
        _game.Select(blocker);

        Assert.True(blocker.HasFlag(EffectFlags.CannotBlock));
        Assert.False(blocker.HasBlocker);
        Assert.True(blocker.HasFlag(EffectFlags.CannotAttack));
    }

    [Fact]
    [BlockTest("Flag.RushCharacter")]
    public void RushCharacter_CanOnlyAttackCharactersInTheTurnItIsPlayed()
    {
        var card = _game.OnField(_game.P1, "This Character can attack Characters on the turn in which it is played.");
        Assert.Equal(EffectFlags.RushCharacter, BlockGame.Step(card).Flag);
        card.PlayedOnTurn = _game.Scenario.TurnNumber;
        _game.Recalculate();
        var target = _game.OnField(_game.P2);
        target.IsRested = true;

        Assert.True(AttackAction.CanAttack(card, _game.P1, _game.Scenario.TurnNumber));
        Assert.Throws<InvalidOperationException>(() =>
            new AttackAction().Execute(_game.P1, _game.P2, MatchPhase.Main, "p1", card.InstanceId, _game.P2.Leader!.InstanceId, _game.Scenario.TurnNumber));
        new AttackAction().Execute(_game.P1, _game.P2, MatchPhase.Main, "p1", card.InstanceId, target.InstanceId, _game.Scenario.TurnNumber);
    }
}
