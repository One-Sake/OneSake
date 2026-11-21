import { getFxSettings } from './fxSettings';

// Animations run one after another, in the order of the game events. The board state is already
// correct when an animation starts - animations are only a layer on top (flying copies, numbers).
//
// Every step returns Web Animations (element.animate, only transform/opacity), so a step can be
// finished at once (skip) and the speed can change. Steps never block input: a click or an own
// action jumps to the end of everything that is queued.

export interface FxContext {
  // Duration factor: 1 = normal, 0.5 = twice as fast.
  speed: number;
  // Only highlights, no movement (animations "off" or prefers-reduced-motion).
  reduced: boolean;
  // True while skipping: no sounds (otherwise everything would sound at once).
  silent: boolean;
  // Lets a step register clean-up work (remove copies, show hidden cards) that also runs on skip.
  onCleanup: (cleanup: () => void) => void;
}

// A step plays its animations and returns them (or nothing for instant steps).
export type FxStep = (context: FxContext) => Animation[] | void;

interface QueuedStep {
  step: FxStep;
  // Steps of the same group (e.g. several DON!! cards) start together.
  group?: string;
  // Runs when the step is over or skipped (e.g. shows a card that was hidden while waiting).
  cleanup?: () => void;
}

// From this many waiting steps on, everything runs at double speed.
const RUSH_THRESHOLD = 4;
// A step never takes longer than this (safety net if an animation never finishes).
const MAX_STEP_MS = 1600;

export class FxQueue {
  private readonly steps: QueuedStep[] = [];
  private running: Animation[] = [];
  private cleanups: (() => void)[] = [];
  private busy = false;
  private watchdog: number | undefined;
  // Each step run has its own id, so a late 'finished' of a skipped step changes nothing.
  private runId = 0;

  enqueue(step: FxStep, group?: string, cleanup?: () => void): void {
    this.steps.push({ step, group, cleanup });
    if (!this.busy) {
      this.next();
    }
  }

  get pending(): number {
    return this.steps.length + (this.busy ? 1 : 0);
  }

  // Jumps to the end: running and waiting animations finish at once (their clean-up runs).
  skipAll(): void {
    this.finishRunning();
    while (this.steps.length > 0) {
      const entry = this.steps.shift()!;
      const cleanups: (() => void)[] = [];
      try {
        const animations =
          entry.step({ speed: 0.001, reduced: true, silent: true, onCleanup: (cleanup) => cleanups.push(cleanup) }) ?? [];
        animations.forEach((animation) => {
          try {
            animation.finish();
          } catch {
            animation.cancel();
          }
        });
      } catch {
        // Ignore: skipping must always work.
      }

      if (entry.cleanup) {
        cleanups.push(entry.cleanup);
      }

      cleanups.forEach((cleanup) => {
        try {
          cleanup();
        } catch {
          // The element may be gone already.
        }
      });
    }
  }

  // Leaving the board: nothing may stay behind.
  dispose(): void {
    this.skipAll();
  }

  private speedFactor(): number {
    const mode = getFxSettings().animations;
    const base = mode === 'fast' ? 0.5 : 1;
    return this.steps.length >= RUSH_THRESHOLD ? base * 0.5 : base;
  }

  private next(): void {
    const first = this.steps.shift();
    if (!first) {
      this.busy = false;
      return;
    }

    this.busy = true;
    const id = ++this.runId;
    const batch = [first];
    while (first.group && this.steps[0]?.group === first.group) {
      batch.push(this.steps.shift()!);
    }

    const settings = getFxSettings();
    const context: FxContext = {
      speed: this.speedFactor(),
      reduced: settings.animations === 'off',
      silent: false,
      onCleanup: (cleanup) => this.cleanups.push(cleanup),
    };

    const animations: Animation[] = [];
    for (const entry of batch) {
      if (entry.cleanup) {
        this.cleanups.push(entry.cleanup);
      }

      try {
        animations.push(...(entry.step(context) ?? []));
      } catch (error) {
        // An animation must never break the match.
        console.warn('[OneSake] fx step failed', error);
      }
    }

    this.running = animations;
    if (animations.length === 0) {
      this.done(id);
      return;
    }

    let open = animations.length;
    const finishOne = () => {
      open -= 1;
      if (open === 0) {
        this.done(id);
      }
    };

    animations.forEach((animation) => {
      animation.finished.then(finishOne, finishOne);
    });
    this.watchdog = window.setTimeout(() => this.finishRunning(), MAX_STEP_MS);
  }

  private finishRunning(): void {
    const running = this.running;
    this.running = [];
    running.forEach((animation) => {
      try {
        animation.finish();
      } catch {
        // An animation with an infinite duration cannot finish: cancel it.
        animation.cancel();
      }
    });

    if (this.busy) {
      this.done(this.runId);
    }
  }

  private done(id: number): void {
    if (id !== this.runId || !this.busy) {
      return;
    }

    this.runId += 1;
    window.clearTimeout(this.watchdog);
    this.running = [];
    const cleanups = this.cleanups;
    this.cleanups = [];
    cleanups.forEach((cleanup) => {
      try {
        cleanup();
      } catch {
        // Ignore: the element may be gone already.
      }
    });

    // Next step in a new task, so a finished animation never runs the next one synchronously.
    this.busy = false;
    window.setTimeout(() => {
      if (!this.busy) {
        this.next();
      }
    }, 0);
  }
}
