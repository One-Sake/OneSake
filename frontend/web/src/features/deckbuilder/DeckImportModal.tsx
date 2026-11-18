import { useEffect, useMemo, useState } from 'react';

import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import { createDeckCard, loadCardById } from './utils/deckCards';
import { createNewDeck } from './utils/deckStorage';
import { parseDeckText, type DeckTextError } from './utils/deckText';
import { getCardColors, getTotalCards, isLeaderCard, validateDeck } from './utils/deckValidation';

interface DeckImportModalProps {
  open: boolean;
  onClose: () => void;
  onImport: (deck: Deck) => void;
}

// undefined = still loading, null = unknown card ID
type CardLookup = Record<string, CardDto | null | undefined>;

const PLACEHOLDER_TEXT = '1xOP01-001\n4xOP01-016\n4xST01-012';

export function DeckImportModal({ onClose, onImport, open }: DeckImportModalProps) {
  const [text, setText] = useState('');
  const [deckName, setDeckName] = useState('');
  const [isNameEdited, setIsNameEdited] = useState(false);
  const [cardsById, setCardsById] = useState<CardLookup>({});
  const [loadError, setLoadError] = useState('');

  const parsed = useMemo(() => parseDeckText(text), [text]);
  const cardIdsKey = parsed.entries.map((entry) => entry.cardId).join(',');

  // Load card data for every ID in parallel. loadCardById caches results.
  useEffect(() => {
    let isCancelled = false;
    const cardIds = cardIdsKey ? cardIdsKey.split(',') : [];

    cardIds.forEach((cardId) => {
      loadCardById(cardId)
        .then((card) => {
          if (!isCancelled) {
            setCardsById((current) => ({ ...current, [cardId]: card }));
          }
        })
        .catch(() => {
          if (!isCancelled) {
            setLoadError('Kartendaten konnten nicht geladen werden. Ist das Backend erreichbar?');
          }
        });
    });

    return () => {
      isCancelled = true;
    };
  }, [cardIdsKey]);

  const isLoadingCards = parsed.entries.some((entry) => cardsById[entry.cardId] === undefined);

  // Builds the deck with the same DeckCard structure as the normal deckbuilder.
  const { importedDeck, importErrors } = useMemo(() => {
    const errors: DeckTextError[] = [...parsed.errors];
    const deck = createNewDeck();
    let leaderCard: CardDto | null = null;

    parsed.entries.forEach((entry) => {
      const card = cardsById[entry.cardId];
      if (card === undefined) {
        return;
      }

      if (card === null) {
        errors.push({ line: entry.line, message: `Unbekannte Karten-ID: ${entry.cardId}` });
        return;
      }

      if (isLeaderCard(card)) {
        if (leaderCard) {
          errors.push({ line: entry.line, message: `Zweiter Leader ignoriert: ${entry.cardId}` });
        } else {
          leaderCard = card;
          if (entry.quantity !== 1) {
            errors.push({ line: entry.line, message: 'Der Leader zählt immer genau 1x.' });
          }
        }
        return;
      }

      deck.cards.push(createDeckCard(card, entry.quantity));
    });

    const finalLeader = leaderCard as CardDto | null;
    if (finalLeader) {
      deck.leaderCardId = finalLeader.card_set_id;
      deck.leaderName = finalLeader.card_name;
      deck.leaderColors = getCardColors(finalLeader.card_color);
    }

    errors.sort((left, right) => left.line - right.line);
    return { importedDeck: deck, importErrors: errors };
  }, [cardsById, parsed]);

  // "Roronoa Zoro (001)" -> "Roronoa Zoro Deck"
  const leaderBaseName = importedDeck.leaderName?.replace(/\s*\([^)]*\)\s*$/, '').trim();
  const suggestedName = leaderBaseName ? `${leaderBaseName} Deck` : 'Importiertes Deck';
  const effectiveName = (isNameEdited ? deckName : suggestedName).trim() || suggestedName;
  const validation = validateDeck(importedDeck);
  const errorLines = new Set(importErrors.map((error) => error.line));
  const canImport =
    !isLoadingCards && (Boolean(importedDeck.leaderCardId) || importedDeck.cards.length > 0);

  const resetAndClose = (): void => {
    setText('');
    setDeckName('');
    setIsNameEdited(false);
    setLoadError('');
    onClose();
  };

  const importDeck = (): void => {
    onImport({ ...importedDeck, name: effectiveName });
    resetAndClose();
  };

  return (
    <Modal
      className="deck-import-modal"
      footer={(
        <>
          <Button onClick={resetAndClose} variant="ghost">
            Abbrechen
          </Button>
          <Button disabled={!canImport} onClick={importDeck}>
            Importieren
          </Button>
        </>
      )}
      onClose={resetAndClose}
      open={open}
      title="Deck importieren"
    >
      <div className="deck-import">
        <label className="field" htmlFor="deckImportText">
          Deckliste
          <textarea
            className="deck-import__textarea"
            id="deckImportText"
            onChange={(event) => setText(event.target.value)}
            placeholder={PLACEHOLDER_TEXT}
            rows={8}
            spellCheck={false}
            value={text}
          />
        </label>

        <label className="field" htmlFor="deckImportName">
          Deckname
          <input
            id="deckImportName"
            maxLength={40}
            onChange={(event) => {
              setDeckName(event.target.value);
              setIsNameEdited(true);
            }}
            placeholder={suggestedName}
            value={isNameEdited ? deckName : ''}
          />
        </label>

        {loadError && <p className="deck-import__error">{loadError}</p>}

        {parsed.entries.length > 0 && (
          <section className="deck-import__preview" aria-label="Vorschau">
            <div className="deck-import__summary">
              <span>
                Leader: <strong>{importedDeck.leaderName || '–'}</strong>
              </span>
              <span>
                Karten: <strong>{getTotalCards(importedDeck.cards)}/50</strong>
              </span>
              {isLoadingCards && <span>Lade Kartendaten…</span>}
            </div>

            <ul className="deck-import__list">
              {parsed.entries.map((entry) => {
                const card = cardsById[entry.cardId];
                return (
                  <li key={entry.cardId} className={errorLines.has(entry.line) ? 'is-error' : ''}>
                    <strong>{entry.quantity}x</strong>
                    <span>{entry.cardId}</span>
                    <small>
                      {card === undefined ? '…' : card === null ? 'unbekannt' : card.card_name}
                    </small>
                  </li>
                );
              })}
            </ul>
          </section>
        )}

        {importErrors.length > 0 && (
          <ul className="deck-import__errors" aria-label="Fehler">
            {importErrors.map((error) => (
              <li key={`${error.line}-${error.message}`}>
                Zeile {error.line}: {error.message}
              </li>
            ))}
          </ul>
        )}

        {!isLoadingCards && parsed.entries.length > 0 && !validation.isValid && (
          <div className="deck-import__validation">
            <strong>Deck ist noch nicht turnierfähig (kann trotzdem gespeichert werden):</strong>
            <ul>
              {validation.errors.map((error) => (
                <li key={error}>{error}</li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </Modal>
  );
}
