using OneSake.Game.State;

namespace OneSake.Game.Effects;

// What the effect engine needs to know about the running match. MatchRoom implements it.
public interface IGameContext
{
    IReadOnlyList<PlayerState> Players { get; }
    int TurnNumber { get; }
    string ActivePlayerId { get; }
    BattleState? Battle { get; }
    void Log(string type, string text);
}

public static class GameContextExtensions
{
    public static PlayerState GetPlayer(this IGameContext context, string playerId)
    {
        return context.Players.First(player => player.PlayerId == playerId);
    }

    public static PlayerState GetOpponent(this IGameContext context, string playerId)
    {
        return context.Players.First(player => player.PlayerId != playerId);
    }

    // The player who owns the card (Leader, field, hand, trash...).
    public static PlayerState? FindOwner(this IGameContext context, CardInstance card)
    {
        if (!string.IsNullOrEmpty(card.OwnerId))
        {
            var owner = context.Players.FirstOrDefault(player => player.PlayerId == card.OwnerId);
            if (owner is not null)
            {
                return owner;
            }
        }

        return context.Players.FirstOrDefault(player =>
            player.Leader == card
            || player.Stage == card
            || player.Characters.Contains(card)
            || player.Hand.Contains(card)
            || player.TrashCards.Contains(card)
            || player.LifeCards.Contains(card)
            || player.DrawDeck.Contains(card));
    }

    public static bool IsOwnersTurn(this IGameContext context, CardInstance card)
    {
        return context.FindOwner(card)?.PlayerId == context.ActivePlayerId;
    }
}
