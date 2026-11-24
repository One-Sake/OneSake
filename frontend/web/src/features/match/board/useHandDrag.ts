import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';

// Drag & drop for hand cards (and DON!! from the cost area) with pointer events,
// so mouse, pen and touch behave the same. Clicking and the keyboard keep working:
// a drag only starts after the pointer moved a few pixels.
//
// Drop targets are plain elements with data attributes:
//   <div data-drop-zone="character" data-slot-index="2" data-card-id="...">
// The board decides with canDrop() which targets are valid and with onDrop() what is sent,
// using the same handlers as the click controls.

export type DragItem =
  | { kind: 'hand'; card: CardInstanceDto }
  | { kind: 'don' };

export interface DropTarget {
  zone: string;
  slotIndex?: number;
  cardId?: string;
}

// drag: follows the pointer. landing: flies into the slot. landed: waits for the server.
// returning: flies back into the hand.
export type DragPhase = 'drag' | 'landing' | 'landed' | 'returning';

export interface DragView {
  item: DragItem;
  phase: DragPhase;
  hover: DropTarget | null;
  hoverValid: boolean;
  overHand: boolean;
}

interface HandDragOptions {
  canDrop: (item: DragItem, target: DropTarget) => boolean;
  // Returning false sends the card back (e.g. the move still has to be confirmed first).
  onDrop: (item: DragItem, target: DropTarget) => boolean | void;
  // Released somewhere without a valid target.
  onMiss: (item: DragItem) => void;
  // True once the game state shows that the move happened (e.g. the card left the hand).
  isConfirmed: (item: DragItem) => boolean;
  pending: boolean;
}

interface Press {
  item: DragItem;
  pointerId: number;
  pointerType: string;
  startX: number;
  startY: number;
  startTime: number;
  source: HTMLElement;
  sourceRect: DOMRect;
  dragging: boolean;
  lastX: number;
  lastY: number;
  tilt: number;
}

const MOUSE_START_PX = 6;
// Touch: hold this long before moving, so a quick swipe or tap is not a drag.
const TOUCH_HOLD_MS = 150;
const TOUCH_CANCEL_PX = 10;
const LAND_MS = 180;
const RETURN_MS = 220;
// If the server neither confirms nor rejects in time, the card goes back to the hand.
const RESULT_TIMEOUT_MS = 3000;
const REJECT_CHECK_MS = 400;
const MAX_TILT_DEG = 12;

function prefersReducedMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function readDropTarget(x: number, y: number): { target: DropTarget; element: HTMLElement } | null {
  const element = document.elementFromPoint(x, y)?.closest<HTMLElement>('[data-drop-zone]');
  if (!element?.dataset.dropZone) {
    return null;
  }

  const slotIndex = element.dataset.slotIndex;
  return {
    element,
    target: {
      zone: element.dataset.dropZone,
      slotIndex: slotIndex === undefined ? undefined : Number(slotIndex),
      cardId: element.dataset.cardId || undefined,
    },
  };
}

function isSameTarget(a: DropTarget | null, b: DropTarget | null): boolean {
  return a?.zone === b?.zone && a?.slotIndex === b?.slotIndex && a?.cardId === b?.cardId;
}

function centerTransform(rect: DOMRect, scale: number): string {
  const x = rect.left + rect.width / 2;
  const y = rect.top + rect.height / 2;
  return `translate(${x}px, ${y}px) translate(-50%, -50%) scale(${scale})`;
}

export function useHandDrag(options: HandDragOptions) {
  const optionsRef = useRef(options);
  optionsRef.current = options;

  const [view, setView] = useState<DragView | null>(null);
  const viewRef = useRef<DragView | null>(null);
  const press = useRef<Press | null>(null);
  const ghostRef = useRef<HTMLDivElement>(null);
  const landingRect = useRef<DOMRect | null>(null);
  const lastSource = useRef<{ element: HTMLElement; rect: DOMRect } | null>(null);
  const suppressClickUntil = useRef(0);
  const sawPending = useRef(false);
  const timers = useRef<number[]>([]);

  function updateView(next: DragView | null): void {
    viewRef.current = next;
    setView(next);
  }

  function clearTimers(): void {
    timers.current.forEach((timer) => window.clearTimeout(timer));
    timers.current = [];
  }

  function later(callback: () => void, ms: number): void {
    timers.current.push(window.setTimeout(callback, ms));
  }

  function finishDrag(): void {
    clearTimers();
    document.body.classList.remove('is-dragging-card');
    updateView(null);
  }

  function returnToHand(): void {
    const current = viewRef.current;
    clearTimers();
    document.body.classList.remove('is-dragging-card');

    if (!current || prefersReducedMotion()) {
      updateView(null);
      return;
    }

    updateView({ ...current, phase: 'returning', hover: null, hoverValid: false });
    later(() => updateView(null), RETURN_MS + 40);
  }

  function placeGhost(x: number, y: number, tilt: number): void {
    if (ghostRef.current) {
      ghostRef.current.style.transform = `translate(${x}px, ${y}px) translate(-50%, -50%) rotate(${tilt}deg)`;
    }
  }

  // The pointer listeners live on the window for the whole lifetime of the board.
  // They only read refs, so they never go stale.
  useEffect(() => {
    function startDrag(current: Press): void {
      current.dragging = true;
      lastSource.current = { element: current.source, rect: current.sourceRect };
      document.body.classList.add('is-dragging-card');
      updateView({ item: current.item, phase: 'drag', hover: null, hoverValid: false, overHand: true });
    }

    function handleMove(event: PointerEvent): void {
      const current = press.current;
      if (!current || event.pointerId !== current.pointerId) {
        return;
      }

      if (!current.dragging) {
        const distance = Math.hypot(event.clientX - current.startX, event.clientY - current.startY);
        if (current.pointerType === 'touch' && performance.now() - current.startTime < TOUCH_HOLD_MS) {
          if (distance > TOUCH_CANCEL_PX) {
            press.current = null;
          }
          return;
        }

        if (distance < MOUSE_START_PX) {
          return;
        }

        startDrag(current);
      }

      event.preventDefault();
      const rawTilt = (event.clientX - current.lastX) * 0.9;
      current.tilt = Math.max(-MAX_TILT_DEG, Math.min(MAX_TILT_DEG, current.tilt * 0.75 + rawTilt * 0.25));
      current.lastX = event.clientX;
      current.lastY = event.clientY;
      placeGhost(event.clientX, event.clientY, current.tilt);

      const hit = readDropTarget(event.clientX, event.clientY);
      const target = hit?.target ?? null;
      const overHand = Boolean(document.elementFromPoint(event.clientX, event.clientY)?.closest('.match-hand'));
      const previous = viewRef.current;
      if (previous && (!isSameTarget(previous.hover, target) || previous.overHand !== overHand)) {
        updateView({
          ...previous,
          hover: target,
          hoverValid: Boolean(target && optionsRef.current.canDrop(current.item, target)),
          overHand,
        });
      }
    }

    function handleUp(event: PointerEvent): void {
      const current = press.current;
      if (!current || event.pointerId !== current.pointerId) {
        return;
      }

      press.current = null;
      if (!current.dragging) {
        return;
      }

      suppressClickUntil.current = performance.now() + 400;
      const hit = readDropTarget(event.clientX, event.clientY);
      const isValid = Boolean(hit && optionsRef.current.canDrop(current.item, hit.target));

      if (!hit || !isValid) {
        optionsRef.current.onMiss(current.item);
        returnToHand();
        return;
      }

      sawPending.current = false;
      if (optionsRef.current.onDrop(current.item, hit.target) === false) {
        returnToHand();
        return;
      }

      document.body.classList.remove('is-dragging-card');

      if (prefersReducedMotion()) {
        finishDrag();
        return;
      }

      landingRect.current = hit.element.getBoundingClientRect();
      updateView({ item: current.item, phase: 'landing', hover: null, hoverValid: false, overHand: false });
      later(() => {
        if (viewRef.current?.phase === 'landing') {
          updateView({ ...viewRef.current, phase: 'landed' });
        }
      }, LAND_MS + 20);
      later(() => {
        if (viewRef.current && !optionsRef.current.isConfirmed(viewRef.current.item)) {
          returnToHand();
        }
      }, RESULT_TIMEOUT_MS);
    }

    function handleCancel(event: PointerEvent): void {
      const current = press.current;
      if (!current || event.pointerId !== current.pointerId) {
        return;
      }

      press.current = null;
      if (current.dragging) {
        suppressClickUntil.current = performance.now() + 400;
        returnToHand();
      }
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape' && press.current?.dragging) {
        press.current = null;
        suppressClickUntil.current = performance.now() + 400;
        returnToHand();
      }
    }

    window.addEventListener('pointermove', handleMove, { passive: false });
    window.addEventListener('pointerup', handleUp);
    window.addEventListener('pointercancel', handleCancel);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('pointermove', handleMove);
      window.removeEventListener('pointerup', handleUp);
      window.removeEventListener('pointercancel', handleCancel);
      window.removeEventListener('keydown', handleKeyDown);
      clearTimers();
      document.body.classList.remove('is-dragging-card');
    };
    // returnToHand/finishDrag/placeGhost only use refs and setState.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Moves the ghost to the right place whenever the phase changes.
  useLayoutEffect(() => {
    const ghost = ghostRef.current;
    if (!ghost || !view) {
      return;
    }

    const ghostWidth = ghost.offsetWidth || 1;
    if (view.phase === 'drag' && press.current) {
      placeGhost(press.current.lastX, press.current.lastY, 0);
    } else if (view.phase === 'landing' && landingRect.current) {
      ghost.style.transform = centerTransform(landingRect.current, Math.min(1, landingRect.current.width / ghostWidth));
    } else if (view.phase === 'returning' && lastSource.current) {
      const { element, rect } = lastSource.current;
      const target = element.isConnected ? element.getBoundingClientRect() : rect;
      ghost.style.transform = centerTransform(target, target.width / ghostWidth);
    }
  }, [view?.phase, view]);

  // After landing: the copy disappears once the server state shows the move.
  useEffect(() => {
    if (view?.phase === 'landed' && options.isConfirmed(view.item)) {
      finishDrag();
    }
  });

  // The server answered (pending went back to false) but nothing changed: the move was rejected.
  useEffect(() => {
    const phase = viewRef.current?.phase;
    if (phase !== 'landing' && phase !== 'landed') {
      return;
    }

    if (options.pending) {
      sawPending.current = true;
      return;
    }

    if (sawPending.current) {
      sawPending.current = false;
      later(() => {
        const current = viewRef.current;
        if (current && current.phase !== 'returning' && !optionsRef.current.isConfirmed(current.item)) {
          returnToHand();
        }
      }, REJECT_CHECK_MS);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [options.pending, view?.phase]);

  function getSourceProps(item: DragItem) {
    return {
      onPointerDown: (event: ReactPointerEvent<HTMLElement>) => {
        if ((event.pointerType === 'mouse' && event.button !== 0) || viewRef.current) {
          return;
        }

        press.current = {
          item,
          pointerId: event.pointerId,
          pointerType: event.pointerType,
          startX: event.clientX,
          startY: event.clientY,
          startTime: performance.now(),
          source: event.currentTarget,
          sourceRect: event.currentTarget.getBoundingClientRect(),
          dragging: false,
          lastX: event.clientX,
          lastY: event.clientY,
          tilt: 0,
        };
      },
    };
  }

  return {
    view,
    ghostRef,
    getSourceProps,
    // A long press opened the inspect overlay: do not turn it into a drag.
    cancelPress: () => {
      if (!press.current?.dragging) {
        press.current = null;
      }
    },
    isDragging: () => Boolean(press.current?.dragging),
    // The click that follows a drag must not play the card a second time.
    shouldSuppressClick: () => performance.now() < suppressClickUntil.current || Boolean(press.current?.dragging),
  };
}
