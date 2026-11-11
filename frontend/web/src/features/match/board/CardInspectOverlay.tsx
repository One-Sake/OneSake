import { useEffect } from 'react';

import type { CardInstanceDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

export interface CardPile {
  title: string;
  cards: CardInstanceDto[];
}

interface CardInspectOverlayProps {
  card: CardInstanceDto | null;
  // Optional list of cards (e.g. the trash). A selected card is shown on top of it.
  pile?: CardPile | null;
  onClose: () => void;
  onSelectCard?: (card: CardInstanceDto) => void;
}

export function CardInspectOverlay({ card, onClose, onSelectCard, pile = null }: CardInspectOverlayProps) {
  const isOpen = Boolean(card || pile);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!card) {
    if (!pile) {
      return null;
    }

    return (
      <div className="card-inspect-overlay" role="presentation" onMouseDown={onClose}>
        <section
          aria-label={pile.title}
          aria-modal="true"
          className="card-inspect-overlay__dialog card-inspect-overlay__dialog--pile"
          onMouseDown={(event) => event.stopPropagation()}
          role="dialog"
        >
          <header className="card-inspect-overlay__header">
            <div>
              <span>{pile.cards.length} cards</span>
              <h2>{pile.title}</h2>
            </div>
            <button aria-label={`Close ${pile.title}`} onClick={onClose} type="button">
              Close
            </button>
          </header>
          {pile.cards.length ? (
            <div className="card-inspect-overlay__pile">
              {pile.cards.map((pileCard) => (
                <MatchCardView
                  key={pileCard.instanceId}
                  ariaLabel={`Inspect ${pileCard.name} (${pileCard.cardId})`}
                  card={{ ...pileCard, isRested: false }}
                  clickable
                  onClick={() => onSelectCard?.(pileCard)}
                  showStats={false}
                  size="slot"
                />
              ))}
            </div>
          ) : (
            <p className="card-inspect-overlay__text">No cards.</p>
          )}
        </section>
      </div>
    );
  }

  const stats: Array<[string, string | number | undefined]> = [
    ['Card ID', card.cardId],
    ['Category', card.category],
    ['Cost', card.category === 'Leader' ? undefined : card.cost],
    ['Power', card.power ?? card.basePower],
    ['Counter', card.counter ? `+${card.counter}` : undefined],
    ['DON!! given', card.attachedDon || undefined],
    ['State', card.isRested ? 'Rested' : undefined],
  ];

  return (
    <div className="card-inspect-overlay" role="presentation" onMouseDown={onClose}>
      <section
        aria-label={`${card.name} card details`}
        aria-modal="true"
        className="card-inspect-overlay__dialog"
        onMouseDown={(event) => event.stopPropagation()}
        role="dialog"
      >
        <header className="card-inspect-overlay__header">
          <div>
            <span>Card Inspect</span>
            <h2>{card.name}</h2>
          </div>
          <button aria-label="Close card inspect" onClick={onClose} type="button">
            {pile ? 'Back' : 'Close'}
          </button>
        </header>
        <MatchCardView card={{ ...card, isRested: false }} showStats={false} size="inspect" />
        <dl className="card-inspect-overlay__meta">
          {stats
            .filter(([, value]) => value !== undefined && value !== '')
            .map(([label, value]) => (
              <div key={label}>
                <dt>{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
        </dl>
        {card.text && <p className="card-inspect-overlay__text">{card.text}</p>}
      </section>
    </div>
  );
}
