import { useEffect, useMemo } from 'react';
import type { CSSProperties, ReactNode } from 'react';

import { formatBountyChange } from '../../../api/leaderboardApi';
import type { GameStateDto, PlayerStateDto } from '../../../types/realtime';
import { playSound } from './sounds';

interface GameOverScreenProps {
  gameState: GameStateDto;
  localPlayer: PlayerStateDto | null;
  opponent: PlayerStateDto | null;
  bountyChange: number | null;
  onRematch?: () => void;
  onLeaveRoom?: () => void;
  // "Fehler melden" and similar actions of the board.
  extraActions?: ReactNode;
  // Why the match ended (e.g. "Gegner hat die Verbindung verloren – Sieg"), empty for a normal end.
  reason?: string;
}

const PARTICLES = 26;

function LeaderChip({ label, player }: { label: string; player: PlayerStateDto | null }) {
  const leader = player?.leader;
  return (
    <div className="fx-game-over__leader">
      {leader?.imageUrl ? <img alt="" src={leader.imageUrl} /> : <span className="fx-game-over__leader-blank" />}
      <div>
        <small>{label}</small>
        <strong>{player?.displayName || '-'}</strong>
        <span>{leader?.name ?? ''}</span>
      </div>
    </div>
  );
}

// "SIEG" with gold particles, "NIEDERLAGE" with a dark veil; opponent, leaders, turns and bounty.
export function GameOverScreen({
  bountyChange,
  extraActions,
  gameState,
  localPlayer,
  onLeaveRoom,
  onRematch,
  opponent,
  reason = '',
}: GameOverScreenProps) {
  const hasWinner = Boolean(gameState.winnerPlayerId);
  const didWin = hasWinner && gameState.winnerPlayerId === localPlayer?.playerId;
  const mood = !hasWinner ? 'draw' : didWin ? 'win' : 'lose';

  useEffect(() => {
    playSound(didWin ? 'win' : 'lose');
  }, [didWin]);

  // Random but stable positions for the confetti.
  const particles = useMemo(
    () =>
      Array.from({ length: PARTICLES }, (_, index) => ({
        '--x': `${Math.round(Math.random() * 100)}%`,
        '--delay': `${(Math.random() * 1.2).toFixed(2)}s`,
        '--drift': `${Math.round((Math.random() - 0.5) * 120)}px`,
        '--size': `${4 + (index % 4) * 2}px`,
      }) as CSSProperties),
    [],
  );

  return (
    <div className={`fx-game-over is-${mood}`} role="dialog" aria-modal="false" aria-label="Spielende">
      <div className="fx-game-over__veil" />
      {mood === 'win' && (
        <div aria-hidden="true" className="fx-game-over__particles">
          {particles.map((style, index) => (
            <span key={index} style={style} />
          ))}
        </div>
      )}
      <section className="fx-game-over__card">
        <h2 className="fx-game-over__title">{mood === 'win' ? 'SIEG' : mood === 'lose' ? 'NIEDERLAGE' : 'UNENTSCHIEDEN'}</h2>
        {reason && <p className="fx-game-over__reason">{reason}</p>}
        <div className="fx-game-over__leaders">
          <LeaderChip label="Du" player={localPlayer} />
          <span className="fx-game-over__vs">vs</span>
          <LeaderChip label="Gegner" player={opponent} />
        </div>
        <dl className="fx-game-over__facts">
          <div>
            <dt>Züge</dt>
            <dd>{gameState.turnNumber}</dd>
          </div>
          <div>
            <dt>Modus</dt>
            <dd>{gameState.isRanked ? 'Ranked' : 'Casual'}</dd>
          </div>
          {bountyChange !== null && (
            <div className={bountyChange >= 0 ? 'is-up' : 'is-down'}>
              <dt>Kopfgeld</dt>
              <dd>{formatBountyChange(bountyChange)}</dd>
            </div>
          )}
        </dl>
        <div className="fx-game-over__actions">
          {onRematch && (
            <button className="fx-game-over__primary" onClick={onRematch} type="button">
              Revanche-Tisch erstellen
            </button>
          )}
          {onLeaveRoom && (
            <button onClick={onLeaveRoom} type="button">
              Zurück zur Lobby
            </button>
          )}
          {extraActions}
        </div>
      </section>
    </div>
  );
}
