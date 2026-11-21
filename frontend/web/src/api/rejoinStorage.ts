// The seat in the current room (player ID + secret rejoin token), so we can get back after a lost
// connection. sessionStorage survives a reload of this tab, localStorage also closing and reopening
// the app. Storage can be blocked (private mode), so every access is wrapped in try/catch.

export interface RejoinData {
  roomCode: string;
  playerId: string;
  rejoinToken: string;
  savedAtUnixMs: number;
}

const STORAGE_KEY = 'onesake.rejoin';
// Older data is useless: the reconnect grace period is much shorter.
const MAX_AGE_MS = 6 * 60 * 60 * 1000;

function readFrom(storage: () => Storage): RejoinData | null {
  try {
    const raw = storage().getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const data = JSON.parse(raw) as Partial<RejoinData>;
    if (
      typeof data.roomCode !== 'string' ||
      typeof data.playerId !== 'string' ||
      typeof data.rejoinToken !== 'string' ||
      typeof data.savedAtUnixMs !== 'number'
    ) {
      return null;
    }

    return Date.now() - data.savedAtUnixMs > MAX_AGE_MS ? null : (data as RejoinData);
  } catch {
    return null;
  }
}

export function loadRejoinData(): RejoinData | null {
  return readFrom(() => window.sessionStorage) ?? readFrom(() => window.localStorage);
}

export function saveRejoinData(data: Omit<RejoinData, 'savedAtUnixMs'>): void {
  const value = JSON.stringify({ ...data, savedAtUnixMs: Date.now() });
  for (const storage of [() => window.sessionStorage, () => window.localStorage]) {
    try {
      storage().setItem(STORAGE_KEY, value);
    } catch {
      // Storage blocked: reconnecting still works while the page stays open.
    }
  }
}

export function clearRejoinData(): void {
  for (const storage of [() => window.sessionStorage, () => window.localStorage]) {
    try {
      storage().removeItem(STORAGE_KEY);
    } catch {
      // Nothing stored.
    }
  }
}
