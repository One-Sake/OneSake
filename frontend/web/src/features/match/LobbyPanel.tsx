import type { ReactNode } from 'react';

import { Button } from '../../components/ui/Button';
import type { ConnectionStatus } from '../../api/signalrClient';
import { ConnectionStatusBadge } from './ConnectionStatusBadge';

interface ReadinessItem {
  label: string;
  ready: boolean;
  text: string;
}

interface LobbyPanelProps {
  roomCodeInput: string;
  displayNameInput: string;
  canCreate: boolean;
  canJoin: boolean;
  canStart: boolean;
  connectionStatus: ConnectionStatus;
  copyStatus: '' | 'copied' | 'failed';
  deckNotice: string;
  displayNamePlaceholder: string;
  joinedRoomCode: string;
  pending: boolean;
  readinessItems: ReadinessItem[];
  deckSlot?: ReactNode;
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
  onStartMatch: () => void;
}

export function LobbyPanel({
  canCreate,
  canJoin,
  canStart,
  connectionStatus,
  copyStatus,
  deckNotice,
  deckSlot,
  displayNamePlaceholder,
  displayNameInput,
  joinedRoomCode,
  onCopyRoomCode,
  onCreateRoom,
  onDisplayNameChange,
  onGenerateRoomCode,
  onJoinRoom,
  onLeaveRoom,
  onOpenBoard,
  onOpenDeckbuilder,
  onRandomizeDisplayName,
  onRoomCodeChange,
  onStartMatch,
  pending,
  readinessItems,
  roomCodeInput,
  serverSlot,
}: LobbyPanelProps) {
  return (
    <section className="match-setup-panel" aria-label="Match setup">
      <div className="match-setup-main">
        <div className="match-setup-section match-setup-section--player">
          <div className="match-setup-card__header">
            <span>Player</span>
            <ConnectionStatusBadge status={connectionStatus} />
          </div>
          <div className="match-display-name-row">
            <div className="field">
              <label htmlFor="displayName">Display Name</label>
              <input
                autoComplete="nickname"
                id="displayName"
                maxLength={24}
                onChange={(event) => onDisplayNameChange(event.target.value)}
                placeholder={displayNamePlaceholder}
                value={displayNameInput}
              />
            </div>
            <Button
              aria-label="Generate random pirate display name"
              className="match-display-name-randomizer"
              onClick={onRandomizeDisplayName}
              variant="secondary"
            >
              RANDOMIZER
            </Button>
          </div>
        </div>

        <div className="match-setup-section match-room-card">
          <div className="match-setup-card__header">
            <span>Room</span>
            {joinedRoomCode ? <strong className="match-room-code-chip">{joinedRoomCode}</strong> : null}
          </div>
          <div className="match-room-code-row">
            <div className="field">
              <label htmlFor="roomCode">Room Code</label>
              <input
                className="match-room-code-input"
                id="roomCode"
                maxLength={12}
                onChange={(event) => onRoomCodeChange(event.target.value)}
                placeholder="SAKE42"
                value={roomCodeInput}
              />
            </div>
            <Button onClick={onGenerateRoomCode} variant="secondary">
              Generate Room Code
            </Button>
          </div>
          <div className="match-room-actions">
            {joinedRoomCode && (
              <Button onClick={onCopyRoomCode} variant="ghost">
                {copyStatus === 'copied'
                  ? 'Copied'
                  : copyStatus === 'failed'
                    ? 'Copy Failed'
                    : 'Copy Room Code'}
              </Button>
            )}
          </div>
          {copyStatus === 'failed' && (
            <p className="match-lobby-notice match-lobby-notice--error" role="alert">
              Clipboard is not available in this browser context.
            </p>
          )}
        </div>

        {deckSlot && <div className="match-setup-section match-setup-section--deck">{deckSlot}</div>}
      </div>

      <aside className="match-setup-aside">
        <div className="match-setup-section match-readiness-card">
          <div className="match-setup-card__header">
            <span>Ready Check</span>
            <strong>
              {readinessItems.filter((item) => item.ready).length}/{readinessItems.length}
            </strong>
          </div>
          <ul className="match-readiness-list">
            {readinessItems.map((item) => (
              <li key={item.label} className={item.ready ? 'is-ready' : ''}>
                <span aria-hidden="true">{item.ready ? 'OK' : '...'}</span>
                <div>
                  <strong>{item.label}</strong>
                  <small>{item.text}</small>
                </div>
              </li>
            ))}
          </ul>
          {deckNotice && (
            <div className="match-readiness-card__deck-action">
              <p className="match-lobby-notice">{deckNotice}</p>
              {onOpenDeckbuilder && (
                <Button onClick={onOpenDeckbuilder} variant="secondary">
                  Open Deckbuilder
                </Button>
              )}
            </div>
          )}
        </div>

        <div className="match-setup-section match-join-card">
          <div className="match-setup-card__header">
            <span>{joinedRoomCode ? 'Joined' : 'Launch'}</span>
            {joinedRoomCode && <strong className="match-room-code-chip">{joinedRoomCode}</strong>}
          </div>
          <div className="match-join-card__actions">
            {joinedRoomCode ? (
              <>
                <Button fullWidth onClick={onOpenBoard} variant="secondary">
                  Open Board
                </Button>
                <Button disabled={pending} fullWidth onClick={onLeaveRoom} variant="danger">
                  Leave Room
                </Button>
              </>
            ) : (
              <>
                <Button disabled={!canCreate || pending} fullWidth onClick={onCreateRoom}>
                  {pending ? 'Please wait...' : 'Create Room'}
                </Button>
                <Button disabled={!canJoin || pending} fullWidth onClick={onJoinRoom} variant="secondary">
                  Join Room
                </Button>
              </>
            )}
            <Button disabled={!canStart || pending} fullWidth onClick={onStartMatch} variant="ghost">
              Start Match
            </Button>
          </div>
        </div>

        {serverSlot}
      </aside>
    </section>
  );
}
