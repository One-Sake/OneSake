import type { CSSProperties, ReactNode } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';
import type { BoardSide, BoardZoneDefinition } from './boardTypes';

interface BoardSlotProps {
  side: BoardSide;
  zone: BoardZoneDefinition;
  // Hides the zone label when a card or stack covers the zone.
  filled?: boolean;
  children?: ReactNode;
}

function toClassName(kind: string): string {
  return kind.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);
}

// One zone of a player half (Character, Leader, Life, ...), placed by the CSS grid.
export function BoardSlot({ children, filled = false, side, zone }: BoardSlotProps) {
  return (
    <div
      aria-label={`${side === 'player' ? 'Your' : 'Opponent'} ${zone.label}`}
      className={`board-zone board-zone--${toClassName(zone.kind)}${filled ? ' is-filled' : ''}`}
      role="group"
    >
      <span className="board-zone__label" aria-hidden="true">
        {zone.label}
      </span>
      {children}
    </div>
  );
}

interface CardStackProps {
  count: number;
  label: string;
  variant: 'card' | 'life' | 'don';
  // Face-up card shown on top (used for the trash).
  topCard?: CardInstanceDto | null;
  onClick?: () => void;
}

// A pile of cards (Deck, Life, Trash, DON!! deck) with a count badge.
export function CardStack({ count, label, onClick, topCard, variant }: CardStackProps) {
  if (count <= 0) {
    return null;
  }

  const layers = Math.min(count, variant === 'life' ? 10 : 4);
  const ariaLabel = `${label}: ${count} ${count === 1 ? 'card' : 'cards'}`;

  const content = (
    <>
      {Array.from({ length: layers }, (_, index) => (
        <span
          key={index}
          aria-hidden="true"
          className={`card-back card-back--${variant}`}
          style={{ '--layer': index } as CSSProperties}
        />
      ))}
      {topCard && (
        <span
          aria-hidden="true"
          className="card-stack__top"
          style={{ '--layer': layers - 1 } as CSSProperties}
        >
          {topCard.imageUrl ? (
            <img alt="" draggable={false} loading="lazy" src={topCard.imageUrl} />
          ) : (
            <span>{topCard.name}</span>
          )}
        </span>
      )}
      <span className="card-stack__badge" aria-hidden="true">
        <small>{label}</small>
        {count}
      </span>
    </>
  );

  const className = `card-stack card-stack--${variant}`;

  if (onClick) {
    return (
      <button aria-label={`${ariaLabel}, show list`} className={className} onClick={onClick} type="button">
        {content}
      </button>
    );
  }

  return (
    <div aria-label={ariaLabel} className={className} role="img" tabIndex={0} title={ariaLabel}>
      {content}
    </div>
  );
}
