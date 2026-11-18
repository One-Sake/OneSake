import { useRef } from 'react';
import type { PointerEvent } from 'react';

const MAX_TILT_DEGREES = 8;

interface LeaderHeroProps {
  imageUrl: string;
  leaderName: string;
}

// Big leader card with a 3D tilt that follows the mouse and a glare sweep.
export function LeaderHero({ imageUrl, leaderName }: LeaderHeroProps) {
  const cardRef = useRef<HTMLDivElement>(null);

  // CSS variables are set directly on the element, so moving the mouse does not re-render React.
  const handlePointerMove = (event: PointerEvent<HTMLDivElement>): void => {
    const card = cardRef.current;
    if (!card || event.pointerType !== 'mouse') {
      return;
    }

    const bounds = card.getBoundingClientRect();
    const x = (event.clientX - bounds.left) / bounds.width - 0.5;
    const y = (event.clientY - bounds.top) / bounds.height - 0.5;

    card.style.setProperty('--tilt-x', `${(-y * MAX_TILT_DEGREES * 2).toFixed(2)}deg`);
    card.style.setProperty('--tilt-y', `${(x * MAX_TILT_DEGREES * 2).toFixed(2)}deg`);
    card.style.setProperty('--glare-x', `${((x + 0.5) * 100).toFixed(1)}%`);
  };

  const handlePointerLeave = (): void => {
    const card = cardRef.current;
    card?.style.removeProperty('--tilt-x');
    card?.style.removeProperty('--tilt-y');
    card?.style.removeProperty('--glare-x');
  };

  return (
    <div className="leader-hero">
      <div className="leader-hero__aura" aria-hidden="true" />
      <div
        ref={cardRef}
        className="leader-hero__card"
        onPointerLeave={handlePointerLeave}
        onPointerMove={handlePointerMove}
      >
        {imageUrl ? (
          <img alt={leaderName} draggable={false} src={imageUrl} />
        ) : (
          <div className="leader-hero__placeholder">
            <span>Leader</span>
            <strong>{leaderName}</strong>
          </div>
        )}
        <span className="leader-hero__glare" aria-hidden="true" />
      </div>
    </div>
  );
}
