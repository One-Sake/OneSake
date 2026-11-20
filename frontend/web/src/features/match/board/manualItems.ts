import type { CardInstanceDto, ManualActionDto } from '../../../types/realtime';

// Manual mode: which actions fit a card or zone right now. The server checks everything again.

export type ManualTarget =
  | { kind: 'hand'; card: CardInstanceDto }
  // Leader, Character or Stage on the field.
  | { kind: 'field'; card: CardInstanceDto; own: boolean }
  | { kind: 'trash'; card: CardInstanceDto }
  | { kind: 'deck' }
  | { kind: 'life' }
  | { kind: 'don' };

export type ManualItem =
  | { kind: 'heading'; label: string }
  | { kind: 'action'; label: string; action: ManualActionDto; disabled?: boolean }
  // A value the player types in (draw N cards, +N power ...).
  | {
      kind: 'number';
      label: string;
      defaultValue: number;
      min: number;
      max: number;
      step: number;
      toAction: (value: number) => ManualActionDto;
    }
  // Opens something on the client (card details).
  | { kind: 'open'; label: string; run: () => void };

export interface ManualContext {
  characterCount: number;
  hasStage: boolean;
  isBattle: boolean;
  activeDon: number;
  restedDon: number;
  donDeck: number;
  deckCount: number;
  lifeCount: number;
  leaderId: string;
}

// The player's turn, or an open decision where effects can happen.
export const MANUAL_DECISION_KINDS = ['EFFECT_CHOICE', 'COUNTER_STEP', 'TRIGGER_DECISION', 'MANUAL_EFFECT'];
export const RUNNING_PHASES = ['Refresh', 'Draw', 'Don', 'Main', 'End'];
const MAX_CHARACTERS = 5;

function action(label: string, manualAction: ManualActionDto, disabled = false): ManualItem {
  return { kind: 'action', label, action: manualAction, disabled };
}

function move(label: string, card: CardInstanceDto, toZone: string, toPosition?: string, disabled = false): ManualItem {
  return action(label, { kind: 'MOVE_CARD', cardInstanceId: card.instanceId, toZone, toPosition }, disabled);
}

function playToField(card: CardInstanceDto, context: ManualContext): ManualItem[] {
  if (card.category === 'Character') {
    return [move('Aufs Feld (ohne Kosten)', card, 'character', undefined, context.characterCount >= MAX_CHARACTERS)];
  }

  if (card.category === 'Stage') {
    return [move('Aufs Feld (ohne Kosten)', card, 'stage', undefined, context.hasStage)];
  }

  return [];
}

function powerItems(card: CardInstanceDto, context: ManualContext): ManualItem[] {
  const power = (amount: number, duration: 'turn' | 'battle' = 'turn'): ManualActionDto => ({
    kind: 'MODIFY_POWER',
    cardInstanceId: card.instanceId,
    amount,
    duration,
  });

  const items: ManualItem[] = [
    { kind: 'heading', label: 'Power (dieser Zug)' },
    action('+1000', power(1000)),
    action('+2000', power(2000)),
    action('−1000', power(-1000)),
    action('−2000', power(-2000)),
    {
      kind: 'number',
      label: 'Eigener Wert',
      defaultValue: 1000,
      min: -20000,
      max: 20000,
      step: 1000,
      toAction: (value) => power(value),
    },
  ];

  if (context.isBattle) {
    items.push(
      { kind: 'heading', label: 'Power (dieser Kampf)' },
      action('+1000 Kampf', power(1000, 'battle')),
      action('+2000 Kampf', power(2000, 'battle')),
      action('−2000 Kampf', power(-2000, 'battle')),
    );
  }

  return items;
}

function costItems(card: CardInstanceDto): ManualItem[] {
  const cost = (amount: number): ManualActionDto => ({
    kind: 'MODIFY_COST',
    cardInstanceId: card.instanceId,
    amount,
    duration: 'turn',
  });

  return [{ kind: 'heading', label: 'Kosten (dieser Zug)' }, action('Kosten +1', cost(1)), action('Kosten −1', cost(-1)), action('Kosten −2', cost(-2))];
}

function restItem(card: CardInstanceDto): ManualItem {
  return card.isRested
    ? action('Aktiv setzen', { kind: 'SET_ACTIVE', cardInstanceId: card.instanceId })
    : action('Rest setzen', { kind: 'SET_REST', cardInstanceId: card.instanceId });
}

function ownFieldItems(card: CardInstanceDto, context: ManualContext): ManualItem[] {
  const isLeader = card.category === 'Leader';
  const isCharacter = card.category === 'Character';
  const items: ManualItem[] = [];

  if (!isLeader) {
    items.push(
      { kind: 'heading', label: 'Karte' },
      move('In den Trash', card, 'trash'),
      move('Auf die Hand', card, 'hand'),
      move('Oben aufs Deck', card, 'deck', 'top'),
      move('Unter das Deck', card, 'deck', 'bottom'),
    );
  }

  if (isCharacter) {
    items.push(action('K.O.', { kind: 'KO_CARD', cardInstanceId: card.instanceId }));
  }

  items.push(restItem(card));

  if (isLeader || isCharacter) {
    items.push(
      action(
        'DON!! anlegen',
        { kind: 'DON_ATTACH', cardInstanceId: card.instanceId, fromZone: context.activeDon > 0 ? 'active' : 'rested' },
        context.activeDon + context.restedDon === 0,
      ),
    );
    items.push(...powerItems(card, context));
  }

  if (isCharacter) {
    items.push(...costItems(card));
  }

  return items;
}

function opponentFieldItems(card: CardInstanceDto, context: ManualContext): ManualItem[] {
  const isLeader = card.category === 'Leader';
  const isCharacter = card.category === 'Character';
  const items: ManualItem[] = [{ kind: 'heading', label: 'Gegnerische Karte' }];

  if (isCharacter) {
    items.push(action('K.O.', { kind: 'KO_CARD', cardInstanceId: card.instanceId }));
  }

  if (!isLeader) {
    items.push(
      action('Auf die Hand des Besitzers', { kind: 'RETURN_TO_HAND', cardInstanceId: card.instanceId }),
      action('Unter das Deck des Besitzers', { kind: 'RETURN_TO_DECK', cardInstanceId: card.instanceId, toPosition: 'bottom' }),
    );
  }

  items.push(restItem(card));

  if (isLeader || isCharacter) {
    items.push(...powerItems(card, context));
  }

  if (isCharacter) {
    items.push(...costItems(card));
  }

  return items;
}

export function getManualItems(target: ManualTarget, context: ManualContext): ManualItem[] {
  switch (target.kind) {
    case 'hand': {
      const card = target.card;
      return [
        { kind: 'heading', label: 'Handkarte' },
        action('Abwerfen', { kind: 'TRASH_FROM_HAND', cardInstanceId: card.instanceId }),
        action('Offen zeigen', { kind: 'REVEAL_CARD', cardInstanceId: card.instanceId }),
        ...playToField(card, context),
        move('Oben aufs Deck', card, 'deck', 'top'),
        move('Unter das Deck', card, 'deck', 'bottom'),
        move('Oben auf die Life (verdeckt)', card, 'life', 'top'),
      ];
    }

    case 'field':
      return target.own ? ownFieldItems(target.card, context) : opponentFieldItems(target.card, context);

    case 'trash': {
      const card = target.card;
      return [
        { kind: 'heading', label: 'Karte im Trash' },
        move('Auf die Hand', card, 'hand'),
        ...playToField(card, context),
        move('Oben aufs Deck', card, 'deck', 'top'),
        move('Unter das Deck', card, 'deck', 'bottom'),
      ];
    }

    case 'deck':
      return [
        { kind: 'heading', label: 'Deck' },
        {
          kind: 'number',
          label: 'Karten ziehen',
          defaultValue: 1,
          min: 1,
          max: 10,
          step: 1,
          toAction: (amount) => ({ kind: 'DRAW', amount }),
        },
        {
          kind: 'number',
          label: 'Oberste N ansehen',
          defaultValue: 3,
          min: 1,
          max: 10,
          step: 1,
          toAction: (amount) => ({ kind: 'LOOK_TOP', amount }),
        },
        action('Deck durchsuchen', { kind: 'SEARCH_DECK' }, context.deckCount === 0),
        action('Deck mischen', { kind: 'SHUFFLE_DECK' }),
      ];

    case 'life':
      return [
        { kind: 'heading', label: 'Life' },
        action('Oberste Life-Karte auf die Hand', { kind: 'LIFE_TO_HAND' }, context.lifeCount === 0),
      ];

    case 'don':
      return [
        { kind: 'heading', label: 'DON!!' },
        action('1 DON!! hinzufügen (aktiv)', { kind: 'DON_ADD', amount: 1, toZone: 'active' }, context.donDeck === 0),
        action('1 DON!! hinzufügen (rest)', { kind: 'DON_ADD', amount: 1, toZone: 'rested' }, context.donDeck === 0),
        action('1 DON!! zurück ins DON!!-Deck', { kind: 'DON_RETURN', amount: 1 }, context.activeDon + context.restedDon === 0),
        action('1 DON!! rest setzen', { kind: 'DON_SET_REST', amount: 1 }, context.activeDon === 0),
        action('1 DON!! aktiv setzen', { kind: 'DON_SET_ACTIVE', amount: 1 }, context.restedDon === 0),
        action(
          '1 DON!! an den Leader anlegen',
          { kind: 'DON_ATTACH', cardInstanceId: context.leaderId, fromZone: context.activeDon > 0 ? 'active' : 'rested' },
          !context.leaderId || context.activeDon + context.restedDon === 0,
        ),
      ];

    default:
      return [];
  }
}
