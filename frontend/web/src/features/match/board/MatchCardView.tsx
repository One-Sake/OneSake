import { useEffect, useRef } from 'react';
import type { MouseEvent, PointerEvent } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';

const LONG_PRESS_MS = 500;

interface MatchCardViewProps {
  card: CardInstanceDto;
  size: 'slot' | 'hand' | 'inspect';
  ariaLabel?: string;
  clickable?: boolean;
  disabled?: boolean;
  // Extra state classes like "is-selected" or "is-targetable".
  stateClassName?: string;
  showStats?: boolean;
  onClick?: () => void;
  // Right click or long press opens the card inspect overlay.
  onInspect?: () => void;
  // Called with the card when the mouse enters it, and with null when it leaves.
  onPreview?: (card: CardInstanceDto | null) => void;
}

function hasPower(card: CardInstanceDto): boolean {
  return card.category === 'Leader' || card.category === 'Character' || !card.category;
}

export function MatchCardView({
  ariaLabel,
  card,
  clickable = false,
  disabled = false,
  onClick,
  onInspect,
  onPreview,
  showStats = true,
  size,
  stateClassName = '',
}: MatchCardViewProps) {
  const pressTimer = useRef<number>();
  const longPressed = useRef(false);

  useEffect(() => () => window.clearTimeout(pressTimer.current), []);

  const className = [
    'match-card-view',
    `match-card-view--${size}`,
    card.imageUrl ? 'match-card-view--image' : '',
    card.isRested ? 'is-rested' : '',
    clickable ? 'match-card-view--clickable' : '',
    disabled ? 'match-card-view--disabled' : '',
    stateClassName,
  ]
    .filter(Boolean)
    .join(' ');

  const power = card.power ?? card.basePower ?? 0;
  const powerBoosted = power > (card.basePower ?? power);

  // ---------- Inspect (right click / long press) and hover preview ----------

  function handleContextMenu(event: MouseEvent): void {
    if (!onInspect) {
      return;
    }

    event.preventDefault();
    onInspect();
  }

  function handlePointerDown(event: PointerEvent): void {
    if (!onInspect || event.pointerType === 'mouse') {
      return;
    }

    longPressed.current = false;
    window.clearTimeout(pressTimer.current);
    pressTimer.current = window.setTimeout(() => {
      longPressed.current = true;
      onInspect();
    }, LONG_PRESS_MS);
  }

  function cancelLongPress(): void {
    window.clearTimeout(pressTimer.current);
  }

  function handleClick(): void {
    // A long press already opened the inspect overlay, so skip the normal click.
    if (longPressed.current) {
      longPressed.current = false;
      return;
    }

    onClick?.();
  }

  const interactionProps = {
    onContextMenu: handleContextMenu,
    onPointerDown: handlePointerDown,
    onPointerUp: cancelLongPress,
    onPointerCancel: cancelLongPress,
    onPointerEnter: (event: PointerEvent) => {
      if (event.pointerType === 'mouse') {
        onPreview?.(card);
      }
    },
    onPointerLeave: () => {
      cancelLongPress();
      onPreview?.(null);
    },
  };

  const content = (
    <>
      {card.imageUrl ? (
        <img
          alt=""
          className="match-card-view__image"
          draggable={false}
          loading="lazy"
          src={card.imageUrl}
        />
      ) : (
        <>
          <span className="match-card-view__frame" aria-hidden="true" />
          <span className="match-card-view__art" aria-hidden="true">
            <span>{card.cardId}</span>
          </span>
          <span className="match-card-view__body">
            <strong>{card.name}</strong>
            <small>{card.cardId}</small>
          </span>
        </>
      )}

      {showStats && size !== 'inspect' && (
        <span className="match-card-view__stats" aria-hidden="true">
          {size === 'hand' && typeof card.cost === 'number' && card.category !== 'Leader' && (
            <span className="match-card-stat match-card-stat--cost">{card.cost}</span>
          )}
          {hasPower(card) && power > 0 && (
            <span className={powerBoosted ? 'match-card-stat match-card-stat--power is-boosted' : 'match-card-stat match-card-stat--power'}>
              {power}
            </span>
          )}
          {size === 'hand' && Boolean(card.counter) && (
            <span className="match-card-stat match-card-stat--counter">+{card.counter}</span>
          )}
          {Boolean(card.attachedDon) && (
            <span className="match-card-stat match-card-stat--don">DON x{card.attachedDon}</span>
          )}
        </span>
      )}
    </>
  );

  const label = ariaLabel ?? `${card.name} (${card.cardId})`;

  if (clickable) {
    return (
      <button
        aria-label={label}
        className={className}
        disabled={disabled}
        onClick={handleClick}
        type="button"
        {...interactionProps}
      >
        {content}
      </button>
    );
  }

  return (
    <span aria-label={label} className={className} role="img" {...interactionProps}>
      {content}
    </span>
  );
}
