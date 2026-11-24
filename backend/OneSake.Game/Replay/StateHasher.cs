using System.Security.Cryptography;
using System.Text;
using OneSake.Game.State;

namespace OneSake.Game.Replay;

// A fingerprint of the complete game state (every zone, card state, DON!!, effects). Two matches
// with the same seed and the same decisions must end with the same hash. Player IDs (connection
// IDs) are replaced by the seat number, log time stamps are not part of it.
public static class StateHasher
{
    public static string Hash(IReadOnlyList<PlayerState> players, IEnumerable<string> matchLines)
    {
        var text = new StringBuilder();
        foreach (var line in matchLines)
        {
            text.Append(line).Append('\n');
        }

        for (var index = 0; index < players.Count; index += 1)
        {
            AppendPlayer(text, players[index], index + 1);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static void AppendPlayer(StringBuilder text, PlayerState player, int seat)
    {
        text.Append($"seat {seat}: deck={player.DeckId} turns={player.TurnsStarted} don={player.DonDeckCount}/{player.ActiveDon}/{player.RestedDon} ")
            .Append($"donEnd={player.DonToActivateAtEndOfTurn} damaged0={player.LeaderDamagedAtZeroLife} view=\n");
        text.Append("flags: ").AppendJoin(',', player.TurnFlags.Order(StringComparer.Ordinal)).Append('\n');
        AppendCards(text, "leader", player.Leader is null ? [] : [player.Leader]);
        AppendCards(text, "stage", player.Stage is null ? [] : [player.Stage]);
        AppendCards(text, "characters", player.Characters);
        AppendCards(text, "hand", player.Hand);
        AppendCards(text, "deck", player.DrawDeck);
        AppendCards(text, "life", player.LifeCards);
        AppendCards(text, "trash", player.TrashCards);
        // "view=" and "viewed:" belonged to the removed manual mode. They stay (always empty), so
        // recordings made before keep the same state hashes.
        AppendCards(text, "viewed", []);
    }

    private static void AppendCards(StringBuilder text, string zone, IEnumerable<CardInstance> cards)
    {
        text.Append(zone).Append(':');
        foreach (var card in cards)
        {
            text.Append(' ').Append(card.InstanceId)
                .Append($"[r{(card.IsRested ? 1 : 0)} d{card.AttachedDon} p{card.PlayedOnTurn} sp{card.StaticPower} sc{card.StaticCost}");
            foreach (var modifier in card.Modifiers)
            {
                text.Append($" m{modifier.Power}/{modifier.Cost}/{modifier.Flag}/{modifier.Duration}/{modifier.CreatedOnTurn}");
            }

            text.Append(" f").AppendJoin(',', card.StaticFlags.Order(StringComparer.Ordinal))
                .Append(" o").AppendJoin(',', card.UsedOncePerTurn.Order(StringComparer.Ordinal))
                .Append(']');
        }

        text.Append('\n');
    }
}
