import { useEffect, useMemo, useState } from 'react';

import type { CardDto } from '../../types/cards';
import { loadCardById } from '../deckbuilder/utils/deckCards';

// Loads our own card data (image, color) for leader card IDs. Only used for pictures and
// the accent color - every meta number comes from the external provider.
export function useMetaCards(cardIds: (string | null | undefined)[]): Record<string, CardDto> {
  const [cardsById, setCardsById] = useState<Record<string, CardDto>>({});
  const idsKey = useMemo(
    () => [...new Set(cardIds.filter((id): id is string => Boolean(id)))].sort().join(','),
    [cardIds],
  );

  useEffect(() => {
    let isCancelled = false;

    (idsKey ? idsKey.split(',') : []).forEach((cardId) => {
      loadCardById(cardId)
        .then((card) => {
          if (!isCancelled && card) {
            setCardsById((current) => (current[cardId] ? current : { ...current, [cardId]: card }));
          }
        })
        .catch(() => {
          // Without card data the leader is shown without a picture.
        });
    });

    return () => {
      isCancelled = true;
    };
  }, [idsKey]);

  return cardsById;
}
