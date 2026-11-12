import type { ChoicePromptDto } from '../../types/realtime';
import { Button } from '../../components/ui/Button';
import { BottomSheet } from '../../components/ui/BottomSheet';
import { getVisibleChoiceOptions } from './choiceOptions';

interface ChoiceSheetProps {
  currentPrompt: ChoicePromptDto | null;
  pending: boolean;
  canSubmitChoice: boolean;
  // Prompt kinds that are handled somewhere else (e.g. on the match board) and are hidden here.
  boardHandledKinds?: string[];
  onSubmitChoice: (option: string, selectedCardInstanceId?: string) => void;
}

export function ChoiceSheet({
  boardHandledKinds = [],
  canSubmitChoice,
  currentPrompt,
  onSubmitChoice,
  pending,
}: ChoiceSheetProps) {
  const visibleOptions = getVisibleChoiceOptions(currentPrompt, boardHandledKinds);

  return (
    <BottomSheet
      open={Boolean(currentPrompt) && visibleOptions.length > 0}
      title={currentPrompt?.title ?? 'Choice'}
    >
      {currentPrompt && (
        <div className="choice-content">
          <div className="button-row">
            {visibleOptions.map((option) => (
              <Button
                key={option}
                className="choice-button"
                disabled={pending || !canSubmitChoice}
                onClick={() => onSubmitChoice(option)}
                variant="secondary"
              >
                {option}
              </Button>
            ))}
          </div>
        </div>
      )}
    </BottomSheet>
  );
}
