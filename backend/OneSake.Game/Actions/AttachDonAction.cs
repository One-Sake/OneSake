using OneSake.Domain;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Rule 6-5-5: give 1 active DON!! from the cost area to your Leader or one of your Characters.
public sealed class AttachDonAction
{
    public CardInstance Execute(
        PlayerState player,
        MatchPhase phase,
        string activePlayerId,
        string? targetInstanceId)
    {
        ActionGuard.EnsureMainPhase(phase);
        ActionGuard.EnsureActivePlayer(player, activePlayerId);

        if (player.ActiveDon <= 0)
        {
            throw new InvalidOperationException("You have no active DON!! to give.");
        }

        var targetId = ActionGuard.RequireId(
            targetInstanceId,
            "Giving DON!! requires your Leader or a Character as target.");

        var target = player.FindFieldCard(targetId)
            ?? throw new InvalidOperationException("DON!! can only be given to your Leader or your Characters.");

        player.ActiveDon -= 1;
        target.AttachedDon += 1;
        return target;
    }
}
