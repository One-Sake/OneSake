import type { CardDto } from '../types/cards';
import { getBackendBaseUrl } from './backendUrl';

function buildCardsUrl(path: string): string {
  return `${getBackendBaseUrl()}/Cards/${path}`;
}

async function fetchCards(path: string): Promise<CardDto[]> {
  const response = await fetch(buildCardsUrl(path));

  if (!response.ok) {
    throw new Error(`Cards request failed (${response.status}).`);
  }

  return (await response.json()) as CardDto[];
}

export async function getCardsById(cardId: string): Promise<CardDto[]> {
  return fetchCards(encodeURIComponent(cardId.trim()));
}

export async function getCardsBySetId(setId: string): Promise<CardDto[]> {
  return fetchCards(`set/${encodeURIComponent(setId.trim())}`);
}

// DON!! cards from the database (GET /cards/don). The standard "DON!! Card" comes first.
export async function getDonCards(): Promise<CardDto[]> {
  return fetchCards('don');
}
