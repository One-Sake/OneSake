import { useEffect, useRef, useState } from 'react';

import { getGuestId } from '../../api/guestId';
import { BOUNTY_PER_POINT, getPlayerBounty } from '../../api/leaderboardApi';
import type { GameStateDto } from '../../types/realtime';

// The server saves the result in the background; give it a moment before asking.
const RETRY_DELAYS_MS = [1200, 3000, 6000];

// Bounty change of the last ranked match (in Berry), null while unknown or unrated.
// Compares the rating before and after the match.
export function useBountyChange(gameState: GameStateDto | null): number | null {
  const [change, setChange] = useState<number | null>(null);
  const before = useRef<{ rating: number; games: number } | null>(null);
  const phase = gameState?.phase;
  const isRanked = Boolean(gameState?.isRanked);

  // Remember the rating when a ranked match starts.
  useEffect(() => {
    if (!isRanked || phase !== 'TurnOrder') {
      return;
    }

    setChange(null);
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

  return change;
}
