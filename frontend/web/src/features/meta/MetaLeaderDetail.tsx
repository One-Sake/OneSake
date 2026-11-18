import { useMemo } from 'react';

import { formatRate, type MetaLeaderDto, type MetaMatchupDto } from '../../api/metaApi';
import type { CardDto } from '../../types/cards';

// Matchups with fewer games are shown greyed out ("wenig Daten").
const LOW_DATA_GAMES = 20;

interface MetaLeaderDetailProps {
  leader: MetaLeaderDto;
  cardsById: Record<string, CardDto>;
  onBack: () => void;
  onPreview: (card: CardDto | null) => void;
}

function RateBar({ label, rate }: { label: string; rate: number | null }) {
  return (
    <span className="meta-bar" title={`${label}: ${formatRate(rate)}`}>
      <span className="meta-bar__label">{label}</span>
      <span className="meta-bar__track">
        {typeof rate === 'number' && (
          <span
            className={rate >= 0.5 ? 'meta-bar__fill is-good' : 'meta-bar__fill is-bad'}
            style={{ width: `${Math.round(rate * 100)}%` }}
          />
        )}
        <span className="meta-bar__mid" aria-hidden="true" />
      </span>
      <span className="meta-bar__value">{formatRate(rate)}</span>
    </span>
  );
}

export function MetaLeaderDetail({ cardsById, leader, onBack, onPreview }: MetaLeaderDetailProps) {
  const card = leader.leaderCardId ? cardsById[leader.leaderCardId] : undefined;

  // Best and worst matchup among the ones with enough games.
  const { best, worst } = useMemo(() => {
    const reliable = leader.matchups.filter(
      (matchup) => matchup.games >= LOW_DATA_GAMES && typeof matchup.winRate === 'number',
    );
    const byRate = [...reliable].sort((left, right) => (right.winRate ?? 0) - (left.winRate ?? 0));
    return { best: byRate[0]?.opponentKey, worst: byRate[byRate.length - 1]?.opponentKey };
  }, [leader.matchups]);

  const rowClass = (matchup: MetaMatchupDto): string =>
    [
      'ui-tile',
      'meta-matchup',
      matchup.games < LOW_DATA_GAMES ? 'is-low-data' : '',
      matchup.opponentKey === best ? 'is-best' : '',
      matchup.opponentKey === worst && worst !== best ? 'is-worst' : '',
    ]
      .filter(Boolean)
      .join(' ');

  return (
    <div className="meta-detail">
      <button className="ui-link meta-detail__back" onClick={onBack} type="button">
        ← Alle Leader
      </button>

      <section className="ui-glass meta-detail__hero">
        <div className="meta-detail__card">
          {card?.card_image ? <img alt={leader.leaderName} src={card.card_image} /> : <span>{leader.leaderKey}</span>}
        </div>
        <div className="meta-detail__info">
          <span className="ui-kicker">{leader.leaderKey}</span>
          <h2 className="ui-title">{leader.leaderName}</h2>
          <div className="meta-detail__stats">
            <div className="ui-stat">
              <span>Spiele</span>
              <strong>{leader.games.toLocaleString('de-DE')}</strong>
            </div>
            <div className="ui-stat ui-stat--accent">
              <span>Winrate</span>
              <strong>{formatRate(leader.winRate)}</strong>
            </div>
            <div className="ui-stat">
              <span>als 1.</span>
              <strong>{formatRate(leader.firstWinRate)}</strong>
            </div>
            <div className="ui-stat">
              <span>als 2.</span>
              <strong>{formatRate(leader.secondWinRate)}</strong>
            </div>
          </div>
        </div>
      </section>

      <section className="ui-glass" aria-labelledby="meta-matchups-title">
        <h3 className="ui-heading" id="meta-matchups-title">
          Matchups <span className="ui-badge">{leader.matchups.length}</span>
        </h3>
        <ul className="meta-matchups">
          {leader.matchups.map((matchup) => {
            const opponentCard = matchup.opponentCardId ? cardsById[matchup.opponentCardId] : undefined;
            return (
              <li
                key={matchup.opponentKey}
                className={rowClass(matchup)}
                onMouseEnter={() => onPreview(opponentCard ?? null)}
                onMouseLeave={() => onPreview(null)}
              >
                <span className="meta-thumb" aria-hidden="true">
                  {opponentCard?.card_image ? <img alt="" loading="lazy" src={opponentCard.card_image} /> : null}
                </span>
                <span className="meta-matchup__name">
                  <strong>{matchup.opponentName}</strong>
                  <small>
                    {matchup.games.toLocaleString('de-DE')} Spiele
                    {matchup.games < LOW_DATA_GAMES && ' · wenig Daten'}
                    {matchup.opponentKey === best && ' · bestes Matchup'}
                    {matchup.opponentKey === worst && worst !== best && ' · schwerstes Matchup'}
                  </small>
                </span>
                <span className="meta-matchup__bars">
                  <RateBar label="als 1." rate={matchup.firstWinRate} />
                  <RateBar label="als 2." rate={matchup.secondWinRate} />
                </span>
                <span className="meta-matchup__total">{formatRate(matchup.winRate)}</span>
              </li>
            );
          })}
        </ul>
      </section>
    </div>
  );
}
