import { FormEvent, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';

import type { ConnectionStatus } from '../../api/signalrClient';
import type {
  CardInstanceDto,
  ChatMessageDto,
  ChoicePromptDto,
  GameStateDto,
  LogEventDto,
} from '../../types/realtime';
import { BountyChangeBanner } from './BountyChangeBanner';
import { ChoiceBar } from './ChoiceBar';
import { ConnectionStatusBadge } from './ConnectionStatusBadge';
import { ReconnectBanner } from './ReconnectBanner';
import { CollapsedFeed, FeedSplitter } from './FeedSplitter';
import { clampFeedWidth, getFeedMaxWidth, loadFeedWidth, saveFeedWidth } from './feedWidth';
import { MatchFeed } from './MatchFeed';
import { useCardArtStyle } from './board/cardArt';
import { MatchBoard } from './board/MatchBoard';
import { MatchCardPreview } from './board/MatchCardPreview';
import { DiceRoll } from './dice/DiceRoll';
import { formatPhase } from './matchFormatters';
import { useBountyChange } from './useBountyChange';
import { FxSettingsMenu } from './fx/FxSettingsMenu';
import './fx/fx.css';

// These prompts are answered directly on the board (click cards / action bar).
const BOARD_HANDLED_PROMPT_KINDS = ['MAIN_ACTION', 'BLOCK_STEP', 'COUNTER_STEP'];

// From this width on the feed is a resizable column; below it is a slide-in overlay.
const DESKTOP_QUERY = '(min-width: 1024px)';

function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => window.matchMedia(query).matches);

  useEffect(() => {
    const media = window.matchMedia(query);
    const update = () => setMatches(media.matches);
    update();
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [query]);

  return matches;
}

function useViewportWidth(): number {
  const [width, setWidth] = useState(() => window.innerWidth);

  useEffect(() => {
    const update = () => setWidth(window.innerWidth);
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

  return width;
}

interface FullscreenMatchViewProps {
  activePlayerDisplay: string;
  canStart: boolean;
  canSubmitChoice: boolean;
  chatInput: string;
  chatMessages: ChatMessageDto[];
  // Local time when our connection dropped while in the room (null = connected).
  connectionLostAt: number | null;
  connectionStatus: ConnectionStatus;
  currentPrompt: ChoicePromptDto | null;
  error: string;
  gameState: GameStateDto | null;
  joinedRoomCode: string;
  logEvents: LogEventDto[];
  onChatInputChange: (value: string) => void;
  onExitBoard: () => void;
  onLeaveRoom: () => void;
  onSubmitOrder: (orderedIds: string[]) => void;

  // Game over: open a new table for a rematch.
  onRematch?: () => void;
  onSendChat: (event: FormEvent) => void;
  onStartMatch: () => void;
  onSubmitChoice: (option: string, selectedCardInstanceId?: string, targetCardInstanceId?: string) => void;
  pending: boolean;
  showReconnected: boolean;
}

export function FullscreenMatchView({
  activePlayerDisplay,
  canStart,
  canSubmitChoice,
  chatInput,
  chatMessages,
  connectionLostAt,
  connectionStatus,
  currentPrompt,
  error,
  gameState,
  joinedRoomCode,
  logEvents,
  onChatInputChange,
  onExitBoard,
  onLeaveRoom,
  onSubmitOrder,

  onRematch,
  onSendChat,
  onStartMatch,
  onSubmitChoice,
  pending,
  showReconnected,
}: FullscreenMatchViewProps) {
  const cardArtStyle = useCardArtStyle();
  // Only used below 1024px, where the feed is a slide-in overlay.
  const [feedOpen, setFeedOpen] = useState(false);
  // Desktop: the feed is a resizable column that can be collapsed to a thin strip.
  const [feedCollapsed, setFeedCollapsed] = useState(false);
  const [feedWidth, setFeedWidth] = useState(loadFeedWidth);
  const [seenChatCount, setSeenChatCount] = useState(chatMessages.length);
  const stageRef = useRef<HTMLElement>(null);
  const isDesktop = useMediaQuery(DESKTOP_QUERY);
  const viewportWidth = useViewportWidth();
  const feedMaxWidth = getFeedMaxWidth(viewportWidth);
  const clampedFeedWidth = clampFeedWidth(feedWidth, viewportWidth);
  const isFeedVisible = isDesktop ? !feedCollapsed : feedOpen;
  const unreadChatCount = isFeedVisible ? 0 : Math.max(0, chatMessages.length - seenChatCount);
  const [previewCard, setPreviewCard] = useState<CardInstanceDto | null>(null);
  const phaseLabel = gameState ? formatPhase(gameState.phase) : 'Lobby';
  const isLobbyPhase = phaseLabel === 'Lobby';
  const canShowStartMatch = canStart && isLobbyPhase;
  const players = gameState?.players ?? [];
  const viewerPlayer =
    players.find((player) => player.playerId === gameState?.viewerPlayerId) ?? players[0] ?? null;
  const hasOpponent = players.some((player) => player.playerId !== viewerPlayer?.playerId);
  const statusMessages: string[] = [];
  const bountyChange = useBountyChange(gameState);

  // Everything in the feed counts as read while it is visible.
  useEffect(() => {
    if (isFeedVisible) {
      setSeenChatCount(chatMessages.length);
    }
  }, [chatMessages.length, isFeedVisible]);

  // While dragging, only the CSS variable changes (no re-render); the state is set on release.
  function handleFeedResize(width: number, live: boolean): void {
    if (live) {
      stageRef.current?.style.setProperty('--feed-w', `${width}px`);
      return;
    }

    setFeedWidth(width);
    saveFeedWidth(width);
  }

  const stageStyle =
    isDesktop && !feedCollapsed ? ({ '--feed-w': `${clampedFeedWidth}px` } as CSSProperties) : undefined;
  const stageClassName = [
    'match-fullscreen__stage',
    isDesktop ? 'is-desktop' : '',
    isDesktop && feedCollapsed ? 'is-feed-collapsed' : '',
    connectionLostAt !== null ? 'is-offline' : '',
  ]
    .filter(Boolean)
    .join(' ');

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
    <section className="match-fullscreen" aria-label="Fullscreen match board" style={cardArtStyle}>
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
        {gameState && (
          <div className={`match-fullscreen__meta match-mode-tag ${gameState.isRanked ? 'is-ranked' : 'is-casual'}`}>
            <strong>{gameState.isRanked ? 'Ranked' : 'Casual'}</strong>
          </div>
        )}
        <ConnectionStatusBadge status={connectionStatus} />
        <FxSettingsMenu />
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
          {!feedOpen && unreadChatCount > 0 && <span className="match-unread-badge">{unreadChatCount}</span>}
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

      <ReconnectBanner
        graceMs={gameState?.viewerReconnectGraceMs}
        lostAt={connectionLostAt}
        showReconnected={showReconnected}
      />

      {error && <p className="match-fullscreen__error" role="alert">{error}</p>}

      <main ref={stageRef} className={stageClassName} style={stageStyle}>
        {isDesktop && feedCollapsed ? (
          <CollapsedFeed onExpand={() => setFeedCollapsed(false)} unreadCount={unreadChatCount} />
        ) : (
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
        )}

        {isDesktop && !feedCollapsed && (
          <FeedSplitter
            maxWidth={feedMaxWidth}
            onCollapse={() => setFeedCollapsed(true)}
            onResize={handleFeedResize}
            width={clampedFeedWidth}
          />
        )}

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
            logEvents={logEvents}
            midlineContent={
              <ChoiceBar
                boardHandledKinds={BOARD_HANDLED_PROMPT_KINDS}
                canSubmitChoice={canSubmitChoice}
                currentPrompt={currentPrompt}
                onSubmitChoice={onSubmitChoice}
                pending={pending}
              />
            }
            onSubmitOrder={onSubmitOrder}

            bountyChange={bountyChange}
            onLeaveRoom={onLeaveRoom}
            onPreviewCard={setPreviewCard}
            onRematch={onRematch}
            onSubmitChoice={onSubmitChoice}
            pending={pending}
          />
          <BountyChangeBanner change={bountyChange} hidden={gameState?.phase === 'GameOver'} />
          <DiceRoll
            canSubmitChoice={canSubmitChoice}
            currentPrompt={currentPrompt}
            gameState={gameState}
            onSubmitChoice={onSubmitChoice}
            pending={pending}
          />
        </div>

        <MatchCardPreview card={previewCard} />
      </main>
    </section>
  );
}
