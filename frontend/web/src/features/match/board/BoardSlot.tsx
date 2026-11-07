import type { ReactNode } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';
import type { BoardSlotDefinition } from './boardTypes';
import { MatchCardView } from './MatchCardView';

interface BoardSlotProps {
  slot: BoardSlotDefinition;
  count?: number;
  active?: boolean;
  card?: CardInstanceDto | null;
  filled?: boolean;
  // Extra classes for the card, e.g. "is-selected", "is-targetable", "is-attacker".
  cardStateClassName?: string;
  onCardClick?: (card: CardInstanceDto) => void;
  // Custom content (used for the DON!! area).
  children?: ReactNode;
}

function toSlotClassName(kind: BoardSlotDefinition['kind']): string {
  return kind.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);
}

export function BoardSlot({
  active = false,
  card,
  cardStateClassName,
  children,
  count,
  filled = false,
  onCardClick,
  slot,
}: BoardSlotProps) {
  const hasCount = typeof count === 'number';

  return (
    <div
      aria-label={`${slot.side} ${slot.label}`}
      className={[
        'board-slot',
        `board-slot--${toSlotClassName(slot.kind)}`,
        `board-slot--${slot.side}`,
        filled || card ? 'board-slot--filled' : '',
        card ? 'board-slot--has-card' : '',
        active ? 'board-slot--active' : '',
      ]
        .filter(Boolean)
        .join(' ')}
      style={{
        left: `${slot.x}%`,
        top: `${slot.y}%`,
        width: `${slot.width}%`,
        height: `${slot.height}%`,
      }}
    >
      <span className="board-slot__label">{slot.label}</span>
      {card ? (
        <MatchCardView
          card={card}
          clickable={Boolean(onCardClick)}
          onClick={() => onCardClick?.(card)}
          size="slot"
          stateClassName={cardStateClassName}
        />
      ) : (
        children ??
        (hasCount && (
          <span className={`board-slot-stack board-slot-stack--${toSlotClassName(slot.kind)}`}>
            <span className="board-slot-stack__cards" aria-hidden="true" />
            <strong className="board-slot__count">{count}</strong>
            <small>{slot.label}</small>
          </span>
        ))
      )}
      <span className="board-slot__debug">{slot.id}</span>
    </div>
  );
}
