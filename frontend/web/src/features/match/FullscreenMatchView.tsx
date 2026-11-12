import { FormEvent, useState } from 'react';

import type { ConnectionStatus } from '../../api/signalrClient';
import type {
  CardInstanceDto,
  ChatMessageDto,
  ChoicePromptDto,
  GameStateDto,
  LogEventDto,
} from '../../types/realtime';
import { ChoiceBar } from './ChoiceBar';
import { ConnectionStatusBadge } from './ConnectionStatusBadge';
import { MatchFeed } from './MatchFeed';
import { MatchBoard } from './board/MatchBoard';
import { MatchCardPreview } from './board/MatchCardPreview';
import { formatPhase } from './matchFormatters';

// These prompts are answered directly on the board (click cards / action bar).
const BOARD_HANDLED_PROMPT_KINDS = ['MAIN_ACTION', 'BLOCK_STEP', 'COUNTER_STEP'];

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
  onSubmitChoice: (option: string, selectedCardInstanceId?: string, targetCardInstanceId?: string) => void;
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
  // Only used below 1200px; on desktop the log/chat feed is always visible.
  const [feedOpen, setFeedOpen] = useState(false);
  const [previewCard, setPreviewCard] = useState<CardInstanceDto | null>(null);
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
          <span>Active</span>
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
          className="match-fullscreen__toggle match-fullscreen__feed-toggle"
          aria-expanded={feedOpen}
          aria-label="Toggle match log and chat"
          onClick={() => setFeedOpen((open) => !open)}
          type="button"
        >
          {feedOpen ? 'Hide' : 'Log / Chat'}
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

      <main className="match-fullscreen__stage">
        <aside className={feedOpen ? 'match-fullscreen__feed is-open' : 'match-fullscreen__feed'}>
          <MatchFeed
            canSendChat={canSubmitChoice}
            chatInput={chatInput}
            chatMessages={chatMessages}
            joinedRoomCode={joinedRoomCode}
            logEvents={logEvents}
            onChatInputChange={onChatInputChange}
            onSendChat={onSendChat}
            pending={pending}
            players={players}
          />
        </aside>

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
            midlineContent={
              <ChoiceBar
                boardHandledKinds={BOARD_HANDLED_PROMPT_KINDS}
                canSubmitChoice={canSubmitChoice}
                currentPrompt={currentPrompt}
                onSubmitChoice={onSubmitChoice}
                pending={pending}
              />
            }
            onPreviewCard={setPreviewCard}
            onSubmitChoice={onSubmitChoice}
            pending={pending}
          />
        </div>

        <MatchCardPreview card={previewCard} />
      </main>
    </section>
  );
}
