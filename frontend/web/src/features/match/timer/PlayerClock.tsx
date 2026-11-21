import { useEffect, useRef, useState } from 'react';

import type { TimerDto } from '../../../types/realtime';
import { formatClock, usePlayerClock } from './turnClock';
import { isTimerSoundOn, playTimerTick, setTimerSoundOn } from './timerSound';

interface PlayerClockProps {
  timer: TimerDto | null | undefined;
  playerId: string | undefined;
}

const RING_RADIUS = 15;
const RING_LENGTH = 2 * Math.PI * RING_RADIUS;

// Ring with the seconds next to a player's name, plus the time bank as a small number.
export function PlayerClock({ timer, playerId }: PlayerClockProps) {
  const clock = usePlayerClock(timer, playerId);
  const bank = playerId ? timer?.timeBankMs[playerId] : undefined;

  if (!clock) {
    // Not this player's turn: only the time bank.
    return timer && bank !== undefined && timer.usesTimeBank ? (
      <span className="match-clock match-clock--idle" title="Zeitbank">
        <small className="match-clock__bank">⌛ {formatClock(bank)}</small>
      </span>
    ) : null;
  }

  return (
    <span
      className={`match-clock is-${clock.level}${clock.usingBank ? ' is-bank' : ''}`}
      role="timer"
      aria-label={`${clock.usingBank ? 'Zeitbank' : 'Zeit'}: ${Math.ceil(clock.remainingMs / 1000)} Sekunden`}
    >
      <svg className="match-clock__ring" viewBox="0 0 36 36" aria-hidden="true">
        <circle className="match-clock__track" cx="18" cy="18" r={RING_RADIUS} />
        <circle
          className="match-clock__progress"
          cx="18"
          cy="18"
          r={RING_RADIUS}
          strokeDasharray={RING_LENGTH}
          strokeDashoffset={RING_LENGTH * (1 - clock.fraction)}
        />
        <text x="18" y="18" dominantBaseline="central" textAnchor="middle">
          {formatClock(clock.remainingMs)}
        </text>
      </svg>
      {clock.usingBank ? (
        <small className="match-clock__bank is-active">Zeitbank</small>
      ) : (
        clock.kind !== 'MULLIGAN' &&
        clock.kind !== 'TURN_ORDER' && <small className="match-clock__bank">⌛ {formatClock(clock.bankMs)}</small>
      )}
    </span>
  );
}

// Bar above the action buttons for our own clock, with the sound switch.
export function OwnTimerBar({ timer, playerId }: PlayerClockProps) {
  const clock = usePlayerClock(timer, playerId);
  const [soundOn, setSoundOn] = useState(isTimerSoundOn);
  const lastTickRef = useRef(0);
  const seconds = clock ? Math.ceil(clock.remainingMs / 1000) : 0;
  const level = clock?.level;

  // One tick per second in the last 5 seconds (higher in the very last ones).
  useEffect(() => {
    if (!soundOn || level !== 'danger' || seconds <= 0 || seconds === lastTickRef.current) {
      return;
    }

    lastTickRef.current = seconds;
    playTimerTick(seconds <= 2);
  }, [level, seconds, soundOn]);

  if (!clock) {
    return null;
  }

  const label =
    clock.kind === 'RESPONSE' ? 'Antwortzeit' : clock.kind === 'MULLIGAN' ? 'Mulligan' : clock.kind === 'TURN_ORDER' ? 'Entscheidung' : 'Zugzeit';

  return (
    <div className={`match-timer-bar is-${clock.level}${clock.usingBank ? ' is-bank' : ''}`}>
      <div className="match-timer-bar__fill" style={{ transform: `scaleX(${clock.fraction})` }} />
      <span className="match-timer-bar__label">
        {clock.usingBank ? 'Zeitbank' : label} · {formatClock(clock.remainingMs)}
      </span>
      <button
        aria-label={soundOn ? 'Tick-Geräusch aus' : 'Tick-Geräusch an'}
        aria-pressed={soundOn}
        className="match-timer-bar__sound"
        onClick={() => {
          setTimerSoundOn(!soundOn);
          setSoundOn(!soundOn);
        }}
        title={soundOn ? 'Tick-Geräusch aus' : 'Tick-Geräusch an'}
        type="button"
      >
        {soundOn ? '🔊' : '🔇'}
      </button>
    </div>
  );
}
