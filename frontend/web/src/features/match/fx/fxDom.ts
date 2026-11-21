import type { FxContext } from './fxQueue';

// Small DOM helpers for the fx layer. Cards and zones carry data attributes:
//   data-fx-card="<instanceId>"            every card on the board and in the own hand
//   data-fx-zone="player-deck" ...         every zone (side + kind), plus "player-hand" / "opponent-hand"
// Only transform and opacity are animated (GPU, no layout work per frame).

export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

export const NORMAL_MS = 380;

export function cardElement(root: ParentNode | null, instanceId: string): HTMLElement | null {
  if (!root || !instanceId) {
    return null;
  }

  // Cards in dialogs (effect choice) show the same card again - only the one on the board counts.
  const matches = root.querySelectorAll<HTMLElement>(`[data-fx-card="${CSS.escape(instanceId)}"]`);
  return [...matches].find((element) => isOnBoard(element)) ?? null;
}

export function isOnBoard(element: Element): boolean {
  return !element.closest('.effect-choice-overlay, .fx-layer');
}

export function zoneElement(root: ParentNode | null, side: string, kind: string): HTMLElement | null {
  return root?.querySelector<HTMLElement>(`[data-fx-zone="${side}-${kind}"]`) ?? null;
}

// Box of an element relative to the fx layer.
export function boxOf(layer: HTMLElement, element: Element | null): Box | null {
  if (!element) {
    return null;
  }

  const base = layer.getBoundingClientRect();
  const rect = element.getBoundingClientRect();
  if (rect.width === 0 && rect.height === 0) {
    return null;
  }

  return { x: rect.left - base.left, y: rect.top - base.top, w: rect.width, h: rect.height };
}

export function centerBox(layer: HTMLElement, w: number, h: number): Box {
  return { x: (layer.clientWidth - w) / 2, y: (layer.clientHeight - h) / 2, w, h };
}

// Card size used for copies when the source is not a card (deck, life, DON!!).
export function cardSize(layer: HTMLElement): { w: number; h: number } {
  const value = getComputedStyle(layer).getPropertyValue('--board-card-w');
  const w = Number.parseFloat(value) || 64;
  return { w, h: (w * 88) / 63 };
}

// An absolutely placed element inside the layer, removed when the step is cleaned up.
export function place(layer: HTMLElement, context: FxContext, box: Box, className: string, html = ''): HTMLElement {
  const element = document.createElement('div');
  element.className = className;
  element.style.left = `${box.x}px`;
  element.style.top = `${box.y}px`;
  element.style.width = `${box.w}px`;
  element.style.height = `${box.h}px`;
  if (html) {
    element.innerHTML = html;
  }

  layer.appendChild(element);
  context.onCleanup(() => element.remove());
  return element;
}

// A copy of a card element (or a card back) that can fly over the board.
export function cardCopy(layer: HTMLElement, context: FxContext, box: Box, source: HTMLElement | null, back?: 'card' | 'don'): HTMLElement {
  const element = place(layer, context, box, `fx-card${back ? ` fx-card--back fx-card--${back}` : ''}`);
  if (source && !back) {
    const copy = source.cloneNode(true) as HTMLElement;
    copy.removeAttribute('data-fx-card');
    copy.removeAttribute('data-fx-hidden');
    copy.classList.remove('is-rested', 'is-selected', 'can-attack', 'is-targetable', 'is-attacker', 'is-battle-target');
    copy.style.width = '100%';
    copy.style.height = '100%';
    element.appendChild(copy);
  }

  return element;
}

// Transform that moves/scales an element placed at "to" so that it covers "from".
export function offset(from: Box, to: Box, extra = ''): string {
  const scaleX = from.w / (to.w || 1);
  const scaleY = from.h / (to.h || 1);
  const dx = from.x + from.w / 2 - (to.x + to.w / 2);
  const dy = from.y + from.h / 2 - (to.y + to.h / 2);
  return `translate(${dx}px, ${dy}px) scale(${scaleX}, ${scaleY}) ${extra}`.trim();
}

export function animate(
  element: Element,
  keyframes: Keyframe[],
  context: FxContext,
  ms = NORMAL_MS,
  options: KeyframeAnimationOptions = {},
): Animation {
  return element.animate(keyframes, {
    duration: Math.max(1, ms * context.speed),
    easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
    fill: 'both',
    ...options,
    delay: (options.delay ?? 0) * context.speed,
  });
}

// Flies a copy from one box to another (the copy is placed at the target).
export function fly(element: HTMLElement, from: Box, to: Box, context: FxContext, ms = NORMAL_MS, options: KeyframeAnimationOptions = {}): Animation {
  return animate(element, [{ transform: offset(from, to), opacity: 0.9 }, { transform: 'none', opacity: 1 }], context, ms, options);
}

// Hides the real card while its copy flies; shown again when the step ends or is skipped.
export function hideUntilDone(element: HTMLElement | null, context: FxContext): void {
  if (!element) {
    return;
  }

  element.dataset.fxHidden = '1';
  context.onCleanup(() => {
    delete element.dataset.fxHidden;
  });
}

// Short highlight that also works with animations off (no movement).
export function flash(element: HTMLElement | null, kind: string, ms = 700): void {
  if (!element) {
    return;
  }

  element.dataset.fxFlash = kind;
  window.setTimeout(() => {
    if (element.dataset.fxFlash === kind) {
      delete element.dataset.fxFlash;
    }
  }, ms);
}

// Marks the card of the opponent's last action for a moment.
export function markRecent(element: HTMLElement | null, ms = 2000): void {
  if (!element) {
    return;
  }

  element.dataset.fxRecent = '1';
  window.setTimeout(() => delete element.dataset.fxRecent, ms);
}

export function vibrate(pattern: number, enabled: boolean): void {
  if (!enabled) {
    return;
  }

  try {
    navigator.vibrate?.(pattern);
  } catch {
    // Not supported.
  }
}
