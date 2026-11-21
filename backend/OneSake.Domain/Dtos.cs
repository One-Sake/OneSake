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
    // Manual mode: cards only this player may see right now (top of the deck, deck search).
    // Only filled in the snapshot for this player, null for the opponent.
    public ManualViewDto? ManualView { get; init; }
}

public record ManualViewDto
{
    // "LOOK" (top N cards of the deck) or "SEARCH" (the whole deck).
    public string Kind { get; init; } = string.Empty;
    public IReadOnlyList<CardInstanceDto> Cards { get; init; } = [];
}

// Manual mode: a player resolves a card effect by hand (move a card, K.O., +power...).
public record ManualActionDto
{
    // DRAW, MOVE_CARD, KO_CARD, RETURN_TO_HAND, RETURN_TO_DECK, SET_REST, SET_ACTIVE, MODIFY_POWER,
    // MODIFY_COST, DON_ADD, DON_RETURN, DON_ATTACH, DON_SET_REST, DON_SET_ACTIVE, LOOK_TOP, SEARCH_DECK,
    // CLOSE_VIEW, SHUFFLE_DECK, REVEAL_CARD, TRASH_FROM_HAND, LIFE_TO_HAND
    public string Kind { get; init; } = string.Empty;
    public string? CardInstanceId { get; init; }
    // Zones: hand, character, stage, leader, trash, deck, life. For DON!!: active, rested.
    public string? FromZone { get; init; }
    public string? ToZone { get; init; }
    // "top", "bottom" or an index.
    public string? ToPosition { get; init; }
    public int? Amount { get; init; }
    public string? TargetPlayerId { get; init; }
    // MODIFY_POWER / MODIFY_COST: "turn" (default) or "battle".
    public string? Duration { get; init; }
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
}

// Sent only to the player who took the seat. The token lets the player come back to the seat
// after a lost connection (RejoinRoom). It is a secret: never sent to the opponent or logged.
public record SeatAssignedDto
{
    public string RoomCode { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string RejoinToken { get; init; } = string.Empty;
}

// Sent to both players when Quick Match found an opponent.
public record MatchFoundDto
{
    public string RoomCode { get; init; } = string.Empty;
}
