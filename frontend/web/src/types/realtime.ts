export type MatchPhase =
  | 'Lobby'
  | 'Mulligan'
  | 'Refresh'
  | 'Draw'
  | 'Main'
  | 'End'
  | 'GameOver'
  | number;

export interface PlayerStateDto {
  playerId: string;
  displayName: string;
  connected: boolean;
  deckCount: number;
  handCount: number;
  lifeCount: number;
  boardCount: number;
  deckName: string;
  leaderCardId: string;
  mainDeckCount: number;
  hasDeck: boolean;
  trashCount: number;
  handCards: CardInstanceDto[];
  boardCards: CardInstanceDto[];
  trashCards: CardInstanceDto[];
}

export interface CardInstanceDto {
  instanceId: string;
  cardId: string;
  name: string;
}

export interface PlayerDeckCardDto {
  cardId: string;
  name: string;
  quantity: number;
}

export interface PlayerDeckSubmissionDto {
  deckId: string;
  deckName: string;
  leaderCardId: string;
  cards: PlayerDeckCardDto[];
}

export interface GameStateDto {
  roomCode: string;
  turnNumber: number;
  activePlayerId: string;
  viewerPlayerId: string;
  phase: MatchPhase;
  players: PlayerStateDto[];
}

export interface ChoicePromptDto {
  choiceId: string;
  playerId: string;
  kind: string;
  title: string;
  options: string[];
}

export interface ChoiceSubmissionDto {
  choiceId: string;
  playerId: string;
  selectedOption: string;
  selectedCardInstanceId?: string;
}

export interface ChatMessageDto {
  roomCode: string;
  senderId: string;
  text: string;
  tsUnixMs: number;
}

export interface LogEventDto {
  roomCode: string;
  seq: number;
  type: string;
  text: string;
  turnNumber: number;
  phase: MatchPhase;
  tsUnixMs: number;
}

export interface RoomSummaryDto {
  roomCode: string;
  playerCount: number;
  maxPlayers: number;
  phase: MatchPhase;
  playerNames: string[];
  createdAtUnixMs: number;
}
