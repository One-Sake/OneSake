import { getBackendBaseUrl } from './backendUrl';

// Mirrors the backend PlayerStatsDto (GET /stats/me).
export interface LeaderStatsDto {
  leaderCardId: string;
  matches: number;
  wins: number;
}

export interface RecentMatchDto {
  playedAtUtc: string;
  opponentName: string;
  ownLeaderCardId: string;
  opponentLeaderCardId: string;
  won: boolean;
  endReason: 'LIFE' | 'DECK_OUT' | 'CONCEDE' | 'DISCONNECT' | 'TIMEOUT' | string;
  turnCount: number;
  isRanked?: boolean;
  // Rating points / bounty won (+) or lost (-); null when the match did not change the rating.
  ratingChange?: number | null;
  bountyChange?: number | null;
}

export interface PlayerStatsDto {
  matches: number;
  wins: number;
  losses: number;
  // Percent from 0 to 100.
  winRate: number;
  favoriteLeaderCardId: string | null;
  leaders: LeaderStatsDto[];
  recent: RecentMatchDto[];
}

// Own statistics: the account when logged in (cookie), otherwise the guest ID of this browser.
export async function getPlayerStats(guestId: string): Promise<PlayerStatsDto> {
  const response = await fetch(`${getBackendBaseUrl()}/stats/me?guestId=${encodeURIComponent(guestId)}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Stats request failed (${response.status}).`);
  }

  return (await response.json()) as PlayerStatsDto;
}
