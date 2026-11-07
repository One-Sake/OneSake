using OneSake.Domain;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Shared checks for actions that the turn player does in the Main Phase.
public static class ActionGuard
{
    public static void EnsureMainPhase(MatchPhase phase)
    {
        switch (phase)
        {
            case MatchPhase.Main:
                return;

            case MatchPhase.GameOver:
                throw new InvalidOperationException("The game is already over.");

            default:
                throw new InvalidOperationException("This action is only possible during the main phase.");
        }
    }

    public static void EnsureActivePlayer(PlayerState player, string activePlayerId)
    {
        if (!string.Equals(player.PlayerId, activePlayerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only the active player can do this.");
        }
    }

    public static string RequireId(string? instanceId, string message)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new InvalidOperationException(message);
        }

        return instanceId;
    }
}
