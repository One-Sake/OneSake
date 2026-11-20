using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// The one place where effects with a duration end (power/cost modifiers, flags).
public static class EffectDurations
{
    // Rule 7-1-5-3 / 7-1-5-4: at the end of the battle, effects that last "during this battle"
    // become invalid (turn player's first, then the non-turn player's - the result is the same).
    public static void EndOfBattle(IGameContext context)
    {
        foreach (var card in context.Players.SelectMany(player => player.FieldCards()))
        {
            card.Modifiers.RemoveAll(modifier => modifier.Duration == Duration.ThisBattle);
        }

        StaticEffects.Recalculate(context);
    }

    // Rule 6-6-1-3: at the end of the turn, effects that last "during this turn" become invalid.
    // "Until the end of your opponent's next turn" effects end at the end of the turn after the
    // one in which they were created.
    public static void EndOfTurn(IGameContext context, int endingTurn)
    {
        foreach (var card in context.Players.SelectMany(player => player.FieldCards()))
        {
            card.Modifiers.RemoveAll(modifier =>
                modifier.Duration is Duration.ThisTurn or Duration.ThisBattle
                || (modifier.Duration == Duration.UntilEndOfOpponentsNextTurn && modifier.CreatedOnTurn < endingTurn));
        }

        // Player flags like "your opponent cannot activate [Blocker] during this turn".
        foreach (var player in context.Players)
        {
            player.TurnFlags.Clear();
        }

        StaticEffects.Recalculate(context);
    }
}
