import { useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties } from 'react';

import type { ChoicePromptDto, GameStateDto } from '../../../types/realtime';
import { getLeaderColors } from '../leaderColors';
import {
  FACE_KEYS,
  SIM_DT,
  TABLE_HEIGHT,
  TABLE_WIDTH,
  quatToMatrix,
  simulateDiceRoll,
  type DiceSimResult,
  type FaceKey,
} from './diceSim';
import { playDiceImpact } from './diceSound';

const TURN_ORDER_KIND = 'TURN_ORDER_DECISION';
const TABLE_TILT_DEG = 28;
const SEEN_STORAGE_KEY = 'onesake.dice.seen';
const SOUND_STORAGE_KEY = 'onesake.dice.sound';

type Stage = 'hidden' | 'intro' | 'rolling' | 'tie' | 'result' | 'decide' | 'announce';

// How long each step is shown (ms).
const INTRO_MS = 900;
const TIE_MS = 1300;
const RESULT_MS = 1300;
const ANNOUNCE_MS = 1600;

// CSS transform of each local face (sim +y is css -y, see toCssMatrix).
const FACE_TRANSFORMS: Record<FaceKey, string> = {
  '+x': 'rotateY(90deg)',
  '-x': 'rotateY(-90deg)',
  '+y': 'rotateX(90deg)',
  '-y': 'rotateX(-90deg)',
  '+z': '',
  '-z': 'rotateY(180deg)',
};

// Which of the 9 grid cells show a pip.
const PIP_CELLS: Record<number, number[]> = {
  1: [4],
  2: [0, 8],
  3: [0, 4, 8],
  4: [0, 2, 6, 8],
  5: [0, 2, 4, 6, 8],
  6: [0, 2, 3, 5, 6, 8],
};

interface DiceRollProps {
  gameState: GameStateDto | null;
  currentPrompt: ChoicePromptDto | null;
  canSubmitChoice: boolean;
  pending: boolean;
  onSubmitChoice: (option: string) => void;
}

function readSeen(): string[] {
  try {
    const value = window.sessionStorage.getItem(SEEN_STORAGE_KEY);
    return value ? (JSON.parse(value) as string[]) : [];
  } catch {
    return [];
  }
}

function markSeen(key: string): void {
  try {
    const next = [...readSeen().filter((entry) => entry !== key), key].slice(-20);
    window.sessionStorage.setItem(SEEN_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Without storage a remount may roll again; the result stays the same.
  }
}

function loadSoundOn(): boolean {
  try {
    return window.localStorage.getItem(SOUND_STORAGE_KEY) !== 'off';
  } catch {
    return true;
  }
}

function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
}

// Sim coordinates -> CSS matrix3d. The viewer's own die always comes from the bottom,
// so for the second player the whole table is turned by 180 degrees (flip = -1).
function toCssMatrix(frame: Float32Array, offset: number, unit: number, flip: number): string {
  const r = quatToMatrix([frame[offset + 3], frame[offset + 4], frame[offset + 5], frame[offset + 6]]);
  const rowSign = [flip, flip, 1];
  // css axes: x = sim x, y = -sim y, z = sim z (mirror m), plus the optional 180 degree turn.
  const m = [1, -1, 1];
  const c = (row: number, col: number): number => m[row] * m[col] * rowSign[row] * r[row * 3 + col];
  const tx = flip * frame[offset] * unit;
  const ty = -flip * frame[offset + 1] * unit;
  const tz = frame[offset + 2] * unit;

  return `matrix3d(${c(0, 0)},${c(1, 0)},${c(2, 0)},0,${c(0, 1)},${c(1, 1)},${c(2, 1)},0,${c(0, 2)},${c(1, 2)},${c(2, 2)},0,${tx},${ty},${tz},1)`;
}

function DieFace({ face, value }: { face: FaceKey; value: number }) {
  const cells = PIP_CELLS[value] ?? [];
  return (
    <span className="dice-face" style={{ transform: `${FACE_TRANSFORMS[face]} translateZ(calc(var(--die-size) / 2))` }}>
      {Array.from({ length: 9 }, (_, index) => (
        <span key={index} className={cells.includes(index) ? 'dice-pip is-on' : 'dice-pip'} />
      ))}
    </span>
  );
}

// Overlay with both dice rolling over the board, the result and the turn order decision.
export function DiceRoll({ canSubmitChoice, currentPrompt, gameState, onSubmitChoice, pending }: DiceRollProps) {
  const roll = gameState?.turnOrderRoll ?? null;
  const roomCode = gameState?.roomCode ?? '';
  const viewerId = gameState?.viewerPlayerId ?? '';
  const rounds = roll?.rounds ?? [];
  const rollKey = roll && rounds.length > 0 ? `${roomCode}:${rounds.map((round) => round.animationSeed).join('-')}` : '';
  const decision = roll?.decision ?? null;

  const [stage, setStage] = useState<Stage>('hidden');
  const [roundIndex, setRoundIndex] = useState(0);
  const [unit, setUnit] = useState(48);
  const [soundOn, setSoundOn] = useState(loadSoundOn);
  const rootRef = useRef<HTMLDivElement>(null);
  const dieRefs = useRef<(HTMLDivElement | null)[]>([]);
  const shadowRefs = useRef<(HTMLDivElement | null)[]>([]);
  const isVisible = stage !== 'hidden';
  const soundOnRef = useRef(soundOn);
  soundOnRef.current = soundOn;

  // Die 0 belongs to rolls[0], die 1 to rolls[1] (same order on both clients).
  const sims = useMemo<DiceSimResult[]>(
    () =>
      rounds.map((round) =>
        simulateDiceRoll(round.animationSeed, [round.rolls[0]?.value ?? 1, round.rolls[1]?.value ?? 1]),
      ),
    // rollKey changes exactly when the rounds change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [rollKey],
  );

  const playerIds = rounds[0]?.rolls.map((entry) => entry.playerId) ?? [];
  const flip = playerIds[1] === viewerId ? -1 : 1;
  const players = gameState?.players ?? [];
  const nameOf = (playerId: string): string =>
    players.find((player) => player.playerId === playerId)?.displayName || 'Gegner';
  const colorOf = (playerId: string): string =>
    getLeaderColors(players.find((player) => player.playerId === playerId)?.leader?.color).primary;

  // Start (or skip) the sequence when a new roll arrives.
  useEffect(() => {
    if (!rollKey) {
      setStage('hidden');
      return;
    }

    if (decision) {
      // Joined/remounted after the decision: no animation at all.
      setStage('hidden');
      return;
    }

    const alreadySeen = readSeen().includes(rollKey);
    markSeen(rollKey);
    if (alreadySeen || prefersReducedMotion()) {
      setRoundIndex(rounds.length - 1);
      setStage('decide');
      return;
    }

    setRoundIndex(0);
    setStage('intro');
    // Only a new roll restarts the sequence.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rollKey]);

  // The decision arrived: show who starts, then hide the overlay.
  useEffect(() => {
    if (decision) {
      setStage((current) => (current === 'hidden' ? 'hidden' : 'announce'));
    }
  }, [decision]);

  // Timed steps.
  useEffect(() => {
    const next: Partial<Record<Stage, [number, () => void]>> = {
      intro: [INTRO_MS, () => setStage('rolling')],
      tie: [
        TIE_MS,
        () => {
          setRoundIndex((index) => index + 1);
          setStage('rolling');
        },
      ],
      result: [RESULT_MS, () => setStage('decide')],
      announce: [ANNOUNCE_MS, () => setStage('hidden')],
    };
    const entry = next[stage];
    if (!entry) {
      return undefined;
    }

    const timeoutId = window.setTimeout(entry[1], entry[0]);
    return () => window.clearTimeout(timeoutId);
  }, [stage]);

  // Board size -> die size (coordinates are relative to the board, so mobile gets smaller dice).
  useEffect(() => {
    const root = rootRef.current;
    if (!root) {
      return undefined;
    }

    const update = (): void => {
      const bounds = root.getBoundingClientRect();
      setUnit(Math.max(22, Math.min(bounds.width / TABLE_WIDTH, bounds.height / TABLE_HEIGHT) * 0.82));
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(root);
    return () => observer.disconnect();
  }, [isVisible]);

  // Drawing: during "rolling" a requestAnimationFrame loop writes the transforms directly
  // (no React render per frame). In every other stage the dice rest on their last frame.
  useEffect(() => {
    const sim = sims[roundIndex];
    if (!sim || stage === 'hidden' || stage === 'intro') {
      return undefined;
    }

    const draw = (frameIndex: number): void => {
      for (let die = 0; die < 2; die += 1) {
        const offset = Math.min(frameIndex, sim.frameCount - 1) * 7;
        const frame = sim.frames[die];
        const dieElement = dieRefs.current[die];
        const shadow = shadowRefs.current[die];
        if (dieElement) {
          dieElement.style.transform = toCssMatrix(frame, offset, unit, flip);
        }

        if (shadow) {
          const height = Math.max(0, frame[offset + 2] - 0.5);
          shadow.style.transform = `translate3d(${flip * frame[offset] * unit}px, ${-flip * frame[offset + 1] * unit}px, 1px) scale(${1 + height * 0.18})`;
          shadow.style.opacity = String(Math.max(0.15, 0.7 - height * 0.14));
        }
      }
    };

    if (stage !== 'rolling') {
      draw(sim.frameCount - 1);
      return undefined;
    }

    let frameId = 0;
    let nextImpact = 0;
    const startedAt = performance.now();
    const tick = (now: number): void => {
      const time = (now - startedAt) / 1000;
      draw(Math.floor(time / SIM_DT));

      while (nextImpact < sim.impacts.length && sim.impacts[nextImpact].time <= time) {
        if (soundOnRef.current) {
          playDiceImpact(sim.impacts[nextImpact].strength);
        }
        nextImpact += 1;
      }

      if (time >= sim.duration) {
        const round = rounds[roundIndex];
        const isTie = round && round.rolls[0]?.value === round.rolls[1]?.value;
        setStage(isTie && roundIndex < rounds.length - 1 ? 'tie' : 'result');
        return;
      }

      frameId = window.requestAnimationFrame(tick);
    };

    frameId = window.requestAnimationFrame(tick);
    return () => window.cancelAnimationFrame(frameId);
    // rounds is derived from rollKey, which already rebuilds sims.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stage, roundIndex, sims, unit, flip]);

  if (stage === 'hidden' || !roll) {
    return null;
  }

  const round = rounds[roundIndex];
  const sim = sims[roundIndex];
  const isFinalRound = roundIndex === rounds.length - 1;
  const showResult = isFinalRound && (stage === 'result' || stage === 'decide' || stage === 'announce');
  const winnerId = roll.winnerPlayerId;
  const winnerValue = round?.rolls.find((entry) => entry.playerId === winnerId)?.value ?? 0;
  const loserValue = round?.rolls.find((entry) => entry.playerId !== winnerId)?.value ?? 0;
  const isViewerWinner = winnerId === viewerId;
  const myPrompt =
    currentPrompt?.kind === TURN_ORDER_KIND && currentPrompt.playerId === viewerId ? currentPrompt : null;
  const firstName = roll.firstPlayerId ? nameOf(roll.firstPlayerId) : '';

  let message = 'Würfeln um den ersten Zug…';
  if (stage === 'tie') {
    message = 'Gleichstand – nochmal!';
  } else if (showResult) {
    message = `${nameOf(winnerId)} gewinnt mit ${winnerValue} : ${loserValue}`;
  }

  if (stage === 'announce') {
    message = `${firstName} beginnt`;
  }

  const toggleSound = (): void => {
    const next = !soundOn;
    setSoundOn(next);
    try {
      window.localStorage.setItem(SOUND_STORAGE_KEY, next ? 'on' : 'off');
    } catch {
      // Optional preference.
    }
  };

  const tableStyle = {
    '--die-size': `${unit}px`,
    width: `${TABLE_WIDTH * unit}px`,
    height: `${TABLE_HEIGHT * unit}px`,
    transform: `rotateX(${TABLE_TILT_DEG}deg)`,
  } as CSSProperties;

  return (
    <div className={`dice-overlay dice-overlay--${stage}`} ref={rootRef} role="status" aria-live="polite">
      <div className="dice-overlay__message">
        <strong key={`${stage}-${roundIndex}`}>{message}</strong>
        {stage === 'decide' && !decision && (
          isViewerWinner ? (
            <div className="match-choice-bar dice-overlay__choices" role="group" aria-label="Wer beginnt?">
              <button
                className="match-choice-bar__button is-primary"
                disabled={!myPrompt || pending || !canSubmitChoice}
                onClick={() => onSubmitChoice('GO_FIRST')}
                type="button"
              >
                ⚔ Ich beginne
              </button>
              <button
                className="match-choice-bar__button"
                disabled={!myPrompt || pending || !canSubmitChoice}
                onClick={() => onSubmitChoice('GO_SECOND')}
                type="button"
              >
                Gegner beginnt
              </button>
            </div>
          ) : (
            <span className="dice-overlay__waiting">{nameOf(winnerId)} entscheidet, wer beginnt…</span>
          )
        )}
      </div>

      <button
        aria-label={soundOn ? 'Würfel-Sound ausschalten' : 'Würfel-Sound einschalten'}
        aria-pressed={soundOn}
        className="dice-overlay__sound"
        onClick={toggleSound}
        title={soundOn ? 'Sound an' : 'Sound aus'}
        type="button"
      >
        {soundOn ? '🔊' : '🔇'}
      </button>

      {stage !== 'intro' && sim && round && (
        <div className="dice-scene">
          <div className={stage === 'tie' ? 'dice-table is-leaving' : 'dice-table'} style={tableStyle}>
            {[0, 1].map((die) => {
              const playerId = playerIds[die] ?? '';
              const isWinner = showResult && playerId === winnerId;
              const color = colorOf(playerId);
              const finalOffset = (sim.frameCount - 1) * 7;
              const finalX = flip * sim.frames[die][finalOffset] * unit;
              const finalY = -flip * sim.frames[die][finalOffset + 1] * unit;

              return (
                <div key={`${roundIndex}-${die}`} className="dice-slot">
                  <div
                    className={isWinner ? 'dice-shadow is-winner' : 'dice-shadow'}
                    ref={(element) => {
                      shadowRefs.current[die] = element;
                    }}
                  />
                  <div
                    className={isWinner ? 'dice-die is-winner' : 'dice-die'}
                    ref={(element) => {
                      dieRefs.current[die] = element;
                    }}
                    style={{ '--die-color': color } as CSSProperties}
                  >
                    <span className="dice-core" />
                    <span className="dice-core" style={{ transform: 'rotateY(90deg)' }} />
                    <span className="dice-core" style={{ transform: 'rotateX(90deg)' }} />
                    {FACE_KEYS.map((face) => (
                      <DieFace key={face} face={face} value={sim.faceValues[die][face]} />
                    ))}
                  </div>
                  {(showResult || stage === 'tie') && (
                    <span
                      className={isWinner ? 'dice-value is-winner' : 'dice-value'}
                      style={{
                        transform: `translate3d(${finalX}px, ${finalY}px, ${unit * 1.7}px) rotateX(${-TABLE_TILT_DEG}deg)`,
                      }}
                    >
                      {round.rolls[die]?.value}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
