import type { ChoicePromptDto } from '../../types/realtime';
import { getVisibleChoiceOptions } from './choiceOptions';

interface ChoiceBarProps {
  currentPrompt: ChoicePromptDto | null;
  pending: boolean;
  canSubmitChoice: boolean;
  // Prompt kinds that are answered directly on the board and are hidden here.
  boardHandledKinds?: string[];
  onSubmitChoice: (option: string) => void;
}

// "NO_BLOCK" -> "No Block"
function formatOption(option: string): string {
  return option
    .toLowerCase()
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

// Small prompt pill on the board midline (e.g. Keep / Mulligan). It never covers the hand.
export function ChoiceBar({
  boardHandledKinds = [],
  canSubmitChoice,
  currentPrompt,
  onSubmitChoice,
  pending,
}: ChoiceBarProps) {
  const visibleOptions = getVisibleChoiceOptions(currentPrompt, boardHandledKinds);

  if (!currentPrompt || visibleOptions.length === 0) {
    return null;
  }

  return (
    <div className="match-choice-bar" role="group" aria-label={currentPrompt.title}>
      <span className="match-choice-bar__title">{currentPrompt.title}</span>
      {visibleOptions.map((option, index) => (
        <button
          key={option}
          className={index === 0 ? 'match-choice-bar__button is-primary' : 'match-choice-bar__button'}
          disabled={pending || !canSubmitChoice}
          onClick={() => onSubmitChoice(option)}
          type="button"
        >
          {formatOption(option)}
        </button>
      ))}
    </div>
  );
}
