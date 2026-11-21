import { useEffect, useRef, useState } from 'react';
import type { CSSProperties, ReactNode } from 'react';

import type {
  CardInstanceDto,
  ChoicePromptDto,
  GameStateDto,
  ManualActionDto,
  PlayerStateDto,
} from '../../../types/realtime';
import { boardZones, CHARACTER_SLOT_COUNT } from './boardLayout';
import { BoardSlot, CardStack, ManualDotsButton } from './BoardSlot';
import { BugReportDialog } from './BugReportDialog';
import type { BoardSide, BoardZoneDefinition } from './boardTypes';
import { cardArtStyle } from './cardArt';
import { CardInspectOverlay } from './CardInspectOverlay';
import { DragGhost } from './DragGhost';
import type { CardPile } from './CardInspectOverlay';
import { EFFECT_PROMPT_KINDS } from '../choiceOptions';
import { getLeaderColors } from '../leaderColors';
import { EffectChoiceOverlay } from './EffectChoiceOverlay';
import { MatchCardView } from './MatchCardView';
import { MatchHand } from './MatchHand';
import { ManualEffectBanner } from './ManualEffectBanner';
import { ManualConfirm, ManualMenu } from './ManualMenu';
import type { ManualMenuState } from './ManualMenu';
import { getManualItems, MANUAL_DECISION_KINDS, RUNNING_PHASES } from './manualItems';
import type { ManualContext, ManualItem, ManualTarget } from './manualItems';
import { ManualViewDialog } from './ManualViewDialog';
import { TriggerOrderOverlay } from './TriggerOrderOverlay';
import { getGameOverReason } from '../matchFormatters';
import { OfflineBadge } from './OfflineBadge';
import { OwnTimerBar, PlayerClock } from '../timer/PlayerClock';
import { useBoardMetrics } from './useBoardMetrics';
import { useHandDrag } from './useHandDrag';
import type { DragItem, DropTarget } from './useHandDrag';

const MAX_CHARACTERS = CHARACTER_SLOT_COUNT;
const PREVIEW_DELAY_MS = 350;
const MAX_OPPONENT_HAND_BACKS = 12;
// Below this card width the stack badges only show the number (no "DECK" / "LIFE" text).
const SMALL_CARD_W = 60;
const DRAG_NOTICE_MS = 2600;

// What the player is doing right now on the board.
type BoardMode =
  | { kind: 'idle' }
  | { kind: 'attack'; attackerId: string }
  | { kind: 'replace'; handCardId: string };

interface MatchBoardProps {
  gameState: GameStateDto | null;
  activePlayerId: string;
  canSubmitChoice: boolean;
  currentPrompt: ChoicePromptDto | null;
  joinedRoomCode?: string;
  onSubmitChoice: (option: string, selectedCardInstanceId?: string, targetCardInstanceId?: string) => void;
  pending: boolean;
  // Shown in the middle of the board, between both Character rows (e.g. the Keep/Mulligan prompt).
  midlineContent?: ReactNode;
  // Called after a short hover delay with the hovered face-up card, and with null when the mouse leaves.
  onPreviewCard?: (card: CardInstanceDto | null) => void;
  // Manual mode: resolve card effects by hand. Resolves to false when the server rejected the action.
  onManualAction?: (action: ManualActionDto) => Promise<boolean>;
  // ORDER_TRIGGERS: the chosen order of simultaneous effects.
  onSubmitOrder?: (orderedIds: string[]) => void;
}

// Drag & drop from the field in manual mode: where the card goes.
const FIELD_DROP_LABELS: Record<string, string> = {
  trash: 'in den Trash legen',
  hand: 'auf die Hand nehmen',
  deck: 'unter das Deck legen',
};

function getVisiblePlayers(gameState: GameStateDto | null): Record<BoardSide, PlayerStateDto | null> {
  const players = gameState?.players ?? [];
  const viewerPlayer =
    players.find((player) => player.playerId === gameState?.viewerPlayerId) ?? players[0] ?? null;
  const opponentPlayer =
    players.find((player) => player.playerId !== viewerPlayer?.playerId) ?? null;

  return {
    player: viewerPlayer,
    opponent: opponentPlayer,
  };
}

// The leader color is used as an accent on the player name plates.
function getPlateStyle(player: PlayerStateDto | null): CSSProperties | undefined {
  return player?.leader?.color
    ? ({ '--leader-accent': getLeaderColors(player.leader.color).primary } as CSSProperties)
    : undefined;
}

function isSameTarget(a: DropTarget | null | undefined, b: DropTarget): boolean {
  return a?.zone === b.zone && a?.slotIndex === b.slotIndex && a?.cardId === b.cardId;
}

// The data attributes useHandDrag reads with document.elementFromPoint.
function getDropAttributes(target: DropTarget) {
  return {
    'data-drop-zone': target.zone,
    'data-slot-index': target.slotIndex,
    'data-card-id': target.cardId,
  };
}

function hasOption(prompt: ChoicePromptDto | null, option: string): boolean {
  return Boolean(prompt?.options.includes(option));
}

function hasKeyword(card: CardInstanceDto, keyword: string): boolean {
  return Boolean(card.keywords?.includes(keyword));
}

function isZoneFilled(zone: BoardZoneDefinition, player: PlayerStateDto | null): boolean {
  switch (zone.kind) {
    case 'leader':
      return Boolean(player?.leader);
    case 'stage':
      return Boolean(player?.stage);
    case 'life':
      return (player?.lifeCount ?? 0) > 0;
    case 'deck':
      return (player?.deckCount ?? 0) > 0;
    case 'trash':
      return (player?.trashCount ?? 0) > 0;
    case 'donDeck':
      return (player?.donDeckCount ?? 0) > 0;
    default:
      return false;
  }
}

export function MatchBoard({
  activePlayerId,
  canSubmitChoice,
  currentPrompt,
  gameState,
  joinedRoomCode = '',
  midlineContent,
  onManualAction,
  onPreviewCard,
  onSubmitOrder,
  onSubmitChoice,
  pending,
}: MatchBoardProps) {
  const [inspectedCard, setInspectedCard] = useState<CardInstanceDto | null>(null);
  const [inspectedPile, setInspectedPile] = useState<CardPile | null>(null);
  const [selectedCardId, setSelectedCardId] = useState('');
  const [mode, setMode] = useState<BoardMode>({ kind: 'idle' });
  const previewTimer = useRef<number>();
  const frameRef = useRef<HTMLDivElement>(null);
  const [dragNotice, setDragNotice] = useState('');
  // Manual mode: the toggle shows the "⋯" buttons all the time (right click / long press always work).
  const [manualMode, setManualMode] = useState(false);
  const [manualMenu, setManualMenu] = useState<ManualMenuState | null>(null);
  const [confirmMove, setConfirmMove] = useState<{ text: string; action: ManualActionDto } | null>(null);
  const [bugReportOpen, setBugReportOpen] = useState(false);
  const dragNoticeTimer = useRef<number>();
  const metrics = useBoardMetrics(frameRef);

  const visiblePlayers = getVisiblePlayers(gameState);
  const localPlayer = visiblePlayers.player;
  const opponent = visiblePlayers.opponent;
  const localHandCards = localPlayer?.handCards ?? [];
  const battle = gameState?.battle ?? null;
  const isGameOver = gameState?.phase === 'GameOver';
  const didWin = isGameOver && gameState?.winnerPlayerId === localPlayer?.playerId;

  const myPrompt =
    currentPrompt && currentPrompt.playerId === localPlayer?.playerId && canSubmitChoice && !pending
      ? currentPrompt
      : null;
  const isMainAction = myPrompt?.kind === 'MAIN_ACTION';
  const isBlockStep = myPrompt?.kind === 'BLOCK_STEP';
  const isCounterStep = myPrompt?.kind === 'COUNTER_STEP';
  const isEffectPrompt = Boolean(myPrompt && EFFECT_PROMPT_KINDS.includes(myPrompt.kind));

  const localFieldCards = [localPlayer?.leader, ...(localPlayer?.boardCards ?? []), localPlayer?.stage].filter(
    (card): card is CardInstanceDto => Boolean(card),
  );
  const selectedCard = localFieldCards.find((card) => card.instanceId === selectedCardId) ?? null;
  const opponentFieldCards = [opponent?.leader, ...(opponent?.boardCards ?? []), opponent?.stage].filter(
    (card): card is CardInstanceDto => Boolean(card),
  );

  // ---------- Manual mode (the server checks every action again) ----------

  const isRunning = RUNNING_PHASES.includes(String(gameState?.phase));
  const ownPromptKind =
    currentPrompt && currentPrompt.playerId === localPlayer?.playerId ? currentPrompt.kind : '';
  const canManual =
    Boolean(onManualAction) &&
    isRunning &&
    canSubmitChoice &&
    (gameState?.activePlayerId === localPlayer?.playerId || MANUAL_DECISION_KINDS.includes(ownPromptKind));
  const manualPrompt = ownPromptKind === 'MANUAL_EFFECT' ? currentPrompt : null;
  const manualContext: ManualContext = {
    characterCount: localPlayer?.boardCards.length ?? 0,
    hasStage: Boolean(localPlayer?.stage),
    isBattle: Boolean(battle),
    activeDon: localPlayer?.activeDonCount ?? 0,
    restedDon: localPlayer?.restedDonCount ?? 0,
    donDeck: localPlayer?.donDeckCount ?? 0,
    deckCount: localPlayer?.deckCount ?? 0,
    lifeCount: localPlayer?.lifeCount ?? 0,
    leaderId: localPlayer?.leader?.instanceId ?? '',
  };

  function runManualAction(action: ManualActionDto): void {
    setManualMenu(null);
    void onManualAction?.(action);
  }

  // Which manual actions fit this card: own hand, own or opponent field, own trash.
  function getCardTarget(card: CardInstanceDto): ManualTarget | null {
    const id = card.instanceId;
    if (localHandCards.some((entry) => entry.instanceId === id)) {
      return { kind: 'hand', card };
    }

    if (localFieldCards.some((entry) => entry.instanceId === id)) {
      return { kind: 'field', card, own: true };
    }

    if (opponentFieldCards.some((entry) => entry.instanceId === id)) {
      return { kind: 'field', card, own: false };
    }

    if (localPlayer?.trashCards.some((entry) => entry.instanceId === id)) {
      return { kind: 'trash', card };
    }

    return null;
  }

  function getCardManualItems(card: CardInstanceDto): ManualItem[] {
    const target = canManual ? getCardTarget(card) : null;
    return target ? getManualItems(target, manualContext) : [];
  }

  // Returns the handler for right click / "⋯", or undefined when no manual action is allowed now.
  function getMenuHandler(title: string, items: () => ManualItem[]): ((x: number, y: number) => void) | undefined {
    if (!canManual) {
      return undefined;
    }

    return (x, y) => {
      window.clearTimeout(previewTimer.current);
      onPreviewCard?.(null);
      setManualMenu({ x, y, title, items: items() });
    };
  }

  function getCardMenuHandler(card: CardInstanceDto) {
    return getMenuHandler(card.name, () => [
      { kind: 'open', label: 'Details ansehen', run: () => setInspectedCard(card) },
      ...getCardManualItems(card),
    ]);
  }

  function renderCardDots(card: CardInstanceDto) {
    const onMenu = getCardMenuHandler(card);
    return onMenu ? <ManualDotsButton label={`${card.name}: manuelle Aktionen`} onMenu={onMenu} /> : null;
  }

  // Reset the local selection whenever a new prompt arrives.
  useEffect(() => {
    setSelectedCardId('');
    setMode({ kind: 'idle' });
  }, [currentPrompt?.choiceId]);

  useEffect(
    () => () => {
      window.clearTimeout(previewTimer.current);
      window.clearTimeout(dragNoticeTimer.current);
    },
    [],
  );

  function submit(option: string, selectedId?: string, targetId?: string): void {
    setSelectedCardId('');
    setMode({ kind: 'idle' });
    onSubmitChoice(option, selectedId, targetId);
  }

  // Shows a large preview of a card after a short hover delay.
  function handlePreview(card: CardInstanceDto | null): void {
    window.clearTimeout(previewTimer.current);

    if (!card) {
      onPreviewCard?.(null);
      return;
    }

    previewTimer.current = window.setTimeout(() => onPreviewCard?.(card), PREVIEW_DELAY_MS);
  }

  function closeInspect(): void {
    // A card opened from the trash list goes back to the list first.
    if (inspectedCard) {
      setInspectedCard(null);
      return;
    }

    setInspectedPile(null);
  }

  // ---------- Rules helpers for the UI (the server checks everything again) ----------

  function canPlayFromHand(card: CardInstanceDto): boolean {
    return (
      isMainAction &&
      hasOption(myPrompt, 'PLAY_CARD') &&
      (card.cost ?? 0) <= (localPlayer?.activeDonCount ?? 0)
    );
  }

  function canCounterWith(card: CardInstanceDto): boolean {
    if (!isCounterStep) {
      return false;
    }

    // Character cards use their Counter value, Events with [Counter] are paid with DON!!.
    const isCounterEvent = Boolean(card.isCounterEvent) && (card.cost ?? 0) <= (localPlayer?.activeDonCount ?? 0);
    return (card.category === 'Character' && (card.counter ?? 0) > 0) || isCounterEvent;
  }

  function canBlockWith(card: CardInstanceDto): boolean {
    return (
      isBlockStep &&
      card.category === 'Character' &&
      hasKeyword(card, 'Blocker') &&
      !card.isRested &&
      card.instanceId !== battle?.targetInstanceId
    );
  }

  function isAttackTarget(card: CardInstanceDto): boolean {
    if (mode.kind !== 'attack') {
      return false;
    }

    // [Rush: Character] in its first turn: not the Leader.
    const attacker = localFieldCards.find((entry) => entry.instanceId === mode.attackerId);
    if (card.category === 'Leader') {
      return !attacker?.canAttackOnlyCharacters;
    }

    return Boolean(card.isRested);
  }

  // ---------- Click handlers ----------

  function handleHandCardClick(card: CardInstanceDto): void {
    if (canCounterWith(card)) {
      submit('COUNTER', card.instanceId);
      return;
    }

    if (!canPlayFromHand(card)) {
      setInspectedCard(card);
      return;
    }

    const boardIsFull = (localPlayer?.boardCards.length ?? 0) >= MAX_CHARACTERS;
    if (card.category === 'Character' && boardIsFull) {
      setMode({ kind: 'replace', handCardId: card.instanceId });
      return;
    }

    submit('PLAY_CARD', card.instanceId);
  }

  function handleOwnCardClick(card: CardInstanceDto): void {
    // The click that ends a manual drag from the field does nothing.
    if (handDrag.shouldSuppressClick()) {
      return;
    }

    if (mode.kind === 'replace' && card.category === 'Character') {
      submit('PLAY_CARD', mode.handCardId, card.instanceId);
      return;
    }

    if (canBlockWith(card)) {
      submit('BLOCK', card.instanceId);
      return;
    }

    const canSelect = card.category === 'Leader' || card.category === 'Character' || Boolean(card.canActivate);
    if (isMainAction && canSelect) {
      setMode({ kind: 'idle' });
      setSelectedCardId((previous) => (previous === card.instanceId ? '' : card.instanceId));
      return;
    }

    setInspectedCard(card);
  }

  function handleOpponentCardClick(card: CardInstanceDto): void {
    if (mode.kind === 'attack' && isAttackTarget(card)) {
      submit('ATTACK', mode.attackerId, card.instanceId);
      return;
    }

    setInspectedCard(card);
  }

  // ---------- Drag & drop (an extra way to call the click handlers above) ----------

  const isCharacterAreaFull = (localPlayer?.boardCards.length ?? 0) >= MAX_CHARACTERS;
  const canDragDon =
    isMainAction && hasOption(myPrompt, 'ATTACH_DON') && (localPlayer?.activeDonCount ?? 0) > 0;

  // Valid drop targets follow the same checks as the click controls (canPlayFromHand, canCounterWith, ...).
  function canDropOn(item: DragItem, target: DropTarget): boolean {
    if (item.kind === 'don') {
      return canDragDon && (target.zone === 'leader' || target.zone === 'character') && Boolean(target.cardId);
    }

    if (item.kind === 'field') {
      return canManual && target.zone in FIELD_DROP_LABELS;
    }

    const card = item.card;
    if (canCounterWith(card)) {
      return target.zone === 'field';
    }

    if (!canPlayFromHand(card)) {
      return false;
    }

    switch (card.category) {
      case 'Character':
        // A full Character area: drop on the Character that should be replaced.
        return target.zone === 'character' && (isCharacterAreaFull ? Boolean(target.cardId) : !target.cardId);
      case 'Stage':
        return target.zone === 'stage';
      case 'Event':
        return target.zone === 'field';
      default:
        return false;
    }
  }

  function hasAnyDropTarget(card: CardInstanceDto): boolean {
    return (
      canCounterWith(card) ||
      (canPlayFromHand(card) && ['Character', 'Stage', 'Event'].includes(card.category ?? ''))
    );
  }

  // Sends exactly what the click controls send for the same move.
  function handleDrop(item: DragItem, target: DropTarget): boolean | void {
    if (item.kind === 'don') {
      if (target.cardId) {
        submit('ATTACH_DON', target.cardId);
      }
      return;
    }

    // Manual mode: not a normal game move, so it is confirmed first. The card flies back meanwhile.
    if (item.kind === 'field') {
      setConfirmMove({
        text: `${item.card.name} ${FIELD_DROP_LABELS[target.zone]}?`,
        action: {
          kind: 'MOVE_CARD',
          cardInstanceId: item.card.instanceId,
          toZone: target.zone,
          toPosition: target.zone === 'deck' ? 'bottom' : undefined,
        },
      });
      return false;
    }

    const card = item.card;
    if (canCounterWith(card)) {
      submit('COUNTER', card.instanceId);
      return;
    }

    if (card.category === 'Character' && isCharacterAreaFull) {
      submit('PLAY_CARD', card.instanceId, target.cardId);
      return;
    }

    submit('PLAY_CARD', card.instanceId);
  }

  function getUnplayableReason(card: CardInstanceDto): string {
    if (isCounterStep) {
      return `${card.name} cannot be used as a Counter right now.`;
    }

    if (!isMainAction) {
      return gameState?.activePlayerId !== localPlayer?.playerId
        ? 'Wait for your turn to play cards.'
        : 'Cards can only be played in your Main Phase.';
    }

    const activeDon = localPlayer?.activeDonCount ?? 0;
    if ((card.cost ?? 0) > activeDon) {
      return `Not enough DON!!: ${card.name} costs ${card.cost ?? 0}, you have ${activeDon} active.`;
    }

    return '';
  }

  function showDragNotice(text: string): void {
    window.clearTimeout(dragNoticeTimer.current);
    setDragNotice(text);
    dragNoticeTimer.current = window.setTimeout(() => setDragNotice(''), DRAG_NOTICE_MS);
  }

  const handDrag = useHandDrag({
    canDrop: canDropOn,
    onDrop: handleDrop,
    onMiss: (item) => {
      if (item.kind === 'hand' && !hasAnyDropTarget(item.card)) {
        const reason = getUnplayableReason(item.card);
        if (reason) {
          showDragNotice(reason);
        }
      }
    },
    isConfirmed: (item) =>
      item.kind === 'don' || !localHandCards.some((card) => card.instanceId === item.card.instanceId),
    pending,
  });

  const draggedItem = handDrag.view?.phase === 'drag' ? handDrag.view.item : null;

  function getDropClassName(target: DropTarget): string {
    if (!draggedItem || !canDropOn(draggedItem, target)) {
      return '';
    }

    return isSameTarget(handDrag.view?.hover, target) ? ' is-drop-target is-drop-hover' : ' is-drop-target';
  }

  // ---------- Visual state per card ----------

  function getCardStateClassName(card: CardInstanceDto, side: BoardSide): string {
    const classes: string[] = [];

    if (battle?.attackerInstanceId === card.instanceId) {
      classes.push('is-attacker');
    }

    if (battle?.targetInstanceId === card.instanceId) {
      classes.push('is-battle-target');
    }

    if (side === 'player') {
      if (card.instanceId === selectedCardId) {
        classes.push('is-selected');
      } else if (isMainAction && hasOption(myPrompt, 'ATTACK') && card.canAttack) {
        classes.push('can-attack');
      }

      if (isMainAction && hasOption(myPrompt, 'ACTIVATE') && card.canActivate) {
        classes.push('can-activate');
      }

      if (canBlockWith(card) || (mode.kind === 'replace' && card.category === 'Character')) {
        classes.push('is-targetable');
      }
    } else if (isAttackTarget(card)) {
      classes.push('is-targetable');
    }

    return classes.join(' ');
  }

  function getHandCardStateClassName(card: CardInstanceDto): string {
    if (canPlayFromHand(card) || canCounterWith(card)) {
      return 'is-usable';
    }

    return isMainAction || isCounterStep ? 'is-unplayable' : '';
  }

  function getHandCardAriaLabel(card: CardInstanceDto): string {
    if (canCounterWith(card)) {
      return card.isCounterEvent
        ? `Use the Event ${card.name} (cost ${card.cost ?? 0})`
        : `Counter with ${card.name} (+${card.counter})`;
    }

    if (canPlayFromHand(card)) {
      return `Play ${card.name} (cost ${card.cost ?? 0})`;
    }

    return `Inspect ${card.name}`;
  }

  // ---------- Action bar ----------

  function getHint(): string {
    if (isGameOver) {
      return didWin ? 'You won the game!' : 'You lost the game.';
    }

    if (isEffectPrompt) {
      return 'Resolve the card effect.';
    }

    if (myPrompt?.kind === 'ORDER_TRIGGERS') {
      return 'Several effects at the same time: choose their order.';
    }

    if (manualPrompt) {
      return 'Resolve the effect by hand (right click or ⋯), then press Done.';
    }

    if (mode.kind === 'attack') {
      const attacker = localFieldCards.find((entry) => entry.instanceId === mode.attackerId);
      return attacker?.canAttackOnlyCharacters
        ? 'Choose a rested Character: [Rush: Character] cannot attack the Leader this turn.'
        : 'Choose a target: the opponent Leader or a rested Character.';
    }

    if (mode.kind === 'replace') {
      return 'Your Character area is full. Click one of your Characters to trash it.';
    }

    if (isBlockStep) {
      return 'You are attacked! Click a [Blocker] Character to block, or choose No Block.';
    }

    if (isCounterStep) {
      return `${battle?.attackerPower ?? 0} vs ${battle?.targetPower ?? 0}: click hand cards with a Counter value, or pass.`;
    }

    if (isMainAction) {
      if (selectedCard) {
        return selectedCard.canActivate
          ? `${selectedCard.name} selected: give DON!!, attack or activate its ability.`
          : `${selectedCard.name} selected: give DON!! or attack.`;
      }

      return 'Click a hand card to play it, or click your Leader/Character to give DON!! or attack.';
    }

    if (gameState?.phase === 'Main' && gameState.activePlayerId !== localPlayer?.playerId && !myPrompt) {
      return 'Waiting for your opponent...';
    }

    if (gameState?.phase === 'TurnOrder') {
      return 'Rolling the dice for the first turn...';
    }

    if (gameState?.phase === 'Mulligan') {
      return 'Decide whether to keep your opening hand.';
    }

    if (gameState && gameState.activePlayerId !== localPlayer?.playerId) {
      return 'Waiting for your opponent...';
    }

    return '';
  }

  const canGiveDon =
    isMainAction && hasOption(myPrompt, 'ATTACH_DON') && Boolean(selectedCard) && selectedCard?.category !== 'Stage';
  const canStartAttack =
    isMainAction && hasOption(myPrompt, 'ATTACK') && Boolean(selectedCard?.canAttack);
  const canActivateSelected =
    isMainAction && hasOption(myPrompt, 'ACTIVATE') && Boolean(selectedCard?.canActivate);

  function giveDon(): void {
    if (canGiveDon && selectedCard) {
      submit('ATTACH_DON', selectedCard.instanceId);
    }
  }

  // ---------- Rendering helpers ----------

  function renderFieldCard(card: CardInstanceDto, side: BoardSide) {
    return (
      <>
        <MatchCardView
          card={card}
          clickable
          onClick={() => (side === 'player' ? handleOwnCardClick(card) : handleOpponentCardClick(card))}
          onInspect={() => setInspectedCard(card)}
          onMenu={getCardMenuHandler(card)}
          onPreview={handlePreview}
          size="slot"
          stateClassName={getCardStateClassName(card, side)}
        />
        {renderCardDots(card)}
      </>
    );
  }

  function renderDonArea(player: PlayerStateDto | null, side: BoardSide) {
    const active = player?.activeDonCount ?? 0;
    const rested = player?.restedDonCount ?? 0;
    const canClickDon = side === 'player' && canGiveDon;
    const donDragProps = side === 'player' && canDragDon ? handDrag.getSourceProps({ kind: 'don' }) : null;

    return (
      <div className="match-don-area" aria-label={`${active} active and ${rested} rested DON!!`}>
        <div className="match-don-area__cards">
          {Array.from({ length: active + rested }, (_, index) => {
            const isRested = index >= active;
            const className = `match-don-card${isRested ? ' is-rested' : ''}`;

            return (
              <span key={index} className="match-don-area__slot">
                {canClickDon && !isRested ? (
                  <button
                    aria-label={`Give DON!! to ${selectedCard?.name ?? 'selected card'}`}
                    className={`${className} is-targetable is-draggable`}
                    onClick={() => !handDrag.shouldSuppressClick() && giveDon()}
                    type="button"
                    {...donDragProps}
                  />
                ) : (
                  <span
                    aria-hidden="true"
                    className={donDragProps && !isRested ? `${className} is-draggable` : className}
                    {...(isRested ? null : donDragProps)}
                  />
                )}
              </span>
            );
          })}
        </div>
        {active + rested > 0 && (
          <span className="card-stack__badge match-don-area__badge" aria-hidden="true">
            <small>DON!!</small>
            {active}/{active + rested}
          </span>
        )}
      </div>
    );
  }

  function renderZoneContent(zone: BoardZoneDefinition, side: BoardSide) {
    const player = visiblePlayers[side];

    switch (zone.kind) {
      case 'character':
        return (
          <div className="board-zone__slots">
            {Array.from({ length: CHARACTER_SLOT_COUNT }, (_, index) => {
              const card = player?.boardCards?.[index];
              const target: DropTarget = { zone: 'character', slotIndex: index, cardId: card?.instanceId };
              const isOwn = side === 'player';
              // Manual mode: own Characters can be dragged to the trash, hand or deck.
              const fieldDragProps =
                isOwn && card && manualMode && canManual ? handDrag.getSourceProps({ kind: 'field', card }) : null;
              return (
                <div
                  key={card?.instanceId ?? `empty-${index}`}
                  className={`board-card-slot${isOwn ? getDropClassName(target) : ''}`}
                  {...(isOwn ? getDropAttributes(target) : null)}
                  {...fieldDragProps}
                >
                  {card && renderFieldCard(card, side)}
                </div>
              );
            })}
          </div>
        );
      case 'leader':
        return player?.leader ? renderFieldCard(player.leader, side) : null;
      case 'stage':
        return player?.stage ? renderFieldCard(player.stage, side) : null;
      case 'life':
        return <CardStack count={player?.lifeCount ?? 0} label="Life" variant="life" />;
      case 'deck':
        return <CardStack count={player?.deckCount ?? 0} label="Deck" variant="card" />;
      case 'donDeck':
        return <CardStack count={player?.donDeckCount ?? 0} label="DON!!" variant="don" />;
      case 'trash': {
        const trashCards = player?.trashCards ?? [];
        const title = side === 'player' ? 'Your Trash' : 'Opponent Trash';
        return (
          <CardStack
            count={player?.trashCount ?? 0}
            label="Trash"
            onClick={() => setInspectedPile({ title, cards: [...trashCards].reverse() })}
            topCard={trashCards[trashCards.length - 1] ?? null}
            variant="card"
          />
        );
      }
      case 'donArea':
        return renderDonArea(player, side);
      default:
        return null;
    }
  }

  // Leader (for DON!!) and Stage are drop targets on the own half.
  function getZoneDropTarget(zone: BoardZoneDefinition, player: PlayerStateDto | null): DropTarget | null {
    if (zone.kind === 'leader' && player?.leader) {
      return { zone: 'leader', cardId: player.leader.instanceId };
    }

    if (zone.kind === 'stage') {
      return { zone: 'stage', cardId: player?.stage?.instanceId };
    }

    // Manual drag & drop from the field.
    if (zone.kind === 'trash' || zone.kind === 'deck') {
      return { zone: zone.kind };
    }

    return null;
  }

  // Manual actions for the own Deck, Life and DON!! zones.
  function getZoneMenuHandler(zone: BoardZoneDefinition, side: BoardSide) {
    if (side !== 'player') {
      return undefined;
    }

    const targets: Partial<Record<string, ManualTarget>> = {
      deck: { kind: 'deck' },
      life: { kind: 'life' },
      donDeck: { kind: 'don' },
      donArea: { kind: 'don' },
    };
    const target = targets[zone.kind];
    return target ? getMenuHandler(zone.label, () => getManualItems(target, manualContext)) : undefined;
  }

  function renderSide(side: BoardSide) {
    const player = visiblePlayers[side];

    return (
      <div
        className={`match-board__side match-board__side--${side}${
          player && player.playerId === activePlayerId ? ' is-active' : ''
        }`}
      >
        {boardZones.map((zone) => {
          const target = side === 'player' ? getZoneDropTarget(zone, player) : null;
          return (
            <BoardSlot
              key={zone.kind}
              dropClassName={target ? getDropClassName(target) : ''}
              dropTarget={target}
              filled={isZoneFilled(zone, player)}
              onMenu={getZoneMenuHandler(zone, side)}
              side={side}
              zone={zone}
            >
              {renderZoneContent(zone, side)}
            </BoardSlot>
          );
        })}
        {side === 'player' && draggedItem && canDropOn(draggedItem, { zone: 'field' }) && (
          <div
            className={`match-drop-field${getDropClassName({ zone: 'field' })}`}
            {...getDropAttributes({ zone: 'field' })}
          >
            <span>{isCounterStep ? 'Use Counter' : 'Activate Event'}</span>
          </div>
        )}
      </div>
    );
  }

  const opponentHandCount = opponent?.handCount ?? 0;
  const hint = dragNotice || getHint();
  const canToggleManual = isRunning && Boolean(onManualAction);
  // "Fehler melden" during the match and on the game over screen.
  const canReportBug = Boolean(joinedRoomCode) && (isRunning || isGameOver);
  const showActions =
    isMainAction || isBlockStep || isCounterStep || mode.kind === 'replace' || canToggleManual || (canReportBug && !isGameOver);

  const frameStyle = {
    ...cardArtStyle,
    ...(metrics
      ? {
          '--board-card-w': `${metrics.cardWidth}px`,
          '--board-card-h': `${(metrics.cardWidth * 88) / 63}px`,
        }
      : null),
  } as CSSProperties;
  const frameClassName = [
    'match-board-frame',
    metrics && metrics.cardWidth < SMALL_CARD_W ? 'is-small-cards' : '',
    manualMode && canManual ? 'is-manual-mode' : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div ref={frameRef} className={frameClassName} style={frameStyle}>
      <div className="match-board" aria-label="OneSake match board">
        <div
          className={`match-board__opponent-bar${opponent?.playerId === activePlayerId ? ' is-active' : ''}`}
        >
          <div className="match-player-plate" style={getPlateStyle(opponent)}>
            <span>Opponent</span>
            <strong>{opponent?.displayName || 'Waiting...'}</strong>
            <OfflineBadge player={opponent} />
            <PlayerClock playerId={opponent?.playerId} timer={gameState?.timer} />
          </div>
          <div className="match-opponent-hand" aria-label={`Opponent hand: ${opponentHandCount} cards`} role="img">
            {Array.from({ length: Math.min(opponentHandCount, MAX_OPPONENT_HAND_BACKS) }, (_, index) => (
              <span
                key={index}
                className="card-back card-back--hand"
                style={{ '--i': index } as CSSProperties}
              />
            ))}
            {opponentHandCount > 0 && <span className="card-stack__badge">{opponentHandCount}</span>}
          </div>
        </div>

        {renderSide('opponent')}

        <div className="match-board__midline">
          {midlineContent}

          {battle && (
            <div className="match-battle-banner" role="status">
              <span>Battle</span>
              <strong>
                {battle.attackerPower} vs {battle.targetPower}
              </strong>
              {battle.counterBonus > 0 && <small>Counter +{battle.counterBonus}</small>}
            </div>
          )}

          <OwnTimerBar playerId={localPlayer?.playerId} timer={gameState?.timer} />

          {showActions && (
            <div className="match-actions" aria-label="Match actions" role="group">
              {canReportBug && (
                <button
                  className="match-actions__bug"
                  onClick={() => setBugReportOpen(true)}
                  title="Fehler melden: der Spielverlauf wird mitgeschickt"
                  type="button"
                >
                  🐞 Fehler melden
                </button>
              )}
              {canToggleManual && (
                <button
                  aria-pressed={manualMode}
                  className={`match-actions__manual${manualMode ? ' is-active' : ''}`}
                  onClick={() => setManualMode((value) => !value)}
                  title="Manuelle Aktionen: Rechtsklick oder Long-Press auf Karten funktioniert immer"
                  type="button"
                >
                  ✋ Manuell
                </button>
              )}
              {isMainAction && (
                <>
                  <button disabled={!canGiveDon} onClick={giveDon} type="button">
                    Give DON!!
                    <small>+1000</small>
                  </button>
                  <button
                    className={mode.kind === 'attack' ? 'is-active' : ''}
                    disabled={!canStartAttack}
                    onClick={() =>
                      selectedCard &&
                      setMode(
                        mode.kind === 'attack'
                          ? { kind: 'idle' }
                          : { kind: 'attack', attackerId: selectedCard.instanceId },
                      )
                    }
                    type="button"
                  >
                    {mode.kind === 'attack' ? 'Cancel Attack' : 'Attack'}
                  </button>
                  <button
                    disabled={!canActivateSelected}
                    onClick={() => selectedCard && submit('ACTIVATE', selectedCard.instanceId)}
                    type="button"
                  >
                    Activate
                    <small>Effect</small>
                  </button>
                </>
              )}
              {mode.kind === 'replace' && (
                <button onClick={() => setMode({ kind: 'idle' })} type="button">
                  Cancel
                </button>
              )}
              {isBlockStep && (
                <button onClick={() => submit('NO_BLOCK')} type="button">
                  No Block
                </button>
              )}
              {isCounterStep && (
                <button className="is-primary" onClick={() => submit('PASS')} type="button">
                  Pass
                </button>
              )}
              {isMainAction && (
                <button className="match-actions__end-turn" onClick={() => submit('END_TURN')} type="button">
                  End Turn
                </button>
              )}
            </div>
          )}
        </div>

        {renderSide('player')}
      </div>

      <div
        className={`match-player-plate match-player-plate--self${
          localPlayer?.playerId === activePlayerId ? ' is-active' : ''
        }`}
        style={getPlateStyle(localPlayer)}
      >
        <span>You</span>
        <strong>{localPlayer?.displayName || 'Player'}</strong>
        <PlayerClock playerId={localPlayer?.playerId} timer={gameState?.timer} />
      </div>

      {hint && (
        <p className="match-board__hint" role="status">
          {hint}
        </p>
      )}

      <MatchHand
        cards={localHandCards}
        drag={{
          getSourceProps: (card) => handDrag.getSourceProps({ kind: 'hand', card }),
          sourceId: handDrag.view?.item.kind === 'hand' ? handDrag.view.item.card.instanceId : '',
          isActive: handDrag.view?.phase === 'drag',
          isDragging: handDrag.isDragging,
          cancelPress: handDrag.cancelPress,
          shouldSuppressClick: handDrag.shouldSuppressClick,
        }}
        getAriaLabel={getHandCardAriaLabel}
        getMenuHandler={getCardMenuHandler}
        getStateClassName={getHandCardStateClassName}
        onCardClick={handleHandCardClick}
        onInspect={setInspectedCard}
        onPreview={handlePreview}
        renderCardExtra={renderCardDots}
      />

      {isGameOver && (
        <div className="match-board-game-over" role="status">
          {gameState?.winnerPlayerId ? (didWin ? 'Victory!' : 'Defeat') : 'Game Over'}
          {getGameOverReason(gameState?.endReason, didWin) && (
            <small className="match-board-game-over__reason">{getGameOverReason(gameState?.endReason, didWin)}</small>
          )}
          {canReportBug && (
            <button className="match-board-game-over__bug" onClick={() => setBugReportOpen(true)} type="button">
              🐞 Fehler melden
            </button>
          )}
        </div>
      )}

      <DragGhost ref={handDrag.ghostRef} cardWidth={metrics?.cardWidth ?? 80} view={handDrag.view} />

      <CardInspectOverlay
        card={inspectedCard}
        manualDisabled={pending}
        manualItems={inspectedCard ? getCardManualItems(inspectedCard) : []}
        onClose={closeInspect}
        onManualAction={(action) => {
          // The card may leave the zone, so the overlay closes.
          setInspectedCard(null);
          setInspectedPile(null);
          runManualAction(action);
        }}
        onSelectCard={setInspectedCard}
        pile={inspectedPile}
      />

      <TriggerOrderOverlay
        disabled={pending || !canSubmitChoice}
        onSubmit={(orderedIds) => onSubmitOrder?.(orderedIds)}
        prompt={myPrompt?.kind === 'ORDER_TRIGGERS' ? myPrompt : null}
      />

      <ManualEffectBanner
        disabled={pending || !canSubmitChoice}
        onDone={() => submit('DONE')}
        prompt={manualPrompt}
      />

      <ManualViewDialog
        disabled={pending || !canSubmitChoice}
        onAction={(action) => onManualAction?.(action) ?? Promise.resolve(false)}
        view={localPlayer?.manualView}
      />

      <ManualMenu
        disabled={pending}
        menu={manualMenu}
        onAction={runManualAction}
        onClose={() => setManualMenu(null)}
      />

      <BugReportDialog
        atSeq={gameState?.recordedInputs ?? 0}
        cards={[...localHandCards, ...localFieldCards, ...opponentFieldCards, ...(localPlayer?.trashCards ?? []), ...(opponent?.trashCards ?? [])]}
        onClose={() => setBugReportOpen(false)}
        open={bugReportOpen}
        roomCode={joinedRoomCode}
      />

      <ManualConfirm
        onCancel={() => setConfirmMove(null)}
        onConfirm={() => {
          if (confirmMove) {
            runManualAction(confirmMove.action);
          }
          setConfirmMove(null);
        }}
        text={confirmMove?.text ?? null}
      />

      <EffectChoiceOverlay
        disabled={pending || !canSubmitChoice}
        onSubmit={(option, cardId) => submit(option, cardId)}
        prompt={isEffectPrompt ? myPrompt : null}
      />
    </div>
  );
}
