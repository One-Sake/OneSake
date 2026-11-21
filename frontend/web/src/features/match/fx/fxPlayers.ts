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
  vibrate,
  zoneElement,
  type Box,
} from './fxDom';
import type { FxContext, FxQueue } from './fxQueue';
import type { FxSettings } from './fxSettings';
import { playSound, type SoundName } from './sounds';

// Sounds start with their animation step (not while skipping).
function sound(context: FxContext, name: SoundName): void {
  if (!context.silent) {
    playSound(name);
  }
}

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
      sound(context, options.glow ? 'event' : 'play');
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
      sound(context, group?.startsWith('draw') ? 'draw' : 'play');
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

// A Life card turns over on its way into the hand.
function flipToCard(env: FxEnv, from: Box, target: HTMLElement): void {
  target.dataset.fxHidden = '1';
  env.queue.enqueue(
    (context) => {
      sound(context, 'life');
      const to = boxOf(env.layer, target);
      if (context.reduced || !to) {
        return [];
      }

      const element = place(env.layer, context, to, 'fx-card');
      const clone = target.cloneNode(true) as HTMLElement;
      clone.removeAttribute('data-fx-card');
      clone.removeAttribute('data-fx-hidden');
      element.appendChild(clone);
      return [
        animate(
          element,
          [
            { transform: `${offset(from, to)} rotateY(180deg)`, opacity: 0.6 },
            { transform: `${offset(from, to)} translateY(-20px) rotateY(90deg)`, opacity: 1, offset: 0.35 },
            { transform: 'none', opacity: 1 },
          ],
          context,
          480,
        ),
      ];
    },
    undefined,
    () => {
      delete target.dataset.fxHidden;
    },
  );
}

// A card shown big in the middle with a label (e.g. "TRIGGER"), then it fades out.
function revealOnly(env: FxEnv, card: CardInstanceDto | null, from: Box, label: string): void {
  env.queue.enqueue((context) => {
    sound(context, 'event');
    const center = revealBox(env);
    const element = card
      ? faceCopy(env, context, center, card, 'fx-card--glow')
      : place(env.layer, context, center, 'fx-card fx-card--back fx-card--glow');
    const tag = document.createElement('span');
    tag.className = 'fx-card__label fx-card__label--trigger';
    tag.textContent = label;
    element.appendChild(tag);
    if (context.reduced) {
      return [animate(element, [{ opacity: 1 }, { opacity: 1, offset: 0.8 }, { opacity: 0 }], context, 700)];
    }

    return [
      animate(
        element,
        [
          { transform: `${offset(from, center)} rotateY(180deg)`, opacity: 0.5 },
          { transform: 'none', opacity: 1, offset: 0.3 },
          { transform: 'scale(1.03)', opacity: 1, offset: 0.8 },
          { transform: 'scale(1.1)', opacity: 0 },
        ],
        context,
        (1000 * Math.max(context.speed, 0.7)) / context.speed,
      ),
    ];
  });
}

// Card backs fly from one box to another (opponent draws, cards we cannot see).
function flyBacks(env: FxEnv, from: Box, to: Box, count: number, group: string, variant: 'card' | 'don' = 'card'): void {
  for (let index = 0; index < count; index += 1) {
    env.queue.enqueue((context) => {
      if (index === 0) {
        sound(context, variant === 'don' ? 'don' : group.startsWith('olife') ? 'life' : 'draw');
      }

      if (context.reduced) {
        return [];
      }

      const element = place(env.layer, context, to, `fx-card fx-card--back fx-card--${variant}`);
      return [fly(element, from, to, context, NORMAL_MS, { delay: index * 70 })];
    }, group);
  }
}

// A short text that rises over a box and fades out ("+2000", "7000 ⚔ 5000", shield).
function popText(env: FxEnv, context: FxContext, box: Box, text: string, className: string, ms = 700): Animation {
  const width = Math.max(box.w * 1.6, 90);
  const element = place(
    env.layer,
    context,
    { x: box.x + box.w / 2 - width / 2, y: box.y + box.h * 0.25, w: width, h: 30 },
    `fx-pop ${className}`,
  );
  element.textContent = text;
  return animate(
    element,
    [
      { transform: 'translateY(8px) scale(0.7)', opacity: 0, offset: 0 },
      { transform: 'translateY(-6px) scale(1.08)', opacity: 1, offset: 0.25 },
      { transform: 'translateY(-14px) scale(1)', opacity: 1, offset: 0.75 },
      { transform: 'translateY(-24px) scale(1)', opacity: 0, offset: 1 },
    ],
    context,
    ms,
  );
}

// The number in a popup counts from one value to another (e.g. the power after a counter).
function countUp(element: HTMLElement, from: number, to: number, ms: number, format: (value: number) => string): void {
  const started = performance.now();
  const step = (now: number) => {
    if (!element.isConnected) {
      return;
    }

    const progress = Math.min(1, (now - started) / ms);
    element.textContent = format(Math.round((from + (to - from) * progress) / 100) * 100);
    if (progress < 1) {
      requestAnimationFrame(step);
    }
  };
  requestAnimationFrame(step);
}

function shake(element: HTMLElement | null, context: FxContext): Animation[] {
  if (!element) {
    return [];
  }

  sound(context, 'hit');

  flash(element, 'hit', 600);
  if (context.reduced) {
    return [];
  }

  return [
    animate(
      element,
      [
        { transform: 'translateX(0)' },
        { transform: 'translateX(-7px) rotate(-2deg)' },
        { transform: 'translateX(6px) rotate(2deg)' },
        { transform: 'translateX(-4px)' },
        { transform: 'translateX(3px)' },
        { transform: 'translateX(0)' },
      ],
      context,
      380,
      { easing: 'linear', fill: 'none' },
    ),
  ];
}

// K.O.: the card breaks into pieces, then a pale ghost flies into the trash.
function shatter(env: FxEnv, context: FxContext, card: CardInstanceDto, box: Box, trash: Box): Animation[] {
  sound(context, 'ko');
  const animations: Animation[] = [];
  const columns = 3;
  const rows = 4;
  const image = card.imageUrl ? `url("${card.imageUrl.replace(/"/g, '%22')}")` : '';
  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const piece = place(
        env.layer,
        context,
        { x: box.x + (column * box.w) / columns, y: box.y + (row * box.h) / rows, w: box.w / columns, h: box.h / rows },
        'fx-shard',
      );
      if (image) {
        piece.style.backgroundImage = image;
        piece.style.backgroundSize = `${box.w}px ${box.h}px`;
        piece.style.backgroundPosition = `${(-column * box.w) / columns}px ${(-row * box.h) / rows}px`;
      }

      const dx = (column - (columns - 1) / 2) * 26 + (Math.random() - 0.5) * 18;
      const dy = (row - (rows - 1) / 2) * 20 + 18 + Math.random() * 14;
      const turn = (Math.random() - 0.5) * 140;
      animations.push(
        animate(
          piece,
          [
            { transform: 'none', opacity: 1 },
            { transform: `translate(${dx}px, ${dy}px) rotate(${turn}deg) scale(0.6)`, opacity: 0 },
          ],
          context,
          420,
          { easing: 'cubic-bezier(0.2, 0.6, 0.4, 1)' },
        ),
      );
    }
  }

  const ghost = faceCopy(env, context, trash, card, 'fx-card--ghost');
  animations.push(
    animate(
      ghost,
      [
        { transform: offset(box, trash), opacity: 0 },
        { transform: offset(box, trash), opacity: 0.55, offset: 0.35 },
        { transform: 'none', opacity: 0 },
      ],
      context,
      520,
      { delay: 120 },
    ),
  );
  return animations;
}

// Gold ring that pulses over a card (an effect of this card starts).
function pulse(env: FxEnv, context: FxContext, box: Box): Animation {
  const ring = place(env.layer, context, { x: box.x - 6, y: box.y - 6, w: box.w + 12, h: box.h + 12 }, 'fx-pulse');
  return animate(
    ring,
    [
      { transform: 'scale(0.85)', opacity: 0 },
      { transform: 'scale(1)', opacity: 1, offset: 0.35 },
      { transform: 'scale(1.18)', opacity: 0 },
    ],
    context,
    600,
  );
}

// A thin golden line from an effect's source to one of its targets.
function line(env: FxEnv, context: FxContext, from: Box, to: Box): Animation {
  const x1 = from.x + from.w / 2;
  const y1 = from.y + from.h / 2;
  const x2 = to.x + to.w / 2;
  const y2 = to.y + to.h / 2;
  const length = Math.hypot(x2 - x1, y2 - y1);
  const element = place(env.layer, context, { x: x1, y: y1 - 2, w: length, h: 4 }, 'fx-line');
  element.style.transformOrigin = '0 50%';
  const angle = Math.atan2(y2 - y1, x2 - x1);
  return animate(
    element,
    [
      { transform: `rotate(${angle}rad) scaleX(0)`, opacity: 1 },
      { transform: `rotate(${angle}rad) scaleX(1)`, opacity: 1, offset: 0.5 },
      { transform: `rotate(${angle}rad) scaleX(1)`, opacity: 0 },
    ],
    context,
    600,
    { delay: 120 },
  );
}

// Power badge: short swell, green for more, red for less.
function powerPop(element: HTMLElement | null, up: boolean, context: FxContext): Animation[] {
  const badge = element?.querySelector<HTMLElement>('.match-card-stat--power');
  if (!badge) {
    return [];
  }

  badge.dataset.fxPower = up ? 'up' : 'down';
  context.onCleanup(() => {
    window.setTimeout(() => delete badge.dataset.fxPower, 400);
  });
  if (context.reduced) {
    return [];
  }

  return [animate(badge, [{ transform: 'scale(1)' }, { transform: 'scale(1.45)', offset: 0.35 }, { transform: 'scale(1)' }], context, 420, { fill: 'none' })];
}

// Cards whose power changed or that were K.O.'d just now (targets for effect lines).
let recentlyChanged: { ids: string[]; at: number } = { ids: [], at: 0 };

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
      const fromLife = drawn.slice(0, lifeLost);
      const fromDeck = drawn.slice(lifeLost);
      const life = cardBoxIn(env, 'player', 'life');
      fromLife.forEach((id) => {
        const target = cardElement(env.frame, id);
        if (target) {
          flipToCard(env, life, target);
        }
      });
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

      const lifeToHand = Math.min(lifeLost, Math.max(0, handGrowth - Math.max(0, draws)));
      if (lifeToHand > 0) {
        flyBacks(env, cardBoxIn(env, 'opponent', 'life'), cardBoxIn(env, 'opponent', 'hand'), lifeToHand, `olife-${groupSeq}`);
      }
    }

    // DON!! given to a card: a DON!! card flies from the cost area onto it.
    now.cards.forEach((card, id) => {
      const oldCard = old.cards.get(id);
      const added = (card.attachedDon ?? 0) - (oldCard?.attachedDon ?? 0);
      if (!oldCard || !isField(now.zones.get(id)) || added <= 0) {
        return;
      }

      env.queue.enqueue((context) => {
        const target = cardElement(env.frame, id);
        const to = boxOf(env.layer, target);
        if (!target || !to) {
          return [];
        }

        if (side === 'opponent') {
          markRecent(target);
        }

        sound(context, 'don');
        const animations = [popText(env, context, to, `+${added * 1000}`, 'fx-pop--counter', 650)];
        if (context.reduced) {
          flash(target, 'gold');
          return animations;
        }

        const size = cardSize(env.layer);
        const donBox = { x: to.x + to.w / 2 - size.w * 0.3, y: to.y + to.h / 2 - size.h * 0.3, w: size.w * 0.6, h: size.h * 0.6 };
        const from = zoneBox(env, side, 'donArea') ?? donBox;
        const don = place(env.layer, context, donBox, 'fx-card fx-card--back fx-card--don');
        animations.push(
          animate(
            don,
            [
              { transform: offset({ ...from, w: donBox.w, h: donBox.h }, donBox), opacity: 1 },
              { transform: 'none', opacity: 1, offset: 0.75 },
              { transform: 'scale(0.6)', opacity: 0 },
            ],
            context,
            420,
          ),
        );
        return animations;
      });
    });

    // DON!! phase: new DON!! cards fly from the DON!! deck into the cost area.
    const donBefore = (before[side]?.activeDonCount ?? 0) + (before[side]?.restedDonCount ?? 0);
    const donAfter = (after[side]?.activeDonCount ?? 0) + (after[side]?.restedDonCount ?? 0);
    const donFromDeck = Math.min(donAfter - donBefore, (before[side]?.donDeckCount ?? 0) - (after[side]?.donDeckCount ?? 0), 3);
    if (donFromDeck > 0) {
      const size = cardSize(env.layer);
      const small = (box: Box) => ({ x: box.x + (box.w - size.w * 0.6) / 2, y: box.y + (box.h - size.h * 0.6) / 2, w: size.w * 0.6, h: size.h * 0.6 });
      const deckZone = zoneBox(env, side, 'donDeck');
      const area = zoneBox(env, side, 'donArea');
      if (deckZone && area) {
        flyBacks(env, small(deckZone), small(area), donFromDeck, `don-${side}-${groupSeq}`, 'don');
      }
    }

    // Power changes on the field (effects, counters, DON!!): the badge swells green or red.
    const changed: string[] = [];
    now.cards.forEach((card, id) => {
      const oldCard = old.cards.get(id);
      if (!oldCard || !isField(now.zones.get(id)) || !isField(old.zones.get(id))) {
        return;
      }

      const difference = (card.power ?? 0) - (oldCard.power ?? 0);
      if (difference !== 0) {
        changed.push(id);
        env.queue.enqueue((context) => powerPop(cardElement(env.frame, id), difference > 0, context), `power-${groupSeq}`);
      }
    });
    if (changed.length > 0) {
      recentlyChanged = { ids: [...recentlyChanged.ids, ...changed], at: performance.now() };
    }

    // K.O.: a Character went from the field into the trash.
    now.zones.forEach((zone, id) => {
      if (zone !== 'trash' || old.zones.get(id) !== 'character') {
        return;
      }

      const card = now.cards.get(id)!;
      const box = env.previousBoxes.get(id);
      const trash = cardBoxIn(env, side, 'trash');
      if (box) {
        recentlyChanged = { ids: [...recentlyChanged.ids, id], at: performance.now() };
        env.queue.enqueue((context) => {
          if (context.reduced) {
            sound(context, 'ko');
            return [];
          }

          return shatter(env, context, card, box, trash);
        });
      }
    });

    // Damage: the Leader shakes, flashes red, and the phone vibrates when we are hit.
    if (lifeLost > 0) {
      const leader = after[side]?.leader;
      env.queue.enqueue((context) => {
        if (side === 'player') {
          vibrate(30, env.settings.vibration);
        }

        return shake(cardElement(env.frame, leader?.instanceId ?? ''), context);
      });
    }
  }

  battleFx(previous, next, env);
}

// Attack, block and counter, read from the battle in the snapshot.
function battleFx(previous: GameStateDto, next: GameStateDto, env: FxEnv): void {
  const before = previous.battle ?? null;
  const battle = next.battle ?? null;
  if (!battle) {
    return;
  }

  const isNewBattle = !before || before.attackerInstanceId !== battle.attackerInstanceId;
  if (isNewBattle) {
    const attackerSide = sideOf(next, battle.attackerPlayerId, env.viewerId);
    env.queue.enqueue((context) => {
      const attacker = cardElement(env.frame, battle.attackerInstanceId);
      const target = cardElement(env.frame, battle.targetInstanceId);
      const from = boxOf(env.layer, attacker);
      const to = boxOf(env.layer, target);
      if (!attacker || !from || !to) {
        return [];
      }

      if (attackerSide === 'opponent') {
        markRecent(attacker);
      }

      sound(context, 'attack');

      const animations = [popText(env, context, to, `${battle.attackerPower} ⚔ ${battle.targetPower}`, 'fx-pop--battle', 900)];
      if (context.reduced) {
        flash(attacker, 'gold');
        return animations;
      }

      // Lunge: the attacker jumps part of the way towards the target and back.
      const dx = (to.x + to.w / 2 - (from.x + from.w / 2)) * 0.35;
      const dy = (to.y + to.h / 2 - (from.y + from.h / 2)) * 0.35;
      animations.push(
        animate(
          attacker,
          [
            { transform: 'translate(0, 0)' },
            { transform: `translate(${dx}px, ${dy}px) scale(1.08)`, offset: 0.45 },
            { transform: 'translate(0, 0)' },
          ],
          context,
          380,
          { fill: 'none', easing: 'cubic-bezier(0.3, 0, 0.3, 1)' },
        ),
      );
      return animations;
    });
    return;
  }

  // Block: the target changed to the blocker.
  if (before && before.targetInstanceId !== battle.targetInstanceId) {
    env.queue.enqueue((context) => {
      const blocker = cardElement(env.frame, battle.targetInstanceId);
      const blockerBox = boxOf(env.layer, blocker);
      const oldTarget = boxOf(env.layer, cardElement(env.frame, before.targetInstanceId));
      if (!blocker || !blockerBox) {
        return [];
      }

      sound(context, 'block');
      const animations = [popText(env, context, blockerBox, '🛡', 'fx-pop--shield', 700)];
      if (!context.reduced && oldTarget) {
        const dx = (oldTarget.x - blockerBox.x) * 0.3;
        const dy = (oldTarget.y - blockerBox.y) * 0.3;
        animations.push(
          animate(
            blocker,
            [{ transform: 'translate(0, 0)' }, { transform: `translate(${dx}px, ${dy}px)`, offset: 0.4 }, { transform: 'translate(0, 0)' }],
            context,
            420,
            { fill: 'none' },
          ),
        );
      } else {
        flash(blocker, 'good');
      }

      return animations;
    });
  }

  // Counter: "+2000" rises over the target and the power counts up.
  const bonus = battle.counterBonus - (before?.counterBonus ?? 0);
  if (before && bonus > 0) {
    env.queue.enqueue((context) => {
      const target = cardElement(env.frame, battle.targetInstanceId);
      const box = boxOf(env.layer, target);
      if (!box) {
        return [];
      }

      flash(target, 'good');
      sound(context, 'counter');
      const plus = popText(env, context, box, `+${bonus}`, 'fx-pop--counter', 800);
      const power = place(env.layer, context, { x: box.x, y: box.y + box.h - 26, w: box.w, h: 24 }, 'fx-pop fx-pop--power');
      countUp(power, before.targetPower, battle.targetPower, 500 * Math.max(context.speed, 0.3), (value) => String(value));
      return [plus, animate(power, [{ opacity: 0 }, { opacity: 1, offset: 0.15 }, { opacity: 1, offset: 0.85 }, { opacity: 0 }], context, 800)];
    });
  }
}

// A card with this name somewhere on the board (for its image). While a [Trigger] resolves, the
// card itself is in no zone - another copy, or at least the name, is shown.
function findCardByName(state: GameStateDto, name: string): CardInstanceDto {
  const cards = state.players.flatMap((player) => [
    ...player.boardCards,
    ...player.trashCards,
    ...player.handCards,
    ...(player.stage ? [player.stage] : []),
  ]);
  return cards.find((card) => card.name === name) ?? { instanceId: '', cardId: '', name };
}

// Log entries add what a snapshot cannot show (who attacked with which power, triggers, effects).
export function logFx(event: LogEventDto, state: GameStateDto | null, env: FxEnv): void {
  if (!state) {
    return;
  }

  if (event.type.startsWith('EFFECT') || event.type === 'ACTIVATE') {
    effectFx(event, state, env);
    return;
  }

  // "Bob reveals Carrot and activates its [Trigger]." - our own trigger is shown with the prompt.
  const trigger = event.type === 'TRIGGER' ? /^(.*) reveals (.+) and activates its \[Trigger\]/.exec(event.text) : null;
  if (trigger) {
    const players = playersBySide(state, env.viewerId);
    if (players.opponent && trigger[1] === players.opponent.displayName) {
      revealOnly(env, findCardByName(state, trigger[2]), cardBoxIn(env, 'opponent', 'life'), 'TRIGGER');
    }
  }
}

// "Nami: Alice draws 1 card." / "Alice activates Hyogoro." - the source pulses, lines go to the
// cards the effect named or just changed.
function effectFx(event: LogEventDto, state: GameStateDto, env: FxEnv): void {
  if (/conditions are not met|cannot be paid|must be resolved manually/.test(event.text)) {
    return;
  }

  const sourceName = event.type === 'ACTIVATE'
    ? /activates (.+)\.$/.exec(event.text)?.[1]
    : /^([^:]+):/.exec(event.text)?.[1];
  if (!sourceName) {
    return;
  }

  const field = state.players.flatMap((player) => [player.leader, player.stage, ...player.boardCards]).filter(
    (card): card is CardInstanceDto => Boolean(card),
  );
  const source = field.find((card) => card.name === sourceName);
  if (!source) {
    return;
  }

  const rest = event.text.slice(sourceName.length);
  const named = field.filter((card) => card.instanceId !== source.instanceId && rest.includes(card.name)).map((card) => card.instanceId);
  const recent = performance.now() - recentlyChanged.at < 800 ? recentlyChanged.ids : [];
  const targets = [...new Set([...named, ...recent])].filter((id) => id !== source.instanceId).slice(0, 4);

  env.queue.enqueue((context) => {
    const element = cardElement(env.frame, source.instanceId);
    const from = boxOf(env.layer, element);
    if (!from) {
      return [];
    }

    if (context.reduced) {
      flash(element, 'gold');
      return [];
    }

    const animations = [pulse(env, context, from)];
    targets.forEach((id) => {
      const to = boxOf(env.layer, cardElement(env.frame, id));
      if (to) {
        animations.push(line(env, context, from, to));
      }
    });
    return animations;
  });
}

// A new prompt for us (e.g. a [Trigger] decision shows the card big in the middle).
export function promptFx(prompt: ChoicePromptDto, env: FxEnv): void {
  if (prompt.kind === 'TRIGGER_DECISION') {
    revealOnly(env, prompt.cards?.[0] ?? null, cardBoxIn(env, 'player', 'life'), 'TRIGGER');
  }
}
