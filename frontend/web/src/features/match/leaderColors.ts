import { getCardColors } from '../deckbuilder/utils/deckValidation';

// Glow colors for the One Piece TCG card colors. Tuned to look good on the dark navy lobby.
const LEADER_COLOR_HEX: Record<string, string> = {
  Red: '#e2483d',
  Green: '#2fb36b',
  Blue: '#3b82d6',
  Purple: '#9b5de5',
  Black: '#8a9aa8',
  Yellow: '#f0bf38',
};

const FALLBACK_COLOR = '#e3bd68';

export interface LeaderColorPair {
  primary: string;
  secondary: string;
}

// Accepts the card_color text ("Red", "Green/Purple", ...) or a list of color names.
export function getLeaderColorNames(cardColor: string | string[] | undefined): string[] {
  return getCardColors(cardColor).filter((color) => color in LEADER_COLOR_HEX);
}

export function getLeaderColorHex(colorName: string): string {
  return LEADER_COLOR_HEX[colorName] ?? FALLBACK_COLOR;
}

export function getLeaderColors(cardColor: string | string[] | undefined): LeaderColorPair {
  const [first, second] = getLeaderColorNames(cardColor);
  const primary = first ? getLeaderColorHex(first) : FALLBACK_COLOR;

  return {
    primary,
    secondary: second ? getLeaderColorHex(second) : primary,
  };
}
