import { FormEvent, useEffect, useMemo, useState } from 'react';

import { getMatchHubUrl } from '../../api/backendUrl';
import { SignalRClient, type ConnectionStatus } from '../../api/signalrClient';
import {
  ChatMessageDto,
  ChoicePromptDto,
  ChoiceSubmissionDto,
  GameStateDto,
  LogEventDto,
  RoomSummaryDto,
} from '../../types/realtime';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import { loadRecentDeckIds, loadStoredDecks, saveRecentDeckId } from '../deckbuilder/utils/deckStorage';
import { validateDeck } from '../deckbuilder/utils/deckValidation';
import { ChatPanel } from './ChatPanel';
import { ChoiceSheet } from './ChoiceSheet';
import { ConnectionStatusBadge } from './ConnectionStatusBadge';
import { FullscreenMatchView } from './FullscreenMatchView';
import { LobbyPanel } from './LobbyPanel';
import { LogPanel } from './LogPanel';
import { toPlayerDeckSubmission } from './matchDeckMapper';
import { MatchDeckSelect } from './MatchDeckSelect';
import { MatchStatePanel } from './MatchStatePanel';
import { generatePirateName } from './pirateNameGenerator';
import { ServerBrowser } from './ServerBrowser';

interface MatchPageProps {
  onImmersiveModeChange?: (isImmersiveMode: boolean) => void;
  onOpenDeckbuilder?: () => void;
}

type CopyStatus = '' | 'copied' | 'failed';

function createRoomCode(): string {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  return Array.from({ length: 6 }, () => alphabet[Math.floor(Math.random() * alphabet.length)]).join(
    '',
  );
}

// SignalR wraps server errors like "An unexpected error occurred invoking 'JoinRoom' on the
// server. HubException: Room ABC does not exist." - only the last part is useful for players.
function toHubErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) {
    return fallback;
  }

  const hubMessage = error.message.split('HubException:')[1];
  return hubMessage ? hubMessage.trim() : error.message;
}

function isMobileViewport(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }

  return window.matchMedia('(pointer: coarse), (max-width: 900px)').matches;
}

async function requestBoardPresentation(): Promise<void> {
  if (!isMobileViewport()) {
    return;
  }

  try {
    if (!document.fullscreenElement && document.documentElement.requestFullscreen) {
      await document.documentElement.requestFullscreen();
    }
  } catch {
    // CSS fullscreen mode remains available if the browser blocks real fullscreen.
  }

  try {
    const orientation = screen.orientation as ScreenOrientation & {
      lock?: (orientation: string) => Promise<void>;
    };
    await orientation.lock?.('landscape');
  } catch {
    // Orientation lock is not available on every browser/device.
  }
}

async function exitBoardPresentation(): Promise<void> {
  try {
    if (document.fullscreenElement && document.exitFullscreen) {
      await document.exitFullscreen();
    }
  } catch {
    // Leaving CSS fullscreen is enough if browser fullscreen cannot be exited here.
  }

  try {
    const orientation = screen.orientation as ScreenOrientation & {
      unlock?: () => void;
    };
    orientation.unlock?.();
  } catch {
    // Orientation unlock is optional and unsupported in some browsers.
  }
}

export function MatchPage({ onImmersiveModeChange, onOpenDeckbuilder }: MatchPageProps) {
  const [{ signalRClient, signalRConfigError }] = useState(() => {
    try {
      return {
        signalRClient: new SignalRClient(getMatchHubUrl()),
        signalRConfigError: '',
      };
    } catch (configError) {
      console.error('[OneSake] Match hub configuration failed', configError);
      return {
        signalRClient: null,
        signalRConfigError:
          configError instanceof Error
            ? configError.message
            : 'Match server URL could not be configured.',
      };
    }
  });

  const [roomCodeInput, setRoomCodeInput] = useState('');
  const [displayNameInput, setDisplayNameInput] = useState('');
  const [joinedRoomCode, setJoinedRoomCode] = useState('');
  const [gameState, setGameState] = useState<GameStateDto | null>(null);
  const [currentPrompt, setCurrentPrompt] = useState<ChoicePromptDto | null>(null);
  const [chatMessages, setChatMessages] = useState<ChatMessageDto[]>([]);
  const [logEvents, setLogEvents] = useState<LogEventDto[]>([]);
  const [chatInput, setChatInput] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected');
  const [savedDecks] = useState(() => loadStoredDecks());
  const [recentDeckIds, setRecentDeckIds] = useState(() => loadRecentDeckIds());
  const [selectedDeckId, setSelectedDeckId] = useState('');
  const [isBoardMode, setIsBoardMode] = useState(false);
  const [isDeckLibraryOpen, setIsDeckLibraryOpen] = useState(false);
  const [copyStatus, setCopyStatus] = useState<CopyStatus>('');
  const [displayNamePlaceholder] = useState(() => generatePirateName());
  const [rooms, setRooms] = useState<RoomSummaryDto[]>([]);

  useEffect(() => {
    if (!signalRClient) {
      return undefined;
    }

    const client = signalRClient;
    client.onRoomList((roomList) => {
      setRooms(roomList);
    });
    client.onStateSnapshot((snapshot) => {
      setGameState(snapshot);
      // Open prompts are cleared on the server when the game ends.
      if (snapshot.phase === 'GameOver' || snapshot.phase === 'Lobby') {
        setCurrentPrompt(null);
      }
    });

    client.onChoicePrompt((prompt) => {
      setCurrentPrompt(prompt);
    });

    client.onChatMessage((message) => {
      setChatMessages((previous) => [...previous, message]);
    });

    client.onLogEvent((event) => {
      setLogEvents((previous) => [...previous, event]);
    });

    client.onConnectionStatus((status) => {
      setConnectionStatus(status);
    });
  }, [signalRClient]);

  // Connect right away so the server list is visible before joining a room.
  useEffect(() => {
    if (!signalRClient || connectionStatus !== 'connected') {
      return;
    }

    signalRClient
      .getRooms()
      .then(setRooms)
      .catch((roomsError) => console.error('[OneSake] Loading rooms failed', roomsError));
  }, [connectionStatus, signalRClient]);

  useEffect(() => {
    signalRClient?.connect().catch(() => {
      // The connection badge and notice already show the error.
    });
  }, [signalRClient]);

  // The server removes us from the room when the connection drops,
  // so the local room state has to be cleared as well.
  useEffect(() => {
    const lostConnection =
      connectionStatus === 'reconnecting' ||
      connectionStatus === 'disconnected' ||
      connectionStatus === 'error';

    if (lostConnection && joinedRoomCode) {
      resetRoomState();
      setError('Connection lost. You left the room - join it again from the server list.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connectionStatus]);

  useEffect(() => {
    if (!selectedDeckId) {
      const recentDeckId = recentDeckIds.find((deckId) =>
        savedDecks.some((deck) => deck.id === deckId),
      );
      const initialDeckId = recentDeckId ?? savedDecks[0]?.id;

      if (initialDeckId) {
        setSelectedDeckId(initialDeckId);
      }
    }
  }, [recentDeckIds, savedDecks, selectedDeckId]);

  useEffect(() => {
    onImmersiveModeChange?.(isBoardMode);

    return () => {
      onImmersiveModeChange?.(false);
    };
  }, [isBoardMode, onImmersiveModeChange]);

  useEffect(() => {
    if (!copyStatus) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setCopyStatus(''), 1800);
    return () => window.clearTimeout(timeoutId);
  }, [copyStatus]);

  const isConnected = connectionStatus === 'connected';
  const selectedDeck = useMemo(
    () => savedDecks.find((deck) => deck.id === selectedDeckId) ?? null,
    [savedDecks, selectedDeckId],
  );
  const recentDecks = useMemo(
    () =>
      recentDeckIds
        .map((deckId) => savedDecks.find((deck) => deck.id === deckId) ?? null)
        .filter((deck): deck is (typeof savedDecks)[number] => Boolean(deck)),
    [recentDeckIds, savedDecks],
  );
  const lobbyDecks = recentDecks.length > 0 ? recentDecks : savedDecks;
  const selectedDeckValidation = useMemo(
    () => (selectedDeck ? validateDeck(selectedDeck) : null),
    [selectedDeck],
  );
  const hasSavedDecks = savedDecks.length > 0;
  const hasValidSelectedDeck = Boolean(selectedDeckValidation?.isValid);
  const hasPlayerName = displayNameInput.trim().length > 0;
  const hasRoomCode = roomCodeInput.trim().length > 0;
  // Creating a room works without a code - one is generated automatically.
  const canCreate =
    hasPlayerName &&
    hasValidSelectedDeck &&
    connectionStatus !== 'connecting' &&
    !signalRConfigError;
  const canJoin = canCreate && hasRoomCode;
  const canStart =
    joinedRoomCode.length > 0 &&
    connectionStatus === 'connected' &&
    hasValidSelectedDeck &&
    !signalRConfigError;
  const deckNotice = useMemo(() => {
    if (!hasSavedDecks) {
      return 'Create and save a deck in the Deckbuilder first.';
    }

    if (!selectedDeck) {
      return 'Choose a deck.';
    }

    if (!selectedDeckValidation?.isValid) {
      return selectedDeckValidation?.errors[0] ?? 'Selected deck is not valid yet.';
    }

    return '';
  }, [hasSavedDecks, selectedDeck, selectedDeckValidation]);
  const connectionNotice = useMemo(() => {
    if (signalRConfigError) {
      return 'Match server configuration is missing. Check VITE_BACKEND_URL for this build.';
    }

    if (connectionStatus === 'reconnecting') {
      return 'Connection lost. Reconnecting to the match server...';
    }

    if (connectionStatus === 'disconnected') {
      return 'Not connected to the match server.';
    }

    if (connectionStatus === 'error') {
      return 'Connection error. Check the backend server and try again.';
    }

    if (connectionStatus === 'connecting') {
      return 'Connecting to the match server...';
    }

    return '';
  }, [connectionStatus, signalRConfigError]);

  const readinessItems = useMemo(
    () => [
      {
        label: 'Player name',
        ready: hasPlayerName,
        text: hasPlayerName ? displayNameInput.trim() : 'Enter a display name.',
      },
      {
        label: 'Room code',
        ready: hasRoomCode,
        text: hasRoomCode ? roomCodeInput.trim().toUpperCase() : 'Create or enter a room code.',
      },
      {
        label: 'Valid deck',
        ready: hasValidSelectedDeck,
        text: hasValidSelectedDeck ? selectedDeck?.name ?? 'Ready' : deckNotice,
      },
      {
        label: joinedRoomCode ? 'Joined' : 'Connection',
        ready: joinedRoomCode.length > 0 || connectionStatus === 'connected',
        text: joinedRoomCode
          ? `Room ${joinedRoomCode}`
          : connectionStatus === 'connected'
            ? 'Connected to match server.'
            : 'Waiting for the match server.',
      },
    ],
    [
      connectionStatus,
      deckNotice,
      displayNameInput,
      hasPlayerName,
      hasRoomCode,
      hasValidSelectedDeck,
      joinedRoomCode,
      roomCodeInput,
      selectedDeck,
    ],
  );

  const activePlayerDisplay = useMemo(() => {
    if (!gameState) {
      return '-';
    }

    const activePlayer = gameState.players.find(
      (player) => player.playerId === gameState.activePlayerId,
    );

    if (!activePlayer) {
      return gameState.activePlayerId || '-';
    }

    return activePlayer.displayName || activePlayer.playerId;
  }, [gameState]);

  const resetRoomState = (): void => {
    setJoinedRoomCode('');
    setGameState(null);
    setCurrentPrompt(null);
    setChatMessages([]);
    setLogEvents([]);
    setChatInput('');
    setIsBoardMode(false);
    void exitBoardPresentation();
  };

  // mode 'create' opens a new room on the server, 'join' enters an existing one.
  const enterRoom = async (mode: 'create' | 'join', roomCode: string): Promise<void> => {
    if (!signalRClient) {
      setError(signalRConfigError || 'Match server is not configured.');
      return;
    }

    const normalizedRoomCode = roomCode.trim().toUpperCase();
    const normalizedDisplayName = displayNameInput.trim();
    if (!normalizedRoomCode || !normalizedDisplayName) {
      setError('RoomCode und DisplayName sind erforderlich.');
      return;
    }

    setPending(true);
    setError('');
    try {
      if (mode === 'create') {
        await signalRClient.createRoom(normalizedRoomCode, normalizedDisplayName);
      } else {
        await signalRClient.joinRoom(normalizedRoomCode, normalizedDisplayName);
      }
      setJoinedRoomCode(normalizedRoomCode);
      setRoomCodeInput(normalizedRoomCode);

      if (selectedDeck && selectedDeckValidation?.isValid) {
        await signalRClient.setPlayerDeck(
          normalizedRoomCode,
          await toPlayerDeckSubmission(selectedDeck),
        );
        setRecentDeckIds(saveRecentDeckId(selectedDeck.id));
      }

      setIsBoardMode(true);
      if (isMobileViewport()) {
        void requestBoardPresentation();
      }
    } catch (joinError) {
      setError(toHubErrorMessage(joinError, 'Join fehlgeschlagen.'));
    } finally {
      setPending(false);
    }
  };

  const createRoom = (): void => {
    const roomCode = roomCodeInput.trim() || createRoomCode();
    void enterRoom('create', roomCode);
  };

  const joinRoom = (roomCode: string = roomCodeInput): void => {
    setRoomCodeInput(roomCode.toUpperCase());
    void enterRoom('join', roomCode);
  };

  const leaveRoom = async (): Promise<void> => {
    if (!signalRClient || !joinedRoomCode) {
      return;
    }

    setPending(true);
    setError('');
    try {
      await signalRClient.leaveRoom(joinedRoomCode);
    } catch (leaveError) {
      // The room may already be gone on the server. We leave locally either way.
      console.warn('[OneSake] LeaveRoom failed', leaveError);
    } finally {
      resetRoomState();
      setPending(false);
    }
  };

  const refreshRooms = (): void => {
    signalRClient
      ?.getRooms()
      .then(setRooms)
      .catch((roomsError) => setError(toHubErrorMessage(roomsError, 'Loading servers failed.')));
  };

  const generateRoomCode = (): void => {
    setRoomCodeInput(createRoomCode());
    setCopyStatus('');
  };

  const randomizeDisplayName = (): void => {
    setDisplayNameInput(generatePirateName());
  };

  const updateRoomCodeInput = (value: string): void => {
    setRoomCodeInput(value.toUpperCase());
  };

  const copyRoomCode = async (): Promise<void> => {
    if (!joinedRoomCode) {
      return;
    }

    try {
      await navigator.clipboard.writeText(joinedRoomCode);
      setCopyStatus('copied');
    } catch {
      setCopyStatus('failed');
    }
  };

  const openBoardMode = (): void => {
    setIsBoardMode(true);
    if (isMobileViewport()) {
      void requestBoardPresentation();
    }
  };

  const exitBoardMode = (): void => {
    setIsBoardMode(false);
    void exitBoardPresentation();
  };

  const startMatch = async (): Promise<void> => {
    if (!joinedRoomCode || !signalRClient) {
      if (!signalRClient) {
        setError(signalRConfigError || 'Match server is not configured.');
      }
      return;
    }

    setPending(true);
    setError('');
    try {
      await signalRClient.startMatch(joinedRoomCode);
      if (selectedDeck && selectedDeckValidation?.isValid) {
        setRecentDeckIds(saveRecentDeckId(selectedDeck.id));
      }
    } catch (startError) {
      const message = startError instanceof Error ? startError.message : 'StartMatch fehlgeschlagen.';
      setError(message);
    } finally {
      setPending(false);
    }
  };

  const selectDeckFromLibrary = (deckId: string): void => {
    setSelectedDeckId(deckId);
    setIsDeckLibraryOpen(false);
  };

  const submitChoice = async (
    option: string,
    selectedCardInstanceId?: string,
    targetCardInstanceId?: string,
  ): Promise<void> => {
    if (!currentPrompt || !joinedRoomCode || !isConnected || !signalRClient) {
      return;
    }

    const submission: ChoiceSubmissionDto = {
      choiceId: currentPrompt.choiceId,
      playerId: currentPrompt.playerId,
      selectedOption: option,
      selectedCardInstanceId,
      targetCardInstanceId,
    };

    setPending(true);
    setError('');
    try {
      await signalRClient.submitChoice(joinedRoomCode, submission);
      // The server may already have sent the NEXT prompt before this call returns.
      // Only clear the prompt we just answered, never a newer one.
      setCurrentPrompt((previous) =>
        previous?.choiceId === submission.choiceId ? null : previous,
      );
    } catch (submitError) {
      const message = toHubErrorMessage(submitError, 'SubmitChoice fehlgeschlagen.');
      setError(message);
    } finally {
      setPending(false);
    }
  };

  const sendChat = async (event: FormEvent): Promise<void> => {
    event.preventDefault();

    const normalizedText = chatInput.trim();
    if (!joinedRoomCode || !normalizedText || !isConnected || !signalRClient) {
      return;
    }

    setPending(true);
    setError('');
    try {
      await signalRClient.sendChat(joinedRoomCode, normalizedText);
      setChatInput('');
    } catch (chatError) {
      const message = chatError instanceof Error ? chatError.message : 'SendChat fehlgeschlagen.';
      setError(message);
    } finally {
      setPending(false);
    }
  };

  if (isBoardMode) {
    return (
      <FullscreenMatchView
        activePlayerDisplay={activePlayerDisplay}
        canStart={canStart}
        canSubmitChoice={isConnected}
        chatInput={chatInput}
        chatMessages={chatMessages}
        connectionStatus={connectionStatus}
        currentPrompt={currentPrompt}
        error={error}
        gameState={gameState}
        joinedRoomCode={joinedRoomCode}
        logEvents={logEvents}
        onChatInputChange={setChatInput}
        onExitBoard={exitBoardMode}
        onLeaveRoom={() => void leaveRoom()}
        onSendChat={sendChat}
        onStartMatch={() => void startMatch()}
        onSubmitChoice={(option, selectedCardInstanceId, targetCardInstanceId) =>
          void submitChoice(option, selectedCardInstanceId, targetCardInstanceId)
        }
        pending={pending}
      />
    );
  }

  return (
    <section className="match-page">
      <section className="panel match-flow-panel" aria-labelledby="match-flow-title">
        <div className="match-flow-panel__header">
          <div>
            <h2 id="match-flow-title">GAME LOBBY</h2>
          </div>
          <ConnectionStatusBadge status={connectionStatus} />
        </div>
        {(connectionNotice || error) && (
          <div className="match-lobby-alerts">
            {connectionNotice && (
              <p className={`connection-notice connection-notice--${connectionStatus}`} role="status">
                {connectionNotice}
              </p>
            )}
            {error && <p className="error-banner" role="alert">{error}</p>}
          </div>
        )}
        <div className="match-flow-steps" aria-label="Match setup steps">
          <span className={hasPlayerName ? 'is-complete' : ''}>1 Player</span>
          <span className={hasRoomCode ? 'is-complete' : ''}>2 Room</span>
          <span className={hasValidSelectedDeck ? 'is-complete' : ''}>3 Deck</span>
          <span className={joinedRoomCode ? 'is-complete' : ''}>4 Board</span>
        </div>

        <LobbyPanel
          canCreate={canCreate}
          canJoin={canJoin}
          canStart={canStart}
          connectionStatus={connectionStatus}
          copyStatus={copyStatus}
          deckSlot={(
            <MatchDeckSelect
              decks={lobbyDecks}
              embedded
              headerAction={(
                <Button onClick={() => setIsDeckLibraryOpen(true)} variant="secondary">
                  DECK LIBRARY
                </Button>
              )}
              onOpenDeckbuilder={onOpenDeckbuilder}
              onSelectDeck={setSelectedDeckId}
              selectedDeckId={selectedDeckId}
              title="MOST RECENT DECKS"
            />
          )}
          deckNotice={deckNotice}
          displayNamePlaceholder={displayNamePlaceholder}
          displayNameInput={displayNameInput}
          joinedRoomCode={joinedRoomCode}
          onCopyRoomCode={() => void copyRoomCode()}
          onCreateRoom={createRoom}
          onDisplayNameChange={setDisplayNameInput}
          onGenerateRoomCode={generateRoomCode}
          onJoinRoom={() => joinRoom()}
          onLeaveRoom={() => void leaveRoom()}
          onOpenBoard={openBoardMode}
          onOpenDeckbuilder={onOpenDeckbuilder}
          onRandomizeDisplayName={randomizeDisplayName}
          onRoomCodeChange={updateRoomCodeInput}
          onStartMatch={() => void startMatch()}
          pending={pending}
          readinessItems={readinessItems}
          roomCodeInput={roomCodeInput}
          serverSlot={(
            <ServerBrowser
              canJoin={canCreate}
              joinedRoomCode={joinedRoomCode}
              onJoinRoom={joinRoom}
              onRefresh={refreshRooms}
              pending={pending}
              rooms={rooms}
            />
          )}
        />

        {joinedRoomCode && (
          <section className="match-open-board-panel match-joined-panel">
            <div>
              <span className="match-setup-kicker">Joined Room</span>
              <h2>{joinedRoomCode}</h2>
              <p>You are still in this room. Open the board again without reconnecting.</p>
            </div>
            <div className="match-joined-panel__actions">
              <Button onClick={() => void copyRoomCode()} variant="secondary">
                {copyStatus === 'copied' ? 'Copied' : 'Copy Room Code'}
              </Button>
              <Button onClick={openBoardMode}>Open Board</Button>
              <Button disabled={!canStart || pending} onClick={() => void startMatch()} variant="secondary">
                Start Match
              </Button>
              <Button disabled={pending} onClick={() => void leaveRoom()} variant="danger">
                Leave Room
              </Button>
            </div>
          </section>
        )}
      </section>

      <section className="panel match-preview-panel" aria-labelledby="match-preview-title">
        <div className="match-preview-panel__header">
          <div>
            <span className="match-setup-kicker">Compact Preview</span>
            <h2 id="match-preview-title">Room Status</h2>
          </div>
          <span>{joinedRoomCode || 'Not joined'}</span>
        </div>
        {joinedRoomCode ? (
          <main className="content-grid">
            <MatchStatePanel
              activePlayerDisplay={activePlayerDisplay}
              canSubmitChoice={isConnected}
              currentPrompt={currentPrompt}
              gameState={gameState}
              joinedRoomCode={joinedRoomCode}
              onSubmitChoice={(option, selectedCardInstanceId) =>
                void submitChoice(option, selectedCardInstanceId)
              }
              pending={pending}
              selectedDeck={selectedDeck}
              selectedDeckValidation={selectedDeckValidation}
            />
            <div className="match-activity-grid">
              <LogPanel logEvents={logEvents} />
              <ChatPanel
                canSendChat={isConnected}
                chatInput={chatInput}
                chatMessages={chatMessages}
                joinedRoomCode={joinedRoomCode}
                onChatInputChange={setChatInput}
                onSendChat={sendChat}
                pending={pending}
              />
            </div>
          </main>
        ) : (
          <div className="match-prejoin-preview">
            <div>
              <span className="match-setup-kicker">Next Step</span>
              <strong>Join a room to load match status, chat and activity.</strong>
            </div>
            <p>Until then, the setup stays focused on name, room code and deck readiness.</p>
          </div>
        )}
      </section>

      <ChoiceSheet
        canSubmitChoice={isConnected}
        currentPrompt={currentPrompt}
        onSubmitChoice={(option, selectedCardInstanceId) =>
          void submitChoice(option, selectedCardInstanceId)
        }
        pending={pending}
      />

      <Modal
        className="match-deck-library-modal"
        onClose={() => setIsDeckLibraryOpen(false)}
        open={isDeckLibraryOpen}
        title="DECK LIBRARY"
      >
        <MatchDeckSelect
          decks={savedDecks}
          embedded
          onOpenDeckbuilder={onOpenDeckbuilder}
          onSelectDeck={selectDeckFromLibrary}
          selectedDeckId={selectedDeckId}
          title="All Decks"
        />
      </Modal>
    </section>
  );
}
