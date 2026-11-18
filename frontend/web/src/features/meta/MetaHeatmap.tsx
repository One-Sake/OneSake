import { useMemo, useState } from 'react';

import { formatRate, type MetaMatchupsDto } from '../../api/metaApi';
import type { CardDto } from '../../types/cards';

type RateMode = 'winRate' | 'firstWinRate' | 'secondWinRate';

const MODES: { id: RateMode; label: string }[] = [
  { id: 'winRate', label: 'gesamt' },
  { id: 'firstWinRate', label: 'als 1.' },
  { id: 'secondWinRate', label: 'als 2.' },
];

// Red below 40 %, neutral at 50 %, green above 60 %.
function cellColor(rate: number | null): string | undefined {
  if (rate === null) {
    return undefined;
  }

  const clamped = Math.max(0.4, Math.min(0.6, rate));
  const t = (clamped - 0.4) / 0.2;
  const hue = Math.round(t * 130);
  const strength = Math.abs(t - 0.5) * 2;
  return `hsl(${hue} ${45 + strength * 30}% ${20 + strength * 14}%)`;
}

interface MetaHeatmapProps {
  data: MetaMatchupsDto;
  cardsById: Record<string, CardDto>;
  onOpenLeader: (leaderKey: string) => void;
}

// Row = leader, column = opponent. Cells below minGames are empty.
export function MetaHeatmap({ cardsById, data, onOpenLeader }: MetaHeatmapProps) {
  const [mode, setMode] = useState<RateMode>('winRate');
  const cells = useMemo(
    () => new Map(data.cells.map((cell) => [`${cell.leaderKey}|${cell.opponentKey}`, cell])),
    [data.cells],
  );

  return (
    <div className="meta-heatmap">
      <div className="meta-heatmap__toolbar" role="group" aria-label="Winrate-Ansicht">
        {MODES.map((entry) => (
          <button
            key={entry.id}
            aria-pressed={mode === entry.id}
            className="ui-chip"
            onClick={() => setMode(entry.id)}
            type="button"
          >
            {entry.label}
          </button>
        ))}
        <span className="meta-muted">
          Top {data.leaders.length} Leader · Felder ab {data.minGames} Spielen · Zeile gegen Spalte
        </span>
      </div>

      <div className="meta-heatmap__scroll">
        <table className="meta-heatmap__table">
          <thead>
            <tr>
              <th className="meta-heatmap__corner" scope="col">
                Leader ↓ / Gegner →
              </th>
              {data.leaders.map((leader) => {
                const card = leader.leaderCardId ? cardsById[leader.leaderCardId] : undefined;
                return (
                  <th key={leader.leaderKey} scope="col" title={leader.leaderName}>
                    <span className="meta-thumb meta-thumb--small" aria-hidden="true">
                      {card?.card_image ? <img alt="" loading="lazy" src={card.card_image} /> : null}
                    </span>
                    <span className="meta-heatmap__col-name">{leader.leaderName}</span>
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {data.leaders.map((row) => {
              const card = row.leaderCardId ? cardsById[row.leaderCardId] : undefined;
              return (
                <tr key={row.leaderKey}>
                  <th scope="row">
                    <button className="meta-leader-cell" onClick={() => onOpenLeader(row.leaderKey)} type="button">
                      <span className="meta-thumb meta-thumb--small" aria-hidden="true">
                        {card?.card_image ? <img alt="" loading="lazy" src={card.card_image} /> : null}
                      </span>
                      <span>
                        <strong>{row.leaderName}</strong>
                        <small>{row.leaderKey}</small>
                      </span>
                    </button>
                  </th>
                  {data.leaders.map((column) => {
                    const cell = cells.get(`${row.leaderKey}|${column.leaderKey}`);
                    const rate = cell ? cell[mode] : null;
                    const games =
                      cell && (mode === 'winRate' ? cell.games : mode === 'firstWinRate' ? cell.firstGames : cell.secondGames);
                    const title = cell
                      ? `${row.leaderName} gegen ${column.leaderName}: ${formatRate(rate)} (${games} Spiele)`
                      : `${row.leaderName} gegen ${column.leaderName}: zu wenig Daten`;
                    return (
                      <td
                        key={column.leaderKey}
                        className={cell && rate !== null ? 'meta-heatmap__cell' : 'meta-heatmap__cell is-empty'}
                        style={{ background: cellColor(rate) }}
                        title={title}
                      >
                        {rate !== null ? Math.round(rate * 100) : '·'}
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
