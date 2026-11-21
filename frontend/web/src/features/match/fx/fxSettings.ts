import { useSyncExternalStore } from 'react';

// Settings of the match "feel" (gear button on the board). Stored in localStorage; storage can be
// blocked (private mode), so every access is wrapped in try/catch and the defaults are used.

export type AnimationMode = 'normal' | 'fast' | 'off';

export interface FxSettings {
  animations: AnimationMode;
  soundMuted: boolean;
  // 0..1
  volume: number;
  vibration: boolean;
  // Counter step: pass by itself when no Counter card can be used (with a random short delay).
  autoPassCounter: boolean;
}

const STORAGE_KEY = 'onesake.matchFx';

function prefersReducedMotion(): boolean {
  try {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  } catch {
    return false;
  }
}

function defaults(): FxSettings {
  return {
    // Reduced motion: no movement by default, only short highlights.
    animations: prefersReducedMotion() ? 'off' : 'normal',
    soundMuted: false,
    volume: 0.5,
    vibration: true,
    autoPassCounter: false,
  };
}

function load(): FxSettings {
  const base = defaults();
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return base;
    }

    const stored = JSON.parse(raw) as Partial<FxSettings>;
    return {
      animations: stored.animations === 'fast' || stored.animations === 'off' || stored.animations === 'normal'
        ? stored.animations
        : base.animations,
      soundMuted: typeof stored.soundMuted === 'boolean' ? stored.soundMuted : base.soundMuted,
      volume: typeof stored.volume === 'number' ? Math.min(1, Math.max(0, stored.volume)) : base.volume,
      vibration: typeof stored.vibration === 'boolean' ? stored.vibration : base.vibration,
      autoPassCounter: typeof stored.autoPassCounter === 'boolean' ? stored.autoPassCounter : base.autoPassCounter,
    };
  } catch {
    return base;
  }
}

let current: FxSettings | null = null;
const listeners = new Set<() => void>();

export function getFxSettings(): FxSettings {
  current ??= load();
  return current;
}

export function updateFxSettings(change: Partial<FxSettings>): void {
  current = { ...getFxSettings(), ...change };
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(current));
  } catch {
    // Not stored: the setting lasts until the page is reloaded.
  }

  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useFxSettings(): FxSettings {
  return useSyncExternalStore(subscribe, getFxSettings);
}
