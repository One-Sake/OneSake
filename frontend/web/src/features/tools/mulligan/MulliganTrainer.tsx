import { useState } from 'react';

import { HandIcon, RefreshIcon } from '../../../components/ui/Icons';
import type { Deck } from '../../../types/decks';
import { getTotalCards } from '../../deckbuilder/utils/deckValidation';
import { useCardImages } from '../../profile/useCardImages';
import { drawOpeningHand } from './mulliganLogic';
import type { MulliganDrawResult, MulliganSessionStats } from './mulliganTypes';

interface MulliganTrainerProps {
  deck: Deck | null;
}

const emptyStats: MulliganSessionStats = {
  handsDrawn: 0,
  keeps: 0,
  mulligans: 0,
};

function createEmptyDrawResult(): MulliganDrawResult {
  return {
    hand: [],
    metrics: {
      cardCount: 0,
      uniqueCardIds: 0,
      cardsWithQuantity: [],
      counter2000Count: 0,
      eventCount: 0,
      lowCostCount: 0,
      characterCount: 0,
    },
    error: '',
  };
}

function getKeepRate(stats: MulliganSessionStats): number {
  const decisions = stats.keeps + stats.mulligans;
  if (decisions === 0) {
    return 0;
  }

  return Math.round((stats.keeps / decisions) * 100);
}

function ToolHeader({ description }: { description: string }) {
  return (
    <div className="tool-card__header">
      <span className="tool-card__icon" aria-hidden="true">
        <HandIcon />
      </span>
      <div>
        <h2 className="ui-heading">Mulligan Trainer</h2>
        <p className="tools-muted">{description}</p>
      </div>
    </div>
  );
}

export function MulliganTrainer({ deck }: MulliganTrainerProps) {
  const [drawResult, setDrawResult] = useState<MulliganDrawResult>(() => createEmptyDrawResult());
  const [stats, setStats] = useState<MulliganSessionStats>(emptyStats);
  const cardImagesById = useCardImages(drawResult.hand.map((card) => card.cardId));

  const totalCards = deck ? getTotalCards(deck.cards) : 0;
  const canDraw = Boolean(deck && totalCards >= 5);

  const drawHand = (): void => {
    if (!deck) {
      return;
    }

    const nextDraw = drawOpeningHand(deck);
    setDrawResult(nextDraw);

    if (!nextDraw.error) {
      setStats((currentStats) => ({
        ...currentStats,
        handsDrawn: currentStats.handsDrawn + 1,
      }));
    }
  };

  const recordDecision = (decision: 'keep' | 'mulligan'): void => {
    setStats((currentStats) => ({
      ...currentStats,
      keeps: decision === 'keep' ? currentStats.keeps + 1 : currentStats.keeps,
      mulligans:
        decision === 'mulligan' ? currentStats.mulligans + 1 : currentStats.mulligans,
    }));
    drawHand();
  };

  const resetSession = (): void => {
    setDrawResult(createEmptyDrawResult());
    setStats(emptyStats);
  };

  if (!deck) {
    return (
      <section className="ui-glass tool-card mulligan-trainer">
        <ToolHeader description="Draw opening hands and practice keep or mulligan decisions." />
      </section>
    );
  }

  const metricCards = [
    { label: 'Cards', value: drawResult.metrics.cardCount },
    { label: 'Unique IDs', value: drawResult.metrics.uniqueCardIds },
    { label: '2k Counters', value: drawResult.metrics.counter2000Count },
    { label: 'Events', value: drawResult.metrics.eventCount },
    { label: 'Low Cost', value: drawResult.metrics.lowCostCount },
    { label: 'Characters', value: drawResult.metrics.characterCount },
  ];

  const statCards = [
    { label: 'Hands Drawn', value: stats.handsDrawn },
    { label: 'Keeps', value: stats.keeps },
    { label: 'Mulligans', value: stats.mulligans },
  ];
  const keepRate = getKeepRate(stats);

  return (
    <section className="ui-glass tool-card mulligan-trainer">
      <div className="mulligan-header">
        <ToolHeader description={`Practice opening hands · ${deck.name} / ${totalCards} cards`} />
        <button
          className="ui-btn-primary ui-btn-primary--compact"
          disabled={!canDraw}
          onClick={drawHand}
          type="button"
        >
          <span>Draw Hand</span>
        </button>
      </div>

      {!canDraw && (
        <div className="status-panel status-panel--error">
          This deck needs at least 5 cards before you can draw an opening hand.
        </div>
      )}

      {drawResult.error && (
        <div className="status-panel status-panel--error">{drawResult.error}</div>
      )}

      {drawResult.hand.length > 0 && (
        <>
          <div className="mulligan-hand">
            {drawResult.hand.map((card) => (
              <article key={card.cardId} className="mulligan-card">
                <div className="mulligan-card__art" aria-hidden="true">
                  {cardImagesById[card.cardId] ? (
                    <img alt="" loading="lazy" src={cardImagesById[card.cardId]} />
                  ) : (
                    card.cardId
                  )}
                </div>
                <div className="mulligan-card__body">
                  <strong>{card.name}</strong>
                  <span>
                    {card.cardId} / x{card.quantity}
                  </span>
                </div>
                <div className="tag-list mulligan-card__tags">
                  {card.color && <span className="tag-chip">{card.color}</span>}
                  {card.type && <span className="tag-chip">{card.type}</span>}
                  {typeof card.cost === 'number' && (
                    <span className="tag-chip">Cost {card.cost}</span>
                  )}
                  {typeof card.counter === 'number' && card.counter > 0 && (
                    <span className="tag-chip">Counter {card.counter}</span>
                  )}
                </div>
              </article>
            ))}
          </div>

          <div className="mulligan-metrics">
            {metricCards.map((metric) => (
              <div key={metric.label} className="ui-stat">
                <span>{metric.label}</span>
                <strong>{metric.value}</strong>
              </div>
            ))}
          </div>

          <div className="mulligan-actions">
            <button className="ui-btn-secondary" onClick={() => recordDecision('keep')} type="button">
              Keep
            </button>
            <button className="ui-btn-secondary" onClick={() => recordDecision('mulligan')} type="button">
              Mulligan
            </button>
          </div>
        </>
      )}

      <div className="mulligan-stats">
        {statCards.map((stat) => (
          <div key={stat.label} className="ui-stat">
            <span>{stat.label}</span>
            <strong>{stat.value}</strong>
          </div>
        ))}
        <div className="ui-stat ui-stat--accent mulligan-stats__rate">
          <span>Keep Rate</span>
          <strong>{keepRate}%</strong>
          <span className="ui-meter" aria-hidden="true">
            <span style={{ width: `${keepRate}%` }} />
          </span>
        </div>
      </div>

      <button className="ui-btn-ghost mulligan-reset" onClick={resetSession} type="button">
        <RefreshIcon />
        Reset Session
      </button>
    </section>
  );
}
