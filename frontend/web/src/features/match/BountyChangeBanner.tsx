import { useEffect, useRef, useState } from 'react';

import { getGuestId } from '../../api/guestId';
import { BOUNTY_PER_POINT, formatBountyChange, getPlayerBounty } from '../../api/leaderboardApi';
import type { GameStateDto } from '../../types/realtime';

const SHOW_MS = 6000;
// The server saves the result in the background; give it a moment before asking.
const RETRY_DELAYS_MS = [1200, 3000, 6000];

interface BountyChangeBannerProps {
  gameState: GameStateDto | null;
}

// Short "Kopfgeld +฿ 24.000" after a ranked match. Compares the rating before and after the match.
export function BountyChangeBanner({ gameState }: BountyChangeBannerProps) {
  const [change, setChange] = useState<number | null>(null);
  const before = useRef<{ rating: number; games: number } | null>(null);
  const phase = gameState?.phase;
  const isRanked = Boolean(gameState?.isRanked);

  // Remember the rating when a ranked match starts.
  useEffect(() => {
    if (!isRanked || phase !== 'TurnOrder') {
      return;
    }

    // Not cancelled on phase changes: the value is still needed after the turn order phase.
    getPlayerBounty(getGuestId())
      .then((bounty) => {
        before.current = { rating: bounty.rating, games: bounty.wins + bounty.losses };
      })
      .catch(() => undefined);
  }, [isRanked, phase]);

  // After the match (game over, or back in the lobby when the opponent gave up):
  // ask again until the new rating is saved. Unrated matches simply show nothing.
  useEffect(() => {
    if ((phase !== 'GameOver' && phase !== 'Lobby') || !before.current) {
      return undefined;
    }

    const start = before.current;
    const timeoutIds: number[] = [];
    let isDone = false;

    RETRY_DELAYS_MS.forEach((delay) => {
      timeoutIds.push(
        window.setTimeout(() => {
          if (isDone) {
            return;
          }

          getPlayerBounty(getGuestId())
            .then((bounty) => {
              if (isDone || bounty.wins + bounty.losses <= start.games) {
                return;
              }

              isDone = true;
              before.current = null;
              setChange((bounty.rating - start.rating) * BOUNTY_PER_POINT);
            })
            .catch(() => undefined);
        }, delay),
      );
    });

    return () => {
      isDone = true;
      timeoutIds.forEach((id) => window.clearTimeout(id));
    };
  }, [phase]);

  useEffect(() => {
    if (change === null) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setChange(null), SHOW_MS);
    return () => window.clearTimeout(timeoutId);
  }, [change]);

  if (change === null) {
    return null;
  }

  return (
    <div className={`bounty-banner ${change >= 0 ? 'is-up' : 'is-down'}`} role="status">
      <span>Kopfgeld</span>
      <strong>{formatBountyChange(change)}</strong>
    </div>
  );
}
