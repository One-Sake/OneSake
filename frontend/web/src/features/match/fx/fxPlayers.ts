import type { CardInstanceDto, ChoicePromptDto, GameStateDto, LogEventDto, PlayerStateDto } from '../../../types/realtime';
import {
  animate,
  boxOf,
  cardElement,
  cardSize,
  centerBox,
  flash,
  fly,
  markRecent,
  NORMAL_MS,
  offset,
  place,
  zoneElement,
  type Box,
} from './fxDom';
import type { FxContext, FxQueue } from './fxQueue';
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

// ---------- Card positions in a snapshot ----------

type Zone = 'hand' | 'character' | 'leader' | 'stage' | 'trash';

interface SideIndex {
  zones: Map<string, Zone>;
  cards: Map<string, CardInstanceDto>;
}

function indexPlayer(player: PlayerStateDto | undefined): SideIndex {
  const zones = new Map<string, Zone>();
  const cards = new Map<string, CardInstanceDto>();
  const add = (card: CardInstanceDto | null | undefined, zone: Zone) => {
    if (card) {
      zones.set(card.instanceId, zone);
      cards.set(card.instanceId, card);
    }
  };

  player?.handCards.forEach((card) => add(card, 'hand'));
  player?.boardCards.forEach((card) => add(card, 'character'));
  add(player?.leader, 'leader');
  add(player?.stage, 'stage');
  player?.trashCards.forEach((card) => add(card, 'trash'));
  return { zones, cards };
}

const isField = (zone: Zone | undefined) => zone === 'character' || zone === 'stage' || zone === 'leader';

// ---------- Building blocks ----------

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (char) => `&#${char.charCodeAt(0)};`);
}

// Face of a card from its data (for cards that are not on the board as an element, e.g. in the trash).
function faceHtml(card: CardInstanceDto): string {
  // The name stays visible below the image until the image is loaded.
  const name = `<span class="fx-card__name">${escapeHtml(card.name)}</span>`;
  return card.imageUrl ? `${name}<img alt="" draggable="false" src="${escapeHtml(card.imageUrl)}">` : name;
}

function faceCopy(env: FxEnv, context: FxContext, box: Box, card: CardInstanceDto, extraClass = ''): HTMLElement {
  return place(env.layer, context, box, `fx-card fx-card--face ${extraClass}`.trim(), faceHtml(card));
}

function zoneBox(env: FxEnv, side: Side, kind: string): Box | null {
  return boxOf(env.layer, zoneElement(env.frame, side, kind));
}

// A card-sized box in the middle of a zone (deck, life, hands).
function cardBoxIn(env: FxEnv, side: Side, kind: string): Box {
  const size = cardSize(env.layer);
  const zone = zoneBox(env, side, kind);
  if (!zone) {
    // Fallback: the edge of the board on that side.
    return { x: (env.layer.clientWidth - size.w) / 2, y: side === 'player' ? env.layer.clientHeight - size.h : 0, ...size };
  }

  return { x: zone.x + (zone.w - size.w) / 2, y: zone.y + (zone.h - size.h) / 2, ...size };
}

function revealBox(env: FxEnv): Box {
  const size = cardSize(env.layer);
  const scale = Math.min(2.6, (env.layer.clientHeight * 0.55) / size.h);
  return centerBox(env.layer, size.w * scale, size.h * scale);
}

// Shows a card big in the middle (0.6 s), then flies it to its place.
// from: where it comes from, to: where it ends (the real card may already be there).
function revealAndFly(
  env: FxEnv,
  card: CardInstanceDto,
  from: Box,
  to: Box,
  options: { label?: string; glow?: boolean; target?: HTMLElement | null; recent?: boolean },
): void {
  const target = options.target ?? null;
  if (target) {
    target.dataset.fxHidden = '1';
  }

  env.queue.enqueue(
    (context) => {
      if (context.reduced) {
        flash(target, options.glow ? 'gold' : 'good');
        return [];
      }

      const center = revealBox(env);
      const element = faceCopy(env, context, to, card, options.glow ? 'fx-card--glow' : '');
      if (options.label) {
        const label = document.createElement('span');
        label.className = 'fx-card__label';
        label.textContent = options.label;
        element.appendChild(label);
      }

      return [
        animate(
          element,
          [
            { transform: offset(from, to), opacity: 0.4, offset: 0 },
            { transform: offset(center, to), opacity: 1, offset: 0.22 },
            { transform: offset(center, to), opacity: 1, offset: 0.72 },
            { transform: 'none', opacity: 1, offset: 1 },
          ],
          context,
          // A reveal must stay readable: at most 30 % faster, even when many animations wait.
          (1000 * Math.max(context.speed, 0.7)) / context.speed,
        ),
      ];
    },
    undefined,
    () => {
      if (target) {
        delete target.dataset.fxHidden;
        if (options.recent) {
          markRecent(target);
        }
      }
    },
  );
}

// A copy flies from "from" to the real card (hidden until it lands).
function flyToCard(env: FxEnv, from: Box, target: HTMLElement, group?: string): void {
  target.dataset.fxHidden = '1';
  env.queue.enqueue(
    (context) => {
      const to = boxOf(env.layer, target);
      if (context.reduced || !to) {
        return [];
      }

      const element = place(env.layer, context, to, 'fx-card');
      const clone = target.cloneNode(true) as HTMLElement;
      clone.removeAttribute('data-fx-card');
      clone.removeAttribute('data-fx-hidden');
      element.appendChild(clone);
      return [fly(element, from, to, context)];
    },
    group,
    () => {
      delete target.dataset.fxHidden;
    },
  );
}

// Card backs fly from one box to another (opponent draws, cards we cannot see).
function flyBacks(env: FxEnv, from: Box, to: Box, count: number, group: string, variant: 'card' | 'don' = 'card'): void {
  for (let index = 0; index < count; index += 1) {
    env.queue.enqueue((context) => {
      if (context.reduced) {
        return [];
      }

      const element = place(env.layer, context, to, `fx-card fx-card--back fx-card--${variant}`);
      return [fly(element, from, to, context, NORMAL_MS, { delay: index * 70 })];
    }, group);
  }
}

function dragLandingRunning(): boolean {
  return Boolean(document.querySelector('.match-drag-ghost.is-landing, .match-drag-ghost.is-landed'));
}

// ---------- Snapshot diff ----------

let groupSeq = 0;

// Two snapshots of the same match: which animations show what happened in between?
export function snapshotFx(previous: GameStateDto, next: GameStateDto, env: FxEnv): void {
  const before = playersBySide(previous, env.viewerId);
  const after = playersBySide(next, env.viewerId);
  const sides: Side[] = ['player', 'opponent'];
  const indexBefore = { player: indexPlayer(before.player), opponent: indexPlayer(before.opponent) };
  const indexAfter = { player: indexPlayer(after.player), opponent: indexPlayer(after.opponent) };
  groupSeq += 1;

  for (const side of sides) {
    const old = indexBefore[side];
    const now = indexAfter[side];
    const lifeLost = Math.max(0, (before[side]?.lifeCount ?? 0) - (after[side]?.lifeCount ?? 0));

    // Played cards: new on the field (Character or Stage).
    now.zones.forEach((zone, id) => {
      if ((zone !== 'character' && zone !== 'stage') || isField(old.zones.get(id))) {
        return;
      }

      const card = now.cards.get(id)!;
      const target = cardElement(env.frame, id);
      const to = boxOf(env.layer, target);
      if (!target || !to) {
        return;
      }

      if (side === 'player') {
        // Drag & drop shows its own landing; a click gets the same landing from the hand.
        if (old.zones.get(id) === 'hand' && dragLandingRunning()) {
          return;
        }

        const from = env.previousBoxes.get(id) ?? cardBoxIn(env, 'player', old.zones.get(id) === 'hand' ? 'hand' : 'life');
        flyToCard(env, from, target);
        return;
      }

      revealAndFly(env, card, cardBoxIn(env, 'opponent', 'hand'), to, { target, recent: true });
    });

    // Events: new in the trash, were in a hand (or a hidden hand) before.
    now.zones.forEach((zone, id) => {
      const card = now.cards.get(id)!;
      const from = old.zones.get(id);
      if (zone !== 'trash' || card.category !== 'Event' || (from && from !== 'hand')) {
        return;
      }

      const trash = cardBoxIn(env, side, 'trash');
      const start = env.previousBoxes.get(id) ?? cardBoxIn(env, side, 'hand');
      revealAndFly(env, card, start, trash, { glow: true });
    });

    // Draws: new hand cards that were nowhere visible (cards from the Life area are shown elsewhere).
    if (side === 'player') {
      const drawn = [...now.zones.entries()].filter(([id, zone]) => zone === 'hand' && !old.zones.has(id)).map(([id]) => id);
      const fromDeck = drawn.slice(lifeLost);
      const deck = cardBoxIn(env, 'player', 'deck');
      fromDeck.forEach((id) => {
        const target = cardElement(env.frame, id);
        if (target) {
          flyToCard(env, deck, target, `draw-${groupSeq}`);
        }
      });
    } else {
      const handGrowth = (after.opponent?.handCount ?? 0) - (before.opponent?.handCount ?? 0);
      const deckDrop = (before.opponent?.deckCount ?? 0) - (after.opponent?.deckCount ?? 0);
      // Drawn cards leave the deck and enter the hand (Life cards enter the hand without the deck).
      const draws = Math.min(deckDrop, handGrowth, 5);
      if (draws > 0) {
        flyBacks(env, cardBoxIn(env, 'opponent', 'deck'), cardBoxIn(env, 'opponent', 'hand'), draws, `odraw-${groupSeq}`);
      }
    }
  }
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
