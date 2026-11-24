namespace OneSake.Domain;

public enum MatchPhase
{
    Lobby,
    // Both players roll a die; the winner chooses to go first or second (before the opening hands).
    TurnOrder,
    Mulligan,
    Refresh,
    Draw,
    Don,
    Main,
    End,
    GameOver
}

public record PlayerStateDto
{
    public string PlayerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Connected { get; init; }
    // Set while the player is disconnected: the seat is kept until this time (then the player loses).
    public long? DisconnectedUntilUnixMs { get; init; }
    public int DeckCount { get; init; }
    public int HandCount { get; init; }
    public int LifeCount { get; init; }
    public int BoardCount { get; init; }
    public string DeckName { get; init; } = string.Empty;
    public string LeaderCardId { get; init; } = string.Empty;
    public int MainDeckCount { get; init; }
    public bool HasDeck { get; init; }
    public int TrashCount { get; init; }

    // DON!! cards: still in the DON!! deck, active in the cost area, rested in the cost area.
    public int DonDeckCount { get; init; }
    public int ActiveDonCount { get; init; }
    public int RestedDonCount { get; init; }

    public CardInstanceDto? Leader { get; init; }
    public CardInstanceDto? Stage { get; init; }
    public IReadOnlyList<CardInstanceDto> HandCards { get; init; } = [];
    // BoardCards are the Characters in the Character area (max 5).
    public IReadOnlyList<CardInstanceDto> BoardCards { get; init; } = [];
    public IReadOnlyList<CardInstanceDto> TrashCards { get; init; } = [];
}

public record CardInstanceDto
{
    public string InstanceId { get; init; } = string.Empty;
    public string CardId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int Cost { get; init; }
    public int BasePower { get; init; }
    // Power including given DON!! cards (only counts during the owner's turn).
    public int Power { get; init; }
    public int Counter { get; init; }
    public bool IsRested { get; init; }
    public int AttachedDon { get; init; }
    public bool CanAttack { get; init; }
    // [Rush: Character] in the turn it was played: it may attack Characters, but not the Leader.
    public bool CanAttackOnlyCharacters { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = [];
    // True when the card has an [Activate: Main] ability that can be used right now.
    public bool CanActivate { get; init; }
    // Event with a [Counter] effect (usable in the counter step for its cost).
    public bool IsCounterEvent { get; init; }
    // "auto" (all effects automated), "partial", "manual" (resolve by hand) or "" (no effect text).
    public string EffectStatus { get; init; } = string.Empty;
    // Card script file status: "verified" (tested), "generated" (not checked by hand), "manual" or "".
    public string ScriptStatus { get; init; } = string.Empty;
    // Effect texts the server does not automate. The players resolve them manually.
    public IReadOnlyList<string> ManualEffects { get; init; } = [];
}

public record BattleDto
{
    public string AttackerPlayerId { get; init; } = string.Empty;
    public string AttackerInstanceId { get; init; } = string.Empty;
    public string TargetInstanceId { get; init; } = string.Empty;
    public int AttackerPower { get; init; }
    public int TargetPower { get; init; }
    public int CounterBonus { get; init; }
    public string Step { get; init; } = string.Empty;
}

public record PlayerDeckCardDto
{
    public string CardId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public int? Cost { get; init; }
    public int? Power { get; init; }
    public int? Counter { get; init; }
    public string Text { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    // Card types like "Straw Hat Crew" and the attribute ("Slash"...). Needed for card effects.
    public string Types { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
}

public record PlayerDeckSubmissionDto
{
    public string DeckId { get; init; } = string.Empty;
    public string DeckName { get; init; } = string.Empty;
    public string LeaderCardId { get; init; } = string.Empty;
    public string LeaderName { get; init; } = string.Empty;
    public string LeaderColor { get; init; } = string.Empty;
    public int? LeaderPower { get; init; }
    public int? LeaderLife { get; init; }
    public string LeaderText { get; init; } = string.Empty;
    public string LeaderImageUrl { get; init; } = string.Empty;
    public string LeaderTypes { get; init; } = string.Empty;
    public string LeaderAttribute { get; init; } = string.Empty;
    public IReadOnlyList<PlayerDeckCardDto> Cards { get; init; } = [];
}

public record GameStateDto
{
    public string RoomCode { get; init; } = string.Empty;
    public int TurnNumber { get; init; }
    public string ActivePlayerId { get; init; } = string.Empty;
    public string ViewerPlayerId { get; init; } = string.Empty;
    public string WinnerPlayerId { get; init; } = string.Empty;
    public MatchPhase Phase { get; init; } = MatchPhase.Lobby;
    public BattleDto? Battle { get; init; }
    public IReadOnlyList<PlayerStateDto> Players { get; init; } = [];

    // Dice roll for the first player. Stays in the state after the decision, so a reconnecting
    // client can show the result without rolling again. Null before the first match.
    public TurnOrderRollDto? TurnOrderRoll { get; init; }

    // Quick Match rooms are ranked (they change the bounty), private tables are casual.
    public bool IsRanked { get; init; }

    // Number of recorded decisions in this match ("atSeq" of a bug report). Not the seed.
    public int RecordedInputs { get; init; }

    // Server time when the snapshot was made. Clients compute countdowns with the offset to their
    // own clock, so a wrong phone clock does not matter.
    public long ServerTimeUnixMs { get; init; }

    // GameOver only: LIFE, DECK_OUT, DISCONNECT ... (null while the match runs).
    public string? EndReason { get; init; }

    // How long the viewer's seat is kept after a lost connection (for the "reconnecting" countdown).
    public long? ViewerReconnectGraceMs { get; init; }

    // Turn timer (null: no timer running, e.g. timer off, lobby, or paused while a player is disconnected).
    public TimerDto? Timer { get; init; }

    // "STANDARD", "RELAXED" or "OFF".
    public string TimerMode { get; init; } = "OFF";

    // IDs of the decisions the viewer still has to make. A prompt that is not in this list was
    // answered by the server (time ran out) and is no longer valid.
    public IReadOnlyList<string> OpenChoiceIds { get; init; } = [];
}

// The running clock. The server decides; the client only shows the countdown.
public record TimerDto
{
    // Whose clock runs ("" for the mulligan: both players at the same time).
    public string ActivePlayerId { get; init; } = string.Empty;
    // "TURN", "RESPONSE", "MULLIGAN" or "TURN_ORDER".
    public string Kind { get; init; } = string.Empty;
    // End of the normal time (server time). After that the time bank runs (if the kind has one).
    public long DeadlineUnixMs { get; init; }
    // True when this kind can use the time bank (turn and response).
    public bool UsesTimeBank { get; init; }
    // True when the normal time is already over at the time of the snapshot.
    public bool UsingTimeBank { get; init; }
    // Time bank of every player (for the running player: what was left when this clock started).
    public IReadOnlyDictionary<string, long> TimeBankMs { get; init; } = new Dictionary<string, long>();
    public string Mode { get; init; } = "STANDARD";
}

public record DiceRollDto
{
    public string PlayerId { get; init; } = string.Empty;
    public int Value { get; init; }
}

public record TurnOrderRoundDto
{
    public IReadOnlyList<DiceRollDto> Rolls { get; init; } = [];
    // Random 32-bit seed, only for the dice animation (both clients compute the same path).
    public uint AnimationSeed { get; init; }
}

public record TurnOrderRollDto
{
    // Every round; all but the last one are ties.
    public IReadOnlyList<TurnOrderRoundDto> Rounds { get; init; } = [];
    public string WinnerPlayerId { get; init; } = string.Empty;
    // "GO_FIRST", "GO_SECOND" or null while the winner is still deciding.
    public string? Decision { get; init; }
    public string? FirstPlayerId { get; init; }
}

public record ChoicePromptDto
{
    public string ChoiceId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
    // Card effect prompts: the cards the player can choose and the effect text.
    public IReadOnlyList<CardInstanceDto> Cards { get; init; } = [];
    public string SourceName { get; init; } = string.Empty;
    public string EffectText { get; init; } = string.Empty;
    // ORDER_TRIGGERS: the effects to put in order.
    public IReadOnlyList<TriggerOptionDto> Triggers { get; init; } = [];
}

public record TriggerOptionDto
{
    public string Id { get; init; } = string.Empty;
    public string CardInstanceId { get; init; } = string.Empty;
    public string CardName { get; init; } = string.Empty;
    public string Timing { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public record ChoiceSubmissionDto
{
    public string ChoiceId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string SelectedOption { get; init; } = string.Empty;
    // The card the action is done with (hand card to play, attacker, blocker, counter card...).
    public string? SelectedCardInstanceId { get; init; }
    // The second card of an action (attack target, Character to replace...).
    public string? TargetCardInstanceId { get; init; }
    // ORDER_TRIGGERS: all effect IDs, first = resolved first.
    public IReadOnlyList<string>? OrderedIds { get; init; }
}

public record ChatMessageDto
{
    public string RoomCode { get; init; } = string.Empty;
    public string SenderId { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public long TsUnixMs { get; init; }
}

public record LogEventDto
{
    public string RoomCode { get; init; } = string.Empty;
    public int Seq { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public int TurnNumber { get; init; }
    public MatchPhase Phase { get; init; } = MatchPhase.Lobby;
    public long TsUnixMs { get; init; }
}

public record RoomSummaryDto
{
    public string RoomCode { get; init; } = string.Empty;
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public MatchPhase Phase { get; init; } = MatchPhase.Lobby;
    public IReadOnlyList<string> PlayerNames { get; init; } = [];
    public long CreatedAtUnixMs { get; init; }
    // "STANDARD", "RELAXED" or "OFF".
    public string TimerMode { get; init; } = "STANDARD";
}

// Sent only to the player who took the seat. The token lets the player come back to the seat
// after a lost connection (RejoinRoom). It is a secret: never sent to the opponent or logged.
public record SeatAssignedDto
{
    public string RoomCode { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string RejoinToken { get; init; } = string.Empty;
}

// Answer of RejoinRoom. Status: "OK", "EXPIRED" (deadline over, match lost) or "INVALID".
public record RejoinResultDto
{
    public string Status { get; init; } = string.Empty;
    public string RoomCode { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
}

// Sent to a player after a rejoin: everything the client lost (the state comes as StateSnapshot).
public record ResyncDto
{
    public string RoomCode { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public IReadOnlyList<LogEventDto> LogEvents { get; init; } = [];
    public IReadOnlyList<ChatMessageDto> ChatMessages { get; init; } = [];
    // All decisions this player still has to make, oldest first.
    public IReadOnlyList<ChoicePromptDto> ChoicePrompts { get; init; } = [];
}

// Sent to both players when Quick Match found an opponent.
public record MatchFoundDto
{
    public string RoomCode { get; init; } = string.Empty;
}
