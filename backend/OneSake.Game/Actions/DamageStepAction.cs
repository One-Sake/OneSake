using OneSake.Game.State;

namespace OneSake.Game.Actions;

public sealed record BattleResult
{
    public bool AttackerWon { get; init; }
    public int AttackerPower { get; init; }
    public int TargetPower { get; init; }
    public bool TargetWasLeader { get; init; }
    public bool CharacterKnockedOut { get; init; }
    public int LifeCardsLost { get; init; }
    // True when the Leader took damage with 0 Life cards -> the attacker wins the game.
    public bool DefenderLostGame { get; init; }
}

// Rule 7-1-4 Damage Step: compare power, then deal damage to the Leader or K.O. the Character.
public sealed class DamageStepAction
{
    public BattleResult Execute(BattleState battle, PlayerState attacker, PlayerState defender)
    {
        // The attacker belongs to the turn player, so its given DON!! count. The defender's do not.
        var attackerPower = battle.Attacker.GetPower(isOwnersTurn: true);
        var targetPower = battle.Target.GetPower(isOwnersTurn: false) + battle.CounterBonus;
        var targetWasLeader = battle.Target.IsLeader;

        if (attackerPower < targetPower)
        {
            return new BattleResult
            {
                AttackerWon = false,
                AttackerPower = attackerPower,
                TargetPower = targetPower,
                TargetWasLeader = targetWasLeader
            };
        }

        if (!targetWasLeader)
        {
            defender.TrashCharacter(battle.Target);
            return new BattleResult
            {
                AttackerWon = true,
                AttackerPower = attackerPower,
                TargetPower = targetPower,
                CharacterKnockedOut = true
            };
        }

        // Rule 7-1-4-1-1-1: 0 Life when damage is dealt -> the defender loses.
        if (defender.LifeCards.Count == 0)
        {
            return new BattleResult
            {
                AttackerWon = true,
                AttackerPower = attackerPower,
                TargetPower = targetPower,
                TargetWasLeader = true,
                DefenderLostGame = true
            };
        }

        // Rule 7-1-4-1-1-2/3: take the top Life card to the hand, twice with [Double Attack].
        // [Banish] trashes the Life card instead. [Trigger] effects are not automated yet.
        var damage = battle.Attacker.HasDoubleAttack ? 2 : 1;
        var lifeCardsLost = 0;

        for (var hit = 0; hit < damage && defender.LifeCards.Count > 0; hit += 1)
        {
            var lifeCard = defender.LifeCards[^1];
            defender.LifeCards.RemoveAt(defender.LifeCards.Count - 1);

            if (battle.Attacker.HasBanish)
            {
                defender.TrashCards.Add(lifeCard);
            }
            else
            {
                defender.Hand.Add(lifeCard);
            }

            lifeCardsLost += 1;
        }

        return new BattleResult
        {
            AttackerWon = true,
            AttackerPower = attackerPower,
            TargetPower = targetPower,
            TargetWasLeader = true,
            LifeCardsLost = lifeCardsLost
        };
    }
}
