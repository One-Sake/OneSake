import { useEffect, useState } from 'react';

import onesakeBoardUrl from '../../../assets/boards/onesake-board.png';
import type {
  CardInstanceDto,
  ChoicePromptDto,
  GameStateDto,
  PlayerStateDto,
} from '../../../types/realtime';
import { boardSlots } from './boardLayout';
import { BoardSlot } from './BoardSlot';
import type { BoardSide, BoardSlotDefinition } from './boardTypes';
import { CardInspectOverlay } from './CardInspectOverlay';
import { MatchCardView } from './MatchCardView';

const MAX_CHARACTERS = 5;

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

function getCharacterIndex(slot: BoardSlotDefinition): number {
  const match = slot.id.match(/character-(\d+)$/);
  return match ? Number(match[1]) : 0;
}

function getSlotCard(slot: BoardSlotDefinition, player: PlayerStateDto | null): CardInstanceDto | null {
  if (!player) {
    return null;
  }

  switch (slot.kind) {
    case 'leader':
      return player.leader ?? null;
    case 'stage':
      return player.stage ?? null;
    case 'character':
      return player.boardCards?.[getCharacterIndex(slot) - 1] ?? null;
    case 'trash':
      return player.trashCards?.[player.trashCards.length - 1] ?? null;
    default:
      return null;
  }
}

function getSlotCount(slot: BoardSlotDefinition, player: PlayerStateDto | null): number | undefined {
  if (!player) {
    return undefined;
  }

  switch (slot.kind) {
    case 'deck':
      return player.deckCount;
    case 'hand':
      return player.handCount;
    case 'life':
      return player.lifeCount;
    case 'trash':
      return player.trashCount;
    case 'donDeck':
      return player.donDeckCount;
    default:
      return undefined;
  }
}

function hasOption(prompt: ChoicePromptDto | null, option: string): boolean {
  return Boolean(prompt?.options.includes(option));
}

function hasKeyword(card: CardInstanceDto, keyword: string): boolean {
  return Boolean(card.keywords?.includes(keyword));
}

function DonArea({ player }: { player: PlayerStateDto | null }) {
  const active = player?.activeDonCount ?? 0;
  const rested = player?.restedDonCount ?? 0;

  return (
    <div className="match-don-area" aria-label={`${active} active and ${rested} rested DON`}>
      <div className="match-don-area__cards">
        {Array.from({ length: active }, (_, index) => (
          <span key={`active-${index}`} className="match-don-card" />
        ))}
        {Array.from({ length: rested }, (_, index) => (
          <span key={`rested-${index}`} className="match-don-card is-rested" />
        ))}
      </div>
      <small>
        DON!! {active} active / {rested} rested
      </small>
    </div>
  );
}

export function MatchBoard({
  activePlayerId,
  canSubmitChoice,
  currentPrompt,
  gameState,
  joinedRoomCode,
  onSubmitChoice,
  pending,
}: MatchBoardProps) {
  const [inspectedCard, setInspectedCard] = useState<CardInstanceDto | null>(null);
  const [selectedCardId, setSelectedCardId] = useState('');
  const [mode, setMode] = useState<BoardMode>({ kind: 'idle' });

  const debugBoardLayout =
    typeof window !== 'undefined' && new URLSearchParams(window.location.search).has('debugBoard');
  const visiblePlayers = getVisiblePlayers(gameState);
  const localPlayer = visiblePlayers.player;
  const opponent = visiblePlayers.opponent;
  const localHandCards = localPlayer?.handCards ?? [];
  const roomCode = gameState?.roomCode || joinedRoomCode || '-';
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

  function submit(option: string, selectedId?: string, targetId?: string): void {
    setSelectedCardId('');
    setMode({ kind: 'idle' });
    onSubmitChoice(option, selectedId, targetId);
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
      }

      if (canBlockWith(card) || (mode.kind === 'replace' && card.category === 'Character')) {
        classes.push('is-targetable');
      }
    } else if (isAttackTarget(card)) {
      classes.push('is-targetable');
    }

    return classes.join(' ');
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

  return (
    <div className="match-board-frame">
      <div className="match-board-scroll">
        <div
          className={debugBoardLayout ? 'match-board match-board--debug' : 'match-board'}
          aria-label="OneSake match board"
        >
          <img alt="OneSake pirate wooden game board" className="match-board-image" src={onesakeBoardUrl} />

          <div className="match-board-overlay">
            {boardSlots.map((slot) => {
              const slotPlayer = visiblePlayers[slot.side];
              const slotCard = getSlotCard(slot, slotPlayer);
              const isFieldCard =
                slot.kind === 'leader' || slot.kind === 'character' || slot.kind === 'stage';

              let onCardClick: ((card: CardInstanceDto) => void) | undefined = setInspectedCard;
              if (isFieldCard) {
                onCardClick = slot.side === 'player' ? handleOwnCardClick : handleOpponentCardClick;
              }

              return (
                <BoardSlot
                  key={slot.id}
                  active={slotPlayer?.playerId === activePlayerId}
                  card={slotCard}
                  cardStateClassName={slotCard ? getCardStateClassName(slotCard, slot.side) : undefined}
                  count={getSlotCount(slot, slotPlayer)}
                  onCardClick={onCardClick}
                  slot={slot}
                >
                  {slot.kind === 'donArea' ? <DonArea player={slotPlayer} /> : undefined}
                </BoardSlot>
              );
            })}

            <div
              className={`match-board-player-tag match-board-player-tag--opponent ${
                opponent?.playerId === activePlayerId ? 'is-active' : ''
              }`}
            >
              <span>Opponent</span>
              <strong>{opponent?.displayName || 'Opponent'}</strong>
            </div>

            <div
              className={`match-board-player-tag match-board-player-tag--player ${
                localPlayer?.playerId === activePlayerId ? 'is-active' : ''
              }`}
            >
              <span>You</span>
              <strong>{localPlayer?.displayName || 'Player'}</strong>
            </div>

            <div className="match-board-room-tag">
              <span>Room</span>
              <strong>{roomCode}</strong>
            </div>

            {battle && (
              <div className="match-battle-banner" role="status">
                <span>Battle</span>
                <strong>
                  {battle.attackerPower} vs {battle.targetPower}
                </strong>
                {battle.counterBonus > 0 && <small>Counter +{battle.counterBonus}</small>}
              </div>
            )}

            {isGameOver && (
              <div className="match-board-game-over" role="status">
                {gameState?.winnerPlayerId ? (didWin ? 'Victory!' : 'Defeat') : 'Game Over'}
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="match-action-panel" aria-label="Match actions">
        <p className="match-action-panel__hint">{getHint()}</p>
        <div className="match-action-panel__buttons">
          {isMainAction && (
            <>
              <button
                disabled={!canGiveDon}
                onClick={() => selectedCard && submit('ATTACH_DON', selectedCard.instanceId)}
                type="button"
              >
                Give DON!! (+1000)
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
          {selectedCard && (
            <button onClick={() => setInspectedCard(selectedCard)} type="button">
              Inspect
            </button>
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
              Pass (no more counters)
            </button>
          )}
          {isMainAction && (
            <button className="is-primary" onClick={() => submit('END_TURN')} type="button">
              End Turn
            </button>
          )}
        </div>
      </div>

      <div className="match-hand-zone" aria-label="Your hand cards">
        <div className="match-hand-zone__header">
          <span>Your Hand</span>
          <strong>
            {localPlayer?.handCount ?? 0} cards · DON!! {localPlayer?.activeDonCount ?? 0} active
          </strong>
        </div>

        <div className="match-hand-card-row">
          {localHandCards.length ? (
            localHandCards.map((card) => {
              const usable = canPlayFromHand(card) || canCounterWith(card);
              return (
                <div
                  key={card.instanceId}
                  className={usable ? 'match-hand-card-shell is-playable' : 'match-hand-card-shell'}
                >
                  <MatchCardView
                    ariaLabel={
                      canCounterWith(card)
                        ? `Counter with ${card.name} (+${card.counter})`
                        : usable
                          ? `Play ${card.name} (cost ${card.cost ?? 0})`
                          : `Inspect ${card.name}`
                    }
                    card={card}
                    clickable
                    onClick={() => handleHandCardClick(card)}
                    size="hand"
                    stateClassName={usable ? 'is-usable' : ''}
                  />
                  <button
                    aria-label={`Inspect ${card.name} (${card.cardId})`}
                    className="match-card-inspect-button"
                    onClick={() => setInspectedCard(card)}
                    type="button"
                  >
                    Inspect
                  </button>
                </div>
              );
            })
          ) : (
            <div className="match-hand-empty">No visible hand cards.</div>
          )}
        </div>
      </div>

      <CardInspectOverlay card={inspectedCard} onClose={() => setInspectedCard(null)} />
    </div>
  );
}
