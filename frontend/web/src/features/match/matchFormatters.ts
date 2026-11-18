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
