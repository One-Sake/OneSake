import type { ReactNode } from 'react';

import type { Deck } from '../../types/decks';
import { Button } from '../../components/ui/Button';
import { validateDeck } from '../deckbuilder/utils/deckValidation';
import { getLeaderImage, useLeaderCards } from './useLeaderCards';

interface MatchDeckSelectProps {
  decks: Deck[];
  selectedDeckId: string;
  embedded?: boolean;
  headerAction?: ReactNode;
  title?: string;
  onSelectDeck: (deckId: string) => void;
  onOpenDeckbuilder?: () => void;
}

export function MatchDeckSelect({
  decks,
  embedded = false,
  headerAction,
  onOpenDeckbuilder,
  onSelectDeck,
  selectedDeckId,
  title = 'Deck',
}: MatchDeckSelectProps) {
  const leaderCardsById = useLeaderCards(decks);

  return (
    <section className={embedded ? 'match-deck-select match-deck-select--embedded' : 'panel match-deck-select'}>
      <div className="panel-title-row">
        <div>
          <h2>{title}</h2>
        </div>
        {headerAction}
      </div>

      {decks.length === 0 ? (
        <div className="match-deck-select__empty">
          <strong>No saved decks.</strong>
          {onOpenDeckbuilder && (
            <Button onClick={onOpenDeckbuilder} variant="secondary">
              Go to Deckbuilder
            </Button>
          )}
        </div>
      ) : (
        <div className="match-deck-list" role="radiogroup" aria-label="Saved decks">
          {decks.map((deck) => {
            const validation = validateDeck(deck);
            const isSelected = deck.id === selectedDeckId;
            const leaderImage = getLeaderImage(deck, leaderCardsById[deck.leaderCardId]);

            return (
              <button
                key={deck.id}
                aria-checked={isSelected}
                aria-label={`Select deck ${deck.name}`}
                className={`match-deck-option ${isSelected ? 'is-selected' : ''} ${
                  validation.isValid ? 'is-valid' : 'is-invalid'
                }`}
                onClick={() => onSelectDeck(deck.id)}
                role="radio"
                type="button"
              >
                <span className="match-deck-option__leader" aria-hidden="true">
                  {leaderImage ? (
                    <img alt="" src={leaderImage} />
                  ) : (
                    <span className="match-deck-option__leader-placeholder">
                      <span>Leader</span>
                      <strong>{deck.leaderCardId || deck.leaderName || '-'}</strong>
                    </span>
                  )}
                </span>
                <strong className="match-deck-option__name">{deck.name}</strong>
              </button>
            );
          })}
        </div>
      )}
    </section>
  );
}
