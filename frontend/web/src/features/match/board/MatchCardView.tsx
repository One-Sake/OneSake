import type { CardInstanceDto } from '../../../types/realtime';

interface MatchCardViewProps {
  card: CardInstanceDto;
  size: 'slot' | 'hand' | 'inspect';
  ariaLabel?: string;
  clickable?: boolean;
  disabled?: boolean;
  // Extra state classes like "is-selected" or "is-targetable".
  stateClassName?: string;
  showStats?: boolean;
  onClick?: () => void;
}

function hasPower(card: CardInstanceDto): boolean {
  return card.category === 'Leader' || card.category === 'Character' || !card.category;
}

export function MatchCardView({
  ariaLabel,
  card,
  clickable = false,
  disabled = false,
  onClick,
  showStats = true,
  size,
  stateClassName = '',
}: MatchCardViewProps) {
  const className = [
    'match-card-view',
    `match-card-view--${size}`,
    card.imageUrl ? 'match-card-view--image' : '',
    card.isRested ? 'is-rested' : '',
    clickable ? 'match-card-view--clickable' : '',
    disabled ? 'match-card-view--disabled' : '',
    stateClassName,
  ]
    .filter(Boolean)
    .join(' ');

  const power = card.power ?? card.basePower ?? 0;
  const powerBoosted = power > (card.basePower ?? power);

  const content = (
    <>
      {card.imageUrl ? (
        <img
          alt=""
          className="match-card-view__image"
          draggable={false}
          loading="lazy"
          src={card.imageUrl}
        />
      ) : (
        <>
          <span className="match-card-view__frame" aria-hidden="true" />
          <span className="match-card-view__art" aria-hidden="true">
            <span>{card.cardId}</span>
          </span>
          <span className="match-card-view__body">
            <strong>{card.name}</strong>
            <small>{card.cardId}</small>
          </span>
        </>
      )}

      {showStats && size !== 'inspect' && (
        <span className="match-card-view__stats" aria-hidden="true">
          {size === 'hand' && typeof card.cost === 'number' && card.category !== 'Leader' && (
            <span className="match-card-stat match-card-stat--cost">{card.cost}</span>
          )}
          {hasPower(card) && power > 0 && (
            <span className={powerBoosted ? 'match-card-stat match-card-stat--power is-boosted' : 'match-card-stat match-card-stat--power'}>
              {power}
            </span>
          )}
          {size === 'hand' && Boolean(card.counter) && (
            <span className="match-card-stat match-card-stat--counter">+{card.counter}</span>
          )}
          {Boolean(card.attachedDon) && (
            <span className="match-card-stat match-card-stat--don">DON x{card.attachedDon}</span>
          )}
        </span>
      )}
    </>
  );

  const label = ariaLabel ?? `${card.name} (${card.cardId})`;

  if (clickable) {
    return (
      <button
        aria-label={label}
        className={className}
        disabled={disabled}
        onClick={onClick}
        title={label}
        type="button"
      >
        {content}
      </button>
    );
  }

  return (
    <span className={className} title={label}>
      {content}
    </span>
  );
}
