import type { LeaderStatsDto, RecentMatchDto } from '../../api/statsApi';

const END_REASON_LABELS: Record<string, string> = {
  LIFE: 'Leben',
  DECK_OUT: 'Deck leer',
  CONCEDE: 'Aufgegeben',
  DISCONNECT: 'Verbindung',
};

export function LeaderThumb({ cardId, imageUrl }: { cardId: string; imageUrl?: string }) {
  return (
    <span className="profile-leader-thumb" title={cardId}>
      {imageUrl ? <img alt={cardId} loading="lazy" src={imageUrl} /> : <small>{cardId || '?'}</small>}
    </span>
  );
}

function formatDate(isoDate: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return date.toLocaleString(undefined, {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

interface RecentMatchListProps {
  matches: RecentMatchDto[];
  imagesById: Record<string, string>;
}

export function RecentMatchList({ imagesById, matches }: RecentMatchListProps) {
  return (
    <ul className="profile-recent-list">
      {matches.map((match) => (
        <li key={`${match.playedAtUtc}-${match.opponentName}`} className="ui-tile">
          <span className={`profile-result-badge ${match.won ? 'is-win' : 'is-loss'}`}>
            {match.won ? 'Sieg' : 'Niederlage'}
          </span>
          <span className="profile-recent-list__leaders">
            <LeaderThumb cardId={match.ownLeaderCardId} imageUrl={imagesById[match.ownLeaderCardId]} />
            <small>vs</small>
            <LeaderThumb cardId={match.opponentLeaderCardId} imageUrl={imagesById[match.opponentLeaderCardId]} />
          </span>
          <span className="profile-recent-list__info">
            <strong>{match.opponentName}</strong>
            <small>
              {formatDate(match.playedAtUtc)} · {match.turnCount} Züge ·{' '}
              {END_REASON_LABELS[match.endReason] ?? match.endReason}
            </small>
          </span>
        </li>
      ))}
    </ul>
  );
}

interface LeaderStatsListProps {
  leaders: LeaderStatsDto[];
  imagesById: Record<string, string>;
}

export function LeaderStatsList({ imagesById, leaders }: LeaderStatsListProps) {
  const maxMatches = Math.max(1, ...leaders.map((leader) => leader.matches));

  return (
    <ul className="profile-leader-stats">
      {leaders.map((leader) => {
        const winRate = Math.round((100 * leader.wins) / Math.max(1, leader.matches));

        return (
          <li key={leader.leaderCardId} className="ui-tile">
            <LeaderThumb cardId={leader.leaderCardId} imageUrl={imagesById[leader.leaderCardId]} />
            <span className="profile-leader-stats__body">
              <span className="profile-leader-stats__label">
                <strong>{leader.leaderCardId}</strong>
                <small>
                  {leader.matches} {leader.matches === 1 ? 'Match' : 'Matches'} · {winRate}% Winrate
                </small>
              </span>
              {/* Bar length = share of matches, gold part = wins */}
              <span
                className="profile-leader-stats__bar"
                style={{ width: `${(100 * leader.matches) / maxMatches}%` }}
              >
                <span style={{ width: `${winRate}%` }} />
              </span>
            </span>
          </li>
        );
      })}
    </ul>
  );
}
