import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr';
import {
  ChatMessageDto,
  ChoiceSubmissionDto,
  ChoicePromptDto,
  GameStateDto,
  LogEventDto,
  ManualActionDto,
  MatchFoundDto,
  PlayerDeckSubmissionDto,
  RejoinResultDto,
  ResyncDto,
  RoomSummaryDto,
  SeatAssignedDto,
} from '../types/realtime';
import { getGuestId } from './guestId';
import { clearRejoinData, loadRejoinData, saveRejoinData, type RejoinData } from './rejoinStorage';

type StateSnapshotHandler = (snapshot: GameStateDto) => void;
type ChoicePromptHandler = (prompt: ChoicePromptDto) => void;
type ChatMessageHandler = (message: ChatMessageDto) => void;
type LogEventHandler = (event: LogEventDto) => void;
type RoomListHandler = (rooms: RoomSummaryDto[]) => void;
type MatchFoundHandler = (matchFound: MatchFoundDto) => void;
type ResyncHandler = (resync: ResyncDto) => void;
type RejoinResultHandler = (result: RejoinResultDto) => void;

// Keep trying for a few minutes (the server keeps the seat for up to 90 s by default),
// quickly at first. The default policy of SignalR gives up after about 40 seconds.
const RECONNECT_DELAYS_MS = [0, 1000, 2000, 3000, 5000];
const RECONNECT_GIVE_UP_MS = 10 * 60 * 1000;
export type ConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'error';

type ConnectionStatusHandler = (status: ConnectionStatus) => void;

export class SignalRClient {
  private readonly hubUrl: string;
  private connection: HubConnection | null = null;
  private connectionStatus: ConnectionStatus = 'disconnected';
  private connectionStatusHandler: ConnectionStatusHandler | null = null;
  private connectPromise: Promise<void> | null = null;

  private stateSnapshotHandler: StateSnapshotHandler | null = null;
  private choicePromptHandler: ChoicePromptHandler | null = null;
  private chatMessageHandler: ChatMessageHandler | null = null;
  private logEventHandler: LogEventHandler | null = null;
  private roomListHandler: RoomListHandler | null = null;
  private matchFoundHandler: MatchFoundHandler | null = null;
  private resyncHandler: ResyncHandler | null = null;
  private rejoinResultHandler: RejoinResultHandler | null = null;
  // True while we sit in a room: after a new connection we automatically take our seat back.
  private autoRejoin = false;
  private retryTimer: number | null = null;

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl;

    if (typeof window !== 'undefined') {
      // Mobile: coming back to the app (or the network coming back) checks the connection right
      // away instead of waiting for the next SignalR retry.
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          void this.ensureConnected();
        }
      });
      window.addEventListener('online', () => void this.ensureConnected());
    }
  }

  // Called by the match page: in a room the seat is taken back after every reconnect.
  setAutoRejoin(enabled: boolean): void {
    this.autoRejoin = enabled;
    if (!enabled && this.retryTimer !== null) {
      window.clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
  }

  private async ensureConnected(): Promise<void> {
    const state = this.connection?.state;
    if (!this.connection || state === HubConnectionState.Connected || state === HubConnectionState.Connecting) {
      return;
    }

    try {
      if (state === HubConnectionState.Reconnecting) {
        // Skip the waiting time of the current retry: start a fresh connection now.
        await this.connection.stop();
      }

      await this.connect();
    } catch {
      // The status badge shows the error, the retry loop continues.
    }
  }

  // After the connection was closed for good: keep trying while we are in a room.
  private scheduleRetry(): void {
    if (!this.autoRejoin || this.retryTimer !== null) {
      return;
    }

    this.retryTimer = window.setTimeout(() => {
      this.retryTimer = null;
      this.connect().catch(() => this.scheduleRetry());
    }, 3000);
  }

  // Takes our stored seat back (new connection after a drop or a reload).
  async rejoinStored(): Promise<RejoinResultDto | null> {
    const data = loadRejoinData();
    if (!data || this.connection?.state !== HubConnectionState.Connected) {
      return null;
    }

    try {
      const result = await this.connection.invoke<RejoinResultDto>(
        'RejoinRoom',
        data.roomCode,
        data.playerId,
        data.rejoinToken,
      );
      if (result.status !== 'OK') {
        clearRejoinData();
      }

      this.rejoinResultHandler?.(result);
      return result;
    } catch (error) {
      console.warn('[OneSake] RejoinRoom failed', error);
      return null;
    }
  }

  // Is the stored seat still there? Does not take it (for the "Back to the match" card).
  async checkStoredRejoin(): Promise<RejoinData | null> {
    const data = loadRejoinData();
    if (!data) {
      return null;
    }

    await this.connect();
    try {
      const result = await this.connection!.invoke<RejoinResultDto>(
        'CheckRejoin',
        data.roomCode,
        data.playerId,
        data.rejoinToken,
      );
      if (result.status === 'OK') {
        return data;
      }
    } catch (error) {
      console.warn('[OneSake] CheckRejoin failed', error);
      return null;
    }

    // Expired or unknown: forget it silently.
    clearRejoinData();
    return null;
  }

  // The guest ID travels in the hub URL, so CreateRoom/JoinRoom keep their signature.
  private getHubUrlWithGuestId(): string {
    try {
      const url = new URL(this.hubUrl, window.location.href);
      url.searchParams.set('guestId', getGuestId());
      return url.toString();
    } catch {
      return this.hubUrl;
    }
  }

  private toConnectionError(error: unknown): Error {
    const message = error instanceof Error ? error.message : String(error);

    if (message.includes('404')) {
      return new Error('Match server not found. Check backend URL or deployment.');
    }

    if (message.includes('Failed to fetch') || message.includes('ERR_CONNECTION')) {
      return new Error('Match server is unreachable. Check backend URL, CORS or network access.');
    }

    return error instanceof Error ? error : new Error(message);
  }

  private notifyConnectionStatus(status: ConnectionStatus): void {
    if (this.connectionStatus === status) {
      return;
    }

    this.connectionStatus = status;
    this.connectionStatusHandler?.(status);
  }

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      this.notifyConnectionStatus('connected');
      return;
    }

    if (this.connectPromise) {
      return this.connectPromise;
    }

    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl(this.getHubUrlWithGuestId())
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (context) =>
            context.elapsedMilliseconds > RECONNECT_GIVE_UP_MS
              ? null
              : RECONNECT_DELAYS_MS[Math.min(context.previousRetryCount, RECONNECT_DELAYS_MS.length - 1)],
        })
        .build();

      this.connection.on('SeatAssigned', (seat: SeatAssignedDto) => {
        saveRejoinData(seat);
      });

      this.connection.on('Resync', (resync: ResyncDto) => {
        this.resyncHandler?.(resync);
      });

      this.connection.on('StateSnapshot', (snapshot: GameStateDto) => {
        this.stateSnapshotHandler?.(snapshot);
      });

      this.connection.on('ChoicePrompt', (prompt: ChoicePromptDto) => {
        this.choicePromptHandler?.(prompt);
      });

      this.connection.on('ChatMessage', (message: ChatMessageDto) => {
        this.chatMessageHandler?.(message);
      });

      this.connection.on('LogEvent', (event: LogEventDto) => {
        this.logEventHandler?.(event);
      });

      this.connection.on('RoomList', (rooms: RoomSummaryDto[]) => {
        this.roomListHandler?.(rooms);
      });

      this.connection.on('MatchFound', (matchFound: MatchFoundDto) => {
        this.matchFoundHandler?.(matchFound);
      });

      this.connection.onreconnecting(() => {
        this.notifyConnectionStatus('reconnecting');
      });

      this.connection.onreconnected(() => {
        this.notifyConnectionStatus('connected');
        if (this.autoRejoin) {
          void this.rejoinStored();
        }
      });

      this.connection.onclose((error) => {
        this.connectPromise = null;
        this.notifyConnectionStatus(error ? 'error' : 'disconnected');
        this.scheduleRetry();
      });
    }

    if (this.connection.state === HubConnectionState.Disconnected) {
      this.notifyConnectionStatus('connecting');
      this.connectPromise = this.connection
        .start()
        .then(() => {
          this.notifyConnectionStatus('connected');
          if (this.autoRejoin) {
            void this.rejoinStored();
          }
        })
        .catch((error) => {
          console.error('[OneSake] SignalR connection failed', {
            hubUrl: this.hubUrl,
            error,
          });
          this.notifyConnectionStatus('error');
          throw this.toConnectionError(error);
        })
        .finally(() => {
          this.connectPromise = null;
        });

      return this.connectPromise;
    }

    if (this.connection.state === HubConnectionState.Reconnecting) {
      this.notifyConnectionStatus('reconnecting');
      throw new Error('Connection is reconnecting. Please try again in a moment.');
    }

    if (this.connection.state === HubConnectionState.Connecting) {
      this.notifyConnectionStatus('connecting');
    }
  }

  async getRooms(): Promise<RoomSummaryDto[]> {
    await this.connect();
    return this.connection!.invoke<RoomSummaryDto[]>('GetRooms');
  }

  async createRoom(roomCode: string, displayName: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('CreateRoom', roomCode, displayName);
  }

  async leaveRoom(roomCode: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('LeaveRoom', roomCode);
  }

  async joinRoom(roomCode: string, displayName: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinRoom', roomCode, displayName);
  }

  // Quick Match: wait for an opponent. The server answers with a "MatchFound" event.
  async joinQueue(displayName: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinQueue', displayName, getGuestId());
  }

  async leaveQueue(): Promise<void> {
    await this.connect();
    await this.connection!.invoke('LeaveQueue');
  }

  async startMatch(roomCode: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('StartMatch', roomCode);
  }

  async setPlayerDeck(roomCode: string, deck: PlayerDeckSubmissionDto): Promise<void> {
    await this.connect();
    await this.connection!.invoke('SetPlayerDeck', roomCode, deck);
  }

  async submitChoice(roomCode: string, submission: ChoiceSubmissionDto): Promise<void> {
    await this.connect();
    await this.connection!.invoke('SubmitChoice', roomCode, submission);
  }

  // Manual mode: resolve a card effect by hand (move a card, K.O., +power ...).
  async manualAction(roomCode: string, action: ManualActionDto): Promise<void> {
    await this.connect();
    await this.connection!.invoke('ManualAction', roomCode, action);
  }

  async sendChat(roomCode: string, text: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('SendChat', roomCode, text);
  }

  onStateSnapshot(handler: StateSnapshotHandler): void {
    this.stateSnapshotHandler = handler;
  }

  onChoicePrompt(handler: ChoicePromptHandler): void {
    this.choicePromptHandler = handler;
  }

  onChatMessage(handler: ChatMessageHandler): void {
    this.chatMessageHandler = handler;
  }

  onLogEvent(handler: LogEventHandler): void {
    this.logEventHandler = handler;
  }

  onRoomList(handler: RoomListHandler): void {
    this.roomListHandler = handler;
  }

  onMatchFound(handler: MatchFoundHandler): void {
    this.matchFoundHandler = handler;
  }

  onResync(handler: ResyncHandler): void {
    this.resyncHandler = handler;
  }

  onRejoinResult(handler: RejoinResultHandler): void {
    this.rejoinResultHandler = handler;
  }

  onConnectionStatus(handler: ConnectionStatusHandler): void {
    this.connectionStatusHandler = handler;
    handler(this.connectionStatus);
  }
}
