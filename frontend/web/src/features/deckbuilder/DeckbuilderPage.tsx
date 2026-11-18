import { useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';

import { getCardsBySetId } from '../../api/cardsApi';
import { Drawer } from '../../components/ui/Drawer';
import { Button } from '../../components/ui/Button';
import { Toast } from '../../components/ui/Toast';
import type { CardDto } from '../../types/cards';
import type { Deck, DeckCard } from '../../types/decks';
import { getLeaderColors } from '../match/leaderColors';
import { CardFilterSheet, type CardFilters } from './CardFilterSheet';
import { CardGrid } from './CardGrid';
import { CardHoverPreview } from './CardHoverPreview';
import { CardSearch } from './CardSearch';
import { CardSizeSlider } from './CardSizeSlider';
import { ColorPaletteFilter } from './ColorPaletteFilter';
import { DeckImportModal } from './DeckImportModal';
import { DeckLibrary } from './DeckLibrary';
import { DeckStackCard } from './DeckStackCard';
import {
  createNewDeck,
  deleteStoredDeck,
  duplicateStoredDeck,
  loadStoredDeck,
  loadStoredDecks,
  saveStoredDeck,
  saveStoredDecks,
  touchDeck,
  upsertStoredDeck,
} from './utils/deckStorage';
import {
  cardMatchesLeaderColors,
  getCardColors,
  getTotalCards,
  isLeaderCard,
} from './utils/deckValidation';
import { copyTextToClipboard } from './utils/clipboard';
import { createDeckCard } from './utils/deckCards';
import { deckToText } from './utils/deckText';


const availableSets = [
  'OP-01',
  'OP-02',
  'OP-03',
  'OP-04',
  'OP-05',
  'OP-06',
  'OP-07',
  'OP-08',
  'OP-09',
  'OP-10',
  'OP-11',
  'OP-12',
  'OP-13',

  'ST-01',
  'ST-02',
  'ST-03',
  'ST-04',
  'ST-05',
  'ST-06',
  'ST-07',
  'ST-08',
  'ST-09',
  'ST-10',
  'ST-11',
  'ST-12',
  'ST-13',
  'ST-14',
  'ST-15',
  'ST-16',
  'ST-17',
  'ST-18',
  'ST-19',
  'ST-20',
  'ST-21',
  'ST-22',
  'ST-23',
  'ST-24',
  'ST-25',
  'ST-26',
  'ST-27',
  'ST-28',
];
const cardsPerRowStorageKey = 'onesake.deckbuilder.cardsPerRow';
const defaultCardsPerRow = 8;
const allSetsOption = 'ALL_SETS';

function clampCardsPerRow(value: number): number {
  if (!Number.isFinite(value)) {
    return defaultCardsPerRow;
  }

  return Math.min(10, Math.max(7, Math.round(value)));
}

const emptyFilters: CardFilters = {
  searchText: '',
  archetype: '',
  selectedColors: [],
  cardType: '',
  cost: '',
  counter: '',
};

function uniqueValues(cards: CardDto[], readValue: (card: CardDto) => string): string[] {
  return [...new Set(cards.map(readValue).filter(Boolean))].sort((a, b) =>
    a.localeCompare(b, undefined, { numeric: true }),
  );
}

function loadCardsPerRow(): number {
  try {
    const storedValue = window.localStorage.getItem(cardsPerRowStorageKey);
    return storedValue ? clampCardsPerRow(Number(storedValue)) : defaultCardsPerRow;
  } catch {
    return defaultCardsPerRow;
  }
}

function saveCardsPerRow(cardsPerRow: number): void {
  try {
    window.localStorage.setItem(cardsPerRowStorageKey, String(clampCardsPerRow(cardsPerRow)));
  } catch {
    // The grid size preference is optional; deckbuilding should work without storage.
  }
}

function getPlayableCardMinWidth(cardsPerRow: number): number {
  return Math.min(210, Math.max(116, Math.round(1180 / clampCardsPerRow(cardsPerRow))));
}

function createPreviewCardFromDeckCard(deckCard: DeckCard, cardImage = ''): CardDto {
  return {
    inventory_price: null,
    market_price: null,
    card_name: deckCard.name,
    set_name: '',
    card_text: '',
    set_id: deckCard.cardId.split('-').slice(0, 2).join('-'),
    rarity: deckCard.rarity ?? '',
    card_set_id: deckCard.cardId,
    card_color: deckCard.color ?? '',
    card_type: deckCard.type ?? '',
    life: null,
    card_cost: deckCard.cost ?? null,
    card_power: deckCard.power ?? null,
    sub_types: deckCard.subTypes ?? '',
    counter_amount: deckCard.counter ?? null,
    attribute: deckCard.attribute ?? '',
    date_scraped: '',
    card_image_id: '',
    card_image: cardImage,
  };
}

function areStringArraysEqual(left: string[] | undefined, right: string[] | undefined): boolean {
  const leftValues = left ?? [];
  const rightValues = right ?? [];

  return (
    leftValues.length === rightValues.length &&
    leftValues.every((value, index) => value === rightValues[index])
  );
}

function compareCardsForDeckbuilder(left: CardDto, right: CardDto): number {
  const leftIsLeader = isLeaderCard(left);
  const rightIsLeader = isLeaderCard(right);

  if (leftIsLeader !== rightIsLeader) {
    return leftIsLeader ? -1 : 1;
  }

  return (
    left.card_set_id.localeCompare(right.card_set_id, undefined, { numeric: true }) ||
    left.card_name.localeCompare(right.card_name, undefined, { numeric: true })
  );
}

function needsDeckCardHydration(deckCard: DeckCard): boolean {
  return (
    deckCard.color === undefined &&
    deckCard.type === undefined &&
    deckCard.cost === undefined &&
    deckCard.power === undefined &&
    deckCard.counter === undefined &&
    deckCard.attribute === undefined &&
    deckCard.subTypes === undefined &&
    deckCard.rarity === undefined
  );
}

function hydrateDeckWithCards(deckToHydrate: Deck, loadedCards: CardDto[]): Deck {
  if (loadedCards.length === 0) {
    return deckToHydrate;
  }

  const loadedCardsById = new Map(loadedCards.map((card) => [card.card_set_id, card]));
  let changed = false;
  const leaderCard = loadedCardsById.get(deckToHydrate.leaderCardId);
  let leaderName = deckToHydrate.leaderName;
  let leaderColors = deckToHydrate.leaderColors;

  if (leaderCard) {
    const nextLeaderColors = getCardColors(leaderCard.card_color);

    if (
      deckToHydrate.leaderName !== leaderCard.card_name ||
      !areStringArraysEqual(deckToHydrate.leaderColors, nextLeaderColors)
    ) {
      leaderName = leaderCard.card_name;
      leaderColors = nextLeaderColors;
      changed = true;
    }
  }

  const hydratedCards = deckToHydrate.cards.map((deckCard) => {
    const loadedCard = loadedCardsById.get(deckCard.cardId);
    if (!loadedCard) {
      return deckCard;
    }

    if (!needsDeckCardHydration(deckCard)) {
      return deckCard;
    }

    const hydratedCard = createDeckCard(loadedCard, deckCard.quantity);
    changed = true;
    return hydratedCard;
  });

  return changed ? { ...deckToHydrate, leaderName, leaderColors, cards: hydratedCards } : deckToHydrate;
}

function dedupeCards(cardsToDedupe: CardDto[]): CardDto[] {
  const cardsById = new Map<string, CardDto>();

  cardsToDedupe.forEach((card) => {
    if (!cardsById.has(card.card_set_id)) {
      cardsById.set(card.card_set_id, card);
    }
  });

  return [...cardsById.values()];
}

export function DeckbuilderPage() {
  const [selectedSetId, setSelectedSetId] = useState(allSetsOption);
  const [filters, setFilters] = useState<CardFilters>(emptyFilters);
  const [setCards, setSetCards] = useState<CardDto[]>([]);
  const [allSetCards, setAllSetCards] = useState<CardDto[]>([]);
  const [deck, setDeck] = useState<Deck>(() => loadStoredDeck());
  const [savedDecks, setSavedDecks] = useState<Deck[]>(() => loadStoredDecks());
  const [previewCard, setPreviewCard] = useState<CardDto | null>(null);
  const [previewedCardId, setPreviewedCardId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [deckNotice, setDeckNotice] = useState('');
  const [deckOpen, setDeckOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState('');
  const [cardsPerRow, setCardsPerRow] = useState(loadCardsPerRow);
  const playableCardMinWidth = getPlayableCardMinWidth(cardsPerRow);
  const cardGridAreaStyle = {
    '--deck-playable-card-height': `${Math.round(playableCardMinWidth * 1.4)}px`,
  } as CSSProperties;

  const hasActiveLeader = Boolean(deck.leaderCardId);
  const shouldUseAllSetCards = selectedSetId === allSetsOption || hasActiveLeader;
  const cardPool = shouldUseAllSetCards && allSetCards.length > 0 ? allSetCards : setCards;
  const sortedCards = useMemo(() => [...cardPool].sort(compareCardsForDeckbuilder), [cardPool]);
  const loadedCardsById = useMemo(
    () => new Map([...setCards, ...allSetCards].map((card) => [card.card_set_id, card])),
    [allSetCards, setCards],
  );
  const activeLeaderColors = useMemo(() => deck.leaderColors ?? [], [deck.leaderColors]);
  const leaderPreviewCard = useMemo(
    () => loadedCardsById.get(deck.leaderCardId) ?? null,
    [deck.leaderCardId, loadedCardsById],
  );
  const fallbackLeaderPreviewCard = useMemo(() => {
    if (!deck.leaderCardId) {
      return null;
    }

    return createPreviewCardFromDeckCard(
      {
        cardId: deck.leaderCardId,
        name: deck.leaderName || deck.leaderCardId,
        quantity: 1,
        color: deck.leaderColors?.join('/'),
        type: 'Leader',
      },
    );
  }, [deck.leaderCardId, deck.leaderColors, deck.leaderName]);
  const totalDeckCards = getTotalCards(deck.cards);
  const activeFilterCount =
    (filters.searchText ? 1 : 0) +
    (filters.archetype ? 1 : 0) +
    filters.selectedColors.length +
    (filters.cardType ? 1 : 0) +
    (filters.cost ? 1 : 0) +
    (filters.counter ? 1 : 0);
  const filterOptions = useMemo(
    () => ({
      cardTypes: uniqueValues(sortedCards, (card) => card.card_type),
      costs: uniqueValues(sortedCards, (card) => String(card.card_cost ?? '')),
      counters: uniqueValues(sortedCards, (card) => String(card.counter_amount ?? '')).filter(
        (counter) => counter !== '4000' && counter !== '5000',
      ),
    }),
    [sortedCards],
  );

  useEffect(() => {
    saveStoredDeck(deck);
  }, [deck]);

  useEffect(() => {
    saveCardsPerRow(cardsPerRow);
  }, [cardsPerRow]);

  useEffect(() => {
    const cardsForHydration = dedupeCards([...setCards, ...allSetCards]);
    if (cardsForHydration.length === 0) {
      return;
    }

    setDeck((currentDeck) => hydrateDeckWithCards(currentDeck, cardsForHydration));

    const storedDecks = loadStoredDecks();
    let changed = false;
    const hydratedDecks = storedDecks.map((storedDeck) => {
      const hydratedDeck = hydrateDeckWithCards(storedDeck, cardsForHydration);
      if (hydratedDeck !== storedDeck) {
        changed = true;
      }

      return hydratedDeck;
    });

    if (changed) {
      saveStoredDecks(hydratedDecks);
      setSavedDecks(hydratedDecks);
    }
  }, [allSetCards, setCards]);

  useEffect(() => {
    if (!deckNotice) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setDeckNotice(''), 3000);
    return () => window.clearTimeout(timeoutId);
  }, [deckNotice]);

  useEffect(() => {
    if (selectedSetId === allSetsOption) {
      setSetCards([]);
      return undefined;
    }

    let ignoreResult = false;

    const loadCards = async (): Promise<void> => {
      setLoading(true);
      setError('');

      try {
        const loadedCards = await getCardsBySetId(selectedSetId);
        if (!ignoreResult) {
          setSetCards(loadedCards);
        }
      } catch (loadError) {
        if (!ignoreResult) {
          const message =
            loadError instanceof Error ? loadError.message : 'Cards could not be loaded.';
          setSetCards([]);
          setError(message);
        }
      } finally {
        if (!ignoreResult) {
          setLoading(false);
        }
      }
    };

    void loadCards();

    return () => {
      ignoreResult = true;
    };
  }, [selectedSetId]);

  useEffect(() => {
    if (!shouldUseAllSetCards || allSetCards.length > 0) {
      return undefined;
    }

    let ignoreResult = false;

    const loadAllSetCards = async (): Promise<void> => {
      setLoading(true);
      setError('');

      try {
        const loadedSetCards = await Promise.all(
          availableSets.map((setId) => getCardsBySetId(setId)),
        );
        if (!ignoreResult) {
          setAllSetCards(dedupeCards(loadedSetCards.flat()));
        }
      } catch (loadError) {
        if (!ignoreResult) {
          const message =
            loadError instanceof Error ? loadError.message : 'Playable cards could not be loaded.';
          setError(message);
        }
      } finally {
        if (!ignoreResult) {
          setLoading(false);
        }
      }
    };

    void loadAllSetCards();

    return () => {
      ignoreResult = true;
    };
  }, [allSetCards.length, shouldUseAllSetCards]);

  const showDeckNotice = (message: string): void => {
    setDeckNotice(message);
  };

  useEffect(() => {
    if (!toastMessage) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToastMessage(''), 2200);
    return () => window.clearTimeout(timeoutId);
  }, [toastMessage]);

  const exportDeck = async (deckToExport: Deck): Promise<void> => {
    const copied = await copyTextToClipboard(deckToText(deckToExport));
    setToastMessage(copied ? 'Deckliste kopiert' : 'Kopieren nicht möglich');
  };

  // Imported decks are saved like a normal "Save Deck" and opened right away.
  const importDeck = (importedDeck: Deck): void => {
    const nextDecks = upsertStoredDeck(importedDeck);
    const savedDeck = nextDecks.find((storedDeck) => storedDeck.id === importedDeck.id) ?? importedDeck;
    setSavedDecks(nextDecks);
    setDeck(savedDeck);
    setDeckOpen(false);
    showDeckNotice(`${savedDeck.name} importiert.`);
  };

  const previewCardForDeckbuilder = (card: CardDto | null): void => {
    setPreviewCard(card);
    setPreviewedCardId(card?.card_set_id ?? '');
  };

  const clearPreviewedCard = (cardId: string): void => {
    setPreviewedCardId((currentCardId) => (currentCardId === cardId ? '' : currentCardId));
  };

  const updateDeck = (update: (currentDeck: Deck) => Deck): void => {
    setDeck((currentDeck) => touchDeck(update(currentDeck)));
  };

  const addCardToDeck = (card: CardDto): void => {
    if (isLeaderCard(card)) {
      updateDeck((currentDeck) => ({
        ...currentDeck,
        leaderCardId: card.card_set_id,
        leaderName: card.card_name,
        leaderColors: getCardColors(card.card_color),
      }));
      setSelectedSetId(allSetsOption);
      showDeckNotice(`${card.card_name} set as leader.`);
      return;
    }

    if (
      activeLeaderColors.length > 0 &&
      !cardMatchesLeaderColors(card.card_color, activeLeaderColors)
    ) {
      showDeckNotice(`${card.card_name} does not match the active leader colors.`);
      return;
    }

    updateDeck((currentDeck) => {
      const existingCard = currentDeck.cards.find((deckCard) => deckCard.cardId === card.card_set_id);

      if (existingCard) {
        if (existingCard.quantity >= 4) {
          return currentDeck;
        }

        return {
          ...currentDeck,
          cards: currentDeck.cards.map((deckCard) =>
            deckCard.cardId === card.card_set_id
              ? { ...createDeckCard(card, deckCard.quantity + 1), quantity: deckCard.quantity + 1 }
              : deckCard,
          ),
        };
      }

      return {
        ...currentDeck,
        cards: [
          ...currentDeck.cards,
          {
            ...createDeckCard(card, 1),
          },
        ],
      };
    });
  };

  const decreaseDeckCard = (cardId: string): void => {
    updateDeck((currentDeck) => ({
      ...currentDeck,
      cards: currentDeck.cards
        .map((deckCard) =>
          deckCard.cardId === cardId ? { ...deckCard, quantity: deckCard.quantity - 1 } : deckCard,
        )
        .filter((deckCard) => deckCard.quantity > 0),
    }));
  };

  const renameDeck = (name: string): void => {
    updateDeck((currentDeck) => ({
      ...currentDeck,
      name,
    }));
  };

  const clearDeck = (): void => {
    setDeck(touchDeck({
      ...createNewDeck(),
      id: deck.id,
      createdAt: deck.createdAt,
      name: deck.name,
    }));
    showDeckNotice('Deck cleared.');
  };

  const removeLeader = (): void => {
    updateDeck((currentDeck) => ({
      ...currentDeck,
      leaderCardId: '',
      leaderName: undefined,
      leaderColors: undefined,
    }));
  };

  const resetFilters = (): void => {
    setFilters(emptyFilters);
  };

  const createDeck = (): void => {
    const newDeck = createNewDeck();
    setDeck(newDeck);
    setDeckOpen(false);
    showDeckNotice('New deck started.');
  };

  const saveDeck = (): void => {
    const nextDecks = upsertStoredDeck(deck);
    const savedDeck = nextDecks.find((storedDeck) => storedDeck.id === deck.id) ?? deck;
    setSavedDecks(nextDecks);
    setDeck(savedDeck);
    showDeckNotice(`${savedDeck.name} saved.`);
  };

  const loadDeck = (deckToLoad: Deck): void => {
    setDeck(deckToLoad);
    setDeckOpen(false);
    showDeckNotice(`${deckToLoad.name} loaded.`);
  };

  const duplicateDeck = (deckId: string): void => {
    const nextDecks = duplicateStoredDeck(deckId);
    setSavedDecks(nextDecks);
    if (nextDecks[0]) {
      setDeck(nextDecks[0]);
      showDeckNotice(`${nextDecks[0].name} created.`);
    }
  };

  const deleteDeck = (deckId: string): void => {
    const nextDecks = deleteStoredDeck(deckId);
    setSavedDecks(nextDecks);

    if (deck.id === deckId) {
      const newDeck = createNewDeck();
      setDeck(newDeck);
      saveStoredDeck(newDeck);
      showDeckNotice('Deleted deck. New deck started.');
      return;
    }

    showDeckNotice('Deck deleted.');
  };

  // Visual only: the leader colors of the deck being edited tint the page background.
  const leaderAccent = getLeaderColors(deck.leaderColors);
  const pageAccentStyle = {
    '--page-accent': leaderAccent.primary,
    '--page-accent-2': leaderAccent.secondary,
  } as CSSProperties;

  return (
    <div className="ui-page-bg deckbuilder-backdrop" style={pageAccentStyle}>
    <section className="deckbuilder-page">
      <div className="deckbuilder-layout">
        <main className="deckbuilder-main">
          <div className="deckbuilder-main-layout">
            <div className="deckbuilder-main-column">
          <section className="deckbuilder-topline" aria-label="Deck overview">
            <section className="panel deckbuilder-compact-card deckbuilder-compact-card--deck">
              <div className="deckbuilder-compact-card__header">
                <div className="deckbuilder-deck-title-row">
                  <h2>Deck List</h2>
                  <strong>{totalDeckCards}/50</strong>
                </div>
              </div>
              <label className="field deckbuilder-deck-name-field" htmlFor="deckNameCompact">
                Deck name
                <input
                  className="deckbuilder-deck-name-input"
                  id="deckNameCompact"
                  maxLength={40}
                  onChange={(event) => renameDeck(event.target.value)}
                  value={deck.name}
                />
              </label>

              <div className="deckbuilder-deck-stacks" aria-label="Cards in current deck">
                {deck.leaderCardId ? (
                  <DeckStackCard
                    cardId={deck.leaderCardId}
                    image={leaderPreviewCard?.card_image}
                    isLeader
                    name={deck.leaderName || deck.leaderCardId}
                    isPreviewed={previewedCardId === deck.leaderCardId}
                    onPreview={() => previewCardForDeckbuilder(leaderPreviewCard ?? fallbackLeaderPreviewCard)}
                    onPreviewEnd={() => clearPreviewedCard(deck.leaderCardId)}
                    quantity={1}
                  />
                ) : (
                  <DeckStackCard cardId="" isEmpty name="Leader" />
                )}
                {deck.cards.map((deckCard) => {
                  const loadedCard = loadedCardsById.get(deckCard.cardId);
                  const cardImage = loadedCard?.card_image;
                  const previewDeckCard = loadedCard ?? createPreviewCardFromDeckCard(deckCard, cardImage);

                  return (
                    <DeckStackCard
                      cardId={deckCard.cardId}
                      image={cardImage}
                      isPreviewed={previewedCardId === deckCard.cardId}
                      key={deckCard.cardId}
                      name={deckCard.name}
                      onPreview={() => previewCardForDeckbuilder(previewDeckCard)}
                      onPreviewEnd={() => clearPreviewedCard(deckCard.cardId)}
                      onRemove={() => decreaseDeckCard(deckCard.cardId)}
                      quantity={deckCard.quantity}
                    />
                  );
                })}
              </div>
            </section>
          </section>

          <div className="deckbuilder-toolbar" aria-label="Deckbuilder filters">
            <section className="set-picker">
              <select
                aria-label={hasActiveLeader ? 'Card pool' : 'Leader set'}
                id="setPicker"
                onChange={(event) => {
                  setSelectedSetId(event.target.value);
                  resetFilters();
                }}
                value={selectedSetId}
              >
                <option value={allSetsOption}>All sets</option>
                {availableSets.map((setId) => (
                  <option key={setId} value={setId}>
                    {setId}
                  </option>
                ))}
              </select>
            </section>

            <CardSearch
              activeFilterCount={activeFilterCount}
              onOpenFilters={() => setFiltersOpen(true)}
              onSearchChange={(searchText) => setFilters((currentFilters) => ({
                ...currentFilters,
                searchText,
              }))}
              searchText={filters.searchText}
            />

            <div className="deckbuilder-filter-panel">
              <ColorPaletteFilter
                compact
                onChange={(selectedColors) => setFilters((currentFilters) => ({
                  ...currentFilters,
                  selectedColors,
                }))}
                selectedColors={filters.selectedColors}
              />
              <CardSizeSlider cardsPerRow={cardsPerRow} onChange={setCardsPerRow} />
              <div className="deckbuilder-toolbar-actions">
                <Button
                  className="deckbuilder-toolbar-action"
                  disabled={!deck.leaderCardId}
                  onClick={removeLeader}
                  variant="ghost"
                >
                  Change leader
                </Button>
                <Button
                  className="deckbuilder-toolbar-action"
                  disabled={!deck.leaderCardId && deck.cards.length === 0}
                  onClick={clearDeck}
                  variant="ghost"
                >
                  Clear deck
                </Button>
                <Button
                  className="deckbuilder-toolbar-action"
                  disabled={!deck.leaderCardId && deck.cards.length === 0}
                  onClick={() => void exportDeck(deck)}
                  title="Deckliste als Text kopieren"
                  variant="ghost"
                >
                  Export
                </Button>
                <Button className="deckbuilder-toolbar-action deckbuilder-save-button" onClick={saveDeck}>
                  Save Deck
                </Button>
                <Button
                  className="deckbuilder-toolbar-action deckbuilder-library-button"
                  onClick={() => setDeckOpen(true)}
                  variant="ghost"
                >
                  Deck Library
                </Button>
              </div>
            </div>
          </div>

          {deckNotice && <div className="panel status-panel">{deckNotice}</div>}
          {loading && <div className="panel status-panel">Loading cards...</div>}
          {error && <div className="panel status-panel status-panel--error">{error}</div>}
          {!loading && !error && (
            <section className="deckbuilder-card-browser">
              <div className="deckbuilder-card-grid-area" style={cardGridAreaStyle}>
                <CardGrid
                  cards={sortedCards}
                  cardsPerRow={cardsPerRow}
                  deckCards={deck.cards}
                  filters={filters}
                  leaderCardId={deck.leaderCardId}
                  leaderColors={activeLeaderColors}
                  selectedSetId={selectedSetId}
                  allSetsOption={allSetsOption}
                  onAddCard={addCardToDeck}
                  onPreviewCard={previewCardForDeckbuilder}
                  onPreviewEnd={clearPreviewedCard}
                  onSetLeader={addCardToDeck}
                  previewedCardId={previewedCardId}
                />
              </div>
            </section>
          )}
            </div>
            <CardHoverPreview card={previewCard} />
          </div>
        </main>
      </div>

      <Drawer onClose={() => setDeckOpen(false)} open={deckOpen} title="Deck Library">
        <DeckLibrary
          activeDeckId={deck.id}
          decks={savedDecks}
          leaderCardsById={loadedCardsById}
          onCreateDeck={createDeck}
          onDeleteDeck={deleteDeck}
          onDuplicateDeck={duplicateDeck}
          onExportDeck={(deckToExport) => void exportDeck(deckToExport)}
          onImportDeck={() => setImportOpen(true)}
          onLoadDeck={loadDeck}
        />
      </Drawer>

      <DeckImportModal onClose={() => setImportOpen(false)} onImport={importDeck} open={importOpen} />
      <Toast message={toastMessage} />

      <CardFilterSheet
        filters={filters}
        onApply={() => setFiltersOpen(false)}
        onChange={setFilters}
        onClose={() => setFiltersOpen(false)}
        onReset={resetFilters}
        open={filtersOpen}
        options={filterOptions}
      />

    </section>
    </div>
  );
}
