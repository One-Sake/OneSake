import type { ChoicePromptDto, GameStateDto, LogEventDto, PlayerStateDto } from '../../../types/realtime';
import type { Box } from './fxDom';
import type { FxQueue } from './fxQueue';
import type { FxSettings } from './fxSettings';

// Everything an animation needs to know about the board.
export interface FxEnv {
  frame: HTMLElement;
  layer: HTMLElement;
  queue: FxQueue;
  settings: FxSettings;
  viewerId: string;
  // Where every card was after the previous render (cards that left the DOM are still known).
  previousBoxes: Map<string, Box>;
}

export type Side = 'player' | 'opponent';

export function sideOf(state: GameStateDto, playerId: string, viewerId: string): Side {
  const viewer = state.players.find((player) => player.playerId === viewerId) ?? state.players[0];
  return playerId === viewer?.playerId ? 'player' : 'opponent';
}

export function playersBySide(state: GameStateDto, viewerId: string): Record<Side, PlayerStateDto | undefined> {
  const viewer = state.players.find((player) => player.playerId === viewerId) ?? state.players[0];
  return {
    player: viewer,
    opponent: state.players.find((player) => player.playerId !== viewer?.playerId),
  };
}

// Two snapshots of the same match: which animations show what happened in between?
export function snapshotFx(previous: GameStateDto, next: GameStateDto, env: FxEnv): void {
  void previous;
  void next;
  void env;
}

// Log entries add what a snapshot cannot show (who attacked with which power, triggers, effects).
export function logFx(event: LogEventDto, state: GameStateDto | null, env: FxEnv): void {
  void event;
  void state;
  void env;
}

// A new prompt for us (e.g. a [Trigger] decision shows the card big in the middle).
export function promptFx(prompt: ChoicePromptDto, env: FxEnv): void {
  void prompt;
  void env;
}
