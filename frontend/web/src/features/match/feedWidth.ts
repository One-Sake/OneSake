// Width of the log/chat feed in the match view (desktop only).

export const FEED_DEFAULT_WIDTH = 300;
export const FEED_MIN_WIDTH = 220;
// The feed may take at most this share of the window width.
export const FEED_MAX_SHARE = 0.4;

const STORAGE_KEY = 'onesake.matchFeedWidth';

export function getFeedMaxWidth(viewportWidth: number): number {
  return Math.max(FEED_MIN_WIDTH, Math.round(viewportWidth * FEED_MAX_SHARE));
}

export function clampFeedWidth(width: number, viewportWidth: number): number {
  return Math.round(Math.min(Math.max(width, FEED_MIN_WIDTH), getFeedMaxWidth(viewportWidth)));
}

export function loadFeedWidth(): number {
  try {
    const stored = Number(window.localStorage.getItem(STORAGE_KEY));
    return Number.isFinite(stored) && stored > 0 ? stored : FEED_DEFAULT_WIDTH;
  } catch {
    return FEED_DEFAULT_WIDTH;
  }
}

export function saveFeedWidth(width: number): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, String(Math.round(width)));
  } catch {
    // Without localStorage the width only lives for this page load.
  }
}
