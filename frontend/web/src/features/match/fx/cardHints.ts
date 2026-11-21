import type { CardInstanceDto } from '../../../types/realtime';

export interface CardHint {
  icon: string;
  label: string;
}

// Short hints for an opponent card, read from its keywords and text (shown on hover).
const HINTS: { test: RegExp; keyword?: string; icon: string; label: string }[] = [
  { test: /\[Blocker\]/i, keyword: 'Blocker', icon: '🛡', label: 'Hat [Blocker]' },
  { test: /\[Rush\]/i, keyword: 'Rush', icon: '⚡', label: '[Rush]' },
  { test: /\[Double Attack\]/i, keyword: 'Double Attack', icon: '⚔', label: '[Double Attack]' },
  { test: /\[Banish\]/i, keyword: 'Banish', icon: '✖', label: '[Banish]' },
  { test: /\[On K\.?O\.?\]/i, icon: '💀', label: '[On K.O.]' },
  { test: /\[When Attacking\]/i, icon: '➹', label: '[When Attacking]' },
  { test: /\[On Block\]/i, icon: '◈', label: '[On Block]' },
  { test: /\[Trigger\]/i, icon: '✦', label: '[Trigger]' },
];

export function getCardHints(card: CardInstanceDto): CardHint[] {
  const text = card.text ?? '';
  return HINTS.filter((hint) => (hint.keyword && card.keywords?.includes(hint.keyword)) || hint.test.test(text)).map(
    ({ icon, label }) => ({ icon, label }),
  );
}
