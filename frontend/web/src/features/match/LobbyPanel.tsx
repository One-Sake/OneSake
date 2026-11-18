import { useState } from 'react';
import type { CSSProperties, ReactNode } from 'react';

import type { ConnectionStatus } from '../../api/signalrClient';
import type { CardDto } from '../../types/cards';
import type { Deck } from '../../types/decks';
import { getTotalCards } from '../deckbuilder/utils/deckValidation';
import { DeckCarousel } from './DeckCarousel';
import { getLeaderColorHex, getLeaderColorNames, getLeaderColors } from './leaderColors';
import { LeaderHero } from './LeaderHero';
import { DiceIcon, SparkIcon } from './LobbyIcons';
import { PrimaryLobbyAction, type LobbyActionMode } from './PrimaryLobbyAction';
import { getLeaderImage } from './useLeaderCards';

interface LobbyPanelProps {
  roomCodeInput: string;
  displayNameInput: string;
  canCreate: boolean;
  canJoin: boolean;
  canStart: boolean;
  connectionStatus: ConnectionStatus;
  connectionNotice: string;
  error: string;
  copyStatus: '' | 'copied' | 'failed';
  deckNotice: string;
  displayNamePlaceholder: string;
  joinedRoomCode: string;
  playersInRoom: number;
  isMatchRunning: boolean;
  pending: boolean;
  decks: Deck[];
  selectedDeck: Deck | null;
  selectedDeckId: string;
  leaderCardsById: Record<string, CardDto | null>;
  serverSlot?: ReactNode;
  onRoomCodeChange: (value: string) => void;
  onDisplayNameChange: (value: string) => void;
  onGenerateRoomCode: () => void;
  onRandomizeDisplayName: () => void;
  onCreateRoom: () => void;
  onJoinRoom: () => void;
  onLeaveRoom: () => void;
  onCopyRoomCode: () => void;
  onOpenBoard: () => void;
  onOpenDeckbuilder?: () => void;
  onOpenDeckLibrary: () => void;
  onSelectDeck: (deckId: string) => void;
  onStartMatch: () => void;
}

const CONNECTION_LABELS: Record<ConnectionStatus, string> = {
  disconnected: 'Nicht verbunden',
  connecting: 'Verbinde mit dem Server…',
  connected: 'Mit dem Server verbunden',
  reconnecting: 'Verbindung wird wiederhergestellt…',
  error: 'Verbindungsfehler',
};

function getActionMode(joinedRoomCode: string, isMatchRunning: boolean, playersInRoom: number): LobbyActionMode {
  if (!joinedRoomCode) {
    return 'create';
  }

  if (isMatchRunning) {
    return 'board';
  }

  return playersInRoom >= 2 ? 'start' : 'waiting';
}

export function LobbyPanel({
  canCreate,
  canJoin,
  canStart,
  connectionNotice,
  connectionStatus,
  copyStatus,
  deckNotice,
  decks,
  displayNameInput,
  displayNamePlaceholder,
  error,
  isMatchRunning,
  joinedRoomCode,
  leaderCardsById,
  onCopyRoomCode,
  onCreateRoom,
  onDisplayNameChange,
  onGenerateRoomCode,
  onJoinRoom,
  onLeaveRoom,
  onOpenBoard,
  onOpenDeckbuilder,
  onOpenDeckLibrary,
  onRandomizeDisplayName,
  onRoomCodeChange,
  onSelectDeck,
  onStartMatch,
  pending,
  playersInRoom,
  roomCodeInput,
  selectedDeck,
  selectedDeckId,
  serverSlot,
}: LobbyPanelProps) {
  const [isJoinByCodeOpen, setIsJoinByCodeOpen] = useState(false);
  const [diceSpinCount, setDiceSpinCount] = useState(0);

  const leaderCard = selectedDeck ? leaderCardsById[selectedDeck.leaderCardId] ?? null : null;
  const leaderColorSource = leaderCard?.card_color || selectedDeck?.leaderColors;
  const leaderColors = getLeaderColors(leaderColorSource);
  const leaderColorNames = getLeaderColorNames(leaderColorSource);
  const leaderName =
    leaderCard?.card_name || selectedDeck?.leaderName || selectedDeck?.leaderCardId || 'Kein Deck';
  const playerName = displayNameInput.trim();

  const mode = getActionMode(joinedRoomCode, isMatchRunning, playersInRoom);
  const isPrimaryDisabled =
    pending || (mode === 'create' && !canCreate) || (mode === 'start' && !canStart);

  // One short line that explains why the main button is locked.
  let blockedReason = '';
  if (mode === 'waiting') {
    blockedReason = 'Teile den Room-Code mit deinem Gegner.';
  } else if (isPrimaryDisabled && !pending) {
    blockedReason =
      deckNotice ||
      (mode === 'create' && !playerName ? 'Gib einen Spielernamen ein.' : '') ||
      connectionNotice;
  }

  // Failures get a slim red strip at the top. "Connecting" is shown by the status dot only.
  const showConnectionStrip = Boolean(connectionNotice) && connectionStatus !== 'connecting';

  const lobbyStyle = {
    '--leader-color': leaderColors.primary,
    '--leader-color-2': leaderColors.secondary,
  } as CSSProperties;

  const randomizeName = (): void => {
    setDiceSpinCount((count) => count + 1);
    onRandomizeDisplayName();
  };

  return (
    <section className="lobby" style={lobbyStyle} aria-label="Game lobby">
      <div className="lobby__backdrop" aria-hidden="true" />

      {(showConnectionStrip || error) && (
        <div className="lobby__alerts">
          {showConnectionStrip && (
            <p className={`lobby-strip lobby-strip--${connectionStatus}`} role="status">
              {connectionNotice}
            </p>
          )}
          {error && (
            <p className="lobby-strip" role="alert">
              {error}
            </p>
          )}
        </div>
      )}

      <header className="lobby__header">
        <div>
          <span className="lobby__kicker">OneSake · Match</span>
          <h1 className="lobby__title">
            Bereit für die See{playerName ? `, ${playerName}` : ''}?
          </h1>
        </div>
        <span
          aria-label={CONNECTION_LABELS[connectionStatus]}
          aria-live="polite"
          className={`lobby-connection-dot lobby-connection-dot--${connectionStatus}`}
          role="img"
          title={CONNECTION_LABELS[connectionStatus]}
        />
      </header>

      <div className="lobby__grid">
        <div className="lobby-glass lobby__deck">
          <h2 className="lobby-heading">Dein Deck</h2>

          <div className="lobby-deck-hero">
            <LeaderHero
              imageUrl={selectedDeck ? getLeaderImage(selectedDeck, leaderCard) : ''}
              leaderName={leaderName}
            />

            <div className="lobby-deck-info">
              <span className="lobby-deck-info__leader">{selectedDeck ? leaderName : 'Leader'}</span>
              <strong className="lobby-deck-info__name">{selectedDeck?.name ?? 'Kein Deck gewählt'}</strong>
              {leaderColorNames.length > 0 && (
                <span className="lobby-deck-info__colors">
                  {leaderColorNames.map((colorName) => (
                    <span key={colorName} className="lobby-color-dot" title={colorName}>
                      <span aria-hidden="true" style={{ background: getLeaderColorHex(colorName) }} />
                      {colorName}
                    </span>
                  ))}
                </span>
              )}
              {selectedDeck && (
                <span className={`lobby-deck-info__status ${deckNotice ? 'is-invalid' : 'is-valid'}`}>
                  {deckNotice || `${getTotalCards(selectedDeck.cards)} Karten · gültig ✓`}
                </span>
              )}
              {!selectedDeck && deckNotice && (
                <span className="lobby-deck-info__status is-invalid">{deckNotice}</span>
              )}
              {decks.length === 0 && onOpenDeckbuilder && (
                <button className="lobby-secondary-button" onClick={onOpenDeckbuilder} type="button">
                  Zum Deckbuilder
                </button>
              )}
            </div>
          </div>

          <DeckCarousel
            decks={decks}
            leaderCardsById={leaderCardsById}
            onOpenDeckLibrary={onOpenDeckLibrary}
            onSelectDeck={onSelectDeck}
            selectedDeckId={selectedDeckId}
          />

          <div className="name-plate">
            <label className="name-plate__label" htmlFor="displayName">
              Spielername
            </label>
            <div className="name-plate__row">
              <input
                autoComplete="nickname"
                className="name-plate__input"
                id="displayName"
                maxLength={24}
                onChange={(event) => onDisplayNameChange(event.target.value)}
                placeholder={displayNamePlaceholder}
                value={displayNameInput}
              />
              <button
                aria-label="Zufälliger Name"
                className="lobby-icon-button name-plate__dice"
                onClick={randomizeName}
                title="Zufälliger Name"
                type="button"
              >
                <span key={diceSpinCount} className={diceSpinCount > 0 ? 'is-spinning' : ''}>
                  <DiceIcon />
                </span>
              </button>
            </div>
          </div>

          <div className="lobby__action-area">
            <PrimaryLobbyAction
              blockedReason={blockedReason}
              copyStatus={copyStatus}
              disabled={isPrimaryDisabled}
              joinedRoomCode={joinedRoomCode}
              mode={mode}
              onCopyRoomCode={onCopyRoomCode}
              onCreateRoom={onCreateRoom}
              onOpenBoard={onOpenBoard}
              onStartMatch={onStartMatch}
              pending={pending}
            />

            <div className="lobby-links">
              {!joinedRoomCode && (
                <button
                  aria-expanded={isJoinByCodeOpen}
                  className="lobby-text-link"
                  onClick={() => setIsJoinByCodeOpen((isOpen) => !isOpen)}
                  type="button"
                >
                  Mit Code beitreten
                </button>
              )}
              {joinedRoomCode && mode !== 'board' && (
                <button className="lobby-text-link" onClick={onOpenBoard} type="button">
                  Board öffnen
                </button>
              )}
              {joinedRoomCode && (
                <button
                  className="lobby-text-link lobby-text-link--danger"
                  disabled={pending}
                  onClick={onLeaveRoom}
                  type="button"
                >
                  Raum verlassen
                </button>
              )}
            </div>

            {isJoinByCodeOpen && !joinedRoomCode && (
              <form
                className="join-by-code"
                onSubmit={(event) => {
                  event.preventDefault();
                  if (canJoin && !pending) {
                    onJoinRoom();
                  }
                }}
              >
                <label className="visually-hidden" htmlFor="roomCode">
                  Room-Code
                </label>
                <input
                  autoFocus
                  className="join-by-code__input"
                  id="roomCode"
                  maxLength={12}
                  onChange={(event) => onRoomCodeChange(event.target.value)}
                  placeholder="SAKE42"
                  value={roomCodeInput}
                />
                <button
                  aria-label="Room-Code generieren"
                  className="lobby-icon-button"
                  onClick={onGenerateRoomCode}
                  title="Room-Code generieren"
                  type="button"
                >
                  <SparkIcon />
                </button>
                <button className="lobby-secondary-button" disabled={!canJoin || pending} type="submit">
                  Beitreten
                </button>
              </form>
            )}
          </div>
        </div>

        <aside className="lobby-glass lobby__tables">{serverSlot}</aside>
      </div>
    </section>
  );
}
