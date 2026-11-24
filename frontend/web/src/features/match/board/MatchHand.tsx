import { useEffect, useRef, useState } from 'react';
import type { CSSProperties, PointerEvent } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

// Connects the hand to useHandDrag (drag & drop is an extra way to play; clicking still works).
export interface HandDragControls {
  getSourceProps: (card: CardInstanceDto) => { onPointerDown: (event: PointerEvent<HTMLElement>) => void };
  // Card that is being dragged right now (shown half transparent in the hand).
  sourceId: string;
  // True while a card follows the pointer: the hand lowers so the board is free.
  isActive: boolean;
  isDragging: () => boolean;
  cancelPress: () => void;
  shouldSuppressClick: () => boolean;
}

interface MatchHandProps {
  cards: CardInstanceDto[];
  drag?: HandDragControls;
  getAriaLabel: (card: CardInstanceDto) => string;
  getStateClassName: (card: CardInstanceDto) => string;
  onCardClick: (card: CardInstanceDto) => void;
  onInspect: (card: CardInstanceDto) => void;
  onPreview?: (card: CardInstanceDto | null) => void;
}

function isTouchDevice(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(hover: none)').matches;
}

// The local hand as a fanned row of cards at the bottom edge of the board.
// Mouse: hover raises the hand, click plays. Touch: first tap selects, second tap plays.
export function MatchHand({
  cards,
  drag,
  getAriaLabel,
  getStateClassName,
  onCardClick,
  onInspect,
  onPreview,
}: MatchHandProps) {
  const [focusedCardId, setFocusedCardId] = useState('');
  const handRef = useRef<HTMLDivElement>(null);

  // Tapping somewhere outside the hand lowers it again.
  useEffect(() => {
    if (!focusedCardId) {
      return undefined;
    }

    function handlePointerDown(event: globalThis.PointerEvent): void {
      if (!handRef.current?.contains(event.target as Node)) {
        setFocusedCardId('');
      }
    }

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [focusedCardId]);

  if (!cards.length) {
    return null;
  }

  function handleClick(card: CardInstanceDto): void {
    if (drag?.shouldSuppressClick()) {
      return;
    }

    if (isTouchDevice() && focusedCardId !== card.instanceId) {
      setFocusedCardId(card.instanceId);
      return;
    }

    setFocusedCardId('');
    onCardClick(card);
  }

  const center = (cards.length - 1) / 2;
  const angleStep = Math.min(5, 28 / cards.length);

  function handleInspect(card: CardInstanceDto): void {
    // A long press during a drag does nothing; a long press without moving opens the overlay.
    if (drag?.isDragging()) {
      return;
    }

    drag?.cancelPress();
    onInspect(card);
  }

  const handClassName = ['match-hand', focusedCardId && !drag?.isActive ? 'is-raised' : '', drag?.isActive ? 'is-dragging' : '']
    .filter(Boolean)
    .join(' ');

  return (
    <div
      ref={handRef}
      aria-label={`Your hand, ${cards.length} cards`}
      className={handClassName}
      data-drop-zone="hand"
      data-fx-zone="player-hand"
      role="group"
    >
      {cards.map((card, index) => {
        const offset = index - center;
        const style = {
          '--rot': `${offset * angleStep}deg`,
          '--lift': (offset * offset * 0.012).toFixed(3),
          '--z': index + 1,
        } as CSSProperties;

        return (
          <div
            key={card.instanceId}
            className={[
              'match-hand__card',
              focusedCardId === card.instanceId && !drag?.isActive ? 'is-focused' : '',
              drag?.sourceId === card.instanceId ? 'is-drag-source' : '',
            ]
              .filter(Boolean)
              .join(' ')}
            style={style}
            {...drag?.getSourceProps(card)}
          >
            <MatchCardView
              ariaLabel={getAriaLabel(card)}
              card={card}
              clickable
              onClick={() => handleClick(card)}
              onInspect={() => handleInspect(card)}
              onPreview={onPreview}
              size="hand"
              stateClassName={getStateClassName(card)}
            />
          </div>
        );
      })}
    </div>
  );
}
