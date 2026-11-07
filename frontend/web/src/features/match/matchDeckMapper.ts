import { getCardsById } from '../../api/cardsApi';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import type { PlayerDeckSubmissionDto } from '../../types/realtime';

// The API can return numbers as strings ("5000") or null.
function toNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

// One card number can have several prints (normal, parallel...). Prefer the normal print.
function pickBasePrint(cardId: string, prints: CardDto[]): CardDto | null {
  return prints.find((print) => print.card_image_id === cardId) ?? prints[0] ?? null;
}

// Loads card details for every unique card id. Missing cards are simply left out.
async function loadCardDetails(cardIds: string[]): Promise<Map<string, CardDto>> {
  const uniqueIds = [...new Set(cardIds.filter(Boolean))];
  const details = new Map<string, CardDto>();

  await Promise.all(
    uniqueIds.map(async (cardId) => {
      try {
        const card = pickBasePrint(cardId, await getCardsById(cardId));
        if (card) {
          details.set(cardId, card);
        }
      } catch (error) {
        console.warn(`[OneSake] Card details for ${cardId} could not be loaded`, error);
      }
    }),
  );

  return details;
}

// Builds the deck that is sent to the match server. The server needs the card stats
// (category, cost, power, counter, keywords in the text) to run the game rules.
export async function toPlayerDeckSubmission(deck: Deck): Promise<PlayerDeckSubmissionDto> {
  const details = await loadCardDetails([deck.leaderCardId, ...deck.cards.map((card) => card.cardId)]);
  const leader = details.get(deck.leaderCardId);

  return {
    deckId: deck.id,
    deckName: deck.name,
    leaderCardId: deck.leaderCardId,
    leaderName: leader?.card_name ?? deck.leaderName ?? deck.leaderCardId,
    leaderColor: leader?.card_color ?? deck.leaderColors?.join(' ') ?? '',
    leaderPower: toNumber(leader?.card_power),
    leaderLife: toNumber(leader?.life),
    leaderText: leader?.card_text ?? '',
    leaderImageUrl: leader?.card_image ?? '',
    cards: deck.cards.map((card) => {
      const detail = details.get(card.cardId);

      return {
        cardId: card.cardId,
        name: detail?.card_name ?? card.name,
        quantity: card.quantity,
        category: detail?.card_type ?? card.type ?? 'Character',
        color: detail?.card_color ?? card.color ?? '',
        cost: toNumber(detail?.card_cost ?? card.cost),
        power: toNumber(detail?.card_power ?? card.power),
        counter: toNumber(detail?.counter_amount ?? card.counter),
        text: detail?.card_text ?? '',
        imageUrl: detail?.card_image ?? '',
      };
    }),
  };
}
