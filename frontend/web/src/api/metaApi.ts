import { getBackendBaseUrl } from './backendUrl';

// Meta data of an EXTERNAL provider, always loaded through our backend (/meta/...).
// The provider itself is never called from the browser. Rates are fractions 0..1 or null.

export interface MetaStatusDto {
  enabled: boolean;
  provider: string;
  attributionName: string;
  attributionUrl: string;
  snapshotDate: string | null;
  fetchedAtUtc: string | null;
  isStale: boolean;
  formats: string[];
}

export interface MetaSourceDto {
  provider: string;
  attributionName: string;
  attributionUrl: string;
  snapshotDate: string;
  fetchedAtUtc: string;
  isStale: boolean;
}

export interface MetaLeaderSummaryDto {
  leaderKey: string;
  leaderName: string;
  leaderCardId: string | null;
  games: number;
  winRate: number | null;
  firstWinRate: number | null;
  secondWinRate: number | null;
  matchupCount: number;
}

export interface MetaMatchupDto {
  opponentKey: string;
  opponentName: string;
  opponentCardId: string | null;
  firstWinRate: number | null;
  secondWinRate: number | null;
  firstGames: number;
  secondGames: number;
  games: number;
  winRate: number | null;
}

export interface MetaLeaderDto extends Omit<MetaLeaderSummaryDto, 'matchupCount'> {
  matchups: MetaMatchupDto[];
}

export interface MetaMatrixCellDto {
  leaderKey: string;
  opponentKey: string;
  games: number;
  firstGames: number;
  secondGames: number;
  winRate: number | null;
  firstWinRate: number | null;
  secondWinRate: number | null;
}

export interface MetaMatchupsDto {
  enabled: boolean;
  source: MetaSourceDto | null;
  minGames: number;
  leaders: { leaderKey: string; leaderName: string; leaderCardId: string | null; games: number }[];
  cells: MetaMatrixCellDto[];
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${getBackendBaseUrl()}${path}`);
  if (!response.ok) {
    throw new Error(`Meta request failed (${response.status}).`);
  }

  return (await response.json()) as T;
}

const formatQuery = (format: string): string => (format ? `format=${encodeURIComponent(format)}` : '');

export function getMetaStatus(): Promise<MetaStatusDto> {
  return getJson('/meta/status');
}

export function getMetaLeaders(
  format: string,
): Promise<{ enabled: boolean; source: MetaSourceDto | null; leaders: MetaLeaderSummaryDto[] }> {
  return getJson(`/meta/leaders?${formatQuery(format)}`);
}

export function getMetaLeader(
  leaderKey: string,
  format: string,
): Promise<{ enabled: boolean; source: MetaSourceDto | null; leader: MetaLeaderDto | null }> {
  return getJson(`/meta/leader/${encodeURIComponent(leaderKey)}?${formatQuery(format)}`);
}

export function getMetaMatchups(format: string, minGames = 20): Promise<MetaMatchupsDto> {
  return getJson(`/meta/matchups?${formatQuery(format)}&minGames=${minGames}`);
}

// 0.5432 -> "54,3 %"
export function formatRate(rate: number | null | undefined): string {
  return typeof rate === 'number' ? `${(rate * 100).toLocaleString('de-DE', { maximumFractionDigits: 1, minimumFractionDigits: 1 })} %` : '–';
}

// "2026-11-17" -> "17.11.2026"
export function formatSnapshotDate(date: string | null | undefined): string {
  if (!date) {
    return '–';
  }

  const [year, month, day] = date.split('-');
  return `${day}.${month}.${year}`;
}
