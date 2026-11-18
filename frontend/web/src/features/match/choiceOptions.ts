import type { ChoicePromptDto } from '../../types/realtime';

// These options need a card selection and can only be used on the board.
const CARD_OPTIONS = ['PLAY_CARD', 'ATTACH_DON', 'ATTACK', 'BLOCK', 'COUNTER', 'ACTIVATE'];

// Card effect questions (choose a target, pay a cost, activate a [Trigger]) - shown in EffectChoiceOverlay.
export const EFFECT_PROMPT_KINDS = ['EFFECT_CHOICE', 'TRIGGER_DECISION'];

// These prompts have their own overlay (dice roll, card effects) and never show as buttons here.
export const OVERLAY_PROMPT_KINDS = ['TURN_ORDER_DECISION', ...EFFECT_PROMPT_KINDS];

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
