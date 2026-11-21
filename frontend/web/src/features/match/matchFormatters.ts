import type { MatchPhase } from '../../types/realtime';

const PHASE_LABELS: Record<number, string> = {
  0: 'Lobby',
  1: 'TurnOrder',
  2: 'Mulligan',
  3: 'Refresh',
  4: 'Draw',
  5: 'Don',
  6: 'Main',
  7: 'End',
  8: 'GameOver',
};

export function formatPhase(phase: MatchPhase): string {
  if (typeof phase === 'string') {
    return phase;
  }

  return PHASE_LABELS[phase] ?? `Unknown(${phase})`;
}

export function formatUnixMs(tsUnixMs: number): string {
  return new Date(tsUnixMs).toLocaleTimeString();
}

// Second line of the game over screen.
export function getGameOverReason(endReason: string | null | undefined, didWin: boolean): string {
  switch (endReason) {
    case 'DISCONNECT':
      return didWin ? 'Gegner hat die Verbindung verloren – Sieg' : 'Verbindung verloren – Niederlage';
    case 'TIMEOUT':
      return didWin ? 'Zeit des Gegners abgelaufen – Sieg' : 'Zeit abgelaufen – Niederlage';
    case 'DECK_OUT':
      return 'Keine Karten mehr im Deck';
    default:
      return '';
  }
}
