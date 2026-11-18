import type { ChoicePromptDto } from '../../../types/realtime';
import { EFFECT_PROMPT_KINDS } from '../choiceOptions';
import { MatchCardView } from './MatchCardView';

const OPTION_LABELS: Record<string, string> = {
  SELECT: 'Select',
  DONE: 'Done',
  YES: 'Yes',
  NO: 'No',
  ACTIVATE: 'Activate [Trigger]',
  DECLINE: 'Add to hand',
};

interface EffectChoiceOverlayProps {
  prompt: ChoicePromptDto | null;
  disabled: boolean;
  onSubmit: (option: string, selectedCardInstanceId?: string) => void;
}

// Shows a question from a card effect: "choose a target", "pay the cost?", "activate the [Trigger]?".
// Cards with "SELECT" are clicked directly, all other options are buttons.
export function EffectChoiceOverlay({ disabled, onSubmit, prompt }: EffectChoiceOverlayProps) {
  if (!prompt || !EFFECT_PROMPT_KINDS.includes(prompt.kind)) {
    return null;
  }

  const cards = prompt.cards ?? [];
  const canSelectCards = prompt.options.includes('SELECT');
  const buttons = prompt.options.filter((option) => option !== 'SELECT');

  return (
    <div className="effect-choice-overlay" role="presentation">
      <section aria-label={prompt.title} aria-modal="true" className="effect-choice-overlay__dialog" role="dialog">
        <header className="effect-choice-overlay__header">
          <span>{prompt.kind === 'TRIGGER_DECISION' ? 'Trigger' : 'Card effect'}</span>
          <h2>{prompt.sourceName || 'Effect'}</h2>
        </header>

        {prompt.effectText && <p className="effect-choice-overlay__text">{prompt.effectText}</p>}
        <p className="effect-choice-overlay__title">{prompt.title}</p>

        {cards.length > 0 && (
          <div className="effect-choice-overlay__cards">
            {cards.map((card) => (
              <MatchCardView
                key={card.instanceId}
                ariaLabel={canSelectCards ? `Select ${card.name}` : card.name}
                card={card}
                clickable={canSelectCards && !disabled}
                onClick={() => canSelectCards && !disabled && onSubmit('SELECT', card.instanceId)}
                size="slot"
                stateClassName={canSelectCards ? 'is-targetable' : ''}
              />
            ))}
          </div>
        )}

        {buttons.length > 0 && (
          <div className="effect-choice-overlay__actions">
            {buttons.map((option, index) => (
              <button
                key={option}
                className={index === 0 ? 'is-primary' : ''}
                disabled={disabled}
                onClick={() => onSubmit(option)}
                type="button"
              >
                {OPTION_LABELS[option] ?? option}
              </button>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
