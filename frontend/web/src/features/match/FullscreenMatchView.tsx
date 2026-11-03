import { FormEvent, useState } from 'react';

import type { ConnectionStatus } from '../../api/signalrClient';
import type {
  ChatMessageDto,
  ChoicePromptDto,
  GameStateDto,
  LogEventDto,
} from '../../types/realtime';
import { ChatPanel } from './ChatPanel';
import { ChoiceSheet } from './ChoiceSheet';
import { ConnectionStatusBadge } from './ConnectionStatusBadge';
import { LogPanel } from './LogPanel';
import { MatchBoard } from './board/MatchBoard';
import { formatPhase } from './matchFormatters';

interface FullscreenMatchViewProps {
  activePlayerDisplay: string;
  canStart: boolean;
  canSubmitChoice: boolean;
  chatInput: string;
  chatMessages: ChatMessageDto[];
  connectionStatus: ConnectionStatus;
  currentPrompt: ChoicePromptDto | null;
  error: string;
  gameState: GameStateDto | null;
  joinedRoomCode: string;
  logEvents: LogEventDto[];
  onChatInputChange: (value: string) => void;
  onExitBoard: () => void;
  onLeaveRoom: () => void;
  onSendChat: (event: FormEvent) => void;
  onStartMatch: () => void;
  onSubmitChoice: (option: string, selectedCardInstanceId?: string) => void;
  pending: boolean;
}

export function FullscreenMatchView({
  activePlayerDisplay,
  canStart,
  canSubmitChoice,
  chatInput,
  chatMessages,
  connectionStatus,
  currentPrompt,
  error,
  gameState,
  joinedRoomCode,
  logEvents,
  onChatInputChange,
  onExitBoard,
  onLeaveRoom,
  onSendChat,
  onStartMatch,
  onSubmitChoice,
  pending,
}: FullscreenMatchViewProps) {
  const [sidePanelOpen, setSidePanelOpen] = useState(false);
  const [sidePanelTab, setSidePanelTab] = useState<'log' | 'chat'>('log');
  const phaseLabel = gameState ? formatPhase(gameState.phase) : 'Lobby';
  const isLobbyPhase = phaseLabel === 'Lobby';
  const canShowStartMatch = canStart && isLobbyPhase;
  const players = gameState?.players ?? [];
  const viewerPlayer =
    players.find((player) => player.playerId === gameState?.viewerPlayerId) ?? players[0] ?? null;
  const hasOpponent = players.some((player) => player.playerId !== viewerPlayer?.playerId);
  const statusMessages: string[] = [];

  if (!gameState) {
    statusMessages.push('Waiting for match state...');
  }

  if (gameState && !hasOpponent) {
    statusMessages.push('Waiting for opponent...');
  }

  if (gameState && viewerPlayer && !viewerPlayer.hasDeck) {
    statusMessages.push('Deck is not registered for this room.');
  }

  if (gameState && isLobbyPhase) {
    statusMessages.push('Match is in lobby. Start when players are ready.');
  }

  function openSidePanel(tab: 'log' | 'chat'): void {
    setSidePanelTab(tab);
    setSidePanelOpen(true);
  }

  return (
    <section className="match-fullscreen" aria-label="Fullscreen match board">
      <header className="match-fullscreen__topbar">
        <div className="match-fullscreen__meta">
          <span>Room</span>
          <strong>{gameState?.roomCode || joinedRoomCode || '-'}</strong>
        </div>
        <div className="match-fullscreen__meta">
          <span>Phase</span>
          <strong>{phaseLabel}</strong>
        </div>
        <div className="match-fullscreen__meta">
          <span>Turn</span>
          <strong>{gameState?.turnNumber ?? 0}</strong>
        </div>
        <div className="match-fullscreen__meta match-fullscreen__meta--wide">
          <span>Active Player</span>
          <strong>{activePlayerDisplay}</strong>
        </div>
        <ConnectionStatusBadge status={connectionStatus} />
        {canShowStartMatch && (
          <button
            className="match-fullscreen__toggle match-fullscreen__toggle--primary"
            disabled={pending}
            onClick={onStartMatch}
            type="button"
          >
            Start Match
          </button>
        )}
        <button
          className="match-fullscreen__toggle"
          aria-expanded={sidePanelOpen}
          aria-label="Toggle match log and chat drawer"
          onClick={() => {
            if (sidePanelOpen) {
              setSidePanelOpen(false);
              return;
            }

            openSidePanel(sidePanelTab);
          }}
          type="button"
        >
          {sidePanelOpen ? 'Hide' : 'Log / Chat'}
        </button>
        <button className="match-fullscreen__exit" onClick={onExitBoard} type="button">
          Exit Board
        </button>
        <button className="match-fullscreen__exit" disabled={pending} onClick={onLeaveRoom} type="button">
          Leave Room
        </button>
      </header>

      <div className="match-fullscreen__orientation-notice" role="note">
        Bitte Ger&auml;t quer halten f&uuml;r die beste Spielerfahrung.
      </div>

      {error && <p className="match-fullscreen__error" role="alert">{error}</p>}

      <main
        className={
          sidePanelOpen
            ? 'match-fullscreen__stage match-fullscreen__stage--side-open'
            : 'match-fullscreen__stage'
        }
      >
        <div className="match-fullscreen__board">
          {statusMessages.length > 0 && (
            <div className="match-fullscreen__status-stack" role="status">
              {statusMessages.map((message) => (
                <p key={message}>{message}</p>
              ))}
            </div>
          )}
          <MatchBoard
            activePlayerId={gameState?.activePlayerId ?? ''}
            canSubmitChoice={canSubmitChoice}
            currentPrompt={currentPrompt}
            gameState={gameState}
            joinedRoomCode={joinedRoomCode}
            onSubmitChoice={onSubmitChoice}
            pending={pending}
          />
        </div>

        <aside
          className={
            sidePanelOpen
              ? 'match-fullscreen__side-panel is-open'
              : 'match-fullscreen__side-panel'
          }
          aria-hidden={!sidePanelOpen}
        >
          <div className="match-fullscreen__drawer-header">
            <div>
              <span>Match Tools</span>
              <strong>{sidePanelTab === 'log' ? 'Match Log' : 'Table Chat'}</strong>
            </div>
            <div className="match-fullscreen__drawer-tabs" role="tablist" aria-label="Match drawer">
              <button
                aria-selected={sidePanelTab === 'log'}
                onClick={() => openSidePanel('log')}
                role="tab"
                type="button"
              >
                Log <span>{logEvents.length}</span>
              </button>
              <button
                aria-selected={sidePanelTab === 'chat'}
                onClick={() => openSidePanel('chat')}
                role="tab"
                type="button"
              >
                Chat <span>{chatMessages.length}</span>
              </button>
            </div>
          </div>
          {sidePanelTab === 'log' ? (
            <LogPanel logEvents={logEvents} />
          ) : (
            <ChatPanel
              canSendChat={canSubmitChoice}
              chatInput={chatInput}
              chatMessages={chatMessages}
              joinedRoomCode={joinedRoomCode}
              onChatInputChange={onChatInputChange}
              onSendChat={onSendChat}
              pending={pending}
            />
          )}
        </aside>
      </main>

      <ChoiceSheet
        canSubmitChoice={canSubmitChoice}
        currentPrompt={currentPrompt}
        onSubmitChoice={onSubmitChoice}
        pending={pending}
      />
    </section>
  );
}
