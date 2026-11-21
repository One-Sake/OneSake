export type MatchPhase =
  | 'Lobby'
  | 'TurnOrder'
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
  // Set while the player is disconnected: the seat is kept until then (server time).
  disconnectedUntilUnixMs?: number | null;
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
  // Manual mode: deck cards only this player sees right now (null for the opponent).
  manualView?: ManualViewDto | null;
}

export interface ManualViewDto {
  // 'LOOK' = top N cards of the deck, 'SEARCH' = the whole deck.
  kind: 'LOOK' | 'SEARCH';
  cards: CardInstanceDto[];
}

// Manual mode: resolve a card effect by hand. The server checks every action.
export interface ManualActionDto {
  kind: string;
  cardInstanceId?: string;
  // hand, character, stage, leader, trash, deck, life (DON!!: active, rested)
  fromZone?: string;
  toZone?: string;
  // 'top', 'bottom' or an index
  toPosition?: string;
  amount?: number;
  targetPlayerId?: string;
  // MODIFY_POWER / MODIFY_COST: 'turn' or 'battle'
  duration?: 'turn' | 'battle';
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
  // [Rush: Character] in the turn it was played: only Characters are valid attack targets.
  canAttackOnlyCharacters?: boolean;
  keywords?: string[];
  // [Activate: Main] ability that can be used right now.
  canActivate?: boolean;
  // Event with a [Counter] effect (usable in the counter step for its cost).
  isCounterEvent?: boolean;
  // 'auto' = all effects automated, 'partial', 'manual' = resolve by hand, '' = no effect text.
  effectStatus?: '' | 'auto' | 'partial' | 'manual';
  // Card script file: 'verified' (tested), 'generated' (not checked by hand), 'manual' or ''.
  scriptStatus?: '' | 'verified' | 'generated' | 'manual';
  // Effect texts the server does not automate.
  manualEffects?: string[];
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
  // Card types ("Straw Hat Crew") and attribute ("Slash"). The server needs them for card effects.
  types?: string;
  attribute?: string;
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
  leaderTypes?: string;
  leaderAttribute?: string;
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
  // Dice roll for the first player (stays after the decision). Missing on older servers.
  turnOrderRoll?: TurnOrderRollDto | null;
  // Quick Match rooms are ranked (they change the bounty), private tables are casual.
  isRanked?: boolean;
  // Number of recorded decisions in this match (sent with a bug report).
  recordedInputs?: number;
  // Server time of the snapshot: countdowns use the offset to the local clock.
  serverTimeUnixMs?: number;
  // GameOver only: 'LIFE', 'DECK_OUT', 'DISCONNECT', 'TIMEOUT' ...
  endReason?: string | null;
  // How long our seat is kept if the connection drops now.
  viewerReconnectGraceMs?: number | null;
  // Turn timer (null: no clock running - timer off, lobby, or paused during a reconnect).
  timer?: TimerDto | null;
  timerMode?: TimerMode;
  // Our open decisions. A prompt not in this list was answered by the server (time ran out).
  openChoiceIds?: string[];
}

export type TimerMode = 'STANDARD' | 'RELAXED' | 'OFF';

export interface TimerDto {
  // Whose clock runs ('' during the mulligan: both players).
  activePlayerId: string;
  kind: 'TURN' | 'RESPONSE' | 'MULLIGAN' | 'TURN_ORDER';
  // End of the normal time (server time); after that the time bank runs if usesTimeBank.
  deadlineUnixMs: number;
  usesTimeBank: boolean;
  usingTimeBank: boolean;
  // Time bank per player in ms (for the running player: what was left when the clock started).
  timeBankMs: Record<string, number>;
  mode: TimerMode;
}

export interface DiceRollDto {
  playerId: string;
  value: number;
}

export interface TurnOrderRoundDto {
  rolls: DiceRollDto[];
  // 32-bit seed, only for the animation.
  animationSeed: number;
}

export interface TurnOrderRollDto {
  rounds: TurnOrderRoundDto[];
  winnerPlayerId: string;
  decision?: 'GO_FIRST' | 'GO_SECOND' | null;
  firstPlayerId?: string | null;
}

export interface ChoicePromptDto {
  choiceId: string;
  playerId: string;
  kind: string;
  title: string;
  options: string[];
  // Card effect prompts (EFFECT_CHOICE, TRIGGER_DECISION): cards to choose from and the effect text.
  cards?: CardInstanceDto[];
  sourceName?: string;
  effectText?: string;
  // ORDER_TRIGGERS: the effects to put in order.
  triggers?: TriggerOptionDto[];
}

export interface TriggerOptionDto {
  id: string;
  cardInstanceId: string;
  cardName: string;
  timing: string;
  text: string;
}

export interface ChoiceSubmissionDto {
  choiceId: string;
  playerId: string;
  selectedOption: string;
  selectedCardInstanceId?: string;
  targetCardInstanceId?: string;
  // ORDER_TRIGGERS: all effect ids, first = resolved first.
  orderedIds?: string[];
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
  timerMode?: TimerMode;
}

// Only for us: our seat in the room and the secret token to get it back after a lost connection.
export interface SeatAssignedDto {
  roomCode: string;
  playerId: string;
  rejoinToken: string;
}

export interface RejoinResultDto {
  status: 'OK' | 'EXPIRED' | 'INVALID';
  roomCode: string;
  playerId: string;
}

// After a rejoin: everything the client lost while it was offline (the state comes as snapshot).
export interface ResyncDto {
  roomCode: string;
  playerId: string;
  logEvents: LogEventDto[];
  chatMessages: ChatMessageDto[];
  choicePrompts: ChoicePromptDto[];
}

// Sent by the server when Quick Match paired two players.
export interface MatchFoundDto {
  roomCode: string;
}
