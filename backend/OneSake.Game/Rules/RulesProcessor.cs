using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// Rule 9: rule processing. Runs after every action and every effect step, before the next
// effect is resolved (rule 9-1-2: "immediately ... even if other actions are in the process").
public static class RulesProcessor
{
    // Rule 9-2-1: every player who meets a defeat condition loses (both players -> nobody wins).
    public static IReadOnlyList<PlayerState> FindLosers(IEnumerable<PlayerState> players)
    {
        return players
            .Where(player =>
                // Rule 9-2-1-1 / 1-2-1-1-1: the Leader took damage while the player had 0 Life cards.
                player.LeaderDamagedAtZeroLife
                // Rule 9-2-1-2 / 1-2-1-1-2: 0 cards in the deck.
                || player.DrawDeck.Count == 0)
            .ToList();
    }

    // Returns the losers. When nobody lost, the always-on effects are calculated again:
    // StaticEffects is the only place that derives power, cost and keywords from effects.
    public static IReadOnlyList<PlayerState> Process(IGameContext context)
    {
        var losers = FindLosers(context.Players);
        if (losers.Count == 0)
        {
            // Rule 8-1-3-3-5: permanent effects are applied again until nothing changes.
            StaticEffects.Recalculate(context);
        }

        return losers;
    }
}
