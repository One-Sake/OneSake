import { useMemo, useState } from 'react';

import { formatRate, type MetaLeaderSummaryDto } from '../../api/metaApi';
import type { CardDto } from '../../types/cards';

type SortKey = 'leaderName' | 'games' | 'winRate' | 'firstWinRate' | 'secondWinRate';

const COLUMNS: { key: SortKey; label: string }[] = [
  { key: 'leaderName', label: 'Leader' },
  { key: 'games', label: 'Spiele' },
  { key: 'winRate', label: 'Winrate' },
  { key: 'firstWinRate', label: 'als 1.' },
  { key: 'secondWinRate', label: 'als 2.' },
];

// Tiles show the most played leaders. The provider has no tiers, so OneSake shows none.
const TOP_TILE_COUNT = 6;

interface MetaLeaderTableProps {
  leaders: MetaLeaderSummaryDto[];
  cardsById: Record<string, CardDto>;
  onOpenLeader: (leaderKey: string) => void;
  onPreview: (card: CardDto | null) => void;
}

function sortValue(leader: MetaLeaderSummaryDto, key: SortKey): number | string {
  if (key === 'leaderName') {
    return leader.leaderName.toLowerCase();
  }

  return leader[key] ?? -1;
}

export function MetaLeaderTable({ cardsById, leaders, onOpenLeader, onPreview }: MetaLeaderTableProps) {
  const [sortKey, setSortKey] = useState<SortKey>('games');
  const [descending, setDescending] = useState(true);

  const sorted = useMemo(() => {
    const copy = [...leaders];
    copy.sort((left, right) => {
      const a = sortValue(left, sortKey);
      const b = sortValue(right, sortKey);
      const result = typeof a === 'string' ? a.localeCompare(String(b)) : a - (b as number);
      return descending ? -result : result;
    });
    return copy;
  }, [descending, leaders, sortKey]);

  const topLeaders = useMemo(
    () => [...leaders].sort((left, right) => right.games - left.games).slice(0, TOP_TILE_COUNT),
    [leaders],
  );

  const changeSort = (key: SortKey): void => {
    if (key === sortKey) {
      setDescending((value) => !value);
      return;
    }

    setSortKey(key);
    setDescending(key !== 'leaderName');
  };

  const previewHandlers = (leader: MetaLeaderSummaryDto) => ({
    onMouseEnter: () => onPreview(leader.leaderCardId ? cardsById[leader.leaderCardId] ?? null : null),
    onMouseLeave: () => onPreview(null),
  });

  return (
    <div className="meta-leaders">
      <section className="meta-top-tiles" aria-label="Meistgespielte Leader">
        {topLeaders.map((leader, index) => {
          const card = leader.leaderCardId ? cardsById[leader.leaderCardId] : undefined;
          return (
            <button
              key={leader.leaderKey}
              className="ui-tile meta-top-tile"
              onClick={() => onOpenLeader(leader.leaderKey)}
              type="button"
              {...previewHandlers(leader)}
            >
              <span className="meta-top-tile__image" aria-hidden="true">
                {card?.card_image ? <img alt="" loading="lazy" src={card.card_image} /> : <span>{leader.leaderKey}</span>}
              </span>
              <span className="meta-top-tile__body">
                <small>#{index + 1} meistgespielt</small>
                <strong>{leader.leaderName}</strong>
                <span className="meta-top-tile__rate">{formatRate(leader.winRate)}</span>
                <small>{leader.games.toLocaleString('de-DE')} Spiele</small>
              </span>
            </button>
          );
        })}
      </section>

      <div className="meta-table-wrap">
        <table className="meta-table">
          <thead>
            <tr>
              {COLUMNS.map((column) => (
                <th
                  key={column.key}
                  aria-sort={sortKey === column.key ? (descending ? 'descending' : 'ascending') : 'none'}
                  scope="col"
                >
                  <button className="meta-table__sort" onClick={() => changeSort(column.key)} type="button">
                    {column.label}
                    <span aria-hidden="true">{sortKey === column.key ? (descending ? ' ▼' : ' ▲') : ''}</span>
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sorted.map((leader) => {
              const card = leader.leaderCardId ? cardsById[leader.leaderCardId] : undefined;
              return (
                <tr key={leader.leaderKey} {...previewHandlers(leader)}>
                  <th scope="row">
                    <button className="meta-leader-cell" onClick={() => onOpenLeader(leader.leaderKey)} type="button">
                      <span className="meta-thumb" aria-hidden="true">
                        {card?.card_image ? <img alt="" loading="lazy" src={card.card_image} /> : null}
                      </span>
                      <span>
                        <strong>{leader.leaderName}</strong>
                        <small>{leader.leaderKey}</small>
                      </span>
                    </button>
                  </th>
                  <td>{leader.games.toLocaleString('de-DE')}</td>
                  <td className="meta-table__rate">{formatRate(leader.winRate)}</td>
                  <td>{formatRate(leader.firstWinRate)}</td>
                  <td>{formatRate(leader.secondWinRate)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
