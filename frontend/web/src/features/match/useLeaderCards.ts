import { useEffect, useMemo, useState } from 'react';

import { getCardsById } from '../../api/cardsApi';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';

type DeckWithLeaderImage = Deck & {
  leaderCard?: {
    card_image?: string;
    cardImage?: string;
    image?: string;
  };
  leaderCardImage?: string;
  leaderImage?: string;
};

// Image of the loaded leader card, or an image stored on the deck itself (older deck formats).
export function getLeaderImage(deck: Deck, leaderCard?: CardDto | null): string {
  const enrichedDeck = deck as DeckWithLeaderImage;

  return (
    leaderCard?.card_image ||
    enrichedDeck.leaderImage ||
    enrichedDeck.leaderCardImage ||
    enrichedDeck.leaderCard?.card_image ||
    enrichedDeck.leaderCard?.cardImage ||
    enrichedDeck.leaderCard?.image ||
    ''
  );
}

// Loads the leader card (image, color, ...) for every deck, keyed by leaderCardId.
export function useLeaderCards(decks: Deck[]): Record<string, CardDto | null> {
  const [leaderCardsById, setLeaderCardsById] = useState<Record<string, CardDto | null>>({});
  const leaderCardIds = useMemo(
    () => [...new Set(decks.map((deck) => deck.leaderCardId).filter(Boolean))],
    [decks],
  );

  useEffect(() => {
    let isCancelled = false;

    async function loadLeaderCards(): Promise<void> {
      const entries = await Promise.all(
        leaderCardIds.map(async (leaderCardId) => {
          try {
            const cards = await getCardsById(leaderCardId);
            const leaderCard =
              cards.find((card) => card.card_set_id === leaderCardId) ?? cards[0] ?? null;

            return [leaderCardId, leaderCard] as const;
          } catch {
            return [leaderCardId, null] as const;
          }
        }),
      );

      if (!isCancelled) {
        setLeaderCardsById(Object.fromEntries(entries));
      }
    }

    void loadLeaderCards();

    return () => {
      isCancelled = true;
    };
  }, [leaderCardIds]);

  return leaderCardsById;
}
