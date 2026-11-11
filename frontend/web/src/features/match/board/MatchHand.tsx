import { useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

interface MatchHandProps {
  cards: CardInstanceDto[];
  getAriaLabel: (card: CardInstanceDto) => string;
  getStateClassName: (card: CardInstanceDto) => string;
  onCardClick: (card: CardInstanceDto) => void;
  onInspect: (card: CardInstanceDto) => void;
}

function isTouchDevice(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(hover: none)').matches;
}

// The local hand as a fanned row of cards at the bottom edge of the board.
// Mouse: hover raises the hand, click plays. Touch: first tap selects, second tap plays.
export function MatchHand({ cards, getAriaLabel, getStateClassName, onCardClick, onInspect }: MatchHandProps) {
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
    if (isTouchDevice() && focusedCardId !== card.instanceId) {
      setFocusedCardId(card.instanceId);
      return;
    }

    setFocusedCardId('');
    onCardClick(card);
  }

  const center = (cards.length - 1) / 2;
  const angleStep = Math.min(5, 28 / cards.length);

  return (
    <div
      ref={handRef}
      aria-label={`Your hand, ${cards.length} cards`}
      className={focusedCardId ? 'match-hand is-raised' : 'match-hand'}
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
            className={focusedCardId === card.instanceId ? 'match-hand__card is-focused' : 'match-hand__card'}
            style={style}
          >
            <MatchCardView
              ariaLabel={getAriaLabel(card)}
              card={card}
              clickable
              onClick={() => handleClick(card)}
              onInspect={() => onInspect(card)}
              size="hand"
              stateClassName={getStateClassName(card)}
            />
          </div>
        );
      })}
    </div>
  );
}
