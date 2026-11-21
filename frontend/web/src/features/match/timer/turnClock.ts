import { useEffect, useRef, useState } from 'react';

import type { TimerDto } from '../../../types/realtime';
import { serverNow } from '../serverClock';

export type ClockLevel = 'normal' | 'warn' | 'danger';

export interface PlayerClockState {
  // Countdown that is shown: the normal time, then the time bank.
  remainingMs: number;
  // 1 = full, 0 = empty (of the part that runs right now).
  fraction: number;
  usingBank: boolean;
  // Time bank of this player (what is left of it).
  bankMs: number;
  // Colour by the total time left (normal time + bank): orange from 15 s, red from 5 s.
  level: ClockLevel;
  kind: TimerDto['kind'];
}

const WARN_MS = 15_000;
const DANGER_MS = 5_000;

function compute(timer: TimerDto, playerId: string, now: number): Omit<PlayerClockState, 'fraction'> {
  const bank = timer.timeBankMs[playerId] ?? 0;
  const baseLeft = timer.deadlineUnixMs - now;
  if (baseLeft > 0) {
    const total = baseLeft + (timer.usesTimeBank ? bank : 0);
    return { remainingMs: baseLeft, usingBank: false, bankMs: bank, level: levelFor(total), kind: timer.kind };
  }

  const bankLeft = timer.usesTimeBank ? Math.max(0, bank + baseLeft) : 0;
  return { remainingMs: bankLeft, usingBank: timer.usesTimeBank, bankMs: bankLeft, level: levelFor(bankLeft), kind: timer.kind };
}

function levelFor(totalMs: number): ClockLevel {
  if (totalMs <= DANGER_MS) {
    return 'danger';
  }

  return totalMs <= WARN_MS ? 'warn' : 'normal';
}

export function isClockOf(timer: TimerDto | null | undefined, playerId: string | undefined): boolean {
  return Boolean(timer && playerId && (timer.activePlayerId === playerId || timer.activePlayerId === ''));
}

// The running clock of one player, updated a few times per second. null = not this player's clock.
export function usePlayerClock(timer: TimerDto | null | undefined, playerId: string | undefined): PlayerClockState | null {
  const running = isClockOf(timer, playerId) ? timer! : null;
  const [now, setNow] = useState(serverNow);
  // The largest value seen for each part (normal time / bank) is "full" for the ring.
  const fullRef = useRef<{ key: string; base: number; bank: number }>({ key: '', base: 1, bank: 1 });

  useEffect(() => {
    if (!running) {
      return undefined;
    }

    setNow(serverNow());
    const id = window.setInterval(() => setNow(serverNow()), 200);
    return () => window.clearInterval(id);
  }, [running]);

  if (!running || !playerId) {
    return null;
  }

  const state = compute(running, playerId, now);
  const key = `${running.kind}:${running.activePlayerId}:${running.deadlineUnixMs}`;
  const full = fullRef.current;
  if (full.key !== key) {
    fullRef.current = { key, base: 1, bank: 1 };
  }

  const current = fullRef.current;
  if (state.usingBank) {
    current.bank = Math.max(current.bank, state.remainingMs);
  } else {
    current.base = Math.max(current.base, state.remainingMs);
  }

  const fraction = state.remainingMs / (state.usingBank ? current.bank : current.base);
  return { ...state, fraction: Math.max(0, Math.min(1, fraction)) };
}

// 84000 -> "1:24", 7400 -> "8"
export function formatClock(ms: number): string {
  const seconds = Math.ceil(ms / 1000);
  if (seconds < 60) {
    return String(seconds);
  }

  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}
