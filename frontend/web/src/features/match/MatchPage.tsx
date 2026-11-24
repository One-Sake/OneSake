import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';

import { getMatchHubUrl } from '../../api/backendUrl';
import { SignalRClient, type ConnectionStatus } from '../../api/signalrClient';
import { clearRejoinData, type RejoinData } from '../../api/rejoinStorage';
import {
  ChatMessageDto,
  ChoicePromptDto,
  ChoiceSubmissionDto,
  GameStateDto,
  LogEventDto,
  MatchFoundDto,
  RoomSummaryDto,
  TimerMode,
} from '../../types/realtime';
import { Modal } from '../../components/ui/Modal';
import { loadRecentDeckIds, loadStoredDecks, saveRecentDeckId } from '../deckbuilder/utils/deckStorage';
import { validateDeck } from '../deckbuilder/utils/deckValidation';
import { ChatPanel } from './ChatPanel';
import { ChoiceSheet } from './ChoiceSheet';
import { FullscreenMatchView } from './FullscreenMatchView';
import { LobbyPanel } from './LobbyPanel';
import { LogPanel } from './LogPanel';
import { toPlayerDeckSubmission } from './matchDeckMapper';
import { MatchDeckSelect } from './MatchDeckSelect';
import { MatchStatePanel } from './MatchStatePanel';
import { formatPhase } from './matchFormatters';
import { generatePirateName } from './pirateNameGenerator';
import { ServerBrowser } from './ServerBrowser';
import { syncServerTime } from './serverClock';
import { useLeaderCards } from './useLeaderCards';

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
  // Time when the Quick Match search started, null while not searching.
  const [queueStartedAt, setQueueStartedAt] = useState<number | null>(null);
  // The SignalR handler is registered once, so it calls the latest version through this ref.
  const matchFoundHandlerRef = useRef<(matchFound: MatchFoundDto) => void>(() => undefined);
  const isSearchingRef = useRef(false);
  // Reconnect: when our connection dropped while in a room, and a short "reconnected" notice.
  const [connectionLostAt, setConnectionLostAt] = useState<number | null>(null);
  const [showReconnected, setShowReconnected] = useState(false);
  // A running match found after a reload ("Back to the match").
  const [rejoinOffer, setRejoinOffer] = useState<RejoinData | null>(null);
  // Timer for tables we open ourselves.
  const [tableTimerMode, setTableTimerMode] = useState<TimerMode>('STANDARD');
  const joinedRoomCodeRef = useRef('');
  const resetRoomStateRef = useRef<() => void>(() => undefined);

  useEffect(() => {
    if (!signalRClient) {
      return undefined;
    }

    const client = signalRClient;
    client.onRoomList((roomList) => {
      setRooms(roomList);
    });
    client.onStateSnapshot((snapshot) => {
      syncServerTime(snapshot.serverTimeUnixMs);
      setGameState(snapshot);
      // Open prompts are cleared on the server when the game ends.
      if (snapshot.phase === 'GameOver' || snapshot.phase === 'Lobby') {
        setCurrentPrompt(null);
      } else if (snapshot.openChoiceIds) {
        // The server decided for us (time ran out): that prompt is gone. New prompts arrive after
        // the snapshot, so this never drops a newer one.
        const openIds = snapshot.openChoiceIds;
        setCurrentPrompt((previous) => (previous && !openIds.includes(previous.choiceId) ? null : previous));
      }

      // After the game the seat is freed on a disconnect: nothing to come back to.
      if (snapshot.phase === 'GameOver') {
        clearRejoinData();
      }
    });

    // Back on our seat: the server sends everything we missed. Log and chat are replaced.
    client.onResync((resync) => {
      setLogEvents(resync.logEvents);
      setChatMessages(resync.chatMessages);
      setCurrentPrompt(resync.choicePrompts[resync.choicePrompts.length - 1] ?? null);
    });

    client.onRejoinResult((result) => {
      setConnectionLostAt(null);
      if (result.status === 'OK') {
        setShowReconnected(true);
        setError('');
        return;
      }

      if (result.status === 'INVALID' && joinedRoomCodeRef.current) {
        resetRoomStateRef.current();
        setError('Das Match ist nicht mehr verfügbar.');
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

    client.onMatchFound((matchFound) => {
      matchFoundHandlerRef.current(matchFound);
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

  // The server keeps our seat for a grace period when the connection drops. The room state stays,
  // the client takes the seat back automatically after reconnecting (RejoinRoom).
  useEffect(() => {
    const lostConnection =
      connectionStatus === 'reconnecting' ||
      connectionStatus === 'disconnected' ||
      connectionStatus === 'error';

    // The server drops the queue entry when the connection is gone.
    if (lostConnection) {
      setQueueStartedAt(null);
    }

    if (lostConnection && joinedRoomCode) {
      setShowReconnected(false);
      setConnectionLostAt((previous) => previous ?? Date.now());
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connectionStatus]);

  useEffect(() => {
    joinedRoomCodeRef.current = joinedRoomCode;
    signalRClient?.setAutoRejoin(Boolean(joinedRoomCode));
    if (joinedRoomCode) {
      setRejoinOffer(null);
    }
  }, [joinedRoomCode, signalRClient]);

  useEffect(() => {
    if (!showReconnected) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setShowReconnected(false), 2500);
    return () => window.clearTimeout(timeoutId);
  }, [showReconnected]);

  // After a reload: is there a running match we can go back to?
  useEffect(() => {
    if (!signalRClient || connectionStatus !== 'connected' || joinedRoomCodeRef.current) {
      return;
    }

    signalRClient
      .checkStoredRejoin()
      .then((data) => {
        if (!joinedRoomCodeRef.current) {
          setRejoinOffer(data);
        }
      })
      .catch(() => setRejoinOffer(null));
  }, [connectionStatus, signalRClient]);

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
  // Quick Match needs the same as Create/Join plus a live connection.
  const canQuickMatch = canCreate && connectionStatus === 'connected';
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

  const leaderCardsById = useLeaderCards(savedDecks);
  // Players in our room: from the live game state, or from the server list as a fallback.
  const joinedRoomSummary = rooms.find((room) => room.roomCode === joinedRoomCode);
  const playersInRoom = Math.max(
    gameState?.players.length ?? 0,
    joinedRoomSummary?.playerCount ?? 0,
  );
  const isMatchRunning = Boolean(gameState) && formatPhase(gameState?.phase ?? 'Lobby') !== 'Lobby';

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
    setConnectionLostAt(null);
    setJoinedRoomCode('');
    setGameState(null);
    setCurrentPrompt(null);
    setChatMessages([]);
    setLogEvents([]);
    setChatInput('');
    setIsBoardMode(false);
    void exitBoardPresentation();
  };

  // "Back to the match": take the stored seat again and open the board.
  const resumeMatch = async (): Promise<void> => {
    if (!signalRClient || !rejoinOffer) {
      return;
    }

    setPending(true);
    setError('');
    try {
      setJoinedRoomCode(rejoinOffer.roomCode);
      joinedRoomCodeRef.current = rejoinOffer.roomCode;
      signalRClient.setAutoRejoin(true);
      const result = await signalRClient.rejoinStored();
      if (result?.status === 'OK' || result?.status === 'EXPIRED') {
        setRoomCodeInput(rejoinOffer.roomCode);
        openBoardMode();
      } else {
        resetRoomState();
        setError('Das Match ist nicht mehr verfügbar.');
      }
    } finally {
      setRejoinOffer(null);
      setPending(false);
    }
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
        await signalRClient.createRoom(normalizedRoomCode, normalizedDisplayName, tableTimerMode);
      } else {
        await signalRClient.joinRoom(normalizedRoomCode, normalizedDisplayName);
      }
      await completeRoomEntry(normalizedRoomCode);
    } catch (joinError) {
      setError(toHubErrorMessage(joinError, 'Join fehlgeschlagen.'));
    } finally {
      setPending(false);
    }
  };

  // Shared by Create/Join and Quick Match: remember the room, send the deck, open the board.
  const completeRoomEntry = async (roomCode: string): Promise<void> => {
    if (!signalRClient) {
      return;
    }

    setQueueStartedAt(null);
    setJoinedRoomCode(roomCode);
    setRoomCodeInput(roomCode);

    if (selectedDeck && selectedDeckValidation?.isValid) {
      await signalRClient.setPlayerDeck(roomCode, await toPlayerDeckSubmission(selectedDeck));
      setRecentDeckIds(saveRecentDeckId(selectedDeck.id));
    }

    setIsBoardMode(true);
    if (isMobileViewport()) {
      void requestBoardPresentation();
    }
  };

  const startQuickMatch = async (): Promise<void> => {
    const normalizedDisplayName = displayNameInput.trim();
    if (!signalRClient || !normalizedDisplayName) {
      return;
    }

    setError('');
    setQueueStartedAt(Date.now());
    try {
      await signalRClient.joinQueue(normalizedDisplayName);
    } catch (queueError) {
      setQueueStartedAt(null);
      setError(toHubErrorMessage(queueError, 'Quick Match fehlgeschlagen.'));
    }
  };

  const cancelQuickMatch = (): void => {
    setQueueStartedAt(null);
    signalRClient?.leaveQueue().catch((queueError) => {
      console.warn('[OneSake] LeaveQueue failed', queueError);
    });
  };

  const handleMatchFound = async (matchFound: MatchFoundDto): Promise<void> => {
    setPending(true);
    setError('');
    try {
      await completeRoomEntry(matchFound.roomCode.trim().toUpperCase());
    } catch (matchError) {
      setError(toHubErrorMessage(matchError, 'Quick Match room could not be entered.'));
    } finally {
      setPending(false);
    }
  };

  useEffect(() => {
    matchFoundHandlerRef.current = (matchFound) => void handleMatchFound(matchFound);
    resetRoomStateRef.current = resetRoomState;
    isSearchingRef.current = queueStartedAt !== null;
  });

  // Leaving the match tab while searching takes the player out of the queue.
  useEffect(
    () => () => {
      if (isSearchingRef.current) {
        signalRClient?.leaveQueue().catch(() => undefined);
      }
    },
    [signalRClient],
  );

  // Game over: a new table with a fresh code (the old room keeps its code), same deck and name.
  const createRematchTable = (): void => {
    setGameState(null);
    setCurrentPrompt(null);
    setChatMessages([]);
    setLogEvents([]);
    void enterRoom('create', createRoomCode());
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
      clearRejoinData();
      await signalRClient.leaveRoom(joinedRoomCode);
    } catch (leaveError) {
      // The room may already be gone on the server. We leave locally either way.
      console.warn('[OneSake] LeaveRoom failed', leaveError);
    } finally {
      resetRoomState();
      // The code input is hidden in the lobby, so a stale code must not be reused by "Create Room".
      setRoomCodeInput('');
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
    orderedIds?: string[],
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
      orderedIds,
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
        connectionLostAt={connectionLostAt}
        connectionStatus={connectionStatus}
        currentPrompt={currentPrompt}
        error={error}
        gameState={gameState}
        joinedRoomCode={joinedRoomCode}
        logEvents={logEvents}
        onChatInputChange={setChatInput}
        onExitBoard={exitBoardMode}
        onLeaveRoom={() => void leaveRoom()}
        onSubmitOrder={(orderedIds) => void submitChoice('ORDER', undefined, undefined, orderedIds)}

        onRematch={createRematchTable}
        onSendChat={sendChat}
        onStartMatch={() => void startMatch()}
        onSubmitChoice={(option, selectedCardInstanceId, targetCardInstanceId) =>
          void submitChoice(option, selectedCardInstanceId, targetCardInstanceId)
        }
        pending={pending}
        showReconnected={showReconnected}
      />
    );
  }

  return (
    <section className="match-page">
      {rejoinOffer && !joinedRoomCode && (
        <div className="match-rejoin-card" role="status">
          <div>
            <strong>Laufendes Match gefunden</strong>
            <span>Raum {rejoinOffer.roomCode} – dein Platz ist noch frei.</span>
          </div>
          <button className="ui-button" disabled={pending} onClick={() => void resumeMatch()} type="button">
            Zurück ins Match
          </button>
        </div>
      )}
      <LobbyPanel
        canCreate={canCreate}
        canJoin={canJoin}
        canQuickMatch={canQuickMatch}
        canStart={canStart}
        connectionNotice={connectionNotice}
        connectionStatus={connectionStatus}
        copyStatus={copyStatus}
        deckNotice={deckNotice}
        decks={lobbyDecks}
        displayNameInput={displayNameInput}
        displayNamePlaceholder={displayNamePlaceholder}
        error={error}
        isMatchRunning={isMatchRunning}
        joinedRoomCode={joinedRoomCode}
        leaderCardsById={leaderCardsById}
        onCancelQuickMatch={cancelQuickMatch}
        onCopyRoomCode={() => void copyRoomCode()}
        onCreateRoom={createRoom}
        onDisplayNameChange={setDisplayNameInput}
        onGenerateRoomCode={generateRoomCode}
        onJoinRoom={() => joinRoom()}
        onLeaveRoom={() => void leaveRoom()}
        onOpenBoard={openBoardMode}
        onOpenDeckbuilder={onOpenDeckbuilder}
        onOpenDeckLibrary={() => setIsDeckLibraryOpen(true)}
        onRandomizeDisplayName={randomizeDisplayName}
        onRoomCodeChange={updateRoomCodeInput}
        onSelectDeck={setSelectedDeckId}
        onStartMatch={() => void startMatch()}
        onStartQuickMatch={() => void startQuickMatch()}
        pending={pending}
        playersInRoom={playersInRoom}
        onTableTimerModeChange={setTableTimerMode}
        queueStartedAt={queueStartedAt}
        roomCodeInput={roomCodeInput}
        tableTimerMode={tableTimerMode}
        selectedDeck={selectedDeck}
        selectedDeckId={selectedDeckId}
        serverSlot={(
          <ServerBrowser
            canCreate={canCreate}
            canJoin={canCreate}
            joinedRoomCode={joinedRoomCode}
            onCreateRoom={createRoom}
            onJoinRoom={joinRoom}
            onRefresh={refreshRooms}
            pending={pending}
            rooms={rooms}
          />
        )}
      />

      {joinedRoomCode && (
        <section className="panel match-preview-panel" aria-labelledby="match-preview-title">
          <div className="match-preview-panel__header">
            <h2 id="match-preview-title">Room Status</h2>
            <span>{joinedRoomCode}</span>
          </div>
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
        </section>
      )}

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
