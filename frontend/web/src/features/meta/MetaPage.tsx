import { useCallback, useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';

import {
  formatSnapshotDate,
  getMetaLeader,
  getMetaLeaders,
  getMetaMatchups,
  getMetaStatus,
  type MetaLeaderDto,
  type MetaLeaderSummaryDto,
  type MetaMatchupsDto,
  type MetaSourceDto,
  type MetaStatusDto,
} from '../../api/metaApi';
import { Tabs } from '../../components/ui/Tabs';
import type { CardDto } from '../../types/cards';
import { getLeaderColors } from '../match/leaderColors';
import { BountyLeaderboard } from './BountyLeaderboard';
import { MetaHeatmap } from './MetaHeatmap';
import { MetaLeaderDetail } from './MetaLeaderDetail';
import { MetaLeaderTable } from './MetaLeaderTable';
import { useMetaCards } from './useMetaCards';

type TabId = 'leaders' | 'matchups' | 'bounty';

const TABS = [
  { id: 'leaders', label: 'Leader' },
  { id: 'matchups', label: 'Matchups' },
  { id: 'bounty', label: 'Bounty-Rangliste' },
];

type Load<T> = { status: 'idle' } | { status: 'loading' } | { status: 'error' } | { status: 'ready'; data: T };

function useLoad<T>(loader: (() => Promise<T>) | null): [Load<T>, () => void] {
  const [state, setState] = useState<Load<T>>({ status: 'idle' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!loader) {
      setState({ status: 'idle' });
      return undefined;
    }

    let isCancelled = false;
    setState({ status: 'loading' });
    loader()
      .then((data) => !isCancelled && setState({ status: 'ready', data }))
      .catch(() => !isCancelled && setState({ status: 'error' }));

    return () => {
      isCancelled = true;
    };
  }, [loader, attempt]);

  return [state, useCallback(() => setAttempt((value) => value + 1), [])];
}

// Mandatory source line under every meta view. These are not OneSake's numbers.
function Attribution({ source, status }: { source: MetaSourceDto | null; status: MetaStatusDto | null }) {
  const name = source?.attributionName ?? status?.attributionName ?? '';
  const url = source?.attributionUrl ?? status?.attributionUrl ?? '';
  const date = source?.snapshotDate ?? status?.snapshotDate ?? null;
  if (!name || !date) {
    return null;
  }

  return (
    <p className="meta-attribution">
      Daten:{' '}
      <a href={url} rel="noopener noreferrer" target="_blank">
        {name}
      </a>{' '}
      · Snapshot {formatSnapshotDate(date)} · keine OneSake-Daten
    </p>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="ui-glass profile-state profile-state--error" role="alert">
      <p>Die Meta-Daten konnten nicht geladen werden. Der Datenanbieter ist gerade nicht erreichbar.</p>
      <button className="ui-btn-secondary" onClick={onRetry} type="button">
        Erneut versuchen
      </button>
    </div>
  );
}

function Skeleton() {
  return (
    <div className="ui-glass meta-skeleton-panel" aria-busy="true" aria-label="Lade Meta-Daten">
      {Array.from({ length: 6 }, (_, index) => (
        <span key={index} className="ui-tile meta-skeleton" />
      ))}
    </div>
  );
}

export function MetaPage() {
  const [activeTab, setActiveTab] = useState<TabId>('leaders');
  const [format, setFormat] = useState('');
  const [openLeaderKey, setOpenLeaderKey] = useState<string | null>(null);
  const [previewCard, setPreviewCard] = useState<CardDto | null>(null);

  const [statusState, retryStatus] = useLoad<MetaStatusDto>(useCallback(() => getMetaStatus(), []));
  const status = statusState.status === 'ready' ? statusState.data : null;
  const metaEnabled = Boolean(status?.enabled);
  const activeFormat = format || status?.formats[0] || '';

  const [leadersState, retryLeaders] = useLoad(
    useMemo(() => (metaEnabled && activeTab === 'leaders' && !openLeaderKey ? () => getMetaLeaders(activeFormat) : null), [
      metaEnabled,
      activeTab,
      openLeaderKey,
      activeFormat,
    ]),
  );
  const [detailState, retryDetail] = useLoad(
    useMemo(
      () => (metaEnabled && openLeaderKey ? () => getMetaLeader(openLeaderKey, activeFormat) : null),
      [metaEnabled, openLeaderKey, activeFormat],
    ),
  );
  const [matchupsState, retryMatchups] = useLoad<MetaMatchupsDto>(
    useMemo(
      () => (metaEnabled && activeTab === 'matchups' && !openLeaderKey ? () => getMetaMatchups(activeFormat, 20) : null),
      [metaEnabled, activeTab, openLeaderKey, activeFormat],
    ),
  );

  const leaders: MetaLeaderSummaryDto[] = leadersState.status === 'ready' ? leadersState.data.leaders : [];
  const detail: MetaLeaderDto | null = detailState.status === 'ready' ? detailState.data.leader : null;
  const matrix = matchupsState.status === 'ready' ? matchupsState.data : null;
  const cardsById = useMetaCards([
    ...leaders.map((leader) => leader.leaderCardId),
    ...(detail ? [detail.leaderCardId, ...detail.matchups.map((matchup) => matchup.opponentCardId)] : []),
    ...(matrix ? matrix.leaders.map((leader) => leader.leaderCardId) : []),
  ]);

  const source =
    (detailState.status === 'ready' && openLeaderKey ? detailState.data.source : null) ??
    (leadersState.status === 'ready' ? leadersState.data.source : null) ??
    (matrix?.source ?? null);
  const hasSnapshot = Boolean(source?.snapshotDate ?? status?.snapshotDate);
  // Only a real older snapshot is "stale"; without any data the error state is shown instead.
  const isStale = hasSnapshot && Boolean(source?.isStale ?? status?.isStale);

  // The leader color of the opened leader tints the page.
  const detailCard = detail?.leaderCardId ? cardsById[detail.leaderCardId] : undefined;
  const accent = getLeaderColors(detailCard?.card_color);
  const pageStyle = (detail
    ? { '--page-accent': accent.primary, '--page-accent-2': accent.secondary }
    : {}) as CSSProperties;

  const openLeader = (leaderKey: string): void => {
    setPreviewCard(null);
    setOpenLeaderKey(leaderKey);
    window.scrollTo({ top: 0 });
  };

  const changeTab = (id: string): void => {
    setActiveTab(id as TabId);
    setOpenLeaderKey(null);
    setPreviewCard(null);
  };

  const isMetaTab = activeTab !== 'bounty';

  return (
    <section className="meta-page ui-page" style={pageStyle}>
      <header className="ui-page-header">
        <div>
          <span className="ui-kicker">OneSake · Meta</span>
          <h1 className="ui-title">Meta &amp; Rangliste</h1>
        </div>
        {isMetaTab && metaEnabled && (
          <div className="meta-header-side">
            {status && status.formats.length > 1 ? (
              <label className="ui-field" htmlFor="metaFormat">
                <span>Format</span>
                <select
                  className="ui-input"
                  id="metaFormat"
                  onChange={(event) => setFormat(event.target.value)}
                  value={activeFormat}
                >
                  {status.formats.map((entry) => (
                    <option key={entry} value={entry}>
                      {entry}
                    </option>
                  ))}
                </select>
              </label>
            ) : (
              <span className="ui-badge meta-format-badge">{activeFormat}</span>
            )}
            <span className="meta-snapshot">Snapshot vom {formatSnapshotDate(source?.snapshotDate ?? status?.snapshotDate)}</span>
          </div>
        )}
      </header>

      <Tabs activeId={activeTab} ariaLabel="Meta-Ansichten" items={TABS} onChange={changeTab} />

      {isMetaTab && isStale && metaEnabled && (
        <p className="meta-stale" role="status">
          Der Datenanbieter ist gerade nicht erreichbar – angezeigt wird der letzte erfolgreiche Snapshot.
        </p>
      )}

      {isMetaTab && statusState.status === 'loading' && <Skeleton />}
      {isMetaTab && statusState.status === 'error' && <ErrorState onRetry={retryStatus} />}
      {isMetaTab && status && !status.enabled && (
        <div className="ui-glass profile-state">
          <p>Meta-Daten sind noch nicht verfügbar.</p>
          <button className="ui-btn-secondary" onClick={() => changeTab('bounty')} type="button">
            Zur Bounty-Rangliste
          </button>
        </div>
      )}

      {isMetaTab && metaEnabled && openLeaderKey && (
        <>
          {detailState.status === 'loading' && <Skeleton />}
          {detailState.status === 'error' && <ErrorState onRetry={retryDetail} />}
          {detail && (
            <MetaLeaderDetail
              cardsById={cardsById}
              leader={detail}
              onBack={() => setOpenLeaderKey(null)}
              onPreview={setPreviewCard}
            />
          )}
        </>
      )}

      {activeTab === 'leaders' && metaEnabled && !openLeaderKey && (
        <>
          {leadersState.status === 'loading' && <Skeleton />}
          {leadersState.status === 'error' && <ErrorState onRetry={retryLeaders} />}
          {leadersState.status === 'ready' && (
            <MetaLeaderTable
              cardsById={cardsById}
              leaders={leaders}
              onOpenLeader={openLeader}
              onPreview={setPreviewCard}
            />
          )}
        </>
      )}

      {activeTab === 'matchups' && metaEnabled && !openLeaderKey && (
        <>
          {matchupsState.status === 'loading' && <Skeleton />}
          {matchupsState.status === 'error' && <ErrorState onRetry={retryMatchups} />}
          {matrix && <MetaHeatmap cardsById={cardsById} data={matrix} onOpenLeader={openLeader} />}
        </>
      )}

      {isMetaTab && metaEnabled && <Attribution source={source} status={status} />}

      {activeTab === 'bounty' && <BountyLeaderboard />}

      {previewCard?.card_image && (
        <aside className="meta-preview" aria-hidden="true">
          <img alt="" src={previewCard.card_image} />
        </aside>
      )}
    </section>
  );
}
