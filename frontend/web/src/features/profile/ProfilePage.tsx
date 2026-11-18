import { useCallback, useEffect, useState } from 'react';

import { getGuestId } from '../../api/guestId';
import { getPlayerStats, type PlayerStatsDto } from '../../api/statsApi';
import { Button } from '../../components/ui/Button';
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

  return (
    <section className="profile-page">
      <header className="panel profile-hero">
        <div>
          <span className="profile-kicker">OneSake Account</span>
          <h1>Captain Profile</h1>
        </div>
        <div className="profile-hero__badge" aria-label="Profile status">
          {isGuestActive ? 'Guest Active' : 'Guest Ready'}
        </div>
      </header>

      <div className="profile-layout">
        <section className="panel profile-card profile-login-card" aria-label="Guest profile">
          <div className="profile-card__header">
            <span>Login</span>
            <strong>Local demo only</strong>
          </div>

          {(!isGuestActive || isEditingName) && (
            <div className="field">
              <label htmlFor="guestName">Captain Name</label>
              <input
                id="guestName"
                maxLength={24}
                onChange={(event) => setDraftName(event.target.value)}
                placeholder={DEFAULT_GUEST_NAME}
                value={draftName}
              />
            </div>
          )}

          {isGuestActive && !isEditingName ? (
            <div className="profile-session-card" role="status">
              <span>Logged in as</span>
              <strong>{displayName}</strong>
            </div>
          ) : null}

          <div className="profile-actions">
            {isGuestActive && !isEditingName ? (
              <Button onClick={() => setIsEditingName(true)} variant="secondary">
                Edit Name
              </Button>
            ) : (
              <Button onClick={continueAsGuest}>Continue as Guest</Button>
            )}
            <Button disabled variant="ghost">
              Login coming soon
            </Button>
            {isGuestActive && (
              <Button onClick={logoutGuest} variant="danger">
                Logout Guest
              </Button>
            )}
          </div>
        </section>

        <section className="panel profile-card profile-preview-card" aria-labelledby="profile-preview-title">
          <div className="profile-avatar" aria-hidden="true">
            <span>{displayName.slice(0, 1).toUpperCase()}</span>
          </div>
          <div className="profile-preview-card__identity">
            <span>Profile Preview</span>
            <h2 id="profile-preview-title">{displayName}</h2>
            <p>Rookie Pirate</p>
          </div>
          <div className="profile-badge-row" aria-label="Profile badges">
            <span>Starter Deckhand</span>
            <span>East Blue Trial</span>
          </div>
        </section>

        <section className="panel profile-card profile-stats-card" aria-labelledby="profile-stats-title">
          <div className="profile-card__header">
            <span>Stats</span>
            <strong>Gast-ID</strong>
          </div>
          <h2 id="profile-stats-title">Captain Log</h2>
          <div className="profile-stat-grid">
            {profileStats.map((stat) => (
              <div key={stat.label} className="profile-stat-card">
                <span>{stat.label}</span>
                <strong>{stat.value}</strong>
              </div>
            ))}
            <div className="profile-stat-card profile-stat-card--leader">
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
            <div className="profile-stat-card is-disabled" title="Bald verfügbar">
              <span>Berries</span>
              <small>bald verfügbar</small>
            </div>
          </div>
        </section>

        <section className="panel profile-card profile-history-card" aria-labelledby="profile-history-title">
          <div className="profile-card__header">
            <span>Verlauf</span>
            {stats && stats.matches > 0 && <strong>{stats.matches} Matches</strong>}
          </div>

          {statsState.status === 'loading' && <p className="profile-state">Lade Statistik…</p>}

          {statsState.status === 'error' && (
            <div className="profile-state profile-state--error" role="alert">
              <p>Die Statistik konnte nicht geladen werden. Ist der Server erreichbar?</p>
              <Button onClick={loadStats} variant="secondary">
                Erneut versuchen
              </Button>
            </div>
          )}

          {stats && stats.matches === 0 && (
            <div className="profile-state">
              <p>Noch keine Matches – spiel dein erstes!</p>
              {onOpenMatch && <Button onClick={onOpenMatch}>Zum Match</Button>}
            </div>
          )}

          {stats && stats.matches > 0 && (
            <div className="profile-history-grid">
              <div>
                <h2 id="profile-history-title">Letzte Matches</h2>
                <RecentMatchList imagesById={leaderImagesById} matches={stats.recent} />
              </div>
              <div>
                <h2>Leader-Statistik</h2>
                <LeaderStatsList imagesById={leaderImagesById} leaders={stats.leaders} />
              </div>
            </div>
          )}
        </section>
      </div>
    </section>
  );
}
