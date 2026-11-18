import { Button } from '../../components/ui/Button';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import { formatCardColors, getTotalCards, validateDeck } from './utils/deckValidation';

interface DeckLibraryProps {
  decks: Deck[];
  activeDeckId: string;
  leaderCardsById: Map<string, CardDto>;
  onCreateDeck: () => void;
  onDeleteDeck: (deckId: string) => void;
  onDuplicateDeck: (deckId: string) => void;
  onExportDeck: (deck: Deck) => void;
  onImportDeck: () => void;
  onLoadDeck: (deck: Deck) => void;
}

function formatUpdatedAt(updatedAt: string): string {
  const date = new Date(updatedAt);
  if (Number.isNaN(date.getTime())) {
    return 'Unknown date';
  }

  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function getDeckTypeCount(deck: Deck, typeName: string): number {
  return deck.cards
    .filter((card) => card.type?.toLowerCase().includes(typeName))
    .reduce((sum, card) => sum + card.quantity, 0);
}

function findLeaderCard(leaderCardsById: Map<string, CardDto>, leaderCardId: string): CardDto | null {
  if (!leaderCardId) {
    return null;
  }

  const exactMatch = leaderCardsById.get(leaderCardId);
  if (exactMatch) {
    return exactMatch;
  }

  const normalizedLeaderCardId = leaderCardId.toLowerCase();
  return (
    [...leaderCardsById.values()].find(
      (card) => card.card_set_id.toLowerCase() === normalizedLeaderCardId,
    ) ?? null
  );
}

export function DeckLibrary({
  activeDeckId,
  decks,
  leaderCardsById,
  onCreateDeck,
  onDeleteDeck,
  onDuplicateDeck,
  onExportDeck,
  onImportDeck,
  onLoadDeck,
}: DeckLibraryProps) {
  return (
    <section className="panel deck-panel deck-library">
      <div className="deck-library__header">
        <h2>Deck Library</h2>
        <div className="deck-library__actions">
          <Button onClick={onImportDeck} variant="ghost">
            Deck importieren
          </Button>
          <Button onClick={onCreateDeck} variant="ghost">
            New Deck
          </Button>
        </div>
      </div>

      {decks.length > 0 ? (
        <ul className="deck-library__list">
          {decks.map((deck) => {
            const leaderCard = findLeaderCard(leaderCardsById, deck.leaderCardId);
            const totalCards = getTotalCards(deck.cards);
            const validation = validateDeck(deck);
            const characterCount = getDeckTypeCount(deck, 'character');
            const eventCount = getDeckTypeCount(deck, 'event');
            const stageCount = getDeckTypeCount(deck, 'stage');
            const statusLabel = validation.isValid
              ? 'Valid'
              : !deck.leaderCardId
                ? 'No leader'
                : totalCards < 50
                  ? 'Missing cards'
                  : 'Incomplete';

            return (
              <li key={deck.id} className={deck.id === activeDeckId ? 'is-active' : ''}>
                <button
                  aria-label={`Load ${deck.name}`}
                  className="deck-library__deck"
                  onClick={() => onLoadDeck(deck)}
                  type="button"
                >
                  <span className="deck-library__leader" aria-hidden="true">
                    {leaderCard?.card_image ? (
                      <img alt="" src={leaderCard.card_image} />
                    ) : (
                      <span className="deck-library__leader-placeholder">
                        <span>Leader</span>
                        <strong>{deck.leaderName || deck.leaderCardId || '-'}</strong>
                      </span>
                    )}
                  </span>
                  <span className="deck-library__content">
                    <span className="deck-library__title-row">
                      <strong>{deck.name}</strong>
                      <span className={validation.isValid ? 'is-valid' : 'is-incomplete'}>
                        {statusLabel}
                      </span>
                    </span>
                    <span className="deck-library__leader-name">
                      {deck.leaderName || leaderCard?.card_name || 'No leader selected'}
                    </span>
                    <span className="deck-library__meta-grid">
                      <span>
                        <small>Leader ID</small>
                        <strong>{deck.leaderCardId || '-'}</strong>
                      </span>
                      <span>
                        <small>Colors</small>
                        <strong>{formatCardColors(deck.leaderColors ?? [])}</strong>
                      </span>
                      <span>
                        <small>Set</small>
                        <strong>
                          {leaderCard?.set_id || deck.leaderCardId.split('-').slice(0, 2).join('-') || '-'}
                        </strong>
                      </span>
                      <span>
                        <small>Main Deck</small>
                        <strong>{totalCards}/50</strong>
                      </span>
                      <span>
                        <small>Unique</small>
                        <strong>{deck.cards.length}</strong>
                      </span>
                      <span>
                        <small>Updated</small>
                        <strong>{formatUpdatedAt(deck.updatedAt)}</strong>
                      </span>
                    </span>
                    <span className="deck-library__type-row">
                      <span>Characters {characterCount}</span>
                      <span>Events {eventCount}</span>
                      <span>Stages {stageCount}</span>
                    </span>
                  </span>
                </button>
                <div className="deck-library__deck-actions">
                  <Button
                    aria-label={`Export ${deck.name}`}
                    onClick={() => onExportDeck(deck)}
                    title="Deckliste als Text kopieren"
                    variant="ghost"
                  >
                    Export
                  </Button>
                  <Button onClick={() => onDuplicateDeck(deck.id)} variant="ghost">
                    Duplicate
                  </Button>
                  <Button onClick={() => onDeleteDeck(deck.id)} variant="danger">
                    Delete
                  </Button>
                </div>
              </li>
            );
          })}
        </ul>
      ) : (
        <p className="deck-library__empty">No saved decks yet.</p>
      )}
    </section>
  );
}
