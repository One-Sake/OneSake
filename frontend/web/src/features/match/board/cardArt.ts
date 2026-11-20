import type { CSSProperties } from 'react';

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
