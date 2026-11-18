import { useEffect, useMemo, useState } from 'react';

import { loadCardById } from '../deckbuilder/utils/deckCards';

// Loads leader images for a list of card IDs (cached by loadCardById).
export function useCardImages(cardIds: string[]): Record<string, string> {
  const [imagesById, setImagesById] = useState<Record<string, string>>({});
  const idsKey = useMemo(() => [...new Set(cardIds.filter(Boolean))].sort().join(','), [cardIds]);

  useEffect(() => {
    let isCancelled = false;

    (idsKey ? idsKey.split(',') : []).forEach((cardId) => {
      loadCardById(cardId)
        .then((card) => {
          if (!isCancelled && card?.card_image) {
            setImagesById((current) => ({ ...current, [cardId]: card.card_image }));
          }
        })
        .catch(() => {
          // A missing thumbnail only shows the card ID instead.
        });
    });

    return () => {
      isCancelled = true;
    };
  }, [idsKey]);

  return imagesById;
}
