import type { BoardZoneDefinition } from './boardTypes';

export const CHARACTER_SLOT_COUNT = 5;

// The zones of one player half. Their position is set by the CSS grid in
// match-board.css (grid-template-areas), which also mirrors the opponent half.
export const boardZones: BoardZoneDefinition[] = [
  { kind: 'character', label: 'Character' },
  { kind: 'life', label: 'Life' },
  { kind: 'leader', label: 'Leader' },
  { kind: 'stage', label: 'Stage' },
  { kind: 'donDeck', label: 'DON!! Deck' },
  { kind: 'donArea', label: 'Cost' },
  { kind: 'deck', label: 'Deck' },
  { kind: 'trash', label: 'Trash' },
];
