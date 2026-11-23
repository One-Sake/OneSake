import { forwardRef } from 'react';
import type { CSSProperties } from 'react';
import { createPortal } from 'react-dom';

import { useCardArtStyle } from './cardArt';
import type { DragView } from './useHandDrag';
import { MatchCardView } from './MatchCardView';

interface DragGhostProps {
  view: DragView | null;
  // Card width on the board; the ghost is drawn a bit larger.
  cardWidth: number;
}

const GHOST_SCALE = 1.1;

// The floating copy of the dragged card. It lives in document.body because the board
// frame is a size container, which would otherwise be the reference for position: fixed.
export const DragGhost = forwardRef<HTMLDivElement, DragGhostProps>(function DragGhost({ cardWidth, view }, ref) {
  const cardArtStyle = useCardArtStyle();

  if (!view) {
    return null;
  }

  const isDon = view.item.kind === 'don';
  const width = cardWidth * GHOST_SCALE * (isDon ? 0.6 : 1);
  const showInvalid = view.phase === 'drag' && !view.hoverValid && !view.overHand;
  const className = [
    'match-drag-ghost',
    `is-${view.phase}`,
    view.hoverValid ? 'is-over-target' : '',
    showInvalid ? 'is-invalid' : '',
  ]
    .filter(Boolean)
    .join(' ');
  const style = {
    ...cardArtStyle,
    width: `${width}px`,
    '--card-w': `${width}px`,
    '--card-radius': `${width * 0.07}px`,
    '--don-w': `${width}px`,
  } as CSSProperties;

  return createPortal(
    <div ref={ref} aria-hidden="true" className={className} style={style}>
      {view.item.kind !== 'don' ? (
        <MatchCardView card={{ ...view.item.card, isRested: false }} size="slot" />
      ) : (
        <span className="match-don-card match-drag-ghost__don" />
      )}
      {showInvalid && <span className="match-drag-ghost__invalid">&#x2A2F;</span>}
    </div>,
    document.body,
  );
});
