import type { ChoicePromptDto } from '../../types/realtime';

// These options need a card selection and can only be used on the board.
const CARD_OPTIONS = ['PLAY_CARD', 'ATTACH_DON', 'ATTACK', 'BLOCK', 'COUNTER'];

// These prompts have their own overlay (dice roll for the first player) and never show as buttons here.
export const OVERLAY_PROMPT_KINDS = ['TURN_ORDER_DECISION'];

// The prompt options that can be answered with a simple button.
export function getVisibleChoiceOptions(
  currentPrompt: ChoicePromptDto | null,
  boardHandledKinds: string[] = [],
): string[] {
  const isHandledElsewhere = Boolean(
    currentPrompt &&
      (boardHandledKinds.includes(currentPrompt.kind) || OVERLAY_PROMPT_KINDS.includes(currentPrompt.kind)),
  );
  return isHandledElsewhere
    ? []
    : (currentPrompt?.options ?? []).filter((option) => !CARD_OPTIONS.includes(option));
}
