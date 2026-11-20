import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';

import { submitBugReport } from '../../../api/bugReportsApi';
import type { BugCategory } from '../../../api/bugReportsApi';
import { getGuestId } from '../../../api/guestId';
import type { CardInstanceDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

const MAX_LENGTH = 1000;

const CATEGORIES: Array<{ value: BugCategory; label: string }> = [
  { value: 'EFFECT', label: 'Effekt falsch' },
  { value: 'ORDER', label: 'Reihenfolge falsch' },
  { value: 'STUCK', label: 'Spiel hängt' },
  { value: 'DISPLAY', label: 'Anzeige' },
  { value: 'OTHER', label: 'Sonstiges' },
];

interface BugReportDialogProps {
  open: boolean;
  roomCode: string;
  // Current seq of the match (the server saves the match up to here).
  atSeq: number;
  // Cards the player can mark as "this card is wrong".
  cards: CardInstanceDto[];
  onClose: () => void;
}

// "Fehler melden": a short description, what went wrong and optionally the card.
// The match itself is attached by the server.
export function BugReportDialog({ atSeq, cards, onClose, open, roomCode }: BugReportDialogProps) {
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState<BugCategory>('EFFECT');
  const [cardId, setCardId] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const [reportId, setReportId] = useState<number | null>(null);

  // Every time the dialog opens it starts empty.
  useEffect(() => {
    if (open) {
      setDescription('');
      setCategory('EFFECT');
      setCardId('');
      setError('');
      setReportId(null);
    }
  }, [open]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose, open]);

  if (!open) {
    return null;
  }

  async function handleSubmit(event: FormEvent): Promise<void> {
    event.preventDefault();
    if (!description.trim() || sending) {
      return;
    }

    setSending(true);
    setError('');
    try {
      const id = await submitBugReport({
        roomCode,
        guestId: getGuestId(),
        description: description.trim(),
        category,
        cardId: cardId || undefined,
        atSeq,
      });
      setReportId(id);
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Meldung fehlgeschlagen.');
    } finally {
      setSending(false);
    }
  }

  // One entry per card number is enough.
  const uniqueCards = cards.filter(
    (card, index) => cards.findIndex((entry) => entry.cardId === card.cardId) === index,
  );

  return (
    <div className="bug-report" role="presentation" onMouseDown={onClose}>
      <section
        aria-label="Fehler melden"
        aria-modal="true"
        className="bug-report__dialog"
        onMouseDown={(event) => event.stopPropagation()}
        role="dialog"
      >
        <header className="bug-report__header">
          <h2>🐞 Fehler melden</h2>
          <button aria-label="Schließen" onClick={onClose} type="button">
            ✕
          </button>
        </header>

        {reportId !== null ? (
          <div className="bug-report__done" role="status">
            <p>Danke! Meldung #{reportId} gespeichert.</p>
            <button className="is-primary" onClick={onClose} type="button">
              Zurück zum Spiel
            </button>
          </div>
        ) : (
          <form onSubmit={(event) => void handleSubmit(event)}>
            <fieldset className="bug-report__categories">
              <legend>Was war falsch?</legend>
              {CATEGORIES.map((entry) => (
                <label key={entry.value} className={category === entry.value ? 'is-selected' : ''}>
                  <input
                    checked={category === entry.value}
                    name="bug-category"
                    onChange={() => setCategory(entry.value)}
                    type="radio"
                    value={entry.value}
                  />
                  {entry.label}
                </label>
              ))}
            </fieldset>

            <label className="bug-report__description">
              <span>
                Beschreibung <small>{description.length}/{MAX_LENGTH}</small>
              </span>
              <textarea
                maxLength={MAX_LENGTH}
                onChange={(event) => setDescription(event.target.value)}
                placeholder="Was ist passiert, und was hättest du erwartet?"
                required
                rows={4}
                value={description}
              />
            </label>

            {uniqueCards.length > 0 && (
              <div className="bug-report__cards">
                <p>Betroffene Karte (optional)</p>
                <div className="bug-report__card-list">
                  {uniqueCards.map((card) => (
                    <MatchCardView
                      key={card.instanceId}
                      ariaLabel={`${card.name} (${card.cardId}) ${cardId === card.cardId ? 'abwählen' : 'auswählen'}`}
                      card={{ ...card, isRested: false }}
                      clickable
                      onClick={() => setCardId((previous) => (previous === card.cardId ? '' : card.cardId))}
                      showStats={false}
                      size="slot"
                      stateClassName={cardId === card.cardId ? 'is-selected' : ''}
                    />
                  ))}
                </div>
              </div>
            )}

            <p className="bug-report__note">
              Der aktuelle Spielverlauf (Zug {atSeq}) wird automatisch mitgeschickt und nur auf unserem Server gespeichert.
            </p>
            {error && (
              <p className="bug-report__error" role="alert">
                {error}
              </p>
            )}

            <div className="bug-report__actions">
              <button onClick={onClose} type="button">
                Abbrechen
              </button>
              <button className="is-primary" disabled={sending || !description.trim()} type="submit">
                {sending ? 'Senden…' : 'Meldung senden'}
              </button>
            </div>
          </form>
        )}
      </section>
    </div>
  );
}
