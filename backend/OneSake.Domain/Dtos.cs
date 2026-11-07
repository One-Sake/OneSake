namespace OneSake.Domain;

public enum MatchPhase
{
    Lobby,
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
    public IReadOnlyList<string> Keywords { get; init; } = [];
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
}

public record ChoicePromptDto
{
    public string ChoiceId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
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
