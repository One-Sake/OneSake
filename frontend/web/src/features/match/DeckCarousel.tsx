import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import { getLeaderImage } from './useLeaderCards';

interface DeckCarouselProps {
  decks: Deck[];
  leaderCardsById: Record<string, CardDto | null>;
  selectedDeckId: string;
  onSelectDeck: (deckId: string) => void;
  onOpenDeckLibrary: () => void;
}

// Horizontal row of small leader thumbnails to switch decks quickly.
export function DeckCarousel({
  decks,
  leaderCardsById,
  onOpenDeckLibrary,
  onSelectDeck,
  selectedDeckId,
}: DeckCarouselProps) {
  return (
    <div className="deck-carousel" role="radiogroup" aria-label="Deine Decks">
      {decks.map((deck) => {
        const isSelected = deck.id === selectedDeckId;
        const leaderImage = getLeaderImage(deck, leaderCardsById[deck.leaderCardId]);

        return (
          <button
            key={deck.id}
            aria-checked={isSelected}
            className={`deck-carousel__item ${isSelected ? 'is-selected' : ''}`}
            onClick={() => onSelectDeck(deck.id)}
            role="radio"
            title={deck.name}
            type="button"
          >
            <span className="deck-carousel__thumb" aria-hidden="true">
              {leaderImage ? (
                <img alt="" loading="lazy" src={leaderImage} />
              ) : (
                <span className="deck-carousel__thumb-fallback">{deck.leaderCardId || '?'}</span>
              )}
            </span>
            <span className="deck-carousel__name">{deck.name}</span>
          </button>
        );
      })}

      <button className="deck-carousel__item deck-carousel__item--library" onClick={onOpenDeckLibrary} type="button">
        <span className="deck-carousel__thumb" aria-hidden="true">
          <span className="deck-carousel__plus">+</span>
        </span>
        <span className="deck-carousel__name">Deck Library</span>
      </button>
    </div>
  );
}
