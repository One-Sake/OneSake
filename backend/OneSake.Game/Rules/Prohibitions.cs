using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// Rule 1-3-3: when an effect requires an action that an active effect prohibits, the prohibiting
// ("cannot") effect always takes precedence. All "cannot" checks for K.O. and rest are here.
public static class Prohibitions
{
    // Rule 10-2-1-3: "cannot be K.O.'d" only matters for a K.O. by an effect or by battle.
    // The parser does not keep apart "by effects" and "by your opponent's effects", so the flag
    // protects against the opponent's effects only (see docs/rules-open-questions.md).
    public static bool CanBeKOd(CardInstance card, bool byEffect, bool byOpponent)
    {
        if (card.IsLeader)
        {
            return false;
        }

        return byEffect
            ? !(byOpponent && card.HasFlag(EffectFlags.CannotBeKOdByEffects))
            : !card.HasFlag(EffectFlags.CannotBeKOdInBattle);
    }

    public static bool CanBeRested(CardInstance card, bool byOpponent)
    {
        return !(byOpponent && card.HasFlag(EffectFlags.CannotBeRestedByEffects));
    }

    // Rule 1-3-3 for attacks: "cannot attack" wins over "can attack" effects like [Rush].
    public static bool CanAttack(CardInstance card)
    {
        return !card.HasFlag(EffectFlags.CannotAttack);
    }
}
