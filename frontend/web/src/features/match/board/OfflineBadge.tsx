import type { PlayerStateDto } from '../../../types/realtime';
import { formatCountdown, useCountdown } from '../serverClock';

// "Getrennt · 1:12" next to a player whose connection dropped (server keeps the seat until then).
export function OfflineBadge({ player }: { player: PlayerStateDto | null | undefined }) {
  const deadline = player && !player.connected ? player.disconnectedUntilUnixMs ?? null : null;
  const left = useCountdown(deadline);

  if (left === null) {
    return null;
  }

  return (
    <small className="match-player-plate__offline" role="status">
      Getrennt · {formatCountdown(left)}
    </small>
  );
}
