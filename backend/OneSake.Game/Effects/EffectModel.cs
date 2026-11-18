namespace OneSake.Game.Effects;

// ---------------------------------------------------------------------------------------------
// Card effect definitions. The EffectParser turns the card text of the API ("card_text") into
// these objects once, the EffectEngine runs them during the match.
//
//   CardScript   = all abilities of one card
//   Ability      = one "[On Play] ..." / "[Activate: Main] ..." / static block
//   Ability      = Timing + Conditions + Costs + Steps
//   EffectStep   = one action like "K.O. up to 1 of your opponent's Characters with a cost of 3 or less"
// ---------------------------------------------------------------------------------------------

// When an ability is used.
public enum EffectTiming
{
    // Always active while the card is on the field (e.g. "[DON!! x1] This Character gains +1000 power").
    Static,
    OnPlay,
    WhenAttacking,
    OnBlock,
    OnKO,
    OnOpponentsAttack,
    EndOfYourTurn,
    EndOfOpponentsTurn,
    // Character / Leader / Stage ability the player starts in the main phase.
    ActivateMain,
    // Event card effects.
    EventMain,
    EventCounter,
    // Life card effect when the Life card is added to the hand by damage.
    Trigger,
}

public sealed class CardScript
{
    public static readonly CardScript Empty = new();

    public List<Ability> Abilities { get; } = [];

    // Effect text parts the parser does not understand. The player has to resolve those manually.
    public List<string> UnsupportedTexts { get; } = [];

    // For the coverage report: the sentence of each unsupported ability that failed.
    public List<string> FailedParts { get; } = [];

    public bool IsFullySupported => UnsupportedTexts.Count == 0;

    public IEnumerable<Ability> WithTiming(EffectTiming timing) =>
        Abilities.Where(ability => ability.Timing == timing);
}

public sealed class Ability
{
    public EffectTiming Timing { get; set; }
    // Original text of this ability, shown in prompts and logs.
    public string Text { get; set; } = string.Empty;

    // [DON!! xN]: the card needs N given DON!! for this ability.
    public int RequiredDon { get; set; }
    public bool OncePerTurn { get; set; }
    // [Your Turn] / [Opponent's Turn]
    public bool OnlyYourTurn { get; set; }
    public bool OnlyOpponentsTurn { get; set; }

    // "If your Leader has the {Straw Hat Crew} type, ..." in front of the whole ability.
    public List<EffectCondition> Conditions { get; } = [];
    // Everything before the ":" like "DON!! -1", "(1)" or "You may rest this Character".
    public List<EffectCost> Costs { get; } = [];
    public List<EffectStep> Steps { get; } = [];

    // Unique key for [Once Per Turn] tracking.
    public int Index { get; set; }
}

public enum CostKind
{
    // DON!! -N: return N DON!! from the field to the DON!! deck.
    ReturnDon,
    // (N): rest N active DON!! in the cost area.
    RestDon,
    TrashFromHand,
    RestThis,
    TrashThis,
    // "You may add 1 card from the top of your Life cards to your hand"
    LifeToHand,
    // "You may rest 1 of your Characters" etc.
    RestOwnCards,
    // "You may return 1 of your Characters to the owner's hand"
    ReturnOwnCharacter,
    // "You may trash 1 of your Characters"
    TrashOwnCharacter,
    // "You may place N cards from your trash at the bottom of your deck"
    TrashToDeckBottom,
    // "You may trash 2 cards from the top of your deck"
    TrashTopOfDeck,
    // "You may trash 1 card from the top of your Life cards"
    TrashLife,
    // "You may turn 1 card from the top of your Life cards face-up" (face-up has no effect here).
    TurnLife,
    // "You may reveal 2 Events from your hand" (only checks that the cards exist).
    RevealFromHand,
    ReturnThis,
}

public sealed class EffectCost
{
    public CostKind Kind { get; init; }
    public int Amount { get; init; } = 1;
    // Filter for cards that pay the cost (e.g. "trash 1 {Straw Hat Crew} type card from your hand").
    public CardFilter? Filter { get; init; }
}

public enum StepKind
{
    // Only chooses cards; the next step can use them with "that Character".
    Select,
    // Target actions (use Target).
    KO,
    Rest,
    SetActive,
    ReturnToHand,
    BottomOfDeck,
    TopOfDeck,
    TrashCard,
    PowerModifier,
    CostModifier,
    GainFlag,
    GiveRestedDon,
    // A target that should not become active in the next refresh phase.
    SkipRefresh,

    // Player actions.
    Draw,
    TrashFromHand,
    OpponentTrashesFromHand,
    AddDonFromDeck,
    SetDonActive,
    ReturnDonToDeck,
    TrashTopOfDeck,
    LookAndReveal,
    LookAndArrange,
    LifeToHand,
    DeckTopToLife,
    HandToLife,
    TrashLife,
    PlayCard,
    PlayThisCard,
    // "Activate this card's [Main] effect" - Flag is the timing name (Main, On Play, On K.O., Counter).
    ActivateOwnAbility,
    // "Set up to 2 of your DON!! cards as active at the end of this turn"
    SetDonActiveAtEndOfTurn,
    RestOpponentDon,
    // "Also treat this card's name as [X]"
    NameAlias,
    HandToDeckBottom,
    OpponentHandToDeckBottom,
    TrashToHand,
    ShuffleDeck,
    // Gives a flag to the player (e.g. the opponent cannot use [Blocker] this turn).
    NegateBlockers,
}

// Where the cards of a step come from.
public enum CardZone
{
    Field,
    Hand,
    Trash,
    Deck,
    Life,
    HandOrTrash,
}

public enum Duration
{
    // Static effects are recalculated all the time, the value is not stored.
    Permanent,
    ThisTurn,
    ThisBattle,
    // "until the end of your opponent's next turn" / "until the start of your next turn"
    UntilEndOfOpponentsNextTurn,
}

public enum PlayerSide
{
    You,
    Opponent,
    Any,
}

public sealed class EffectStep
{
    public StepKind Kind { get; init; }
    public int Amount { get; init; }
    public int SecondAmount { get; init; }
    public bool Optional { get; init; }

    public TargetSpec? Target { get; init; }
    public CardFilter? Filter { get; init; }
    public CardZone Zone { get; init; } = CardZone.Field;
    public Duration Duration { get; init; } = Duration.ThisTurn;
    public string Flag { get; init; } = string.Empty;
    public bool Rested { get; init; }
    // LookAndReveal / LookAndArrange: where the rest goes ("bottom", "top", "trash", "topOrBottom").
    public string RestDestination { get; set; } = "bottom";
    // LookAndReveal: "add it to your hand" (default) or "play it".
    public bool PlayRevealed { get; init; }
    // PlayCard: life card side ("top"/"bottom") or deck side.
    public string Position { get; init; } = "top";
    public PlayerSide Side { get; init; } = PlayerSide.You;

    public List<EffectCondition> Conditions { get; } = [];
    public string Text { get; init; } = string.Empty;
}

// Which cards a step affects.
public sealed class TargetSpec
{
    // Maximum number of cards ("up to 2").
    public int Count { get; init; } = 1;
    // "up to N": the player may choose fewer.
    public bool UpTo { get; init; } = true;
    // "all of your opponent's Characters"
    public bool All { get; init; }
    // "this Character", "this Leader", "this card"
    public bool Self { get; init; }
    // "your Leader"
    public bool YourLeader { get; init; }
    // "that Character" = the card chosen by the step before.
    public bool Previous { get; init; }
    // Target chosen by the opponent of the effect owner.
    public CardFilter Filter { get; init; } = new();
}

public sealed class CardFilter
{
    public PlayerSide Owner { get; set; } = PlayerSide.You;
    public bool AllowLeader { get; set; }
    public bool AllowCharacter { get; set; } = true;
    public bool AllowStage { get; set; }
    // For hand/trash/deck filters: "Event", "Character", "Stage" or empty for any card.
    public string Category { get; set; } = string.Empty;

    public int? MaxCost { get; set; }
    public int? MinCost { get; set; }
    public int? MaxPower { get; set; }
    public int? MinPower { get; set; }
    // "base cost" / "base power": ignore modifiers.
    public bool UseBaseValues { get; set; }
    public bool? Rested { get; set; }
    public string TypeIncludes { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameNot { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public bool ExcludeSource { get; set; }
    public bool RequiresTrigger { get; set; }
    // "[Nami] or {Straw Hat Crew} type card": Name OR TypeIncludes must match.
    public bool NameOrType { get; set; }
    // "with a cost equal to or less than the number of DON!! cards on your field" etc.
    public string MaxCostFrom { get; set; } = string.Empty;

    public CardFilter Clone() => (CardFilter)MemberwiseClone();
}

public enum ConditionKind
{
    LeaderHasType,
    LeaderIsName,
    LeaderIsMulticolored,
    LeaderColor,
    LifeAtMost,
    LifeAtLeast,
    OpponentLifeAtMost,
    OpponentLifeAtLeast,
    HandAtMost,
    HandAtLeast,
    OpponentHandAtLeast,
    OpponentHandAtMost,
    DonOnFieldAtLeast,
    DonOnFieldAtMost,
    OpponentDonOnFieldAtLeast,
    CharactersAtLeast,
    CharactersAtMost,
    // "If you have a {X} type Character" / "If you have [Name]"
    HasCard,
    HasNoCard,
    OpponentHasCard,
    TrashAtLeast,
    DeckAtMost,
    ThisIsRested,
    ThisIsActive,
    // "If you have less Life cards than your opponent"
    LifeLessThanOpponent,
    // "If your opponent has more DON!! cards on their field than you"
    OpponentHasMoreDon,
    GivenDonAtLeast,
    // "If the number of DON!! cards on your field is equal to or less than the number on your opponent's field"
    DonNotMoreThanOpponent,
    // "If this Character battles your opponent's Leader" etc. are not supported.
}

public sealed class EffectCondition
{
    public ConditionKind Kind { get; init; }
    public int Amount { get; init; }
    public string Text { get; init; } = string.Empty;
    public CardFilter? Filter { get; init; }
}

// Flags that cards can get from effects. Keywords use the same strings as CardKeywords.
public static class EffectFlags
{
    public const string CannotAttack = "CannotAttack";
    public const string CannotBeKOdInBattle = "CannotBeKOdInBattle";
    public const string CannotBeKOdByEffects = "CannotBeKOdByEffects";
    public const string CannotBeRestedByEffects = "CannotBeRestedByEffects";
    public const string CannotBeBlocked = "CannotBeBlocked";
    public const string CanAttackActiveCharacters = "CanAttackActiveCharacters";
    public const string SkipNextRefresh = "SkipNextRefresh";
    public const string CannotBlock = "CannotBlock";
    // [Rush: Character] - can attack Characters (not the Leader) in the turn it is played.
    public const string RushCharacter = "RushCharacter";
    public const string NameAliasPrefix = "Name:";
}
