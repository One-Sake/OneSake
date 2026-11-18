import { useMemo, useState } from 'react';
import type { CSSProperties } from 'react';

import { loadStoredDecks } from '../deckbuilder/utils/deckStorage';
import { validateDeck } from '../deckbuilder/utils/deckValidation';
import { getLeaderColors } from '../match/leaderColors';
import { useLeaderCards } from '../match/useLeaderCards';
import { MulliganTrainer } from './mulligan/MulliganTrainer';
import { ProbabilityCalculator } from './probability/ProbabilityCalculator';
import { SavedDeckSelect } from './SavedDeckSelect';

interface ToolsPageProps {
  onOpenDeckbuilder?: () => void;
}

export function ToolsPage({ onOpenDeckbuilder }: ToolsPageProps) {
  const [savedDecks] = useState(() => loadStoredDecks());
  const [selectedDeckId, setSelectedDeckId] = useState(savedDecks[0]?.id ?? '');
  const selectedDeck = useMemo(
    () => savedDecks.find((deck) => deck.id === selectedDeckId) ?? null,
    [selectedDeckId, savedDecks],
  );
  const selectedDeckValidation = useMemo(
    () => (selectedDeck ? validateDeck(selectedDeck) : null),
    [selectedDeck],
  );
  const leaderCardsById = useLeaderCards(savedDecks);

  // The leader colors of the selected deck tint the page background.
  const leaderCard = selectedDeck ? leaderCardsById[selectedDeck.leaderCardId] ?? null : null;
  const leaderColors = getLeaderColors(leaderCard?.card_color || selectedDeck?.leaderColors);
  const pageStyle = {
    '--page-accent': leaderColors.primary,
    '--page-accent-2': leaderColors.secondary,
    '--leader-color': leaderColors.primary,
    '--leader-color-2': leaderColors.secondary,
  } as CSSProperties;

  return (
    <section className="tools-page ui-page" style={pageStyle}>
      <header className="ui-page-header">
        <div>
          <span className="ui-kicker">OneSake · Game Analysis</span>
          <h1 className="ui-title">Captain Tools</h1>
        </div>
        {selectedDeck && selectedDeckValidation && (
          <span
            className={`ui-badge tools-deck-badge ${
              selectedDeckValidation.isValid ? 'ui-badge--success' : 'ui-badge--danger'
            }`}
          >
            {selectedDeckValidation.isValid ? 'Valid Deck' : 'Needs Work'}
          </span>
        )}
      </header>

      <SavedDeckSelect
        decks={savedDecks}
        leaderCardsById={leaderCardsById}
        onOpenDeckbuilder={onOpenDeckbuilder}
        onSelectDeck={setSelectedDeckId}
        selectedDeckId={selectedDeckId}
      />

      <div className="tools-grid">
        <MulliganTrainer deck={selectedDeck} />
        <ProbabilityCalculator deck={selectedDeck} />
      </div>
    </section>
  );
}
