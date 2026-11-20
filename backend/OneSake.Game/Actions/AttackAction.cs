using OneSake.Domain;
using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Rule 7-1-1 Attack Step: rest an active Leader/Character and choose the opponent's Leader
// or one of their rested Characters as target.
public sealed class AttackAction
{
    public BattleState Execute(
        PlayerState attacker,
        PlayerState opponent,
        MatchPhase phase,
        string activePlayerId,
        string? attackerInstanceId,
        string? targetInstanceId,
        int turnNumber,
        GameEventBus? events = null)
    {
        ActionGuard.EnsureMainPhase(phase);
        ActionGuard.EnsureActivePlayer(attacker, activePlayerId);

        if (!CanBattleThisTurn(attacker))
        {
            throw new InvalidOperationException("Neither player can battle on their first turn.");
        }

        var attackerId = ActionGuard.RequireId(attackerInstanceId, "Choose your attacking Leader or Character.");
        var targetId = ActionGuard.RequireId(targetInstanceId, "Choose the target of your attack.");

        var attackingCard = attacker.FindFieldCard(attackerId)
            ?? throw new InvalidOperationException("The attacker must be your Leader or one of your Characters.");

        if (!CanAttack(attackingCard, attacker, turnNumber))
        {
            throw new InvalidOperationException($"{attackingCard.Name} cannot attack right now.");
        }

        var targetCard = opponent.FindFieldCard(targetId)
            ?? throw new InvalidOperationException("The target must be the opponent's Leader or one of their Characters.");

        if (targetCard.IsCharacter && !targetCard.IsRested && !attackingCard.HasFlag(EffectFlags.CanAttackActiveCharacters))
        {
            throw new InvalidOperationException("You can only attack rested Characters.");
        }

        // [Rush: Character]: in the turn it was played, it may only attack Characters.
        if (targetCard.IsLeader && attackingCard.IsCharacter && attackingCard.PlayedOnTurn == turnNumber && !attackingCard.HasRush)
        {
            throw new InvalidOperationException($"{attackingCard.Name} can only attack Characters in the turn it was played.");
        }

        attackingCard.IsRested = true;

        var battle = new BattleState
        {
            AttackerPlayerId = attacker.PlayerId,
            DefenderPlayerId = opponent.PlayerId,
            Attacker = attackingCard,
            Target = targetCard,
            Step = BattleStep.Block
        };

        events?.Publish(new CardRested(attackingCard, attacker.PlayerId));
        events?.Publish(new AttackDeclared(attackingCard, targetCard, attacker.PlayerId, opponent.PlayerId));
        return battle;
    }

    // Rule 6-5-6-1: no battles on your own first turn.
    public static bool CanBattleThisTurn(PlayerState player)
    {
        return player.TurnsStarted > 1;
    }

    // Active Leader, or active Character that was not played this turn (unless it has [Rush]).
    public static bool CanAttack(CardInstance card, PlayerState owner, int turnNumber)
    {
        if (card.IsRested || !CanBattleThisTurn(owner) || card.HasFlag(EffectFlags.CannotAttack))
        {
            return false;
        }

        if (card.IsLeader)
        {
            return true;
        }

        return card.IsCharacter
            && (card.PlayedOnTurn != turnNumber || card.HasRush || card.HasFlag(EffectFlags.RushCharacter));
    }
}
