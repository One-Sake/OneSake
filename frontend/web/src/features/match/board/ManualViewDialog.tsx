import { useEffect, useMemo, useState } from 'react';
import type { DragEvent } from 'react';

import type { CardInstanceDto, ManualActionDto, ManualViewDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

type Row = 'top' | 'hand' | 'bottom' | 'trash';
type Rows = Record<Row, string[]>;

const ROWS: Array<{ row: Row; label: string; hint: string }> = [
  { row: 'top', label: 'Oben aufs Deck', hint: 'Reihenfolge per Drag: links = oberste Karte' },
  { row: 'hand', label: 'Auf die Hand', hint: '' },
  { row: 'bottom', label: 'Unter das Deck', hint: 'Reihenfolge per Drag: links kommt zuerst darunter' },
  { row: 'trash', label: 'In den Trash', hint: '' },
];

const BUTTON_LABELS: Record<Row, string> = {
  top: 'Oben',
  hand: 'Hand',
  bottom: 'Unten',
  trash: 'Trash',
};

interface ManualViewDialogProps {
  view: ManualViewDto | null | undefined;
  disabled: boolean;
  // Resolves to false when the server rejected the action.
  onAction: (action: ManualActionDto) => Promise<boolean>;
}

function emptyRows(cards: CardInstanceDto[]): Rows {
  return { top: cards.map((card) => card.instanceId), hand: [], bottom: [], trash: [] };
}

// Only the own player sees these cards (the server sends them only in the own snapshot).
export function ManualViewDialog({ disabled, onAction, view }: ManualViewDialogProps) {
  if (!view) {
    return null;
  }

  return view.kind === 'SEARCH' ? (
    <SearchDialog cards={view.cards} disabled={disabled} onAction={onAction} />
  ) : (
    <LookDialog cards={view.cards} disabled={disabled} onAction={onAction} />
  );
}

// "Look at the top N cards": distribute them to the top/bottom of the deck, the hand or the trash.
function LookDialog({ cards, disabled, onAction }: { cards: CardInstanceDto[]; disabled: boolean; onAction: ManualViewDialogProps['onAction'] }) {
  const cardKey = cards.map((card) => card.instanceId).join('|');
  const [rows, setRows] = useState<Rows>(() => emptyRows(cards));
  const [dragId, setDragId] = useState('');
  const [busy, setBusy] = useState(false);
  const byId = useMemo(() => new Map(cards.map((card) => [card.instanceId, card])), [cards]);

  // New cards (another look) start again in the top row.
  useEffect(() => {
    setRows(emptyRows(cards));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cardKey]);

  function moveTo(id: string, row: Row, beforeId?: string): void {
    setRows((previous) => {
      const next: Rows = {
        top: previous.top.filter((entry) => entry !== id),
        hand: previous.hand.filter((entry) => entry !== id),
        bottom: previous.bottom.filter((entry) => entry !== id),
        trash: previous.trash.filter((entry) => entry !== id),
      };
      const index = beforeId ? next[row].indexOf(beforeId) : -1;
      next[row].splice(index < 0 ? next[row].length : index, 0, id);
      return next;
    });
  }

  function handleDrop(event: DragEvent, row: Row, beforeId?: string): void {
    event.preventDefault();
    event.stopPropagation();
    const id = event.dataTransfer.getData('text/plain') || dragId;
    if (id && id !== beforeId) {
      moveTo(id, row, beforeId);
    }
    setDragId('');
  }

  // Sends the moves one after another. The top row keeps its order (index 0 = top card).
  async function finish(): Promise<void> {
    setBusy(true);
    const original = cards.map((card) => card.instanceId).filter((id) => rows.top.includes(id));
    const topChanged = rows.top.some((id, index) => original[index] !== id);
    const moves: ManualActionDto[] = [
      ...rows.hand.map((id) => ({ kind: 'MOVE_CARD', cardInstanceId: id, toZone: 'hand' })),
      ...rows.trash.map((id) => ({ kind: 'MOVE_CARD', cardInstanceId: id, toZone: 'trash' })),
      ...rows.bottom.map((id) => ({ kind: 'MOVE_CARD', cardInstanceId: id, toZone: 'deck', toPosition: 'bottom' })),
      ...(topChanged
        ? rows.top.map((id, index) => ({ kind: 'MOVE_CARD', cardInstanceId: id, toZone: 'deck', toPosition: String(index) }))
        : []),
      { kind: 'CLOSE_VIEW' },
    ];

    for (const move of moves) {
      if (!(await onAction(move))) {
        break;
      }
    }

    setBusy(false);
  }

  const isDisabled = disabled || busy;

  return (
    <div className="manual-view" role="presentation">
      <section aria-label="Oberste Karten ansehen" aria-modal="true" className="manual-view__dialog" role="dialog">
        <header className="manual-view__header">
          <span>✋ Nur du siehst diese Karten</span>
          <h2>Oberste {cards.length} Karten</h2>
        </header>

        {ROWS.map(({ hint, label, row }) => (
          <div
            key={row}
            className={`manual-view__row manual-view__row--${row}`}
            onDragOver={(event) => event.preventDefault()}
            onDrop={(event) => handleDrop(event, row)}
          >
            <p className="manual-view__row-label">
              {label}
              {hint && <small>{hint}</small>}
            </p>
            <div className="manual-view__cards">
              {rows[row].length === 0 && <span className="manual-view__empty">Hierher ziehen</span>}
              {rows[row].map((id) => {
                const card = byId.get(id);
                if (!card) {
                  return null;
                }

                return (
                  <div
                    key={id}
                    className={`manual-view__card${dragId === id ? ' is-dragging' : ''}`}
                    draggable={!isDisabled}
                    onDragEnd={() => setDragId('')}
                    onDragOver={(event) => event.preventDefault()}
                    onDragStart={(event) => {
                      event.dataTransfer.setData('text/plain', id);
                      setDragId(id);
                    }}
                    onDrop={(event) => handleDrop(event, row, id)}
                  >
                    <MatchCardView card={{ ...card, isRested: false }} showStats={false} size="slot" />
                    <div className="manual-view__card-actions">
                      {(Object.keys(BUTTON_LABELS) as Row[])
                        .filter((target) => target !== row)
                        .map((target) => (
                          <button
                            key={target}
                            disabled={isDisabled}
                            onClick={() => moveTo(id, target)}
                            type="button"
                          >
                            {BUTTON_LABELS[target]}
                          </button>
                        ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ))}

        <div className="manual-view__actions">
          <button className="is-primary" disabled={isDisabled} onClick={() => void finish()} type="button">
            Fertig
          </button>
        </div>
      </section>
    </div>
  );
}

// "Search your deck": pick one card for the hand (or the trash). The deck is shuffled afterwards.
function SearchDialog({ cards, disabled, onAction }: { cards: CardInstanceDto[]; disabled: boolean; onAction: ManualViewDialogProps['onAction'] }) {
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [busy, setBusy] = useState(false);

  const normalizedQuery = query.trim().toLowerCase();
  const visibleCards = normalizedQuery
    ? cards.filter((card) =>
        [card.name, card.cardId, card.category, card.text].some((value) => value?.toLowerCase().includes(normalizedQuery)),
      )
    : cards;
  const selectedCard = cards.find((card) => card.instanceId === selectedId) ?? null;

  async function finish(toZone?: 'hand' | 'trash'): Promise<void> {
    setBusy(true);
    const moved = !selectedCard || !toZone || (await onAction({ kind: 'MOVE_CARD', cardInstanceId: selectedCard.instanceId, toZone }));
    if (moved) {
      await onAction({ kind: 'CLOSE_VIEW' });
    }
    setBusy(false);
  }

  const isDisabled = disabled || busy;

  return (
    <div className="manual-view" role="presentation">
      <section aria-label="Deck durchsuchen" aria-modal="true" className="manual-view__dialog manual-view__dialog--search" role="dialog">
        <header className="manual-view__header">
          <span>✋ Nur du siehst diese Karten · danach wird gemischt</span>
          <h2>Deck durchsuchen ({cards.length})</h2>
        </header>

        <input
          aria-label="Karten filtern"
          autoFocus
          className="manual-view__search"
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Name, ID, Typ oder Text…"
          type="search"
          value={query}
        />

        <div className="manual-view__grid">
          {visibleCards.map((card) => (
            <MatchCardView
              key={card.instanceId}
              ariaLabel={`${card.name} (${card.cardId}) wählen`}
              card={{ ...card, isRested: false }}
              clickable={!isDisabled}
              onClick={() => setSelectedId((previous) => (previous === card.instanceId ? '' : card.instanceId))}
              showStats={false}
              size="slot"
              stateClassName={card.instanceId === selectedId ? 'is-selected' : ''}
            />
          ))}
          {visibleCards.length === 0 && <p className="manual-view__empty">Keine Karte gefunden.</p>}
        </div>

        <div className="manual-view__actions">
          <button className="is-primary" disabled={isDisabled || !selectedCard} onClick={() => void finish('hand')} type="button">
            {selectedCard ? `${selectedCard.name} auf die Hand` : 'Karte wählen'}
          </button>
          <button disabled={isDisabled || !selectedCard} onClick={() => void finish('trash')} type="button">
            In den Trash
          </button>
          <button disabled={isDisabled} onClick={() => void finish()} type="button">
            Ohne Karte beenden
          </button>
        </div>
      </section>
    </div>
  );
}
