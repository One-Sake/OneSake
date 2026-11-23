import { useEffect, useState } from 'react';
import type { CSSProperties } from 'react';
import { getDonCards } from '../../../api/cardsApi';

// One place for the card back and DON!! images (see src/assets/cards/README.md).
// The files are optional: import.meta.glob simply finds nothing when they are missing,
// so the project still builds and the CSS shows a neutral fallback instead.
// Vite turns found files into hashed asset URLs, so the browser loads and caches them once.
const files = import.meta.glob<string>('../../../assets/cards/*.webp', {
  eager: true,
  import: 'default',
  query: '?url',
});

function findArt(fileName: string): string | null {
  const key = Object.keys(files).find((path) => path.endsWith(`/${fileName}`));
  return key ? files[key] : null;
}

export const cardArt = {
  cardBack: findArt('card-back.webp'),
  donBack: findArt('don-back.webp'),
  donFront: findArt('don-front.webp'),
};

// CSS variables for match-board.css / match.css. Unset variables fall back to the neutral design.
export const cardArtStyle = {
  ...(cardArt.cardBack ? { '--art-card-back': `url("${cardArt.cardBack}")` } : null),
  ...(cardArt.donBack ? { '--art-don-back': `url("${cardArt.donBack}")` } : null),
  // The DON!! front image already shows the name, so the text label is hidden.
  ...(cardArt.donFront ? { '--art-don-front': `url("${cardArt.donFront}")`, '--art-don-front-label': '""' } : null),
} as CSSProperties;

const STANDARD_DON_NAME = 'DON!! Card';

let standardDonImage: string | null = null;
let standardDonRequest: Promise<string | null> | null = null;

// Loads the image of the standard DON!! card from the card API once per session.
// Resolves to null when the API has no DON!! card or is not reachable (the board then keeps its fallback).
function loadStandardDonImage(): Promise<string | null> {
  if (!standardDonRequest) {
    standardDonRequest = getDonCards()
      .then((cards) => {
        const standard = cards.find((card) => card.card_name === STANDARD_DON_NAME) ?? cards[0];
        standardDonImage = standard?.card_image || null;
        return standardDonImage;
      })
      .catch(() => {
        // Do not cache failures, so the next match can try again.
        standardDonRequest = null;
        return null;
      });
  }

  return standardDonRequest;
}

// cardArtStyle plus the standard DON!! card from the database as DON!! front.
// A local don-front.webp (see README) still has priority over the database image.
export function useCardArtStyle(): CSSProperties {
  const [donImage, setDonImage] = useState<string | null>(standardDonImage);

  useEffect(() => {
    if (cardArt.donFront || donImage) {
      return;
    }

    let active = true;
    void loadStandardDonImage().then((image) => {
      if (active && image) {
        setDonImage(image);
      }
    });

    return () => {
      active = false;
    };
  }, [donImage]);

  if (cardArt.donFront || !donImage) {
    return cardArtStyle;
  }

  return {
    ...cardArtStyle,
    '--art-don-front': `url(${JSON.stringify(donImage)})`,
    '--art-don-front-label': '""',
  } as CSSProperties;
}
