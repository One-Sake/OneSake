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
  PlayerDeckSubmissionDto,
  RoomSummaryDto,
} from '../types/realtime';

type StateSnapshotHandler = (snapshot: GameStateDto) => void;
type ChoicePromptHandler = (prompt: ChoicePromptDto) => void;
type ChatMessageHandler = (message: ChatMessageDto) => void;
type LogEventHandler = (event: LogEventDto) => void;
type RoomListHandler = (rooms: RoomSummaryDto[]) => void;
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

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl;
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
        .withUrl(this.hubUrl)
        .withAutomaticReconnect()
        .build();

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

      this.connection.onreconnecting(() => {
        this.notifyConnectionStatus('reconnecting');
      });

      this.connection.onreconnected(() => {
        this.notifyConnectionStatus('connected');
      });

      this.connection.onclose((error) => {
        this.connectPromise = null;
        this.notifyConnectionStatus(error ? 'error' : 'disconnected');
      });
    }

    if (this.connection.state === HubConnectionState.Disconnected) {
      this.notifyConnectionStatus('connecting');
      this.connectPromise = this.connection
        .start()
        .then(() => {
          this.notifyConnectionStatus('connected');
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

  onConnectionStatus(handler: ConnectionStatusHandler): void {
    this.connectionStatusHandler = handler;
    handler(this.connectionStatus);
  }
}
