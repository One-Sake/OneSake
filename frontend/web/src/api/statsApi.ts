import { getBackendBaseUrl } from './backendUrl';

// Mirrors the backend PlayerStatsDto (GET /stats/player/{guestId}).
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
  endReason: 'LIFE' | 'DECK_OUT' | 'CONCEDE' | 'DISCONNECT' | string;
  turnCount: number;
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

export async function getPlayerStats(guestId: string): Promise<PlayerStatsDto> {
  const response = await fetch(`${getBackendBaseUrl()}/stats/player/${encodeURIComponent(guestId)}`);

  if (!response.ok) {
    throw new Error(`Stats request failed (${response.status}).`);
  }

  return (await response.json()) as PlayerStatsDto;
}
