import { getBackendBaseUrl } from './backendUrl';

// OneSake's own player ranking (ELO from ranked Quick Match games, shown as One Piece bounty).

export interface LeaderboardEntryDto {
  rank: number;
  displayName: string;
  rating: number;
  bounty: number;
  wins: number;
  losses: number;
  winRate: number;
  favoriteLeaderCardId: string | null;
  lastPlayedUtc: string;
}

export interface RatingPointDto {
  endedAtUtc: string;
  rating: number;
}

export interface PlayerBountyDto {
  // Null until the player has 5 ranked games.
  rank: number | null;
  displayName: string | null;
  rating: number;
  bounty: number;
  peak: number;
  wins: number;
  losses: number;
  favoriteLeaderCardId: string | null;
  ratingHistory: RatingPointDto[];
}

export const BOUNTY_PER_POINT = 1000;

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${getBackendBaseUrl()}${path}`);
  if (!response.ok) {
    throw new Error(`Request failed (${response.status}).`);
  }

  return (await response.json()) as T;
}

export function getLeaderboard(limit = 100): Promise<LeaderboardEntryDto[]> {
  return getJson(`/leaderboard?limit=${limit}`);
}

export function getPlayerBounty(guestId: string): Promise<PlayerBountyDto> {
  return getJson(`/leaderboard/player/${encodeURIComponent(guestId)}`);
}

// 1250000 -> "฿ 1.250.000"
export function formatBounty(bounty: number): string {
  return `฿ ${Math.round(bounty).toLocaleString('de-DE')}`;
}

// 24000 -> "+฿ 24.000", -12000 -> "−฿ 12.000"
export function formatBountyChange(change: number): string {
  const sign = change >= 0 ? '+' : '−';
  return `${sign}฿ ${Math.abs(Math.round(change)).toLocaleString('de-DE')}`;
}
