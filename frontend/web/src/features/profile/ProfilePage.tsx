import { useCallback, useEffect, useState } from 'react';

import { getGuestId } from '../../api/guestId';
import { getPlayerStats, type PlayerStatsDto } from '../../api/statsApi';
import { loadStoredDecks } from '../deckbuilder/utils/deckStorage';
import { LeaderStatsList, LeaderThumb, RecentMatchList } from './ProfileMatchStats';
import { useCardImages } from './useCardImages';

const GUEST_NAME_STORAGE_KEY = 'onesake.profile.guestName';
const DEFAULT_GUEST_NAME = 'Guest Captain';

function loadGuestName(): string {
  if (typeof window === 'undefined') {
    return DEFAULT_GUEST_NAME;
  }

  try {
    return window.localStorage.getItem(GUEST_NAME_STORAGE_KEY) || DEFAULT_GUEST_NAME;
  } catch {
    return DEFAULT_GUEST_NAME;
  }
}

type StatsState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; stats: PlayerStatsDto };

interface ProfilePageProps {
  onOpenMatch?: () => void;
}

export function ProfilePage({ onOpenMatch }: ProfilePageProps) {
  const [deckCount] = useState(() => loadStoredDecks().length);
  const [statsState, setStatsState] = useState<StatsState>({ status: 'loading' });

  const [guestName, setGuestName] = useState(loadGuestName);
  const [draftName, setDraftName] = useState(loadGuestName);
  const [isGuestActive, setIsGuestActive] = useState(() => loadGuestName() !== DEFAULT_GUEST_NAME);
  const [isEditingName, setIsEditingName] = useState(false);

  const displayName = guestName.trim() || DEFAULT_GUEST_NAME;

  const loadStats = useCallback(() => {
    let isCancelled = false;
    setStatsState({ status: 'loading' });

    getPlayerStats(getGuestId())
      .then((stats) => {
        if (!isCancelled) {
          setStatsState({ status: 'ready', stats });
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setStatsState({ status: 'error' });
        }
      });

    return () => {
      isCancelled = true;
    };
  }, []);

  useEffect(() => loadStats(), [loadStats]);

  const stats = statsState.status === 'ready' ? statsState.stats : null;
  const leaderImagesById = useCardImages(
    stats
      ? [
          ...stats.leaders.map((leader) => leader.leaderCardId),
          ...stats.recent.flatMap((match) => [match.ownLeaderCardId, match.opponentLeaderCardId]),
        ]
      : [],
  );
  const statValue = (value: string): string => (stats ? value : statsState.status === 'loading' ? '…' : '–');
  const profileStats = [
    { label: 'Decks', value: String(deckCount) },
    { label: 'Matches', value: statValue(String(stats?.matches ?? 0)) },
    { label: 'Wins', value: statValue(String(stats?.wins ?? 0)) },
    { label: 'Winrate', value: statValue(`${Math.round(stats?.winRate ?? 0)}%`) },
  ];

  const continueAsGuest = (): void => {
    const normalizedName = draftName.trim() || DEFAULT_GUEST_NAME;
    setGuestName(normalizedName);
    setDraftName(normalizedName);
    setIsGuestActive(true);
    setIsEditingName(false);
    try {
      window.localStorage.setItem(GUEST_NAME_STORAGE_KEY, normalizedName);
    } catch {
      // Guest profile storage is optional for the demo UI.
    }
  };

  const logoutGuest = (): void => {
    setGuestName(DEFAULT_GUEST_NAME);
    setDraftName(DEFAULT_GUEST_NAME);
    setIsGuestActive(false);
    setIsEditingName(false);
    try {
      window.localStorage.removeItem(GUEST_NAME_STORAGE_KEY);
    } catch {
      // Guest profile storage is optional for the demo UI.
    }
  };

  const isShowingName = isGuestActive && !isEditingName;

  return (
    <section className="profile-page ui-page">
      <header className="ui-page-header">
        <div>
          <span className="ui-kicker">OneSake · Account</span>
          <h1 className="ui-title">Captain Profile</h1>
        </div>
        <span
          aria-label="Profile status"
          className={`ui-badge profile-status-badge ${isGuestActive ? 'ui-badge--success' : ''}`}
        >
          {isGuestActive ? 'Guest Active' : 'Guest Ready'}
        </span>
      </header>

      <section className="ui-glass profile-hero" aria-label="Guest profile">
        <div className="profile-avatar" aria-hidden="true">
          <span>{displayName.slice(0, 1).toUpperCase()}</span>
        </div>

        <div className="profile-identity">
          <div className="name-plate">
            {isShowingName ? (
              <>
                <span className="ui-label">Logged in as</span>
                <strong className="profile-name" role="status">
                  {displayName}
                </strong>
              </>
            ) : (
              <>
                <label className="ui-label" htmlFor="guestName">
                  Captain Name
                </label>
                <input
                  className="ui-plate-input"
                  id="guestName"
                  maxLength={24}
                  onChange={(event) => setDraftName(event.target.value)}
                  placeholder={DEFAULT_GUEST_NAME}
                  value={draftName}
                />
              </>
            )}
          </div>

          <p className="profile-rank">Rookie Pirate · Local demo only</p>

          <div className="profile-badge-row" aria-label="Profile badges">
            <span>Starter Deckhand</span>
            <span>East Blue Trial</span>
          </div>

          <div className="profile-actions">
            {isShowingName ? (
              <button className="ui-btn-secondary" onClick={() => setIsEditingName(true)} type="button">
                Edit Name
              </button>
            ) : (
              <button
                className="ui-btn-primary ui-btn-primary--compact"
                onClick={continueAsGuest}
                type="button"
              >
                <span>Continue as Guest</span>
              </button>
            )}
            <button className="ui-btn-ghost" disabled type="button">
              Login coming soon
            </button>
            {isGuestActive && (
              <button className="ui-link ui-link--danger" onClick={logoutGuest} type="button">
                Logout Guest
              </button>
            )}
          </div>
        </div>
      </section>

      <div className="profile-layout">
        <section className="ui-glass profile-stats-card" aria-labelledby="profile-stats-title">
          <div className="profile-card__header">
            <h2 className="ui-heading" id="profile-stats-title">
              Captain Log
            </h2>
            <span className="ui-badge profile-header-badge">Gast-ID</span>
          </div>
          <div className="profile-stat-grid">
            {profileStats.map((stat) => (
              <div key={stat.label} className="ui-stat profile-stat-card">
                <span>{stat.label}</span>
                <strong>{stat.value}</strong>
              </div>
            ))}
            <div className="ui-stat profile-stat-card profile-stat-card--leader">
              <span>Lieblings-Leader</span>
              {stats?.favoriteLeaderCardId ? (
                <span className="profile-favorite-leader">
                  <LeaderThumb
                    cardId={stats.favoriteLeaderCardId}
                    imageUrl={leaderImagesById[stats.favoriteLeaderCardId]}
                  />
                  <strong>{stats.favoriteLeaderCardId}</strong>
                </span>
              ) : (
                <strong>–</strong>
              )}
            </div>
            <div className="ui-stat profile-stat-card is-disabled" title="Bald verfügbar">
              <span>Berries</span>
              <small>bald verfügbar</small>
            </div>
          </div>
        </section>

        <section className="ui-glass profile-history-card" aria-labelledby="profile-history-title">
          <div className="profile-card__header">
            <h2 className="ui-heading" id="profile-history-title">
              Verlauf
            </h2>
            {stats && stats.matches > 0 && (
              <span className="ui-badge profile-header-badge">{stats.matches} Matches</span>
            )}
          </div>

          {statsState.status === 'loading' && <p className="profile-state">Lade Statistik…</p>}

          {statsState.status === 'error' && (
            <div className="profile-state profile-state--error" role="alert">
              <p>Die Statistik konnte nicht geladen werden. Ist der Server erreichbar?</p>
              <button className="ui-btn-secondary" onClick={loadStats} type="button">
                Erneut versuchen
              </button>
            </div>
          )}

          {stats && stats.matches === 0 && (
            <div className="profile-state">
              <p>Noch keine Matches – spiel dein erstes!</p>
              {onOpenMatch && (
                <button className="ui-btn-primary ui-btn-primary--compact" onClick={onOpenMatch} type="button">
                  <span>Zum Match</span>
                </button>
              )}
            </div>
          )}

          {stats && stats.matches > 0 && (
            <div className="profile-history-grid">
              <div>
                <h3>Letzte Matches</h3>
                <RecentMatchList imagesById={leaderImagesById} matches={stats.recent} />
              </div>
              <div>
                <h3>Leader-Statistik</h3>
                <LeaderStatsList imagesById={leaderImagesById} leaders={stats.leaders} />
              </div>
            </div>
          )}
        </section>
      </div>
    </section>
  );
}
