# Card art for the match board

The match board shows these images for card backs and DON!! cards.
They are loaded by `src/features/match/board/cardArt.ts`.

| File             | Used for                                                        |
| ---------------- | --------------------------------------------------------------- |
| `card-back.webp` | Back of main deck cards (Deck, Life, opponent hand)              |
| `don-back.webp`  | Back of DON!! cards (DON!! deck)                                 |
| `don-front.webp` | Front of DON!! cards (cost area, active and rested)              |

## Format

- WebP, about 400 x 560 px (aspect ratio 63:88, like a real card)
- Under 80 KB per file
- No rounded corners or borders in the image itself; the board adds them

## Rights

The files are added by the team, **only with cleared rights** for the artwork.
Do not download or recreate official artwork for this folder.

While a file is missing, the board shows a neutral OneSake fallback
(navy card back with the OneSake name), and the build still works.
Exception: without `don-front.webp`, the DON!! front uses the image of the standard DON!! card
from the card API (`GET /cards/don`); only if that is not reachable does the fallback stay.
