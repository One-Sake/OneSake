import { useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';

import type {
  CardInstanceDto,
  ChoicePromptDto,
  GameStateDto,
  PlayerStateDto,
} from '../../../types/realtime';
import { boardZones, CHARACTER_SLOT_COUNT } from './boardLayout';
import { BoardSlot, CardStack } from './BoardSlot';
import type { BoardSide, BoardZoneDefinition } from './boardTypes';
import { CardInspectOverlay } from './CardInspectOverlay';
import type { CardPile } from './CardInspectOverlay';
import { MatchCardView } from './MatchCardView';
import { MatchHand } from './MatchHand';

const MAX_CHARACTERS = CHARACTER_SLOT_COUNT;
const PREVIEW_DELAY_MS = 350;
const MAX_OPPONENT_HAND_BACKS = 12;

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
}

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
  onSubmitChoice,
  pending,
}: MatchBoardProps) {
  const [inspectedCard, setInspectedCard] = useState<CardInstanceDto | null>(null);
  const [inspectedPile, setInspectedPile] = useState<CardPile | null>(null);
  const [previewCard, setPreviewCard] = useState<CardInstanceDto | null>(null);
  const [selectedCardId, setSelectedCardId] = useState('');
  const [mode, setMode] = useState<BoardMode>({ kind: 'idle' });
  const previewTimer = useRef<number>();

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

  const localFieldCards = [localPlayer?.leader, ...(localPlayer?.boardCards ?? [])].filter(
    (card): card is CardInstanceDto => Boolean(card),
  );
  const selectedCard = localFieldCards.find((card) => card.instanceId === selectedCardId) ?? null;

  // Reset the local selection whenever a new prompt arrives.
  useEffect(() => {
    setSelectedCardId('');
    setMode({ kind: 'idle' });
  }, [currentPrompt?.choiceId]);

  useEffect(() => () => window.clearTimeout(previewTimer.current), []);

  function submit(option: string, selectedId?: string, targetId?: string): void {
    setSelectedCardId('');
    setMode({ kind: 'idle' });
    onSubmitChoice(option, selectedId, targetId);
  }

  // Shows a large preview of a board card after a short hover delay.
  function handlePreview(card: CardInstanceDto | null): void {
    window.clearTimeout(previewTimer.current);

    if (!card) {
      setPreviewCard(null);
      return;
    }

    previewTimer.current = window.setTimeout(() => setPreviewCard(card), PREVIEW_DELAY_MS);
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
    return isCounterStep && card.category === 'Character' && (card.counter ?? 0) > 0;
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
    return mode.kind === 'attack' && (card.category === 'Leader' || Boolean(card.isRested));
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
    if (mode.kind === 'replace' && card.category === 'Character') {
      submit('PLAY_CARD', mode.handCardId, card.instanceId);
      return;
    }

    if (canBlockWith(card)) {
      submit('BLOCK', card.instanceId);
      return;
    }

    if (isMainAction && (card.category === 'Leader' || card.category === 'Character')) {
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
      return `Counter with ${card.name} (+${card.counter})`;
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

    if (mode.kind === 'attack') {
      return 'Choose a target: the opponent Leader or a rested Character.';
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
        return `${selectedCard.name} selected: give DON!! or attack.`;
      }

      return 'Click a hand card to play it, or click your Leader/Character to give DON!! or attack.';
    }

    if (gameState?.phase === 'Mulligan') {
      return 'Decide whether to keep your opening hand.';
    }

    if (gameState && gameState.activePlayerId !== localPlayer?.playerId) {
      return 'Waiting for your opponent...';
    }

    return '';
  }

  const canGiveDon = isMainAction && hasOption(myPrompt, 'ATTACH_DON') && Boolean(selectedCard);
  const canStartAttack =
    isMainAction && hasOption(myPrompt, 'ATTACK') && Boolean(selectedCard?.canAttack);

  function giveDon(): void {
    if (canGiveDon && selectedCard) {
      submit('ATTACH_DON', selectedCard.instanceId);
    }
  }

  // ---------- Rendering helpers ----------

  function renderFieldCard(card: CardInstanceDto, side: BoardSide) {
    return (
      <MatchCardView
        card={card}
        clickable
        onClick={() => (side === 'player' ? handleOwnCardClick(card) : handleOpponentCardClick(card))}
        onInspect={() => setInspectedCard(card)}
        onPreview={handlePreview}
        size="slot"
        stateClassName={getCardStateClassName(card, side)}
      />
    );
  }

  function renderDonArea(player: PlayerStateDto | null, side: BoardSide) {
    const active = player?.activeDonCount ?? 0;
    const rested = player?.restedDonCount ?? 0;
    const canClickDon = side === 'player' && canGiveDon;

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
                    className={`${className} is-targetable`}
                    onClick={giveDon}
                    type="button"
                  />
                ) : (
                  <span aria-hidden="true" className={className} />
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
              return (
                <div key={card?.instanceId ?? `empty-${index}`} className="board-card-slot">
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

  function renderSide(side: BoardSide) {
    const player = visiblePlayers[side];

    return (
      <div
        className={`match-board__side match-board__side--${side}${
          player && player.playerId === activePlayerId ? ' is-active' : ''
        }`}
      >
        {boardZones.map((zone) => (
          <BoardSlot key={zone.kind} filled={isZoneFilled(zone, player)} side={side} zone={zone}>
            {renderZoneContent(zone, side)}
          </BoardSlot>
        ))}
      </div>
    );
  }

  const opponentHandCount = opponent?.handCount ?? 0;
  const hint = getHint();
  const showActions = isMainAction || isBlockStep || isCounterStep || mode.kind === 'replace';

  return (
    <div className="match-board-frame">
      <div className="match-board" aria-label="OneSake match board">
        <div
          className={`match-board__opponent-bar${opponent?.playerId === activePlayerId ? ' is-active' : ''}`}
        >
          <div className="match-player-plate">
            <span>Opponent</span>
            <strong>{opponent?.displayName || 'Waiting...'}</strong>
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
          {battle && (
            <div className="match-battle-banner" role="status">
              <span>Battle</span>
              <strong>
                {battle.attackerPower} vs {battle.targetPower}
              </strong>
              {battle.counterBonus > 0 && <small>Counter +{battle.counterBonus}</small>}
            </div>
          )}

          {showActions && (
            <div className="match-actions" aria-label="Match actions" role="group">
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
      >
        <span>You</span>
        <strong>{localPlayer?.displayName || 'Player'}</strong>
      </div>

      {hint && (
        <p className="match-board__hint" role="status">
          {hint}
        </p>
      )}

      <MatchHand
        cards={localHandCards}
        getAriaLabel={getHandCardAriaLabel}
        getStateClassName={getHandCardStateClassName}
        onCardClick={handleHandCardClick}
        onInspect={setInspectedCard}
      />

      {previewCard && (
        <aside className="match-card-preview" aria-hidden="true">
          <MatchCardView card={{ ...previewCard, isRested: false }} showStats={false} size="inspect" />
          <strong>{previewCard.name}</strong>
          {(previewCard.power ?? previewCard.basePower) !== undefined && (
            <small>
              {previewCard.category} · {previewCard.power ?? previewCard.basePower} power
            </small>
          )}
        </aside>
      )}

      {isGameOver && (
        <div className="match-board-game-over" role="status">
          {gameState?.winnerPlayerId ? (didWin ? 'Victory!' : 'Defeat') : 'Game Over'}
        </div>
      )}

      <CardInspectOverlay
        card={inspectedCard}
        onClose={closeInspect}
        onSelectCard={setInspectedCard}
        pile={inspectedPile}
      />
    </div>
  );
}
