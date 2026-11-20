import { useEffect, useState } from 'react';
import type { DragEvent } from 'react';

import type { ChoicePromptDto, TriggerOptionDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

interface TriggerOrderOverlayProps {
  prompt: ChoicePromptDto | null;
  disabled: boolean;
  onSubmit: (orderedIds: string[]) => void;
}

// Several effects of one player happen at the same time: the player chooses the order
// (first = resolved first). Sort by drag & drop or with the arrow buttons.
export function TriggerOrderOverlay({ disabled, onSubmit, prompt }: TriggerOrderOverlayProps) {
  const [order, setOrder] = useState<TriggerOptionDto[]>([]);
  const [dragId, setDragId] = useState('');

  useEffect(() => {
    setOrder(prompt?.triggers ?? []);
  }, [prompt?.choiceId, prompt?.triggers]);

  if (!prompt || prompt.kind !== 'ORDER_TRIGGERS') {
    return null;
  }

  const cards = prompt.cards ?? [];

  function move(index: number, offset: number): void {
    setOrder((previous) => {
      const target = index + offset;
      if (target < 0 || target >= previous.length) {
        return previous;
      }

      const next = [...previous];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  function handleDrop(event: DragEvent, beforeId: string): void {
    event.preventDefault();
    const id = event.dataTransfer.getData('text/plain') || dragId;
    setDragId('');
    if (!id || id === beforeId) {
      return;
    }

    setOrder((previous) => {
      const dragged = previous.find((entry) => entry.id === id);
      if (!dragged) {
        return previous;
      }

      const rest = previous.filter((entry) => entry.id !== id);
      const index = rest.findIndex((entry) => entry.id === beforeId);
      rest.splice(index < 0 ? rest.length : index, 0, dragged);
      return rest;
    });
  }

  return (
    <div className="effect-choice-overlay" role="presentation">
      <section aria-label="Reihenfolge der Effekte" aria-modal="true" className="effect-choice-overlay__dialog" role="dialog">
        <header className="effect-choice-overlay__header">
          <span>Gleichzeitige Effekte</span>
          <h2>Reihenfolge wählen</h2>
        </header>
        <p className="effect-choice-overlay__title">{prompt.title}</p>

        <ol className="trigger-order">
          {order.map((trigger, index) => {
            const card = cards.find((entry) => entry.instanceId === trigger.cardInstanceId);
            return (
              <li
                key={trigger.id}
                className={`trigger-order__item${dragId === trigger.id ? ' is-dragging' : ''}`}
                draggable={!disabled}
                onDragEnd={() => setDragId('')}
                onDragOver={(event) => event.preventDefault()}
                onDragStart={(event) => {
                  event.dataTransfer.setData('text/plain', trigger.id);
                  setDragId(trigger.id);
                }}
                onDrop={(event) => handleDrop(event, trigger.id)}
              >
                <span className="trigger-order__number">{index + 1}</span>
                {card && (
                  <span className="trigger-order__card">
                    <MatchCardView card={{ ...card, isRested: false }} showStats={false} size="slot" />
                  </span>
                )}
                <span className="trigger-order__text">
                  <strong>{trigger.cardName}</strong>
                  <small>{trigger.text}</small>
                </span>
                <span className="trigger-order__buttons">
                  <button
                    aria-label={`${trigger.cardName} nach oben`}
                    disabled={disabled || index === 0}
                    onClick={() => move(index, -1)}
                    type="button"
                  >
                    ▲
                  </button>
                  <button
                    aria-label={`${trigger.cardName} nach unten`}
                    disabled={disabled || index === order.length - 1}
                    onClick={() => move(index, 1)}
                    type="button"
                  >
                    ▼
                  </button>
                </span>
              </li>
            );
          })}
        </ol>

        <div className="effect-choice-overlay__actions">
          <button
            className="is-primary"
            disabled={disabled || order.length === 0}
            onClick={() => onSubmit(order.map((trigger) => trigger.id))}
            type="button"
          >
            Bestätigen
          </button>
        </div>
      </section>
    </div>
  );
}
