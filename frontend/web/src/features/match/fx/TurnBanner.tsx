import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';

import type { GameStateDto } from '../../../types/realtime';
import { getFxSettings } from './fxSettings';
import { playSound } from './sounds';

const RUNNING_PHASES = new Set(['Refresh', 'Draw', 'Don', 'Main', 'End']);
const BANNER_MS = 900;
const GLOW_MS = 1400;

interface TurnBannerProps {
  gameState: GameStateDto | null;
  frameRef: RefObject<HTMLElement>;
}

// "DEIN ZUG" (gold) / "ZUG DES GEGNERS" (muted) across the board when the turn changes,
// then the own action bar glows for a moment.
export function TurnBanner({ frameRef, gameState }: TurnBannerProps) {
  const [banner, setBanner] = useState<{ mine: boolean; turn: number; key: number } | null>(null);
  const lastTurn = useRef('');
  const phase = String(gameState?.phase ?? '');
  const activeId = gameState?.activePlayerId ?? '';
  const turn = gameState?.turnNumber ?? 0;
  const mine = Boolean(activeId) && activeId === gameState?.viewerPlayerId;

  useEffect(() => {
    if (!RUNNING_PHASES.has(phase) || !activeId) {
      if (phase === 'Lobby' || phase === 'TurnOrder') {
        lastTurn.current = '';
      }
      return undefined;
    }

    const key = `${turn}:${activeId}`;
    if (key === lastTurn.current) {
      return undefined;
    }

    lastTurn.current = key;
    setBanner({ mine, turn, key: Date.now() });
    playSound(mine ? 'turnMine' : 'turnTheirs');

    const frame = frameRef.current;
    const timers = [window.setTimeout(() => setBanner(null), BANNER_MS * (getFxSettings().animations === 'fast' ? 0.6 : 1))];
    if (mine && frame) {
      timers.push(
        window.setTimeout(() => {
          frame.dataset.fxTurnGlow = '1';
        }, BANNER_MS * 0.6),
      );
      timers.push(window.setTimeout(() => delete frame.dataset.fxTurnGlow, BANNER_MS * 0.6 + GLOW_MS));
    }

    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [activeId, frameRef, mine, phase, turn]);

  if (!banner) {
    return null;
  }

  return (
    <div key={banner.key} aria-live="polite" className={`fx-turn-banner ${banner.mine ? 'is-mine' : 'is-theirs'}`} role="status">
      <strong>{banner.mine ? 'DEIN ZUG' : 'ZUG DES GEGNERS'}</strong>
      <span>Zug {banner.turn}</span>
    </div>
  );
}
