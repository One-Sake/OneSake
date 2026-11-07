export type MatchPhase =
  | 'Lobby'
  | 'Mulligan'
  | 'Refresh'
  | 'Draw'
  | 'Don'
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
  donDeckCount: number;
  activeDonCount: number;
  restedDonCount: number;
  leader: CardInstanceDto | null;
  stage: CardInstanceDto | null;
  handCards: CardInstanceDto[];
  boardCards: CardInstanceDto[];
  trashCards: CardInstanceDto[];
}

export interface CardInstanceDto {
  instanceId: string;
  cardId: string;
  name: string;
  category?: string;
  color?: string;
  imageUrl?: string;
  text?: string;
  cost?: number;
  basePower?: number;
  power?: number;
  counter?: number;
  isRested?: boolean;
  attachedDon?: number;
  canAttack?: boolean;
  keywords?: string[];
}

export interface BattleDto {
  attackerPlayerId: string;
  attackerInstanceId: string;
  targetInstanceId: string;
  attackerPower: number;
  targetPower: number;
  counterBonus: number;
  step: string;
}

export interface PlayerDeckCardDto {
  cardId: string;
  name: string;
  quantity: number;
  category?: string;
  color?: string;
  cost?: number | null;
  power?: number | null;
  counter?: number | null;
  text?: string;
  imageUrl?: string;
}

export interface PlayerDeckSubmissionDto {
  deckId: string;
  deckName: string;
  leaderCardId: string;
  leaderName?: string;
  leaderColor?: string;
  leaderPower?: number | null;
  leaderLife?: number | null;
  leaderText?: string;
  leaderImageUrl?: string;
  cards: PlayerDeckCardDto[];
}

export interface GameStateDto {
  roomCode: string;
  turnNumber: number;
  activePlayerId: string;
  viewerPlayerId: string;
  winnerPlayerId?: string;
  phase: MatchPhase;
  battle?: BattleDto | null;
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
  targetCardInstanceId?: string;
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
