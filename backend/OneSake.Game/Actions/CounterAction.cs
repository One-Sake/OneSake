using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Rule 7-1-3 Counter Step: the defender trashes Character cards with a Counter value from
// their hand. The target gains that much power during this battle.
public sealed class CounterAction
{
    public CardInstance Execute(BattleState battle, PlayerState defender, string? counterCardInstanceId)
    {
        if (battle.Step != BattleStep.Counter)
        {
            throw new InvalidOperationException("Counters can only be used in the counter step.");
        }

        var cardId = ActionGuard.RequireId(counterCardInstanceId, "Choose a hand card with a Counter value.");

        var card = defender.FindHandCard(cardId)
            ?? throw new InvalidOperationException("The counter card must be in your hand.");

        if (!IsCounterCard(card))
        {
            throw new InvalidOperationException($"{card.Name} has no Counter value.");
        }

        defender.Hand.Remove(card);
        defender.TrashCards.Add(card);
        battle.CounterBonus += card.Counter;
        return card;
    }

    public static bool IsCounterCard(CardInstance card)
    {
        return card.IsCharacter && card.Counter > 0;
    }

    public static bool HasCounterCard(PlayerState defender)
    {
        return defender.Hand.Any(IsCounterCard);
    }
}
