import { BOUNTY_PER_POINT, formatBounty, type PlayerBountyDto, type RatingPointDto } from '../../api/leaderboardApi';

interface RatingCurveProps {
  points: RatingPointDto[];
}

// Small rating curve as inline SVG (no chart library).
export function RatingCurve({ points }: RatingCurveProps) {
  if (points.length < 2) {
    return null;
  }

  const width = 240;
  const height = 64;
  const padding = 4;
  const ratings = points.map((point) => point.rating);
  const min = Math.min(...ratings);
  const max = Math.max(...ratings);
  const span = Math.max(1, max - min);
  const coordinates = points.map((point, index) => {
    const x = padding + (index / (points.length - 1)) * (width - padding * 2);
    const y = padding + (1 - (point.rating - min) / span) * (height - padding * 2);
    return [x, y] as const;
  });
  const line = coordinates.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
  const area = `${padding},${height - padding} ${line} ${width - padding},${height - padding}`;
  const [lastX, lastY] = coordinates[coordinates.length - 1];

  return (
    <svg
      aria-label={`Rating-Verlauf der letzten ${points.length} Ranked-Matches, von ${min} bis ${max}`}
      className="bounty-curve"
      role="img"
      viewBox={`0 0 ${width} ${height}`}
    >
      <polygon className="bounty-curve__area" points={area} />
      <polyline className="bounty-curve__line" points={line} />
      <circle className="bounty-curve__dot" cx={lastX} cy={lastY} r="3.5" />
    </svg>
  );
}

interface BountyPosterProps {
  bounty: PlayerBountyDto | null;
  status: 'loading' | 'error' | 'ready';
  displayName: string;
  leaderImageUrl?: string;
  onOpenMatch?: () => void;
  onRetry: () => void;
}

// "Steckbrief" in the style of a WANTED poster.
export function BountyPoster({ bounty, displayName, leaderImageUrl, onOpenMatch, onRetry, status }: BountyPosterProps) {
  const rankedGames = bounty ? bounty.wins + bounty.losses : 0;

  return (
    <section className="ui-glass bounty-section" aria-labelledby="bounty-title">
      <div className="profile-card__header">
        <h2 className="ui-heading" id="bounty-title">
          Steckbrief
        </h2>
        <span className="ui-badge profile-header-badge">Ranked</span>
      </div>

      <div className="bounty-layout">
        <article className="wanted-poster" aria-label={`Steckbrief von ${displayName}`}>
          <span className="wanted-poster__title">Wanted</span>
          <div className="wanted-poster__portrait">
            {leaderImageUrl ? (
              <img alt="" src={leaderImageUrl} />
            ) : (
              <span aria-hidden="true">{displayName.slice(0, 1).toUpperCase()}</span>
            )}
          </div>
          <span className="wanted-poster__subtitle">Dead or Alive</span>
          <strong className="wanted-poster__name">{bounty?.displayName || displayName}</strong>
          <strong className="wanted-poster__bounty">
            {status === 'loading' ? '฿ …' : formatBounty(bounty?.bounty ?? 1_000_000)}
          </strong>
          <span className="wanted-poster__marine">Marine</span>
        </article>

        <div className="bounty-details">
          {status === 'error' && (
            <div className="profile-state profile-state--error" role="alert">
              <p>Das Kopfgeld konnte nicht geladen werden.</p>
              <button className="ui-btn-secondary" onClick={onRetry} type="button">
                Erneut versuchen
              </button>
            </div>
          )}

          {status === 'ready' && bounty && rankedGames === 0 && (
            <div className="profile-state">
              <p>
                Noch kein Kopfgeld-Match gespielt. Jeder Pirat startet mit {formatBounty(1_000_000)} – spiel ein
                Quick Match (Ranked), um dein Kopfgeld zu steigern!
              </p>
              {onOpenMatch && (
                <button className="ui-btn-primary ui-btn-primary--compact" onClick={onOpenMatch} type="button">
                  <span>Zum Quick Match</span>
                </button>
              )}
            </div>
          )}

          {status === 'ready' && bounty && rankedGames > 0 && (
            <>
              <div className="bounty-stats">
                <div className="ui-stat ui-stat--accent">
                  <span>Rang</span>
                  <strong>{bounty.rank ? `#${bounty.rank}` : '–'}</strong>
                </div>
                <div className="ui-stat">
                  <span>Peak</span>
                  <strong>{formatBounty(bounty.peak * BOUNTY_PER_POINT)}</strong>
                </div>
                <div className="ui-stat">
                  <span>Ranked</span>
                  <strong>
                    {bounty.wins}–{bounty.losses}
                  </strong>
                </div>
              </div>
              {!bounty.rank && (
                <p className="bounty-hint">Ab 5 Ranked-Matches erscheinst du in der OneSake-Rangliste.</p>
              )}
              <RatingCurve points={bounty.ratingHistory} />
            </>
          )}
        </div>
      </div>
    </section>
  );
}
