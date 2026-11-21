import { useEffect, useState } from 'react';

// Countdowns (reconnect, turn timer) are computed with the server time, so a phone clock that is
// wrong by a few minutes does not matter. Every snapshot carries the server time.
let serverOffsetMs = 0;

export function syncServerTime(serverTimeUnixMs: number | undefined): void {
  if (typeof serverTimeUnixMs === 'number' && serverTimeUnixMs > 0) {
    serverOffsetMs = serverTimeUnixMs - Date.now();
  }
}

export function serverNow(): number {
  return Date.now() + serverOffsetMs;
}

// Milliseconds until the deadline, updated a few times per second. null = no deadline.
// The deadline is server time, or local time with clock = 'local'.
export function useCountdown(
  deadlineUnixMs: number | null | undefined,
  clock: 'server' | 'local' = 'server',
  intervalMs = 250,
): number | null {
  const read = clock === 'server' ? serverNow : Date.now;
  const [now, setNow] = useState(read);

  useEffect(() => {
    if (deadlineUnixMs == null) {
      return undefined;
    }

    const readNow = clock === 'server' ? serverNow : Date.now;
    setNow(readNow());
    const id = window.setInterval(() => setNow(readNow()), intervalMs);
    return () => window.clearInterval(id);
  }, [clock, deadlineUnixMs, intervalMs]);

  return deadlineUnixMs == null ? null : Math.max(0, deadlineUnixMs - now);
}

// 84000 -> "1:24"
export function formatCountdown(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}
