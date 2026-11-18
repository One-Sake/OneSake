import { PlusIcon } from '../../components/ui/Icons';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import { getTotalCards, validateDeck } from '../deckbuilder/utils/deckValidation';
import { DeckCarousel } from '../match/DeckCarousel';
import { getLeaderColorHex, getLeaderColorNames } from '../match/leaderColors';
import { LeaderHero } from '../match/LeaderHero';
import { getLeaderImage } from '../match/useLeaderCards';

interface SavedDeckSelectProps {
  decks: Deck[];
  leaderCardsById: Record<string, CardDto | null>;
  selectedDeckId: string;
  onSelectDeck: (deckId: string) => void;
  onOpenDeckbuilder?: () => void;
}

// Deck picker in the style of the lobby: big leader card, deck info and a carousel.
export function SavedDeckSelect({
  decks,
  leaderCardsById,
  onOpenDeckbuilder,
  onSelectDeck,
  selectedDeckId,
}: SavedDeckSelectProps) {
  const selectedDeck = decks.find((deck) => deck.id === selectedDeckId) ?? decks[0] ?? null;

  if (decks.length === 0 || !selectedDeck) {
    return (
      <section className="ui-glass tools-deck-select tools-deck-select--empty">
        <h2 className="ui-heading">Deck Library</h2>
        <p className="tools-muted">No decks available yet. Build one first, then come back to train with it.</p>
        {onOpenDeckbuilder && (
          <button className="ui-btn-secondary tools-deck-select__action" onClick={onOpenDeckbuilder} type="button">
            <PlusIcon />
            Create one in Deckbuilder
          </button>
        )}
      </section>
    );
  }

  const leaderCard = leaderCardsById[selectedDeck.leaderCardId] ?? null;
  const leaderColorNames = getLeaderColorNames(leaderCard?.card_color || selectedDeck.leaderColors);
  const leaderName =
    leaderCard?.card_name || selectedDeck.leaderName || selectedDeck.leaderCardId || 'No leader';
  const selectedDeckTotal = getTotalCards(selectedDeck.cards);
  const selectedDeckValidation = validateDeck(selectedDeck);

  return (
    <section className="ui-glass tools-deck-select">
      <h2 className="ui-heading">
        Deck Library <span className="ui-badge">{decks.length}</span>
      </h2>

      <div className="tools-deck-select__hero">
        <LeaderHero imageUrl={getLeaderImage(selectedDeck, leaderCard)} leaderName={leaderName} />

        <div className="tools-deck-select__side">
          <div className="lobby-deck-info">
            <span className="lobby-deck-info__leader">{leaderName}</span>
            <strong className="lobby-deck-info__name">{selectedDeck.name}</strong>
            {leaderColorNames.length > 0 && (
              <span className="lobby-deck-info__colors">
                {leaderColorNames.map((colorName) => (
                  <span key={colorName} className="ui-color-dot" title={colorName}>
                    <span aria-hidden="true" style={{ background: getLeaderColorHex(colorName) }} />
                    {colorName}
                  </span>
                ))}
              </span>
            )}
            <span
              className={`lobby-deck-info__status ${selectedDeckValidation.isValid ? 'is-valid' : 'is-invalid'}`}
            >
              {selectedDeckTotal}/50 cards · {selectedDeckValidation.isValid ? 'Valid ✓' : 'Invalid'}
            </span>
          </div>

          <DeckCarousel
            decks={decks}
            leaderCardsById={leaderCardsById}
            onOpenDeckLibrary={() => onOpenDeckbuilder?.()}
            onSelectDeck={onSelectDeck}
            selectedDeckId={selectedDeck.id}
          />
        </div>
      </div>
    </section>
  );
}
