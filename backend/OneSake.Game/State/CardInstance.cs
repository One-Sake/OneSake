namespace OneSake.Game.State;

// One physical card in a match. Static card data (cost, power...) comes from the deck submission,
// the mutable fields (rested, given DON!!...) change while the game is played.
public sealed class CardInstance
{
    public const string LeaderCategory = "Leader";
    public const string CharacterCategory = "Character";
    public const string EventCategory = "Event";
    public const string StageCategory = "Stage";

    public string InstanceId { get; init; } = string.Empty;
    public string CardId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = CharacterCategory;
    public string Color { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int Cost { get; init; }
    public int BasePower { get; init; }
    public int Counter { get; init; }
    public int Life { get; init; }

    public bool IsRested { get; set; }
    public int AttachedDon { get; set; }
    // Turn number in which this card was played. Characters cannot attack in that turn (unless [Rush]).
    public int PlayedOnTurn { get; set; }

    public bool IsLeader => Category == LeaderCategory;
    public bool IsCharacter => Category == CharacterCategory;
    public bool IsEvent => Category == EventCategory;
    public bool IsStage => Category == StageCategory;

    public IReadOnlyList<string> Keywords => CardKeywords.Parse(Text);
    public bool HasRush => Keywords.Contains(CardKeywords.Rush);
    public bool HasBlocker => Keywords.Contains(CardKeywords.Blocker);
    public bool HasDoubleAttack => Keywords.Contains(CardKeywords.DoubleAttack);
    public bool HasBanish => Keywords.Contains(CardKeywords.Banish);

    // Rule 6-5-5-2: each given DON!! adds 1000 power, but only during the owner's turn.
    public int GetPower(bool isOwnersTurn)
    {
        return BasePower + (isOwnersTurn ? AttachedDon * 1000 : 0);
    }

    // Resets all match state when a card leaves the field (KO, trash, back to hand...).
    public void ResetFieldState()
    {
        IsRested = false;
        AttachedDon = 0;
        PlayedOnTurn = 0;
    }
}
