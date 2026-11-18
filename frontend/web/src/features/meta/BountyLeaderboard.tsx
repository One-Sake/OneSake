import { useCallback, useEffect, useState } from 'react';

import { getGuestId } from '../../api/guestId';
import {
  formatBounty,
  getLeaderboard,
  getPlayerBounty,
  type LeaderboardEntryDto,
  type PlayerBountyDto,
} from '../../api/leaderboardApi';
import { useCardImages } from '../profile/useCardImages';

type LoadState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; entries: LeaderboardEntryDto[]; me: PlayerBountyDto | null };

const MEDALS = ['gold', 'silver', 'bronze'];

// OneSake's own ranking (our player data, clearly separated from the external meta data).
export function BountyLeaderboard() {
  const [state, setState] = useState<LoadState>({ status: 'loading' });

  const load = useCallback(() => {
    let isCancelled = false;
    setState({ status: 'loading' });

    Promise.all([getLeaderboard(100), getPlayerBounty(getGuestId()).catch(() => null)])
      .then(([entries, me]) => {
        if (!isCancelled) {
          setState({ status: 'ready', entries, me });
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setState({ status: 'error' });
        }
      });

    return () => {
      isCancelled = true;
    };
  }, []);

  useEffect(() => load(), [load]);

  const entries = state.status === 'ready' ? state.entries : [];
  const me = state.status === 'ready' ? state.me : null;
  const imagesById = useCardImages(entries.map((entry) => entry.favoriteLeaderCardId ?? ''));
  const myRank = me?.rank ?? null;
  const isMeInList = myRank !== null && myRank <= entries.length;

  return (
    <section className="ui-glass bounty-board" aria-labelledby="bounty-board-title">
      <div className="profile-card__header">
        <h2 className="ui-heading" id="bounty-board-title">
          OneSake-Rangliste
        </h2>
        <span className="ui-badge profile-header-badge">Top 100</span>
      </div>
      <p className="meta-muted">
        Kopfgeld aus unseren eigenen Ranked-Matches (Quick Match). Ab 5 Ranked-Matches bist du dabei.
      </p>

      {state.status === 'loading' && (
        <ul className="bounty-board__list" aria-busy="true">
          {Array.from({ length: 6 }, (_, index) => (
            <li key={index} className="ui-tile bounty-row meta-skeleton" />
          ))}
        </ul>
      )}

      {state.status === 'error' && (
        <div className="profile-state profile-state--error" role="alert">
          <p>Die Rangliste konnte nicht geladen werden.</p>
          <button className="ui-btn-secondary" onClick={load} type="button">
            Erneut versuchen
          </button>
        </div>
      )}

      {state.status === 'ready' && entries.length === 0 && (
        <p className="profile-state">Noch niemand hat 5 Ranked-Matches gespielt – sei der Erste!</p>
      )}

      {state.status === 'ready' && entries.length > 0 && (
        <ol className="bounty-board__list">
          {entries.map((entry) => {
            const isMe = entry.rank === myRank;
            const medal = MEDALS[entry.rank - 1];
            const leaderImage = entry.favoriteLeaderCardId ? imagesById[entry.favoriteLeaderCardId] : undefined;

            return (
              <li
                key={`${entry.rank}-${entry.displayName}`}
                className={['ui-tile', 'bounty-row', medal ? `is-${medal}` : '', isMe ? 'is-me' : '']
                  .filter(Boolean)
                  .join(' ')}
              >
                <span className="bounty-row__rank">#{entry.rank}</span>
                <span className="bounty-row__leader" aria-hidden="true">
                  {leaderImage ? <img alt="" loading="lazy" src={leaderImage} /> : null}
                </span>
                <span className="bounty-row__name">
                  <strong>
                    {entry.displayName}
                    {isMe && <em> (Du)</em>}
                  </strong>
                  <small>
                    {entry.wins}–{entry.losses} · {entry.winRate.toLocaleString('de-DE')} % Winrate
                  </small>
                </span>
                <span className="bounty-row__bounty">{formatBounty(entry.bounty)}</span>
              </li>
            );
          })}
        </ol>
      )}

      {state.status === 'ready' && me && !isMeInList && (
        <div className="bounty-board__me" role="status">
          Du: {myRank ? `#${myRank}` : 'noch kein Rang'} · {formatBounty(me.bounty)}
        </div>
      )}
    </section>
  );
}
