import { getCardsById } from '../../../api/cardsApi';
import type { CardDto } from '../../../types/cards';
import type { DeckCard } from '../../../types/decks';

function optionalCardText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value : undefined;
}

// Converts a card from the card API into the DeckCard stored in a deck.
export function createDeckCard(card: CardDto, quantity: number): DeckCard {
  return {
    cardId: card.card_set_id,
    name: card.card_name,
    quantity,
    color: optionalCardText(card.card_color),
    type: optionalCardText(card.card_type),
    cost: card.card_cost,
    power: card.card_power,
    counter: card.counter_amount,
    attribute: optionalCardText(card.attribute),
    subTypes: optionalCardText(card.sub_types),
    rarity: optionalCardText(card.rarity),
  };
}

// Cache per card ID, so the same card is only requested once per session.
const cardRequestsById = new Map<string, Promise<CardDto | null>>();

// Loads one card by ID (GET /cards/{id}). Resolves to null for unknown IDs.
// The API also returns parallel/alt-art versions, so the normal print is preferred.
export function loadCardById(cardId: string): Promise<CardDto | null> {
  const normalizedCardId = cardId.trim().toUpperCase();
  const cachedRequest = cardRequestsById.get(normalizedCardId);
  if (cachedRequest) {
    return cachedRequest;
  }

  const request = getCardsById(normalizedCardId)
    .then((cards) => {
      const exactCards = cards.filter(
        (card) => card.card_set_id.toUpperCase() === normalizedCardId,
      );
      return (
        exactCards.find((card) => !card.card_name.toLowerCase().includes('parallel')) ??
        exactCards[0] ??
        null
      );
    })
    .catch((error: unknown) => {
      // The API answers unknown IDs with 404.
      if (error instanceof Error && error.message.includes('(404)')) {
        return null;
      }

      // Do not cache other failures, so a retry can work later.
      cardRequestsById.delete(normalizedCardId);
      throw error;
    });

  cardRequestsById.set(normalizedCardId, request);
  return request;
}
