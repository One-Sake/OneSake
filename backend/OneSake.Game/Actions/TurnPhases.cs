using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

// The automatic phases at the start of a turn (rules 6-2 to 6-4).
public static class TurnPhases
{
    // Rule 6-2: Refresh Phase.
    public static void Refresh(PlayerState player, IGameContext context)
    {
        // Rule 6-2-3: given DON!! go back to the cost area rested.
        foreach (var card in player.FieldCards().Where(card => !card.IsStage))
        {
            player.RestedDon += card.AttachedDon;
            card.AttachedDon = 0;
        }

        // Rule 6-2-4: all rested cards (Leader, Characters, Stage, cost area) become active.
        foreach (var card in player.FieldCards())
        {
            RefreshCard(card, context);
        }

        player.ActiveDon += player.RestedDon;
        player.RestedDon = 0;

        StaticEffects.Recalculate(context);
        context.Log("REFRESH_PHASE", $"Refresh phase for {player.DisplayName}.");
    }

    // Rule 6-3-1: draw 1 card. The player going first does not draw on their first turn.
    // An empty deck is a defeat condition (rule 9-2-1-2), checked by rule processing.
    public static void Draw(PlayerState player, IGameContext context)
    {
        if (context.TurnNumber == 1)
        {
            context.Log("SKIP_DRAW", $"{player.DisplayName} goes first and does not draw.");
            return;
        }

        if (player.DrawDeck.Count > 0)
        {
            player.DrawCards(1);
            context.Log("DRAW_CARD", $"{player.DisplayName} drew 1 card.");
        }
        else
        {
            context.Log("DECK_EMPTY", $"{player.DisplayName} cannot draw because their deck is empty.");
        }
    }

    // Rule 6-4: place 2 DON!! (only 1 for the first player's first turn, rules 6-4-2/6-4-3:
    // as many as are left in the DON!! deck).
    public static void Don(PlayerState player, IGameContext context)
    {
        var amount = Math.Min(context.TurnNumber == 1 ? 1 : 2, player.DonDeckCount);
        player.DonDeckCount -= amount;
        player.ActiveDon += amount;

        context.Log("DON_PHASE", $"{player.DisplayName} placed {amount} DON!! ({player.ActiveDon} active).");
    }

    // A card with "will not become active in the next Refresh Phase" stays rested once.
    private static void RefreshCard(CardInstance card, IGameContext context)
    {
        if (card.Modifiers.RemoveAll(modifier => modifier.Flag == EffectFlags.SkipNextRefresh) > 0)
        {
            context.Log("REFRESH_SKIPPED", $"{card.Name} does not become active this Refresh Phase.");
            return;
        }

        card.IsRested = false;
    }
}
