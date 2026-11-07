using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Rule 7-1-2 Block Step: the defender may rest one active [Blocker] Character.
// That Character becomes the new target of the attack.
public sealed class BlockAction
{
    public CardInstance Execute(BattleState battle, PlayerState defender, string? blockerInstanceId)
    {
        if (battle.Step != BattleStep.Block || battle.BlockerUsed)
        {
            throw new InvalidOperationException("Blocking is not possible right now.");
        }

        var blockerId = ActionGuard.RequireId(blockerInstanceId, "Choose a Character with [Blocker].");

        var blocker = defender.Characters.FirstOrDefault(card => card.InstanceId == blockerId)
            ?? throw new InvalidOperationException("The blocker must be one of your Characters.");

        if (!CanBlock(blocker, battle))
        {
            throw new InvalidOperationException($"{blocker.Name} cannot block.");
        }

        blocker.IsRested = true;
        battle.Target = blocker;
        battle.BlockerUsed = true;
        return blocker;
    }

    public static bool CanBlock(CardInstance card, BattleState battle)
    {
        return card.IsCharacter
            && card.HasBlocker
            && !card.IsRested
            && card.InstanceId != battle.Target.InstanceId;
    }

    public static bool HasPossibleBlocker(PlayerState defender, BattleState battle)
    {
        return defender.Characters.Any(card => CanBlock(card, battle));
    }
}
