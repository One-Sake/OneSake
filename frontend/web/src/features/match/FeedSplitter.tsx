import { useRef } from 'react';
import type { KeyboardEvent, PointerEvent } from 'react';

import { FEED_DEFAULT_WIDTH, FEED_MIN_WIDTH } from './feedWidth';

const KEYBOARD_STEP_PX = 16;

interface FeedSplitterProps {
  width: number;
  maxWidth: number;
  // Called on every pointer move (live = true) and once when the drag ends (live = false).
  onResize: (width: number, live: boolean) => void;
  onCollapse: () => void;
}

// Vertical drag handle between the log/chat feed and the board.
// Mouse, touch and pen use pointer events; the keyboard uses the arrow keys.
export function FeedSplitter({ maxWidth, onCollapse, onResize, width }: FeedSplitterProps) {
  const drag = useRef<{ startX: number; startWidth: number; lastWidth: number } | null>(null);

  function clamp(value: number): number {
    return Math.round(Math.min(Math.max(value, FEED_MIN_WIDTH), maxWidth));
  }

  function handlePointerDown(event: PointerEvent<HTMLDivElement>): void {
    if (event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = { startX: event.clientX, startWidth: width, lastWidth: width };
    document.body.classList.add('is-resizing-feed');
  }

  function handlePointerMove(event: PointerEvent<HTMLDivElement>): void {
    if (!drag.current) {
      return;
    }

    const next = clamp(drag.current.startWidth + event.clientX - drag.current.startX);
    if (next !== drag.current.lastWidth) {
      drag.current.lastWidth = next;
      onResize(next, true);
    }
  }

  function endDrag(event: PointerEvent<HTMLDivElement>): void {
    if (!drag.current) {
      return;
    }

    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    onResize(drag.current.lastWidth, false);
    drag.current = null;
    document.body.classList.remove('is-resizing-feed');
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>): void {
    const keySteps: Record<string, number> = {
      ArrowLeft: -KEYBOARD_STEP_PX,
      ArrowRight: KEYBOARD_STEP_PX,
    };

    if (event.key in keySteps) {
      event.preventDefault();
      onResize(clamp(width + keySteps[event.key]), false);
    } else if (event.key === 'Home') {
      event.preventDefault();
      onResize(FEED_MIN_WIDTH, false);
    } else if (event.key === 'End') {
      event.preventDefault();
      onResize(maxWidth, false);
    }
  }

  return (
    <div className="match-feed-splitter">
      <button
        aria-label="Collapse log and chat"
        className="match-feed-splitter__collapse"
        onClick={onCollapse}
        title="Collapse log and chat"
        type="button"
      >
        <span aria-hidden="true">&lsaquo;</span>
      </button>
      <div
        aria-label="Resize log and chat"
        aria-orientation="vertical"
        aria-valuemax={maxWidth}
        aria-valuemin={FEED_MIN_WIDTH}
        aria-valuenow={width}
        className="match-feed-splitter__handle"
        onDoubleClick={() => onResize(clamp(FEED_DEFAULT_WIDTH), false)}
        onKeyDown={handleKeyDown}
        onLostPointerCapture={endDrag}
        onPointerCancel={endDrag}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={endDrag}
        role="separator"
        tabIndex={0}
        title="Drag to resize, double click to reset"
      />
    </div>
  );
}

interface CollapsedFeedProps {
  unreadCount: number;
  onExpand: () => void;
}

// The thin strip shown while the feed is collapsed.
export function CollapsedFeed({ onExpand, unreadCount }: CollapsedFeedProps) {
  const label = unreadCount > 0 ? `Show log and chat (${unreadCount} unread)` : 'Show log and chat';

  return (
    <button aria-label={label} className="match-feed-rail" onClick={onExpand} title={label} type="button">
      <ChatIcon />
      {unreadCount > 0 && <span className="match-unread-badge">{unreadCount > 99 ? '99+' : unreadCount}</span>}
    </button>
  );
}

export function ChatIcon() {
  return (
    <svg aria-hidden="true" className="match-chat-icon" fill="none" viewBox="0 0 24 24">
      <path
        d="M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v8a2.5 2.5 0 0 1-2.5 2.5H10l-4.5 4v-4A2.5 2.5 0 0 1 3 13.5v-8Z"
        stroke="currentColor"
        strokeLinejoin="round"
        strokeWidth="1.8"
        transform="translate(0.5 0)"
      />
      <path d="M8 8.5h8M8 12h5" stroke="currentColor" strokeLinecap="round" strokeWidth="1.8" />
    </svg>
  );
}
