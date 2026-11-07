using OneSake.Domain;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

public sealed class EndTurnAction
{
    public void Execute(PlayerState player, MatchPhase phase, string activePlayerId)
    {
        ActionGuard.EnsureMainPhase(phase);
        ActionGuard.EnsureActivePlayer(player, activePlayerId);
    }
}
