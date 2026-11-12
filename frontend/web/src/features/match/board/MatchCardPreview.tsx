import type { CardInstanceDto } from '../../../types/realtime';
import { MatchCardView } from './MatchCardView';

interface MatchCardPreviewProps {
  card: CardInstanceDto | null;
}

// Large hover preview: only the card image, like the deckbuilder preview.
export function MatchCardPreview({ card }: MatchCardPreviewProps) {
  return (
    <aside className="match-card-preview" aria-hidden="true">
      {card &&
        (card.imageUrl ? (
          <img key={card.instanceId} alt="" draggable={false} src={card.imageUrl} />
        ) : (
          <MatchCardView card={{ ...card, isRested: false }} showStats={false} size="inspect" />
        ))}
    </aside>
  );
}
