import type { Deck } from '../../../types/decks';

// Text format used by the OPTCG community, one card per line:
//   1xOP01-001   <- first line is the leader
//   4xOP01-016
// Pure functions only, so they are easy to test and reuse.

export interface DeckTextEntry {
  cardId: string;
  quantity: number;
  // 1-based line number of the first line that mentioned this card.
  line: number;
}

export interface DeckTextError {
  line: number;
  message: string;
}

export interface ParsedDeckText {
  entries: DeckTextEntry[];
  errors: DeckTextError[];
}

// "4xOP01-016", "4 x OP01-016", "4 OP01-016", "OP01-016" (quantity 1), optional text after the ID.
const LINE_PATTERN = /^(?:(\d+)\s*[x×*]?\s*)?([A-Z]+\d*-\d+[A-Z]?)(?:\s.*)?$/i;

function stripComment(line: string): string {
  const hashIndex = line.indexOf('#');
  const slashIndex = line.indexOf('//');
  const cutIndexes = [hashIndex, slashIndex].filter((index) => index >= 0);
  const cutIndex = cutIndexes.length > 0 ? Math.min(...cutIndexes) : -1;

  return (cutIndex >= 0 ? line.slice(0, cutIndex) : line).trim();
}

export function parseDeckText(text: string): ParsedDeckText {
  const entries: DeckTextEntry[] = [];
  const errors: DeckTextError[] = [];
  const lines = text.replace(/\r\n?/g, '\n').split('\n');

  lines.forEach((rawLine, index) => {
    const lineNumber = index + 1;
    const line = stripComment(rawLine);
    if (!line) {
      return;
    }

    const match = LINE_PATTERN.exec(line);
    if (!match) {
      errors.push({ line: lineNumber, message: `Zeile nicht erkannt: "${line}"` });
      return;
    }

    const quantity = match[1] === undefined ? 1 : Number(match[1]);
    const cardId = match[2].toUpperCase();

    if (!Number.isInteger(quantity) || quantity <= 0) {
      errors.push({ line: lineNumber, message: `Ungültige Anzahl für ${cardId}.` });
      return;
    }

    // The same card on several lines is added up.
    const existingEntry = entries.find((entry) => entry.cardId === cardId);
    if (existingEntry) {
      existingEntry.quantity += quantity;
    } else {
      entries.push({ cardId, quantity, line: lineNumber });
    }
  });

  return { entries, errors };
}

function compareByCostThenId(
  left: { cardId: string; cost?: number | null },
  right: { cardId: string; cost?: number | null },
): number {
  // Cards without a cost (e.g. stages without data) go last.
  const leftCost = typeof left.cost === 'number' ? left.cost : Number.MAX_SAFE_INTEGER;
  const rightCost = typeof right.cost === 'number' ? right.cost : Number.MAX_SAFE_INTEGER;

  if (leftCost !== rightCost) {
    return leftCost - rightCost;
  }

  return left.cardId.localeCompare(right.cardId);
}

export function deckToText(deck: Deck): string {
  const lines: string[] = [];

  if (deck.leaderCardId) {
    lines.push(`1x${deck.leaderCardId}`);
  }

  [...deck.cards]
    .filter((card) => card.quantity > 0)
    .sort(compareByCostThenId)
    .forEach((card) => lines.push(`${card.quantity}x${card.cardId}`));

  return lines.join('\n');
}
