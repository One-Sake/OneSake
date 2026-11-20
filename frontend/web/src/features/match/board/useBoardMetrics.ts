import { useLayoutEffect, useState } from 'react';
import type { RefObject } from 'react';

// Card size for the match board, computed from the real size of the board frame.
//
// The numbers below describe the existing grid in match-board.css. They must match it:
// if a row height, gap or the action bar width changes there, change it here as well.

const CARD_RATIO = 88 / 63;
const MIN_CARD_W = 20;
const MAX_CARD_W = 190;

// Gap between cards, as a share of the card width (CSS: --gap).
const GAP = 0.08;

// Normal layout: 7 columns, 3 card rows per player.
const NORMAL = {
  columns: 7,
  cardRows: 6,
  gapRows: 4,
  // Opponent hand bar + midline + resting hand, as a share of the card height.
  extraRows: 0.26 + 0.18 + 0.72,
  // Room for the hint toast between the board and the resting hand (CSS: --bottom-extra).
  bottomPx: 36,
};

// Short boards (phones in landscape): 10 columns, 2 card rows per player.
// Same condition as the "@container board (max-height: 520px) and (min-aspect-ratio: 3 / 2)" rule.
const COMPACT = {
  columns: 10,
  cardRows: 4,
  gapRows: 2,
  extraRows: 0.3 + 0.24 + 0.64,
  bottomPx: 26,
};

// Below this width the action buttons move above the hand (CSS: "@container board (max-width: 560px)").
const NARROW_MAX_W = 560;
const NARROW_EXTRA_BOTTOM_PX = 48;

// Action bar width: clamp(min, card width * factor, max) plus its distance to the edge.
const ACTIONS_NORMAL = { min: 112, factor: 1.3, max: 200, edge: 16 };
const ACTIONS_COMPACT = { min: 92, factor: 1.5, max: 140, edge: 10 };

export interface BoardMetrics {
  cardWidth: number;
  isCompact: boolean;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function computeCardWidth(width: number, height: number): BoardMetrics {
  const isCompact = height <= 520 && width / height >= 3 / 2;
  const isNarrow = width <= NARROW_MAX_W;
  const layout = isCompact ? COMPACT : NORMAL;
  const actions = isCompact ? ACTIONS_COMPACT : ACTIONS_NORMAL;

  // Height: card rows + gaps + bars must fit.
  const bottom = layout.bottomPx + (isNarrow ? NARROW_EXTRA_BOTTOM_PX : 0);
  const heightPerCard = (layout.cardRows + layout.extraRows) * CARD_RATIO + layout.gapRows * GAP;
  const byHeight = (height - bottom) / heightPerCard;

  // Width: the grid stays centered, so the action bar needs free room on both sides.
  const gridPerCard = layout.columns + (layout.columns - 1) * GAP;
  let byWidth = (width - 16) / gridPerCard;
  if (!isNarrow) {
    // The action bar width depends on the card width, so refine a few times.
    for (let step = 0; step < 4; step += 1) {
      const actionsWidth = clamp(byWidth * actions.factor, actions.min, actions.max) + actions.edge;
      byWidth = (width - 2 * actionsWidth) / gridPerCard;
    }
  }

  const cardWidth = clamp(Math.floor(Math.min(byHeight, byWidth) * 2) / 2, MIN_CARD_W, MAX_CARD_W);
  return { cardWidth, isCompact };
}

// Watches the board frame and returns the largest card width that fits without scrolling.
export function useBoardMetrics(frameRef: RefObject<HTMLElement>): BoardMetrics | null {
  const [metrics, setMetrics] = useState<BoardMetrics | null>(null);

  useLayoutEffect(() => {
    const frame = frameRef.current;
    if (!frame || typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    function update(width: number, height: number): void {
      if (width <= 0 || height <= 0) {
        return;
      }

      const next = computeCardWidth(width, height);
      setMetrics((previous) =>
        previous && previous.cardWidth === next.cardWidth && previous.isCompact === next.isCompact
          ? previous
          : next,
      );
    }

    update(frame.clientWidth, frame.clientHeight);
    const observer = new ResizeObserver(([entry]) => update(entry.contentRect.width, entry.contentRect.height));
    observer.observe(frame);
    return () => observer.disconnect();
  }, [frameRef]);

  return metrics;
}
