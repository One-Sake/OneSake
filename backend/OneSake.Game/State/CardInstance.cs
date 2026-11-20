using OneSake.Game.Effects;

namespace OneSake.Game.State;

// A power/cost/flag change from an effect that ends after some time ("during this turn").
public sealed class CardModifier
{
    public int Power { get; init; }
    public int Cost { get; init; }
    public string Flag { get; init; } = string.Empty;
    public Duration Duration { get; init; } = Duration.ThisTurn;
    // Turn number in which the modifier was created. Used to find out when it ends.
    public int CreatedOnTurn { get; init; }
}

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
    // Types like "Straw Hat Crew Supernovas" (the API joins them with spaces or "/").
    public string Types { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
    public int Cost { get; init; }
    public int BasePower { get; init; }
    public int Counter { get; init; }
    public int Life { get; init; }
    // The player who owns this card. Set when the match deck is built.
    public string OwnerId { get; set; } = string.Empty;

    public bool IsRested { get; set; }
    public int AttachedDon { get; set; }
    // Turn number in which this card was played. Characters cannot attack in that turn (unless [Rush]).
    public int PlayedOnTurn { get; set; }

    // Effect modifiers that end after this turn / battle.
    public List<CardModifier> Modifiers { get; } = [];
    // Values from always-on (static) effects. Recalculated by StaticEffects after every action.
    public int StaticPower { get; set; }
    public int StaticCost { get; set; }
    public HashSet<string> StaticFlags { get; } = new(StringComparer.OrdinalIgnoreCase);
    // Abilities with [Once Per Turn] that were used, as "turn:abilityIndex".
    public HashSet<string> UsedOncePerTurn { get; } = [];

    public bool IsLeader => Category == LeaderCategory;
    public bool IsCharacter => Category == CharacterCategory;
    public bool IsEvent => Category == EventCategory;
    public bool IsStage => Category == StageCategory;

    // The checked script file for this card ID (CardScripts/), or the parsed text when there is none.
    public ResolvedCardScript ScriptInfo => CardScriptRegistry.Resolve(CardId, Text, Category);
    public CardScript Script => ScriptInfo.Script;

    public IReadOnlyList<string> Keywords
    {
        get
        {
            var keywords = CardKeywords.Parse(Text).ToList();
            foreach (var flag in AllFlags())
            {
                var keyword = CardKeywords.Normalize(flag);
                if (keyword is not null && !keywords.Contains(keyword))
                {
                    keywords.Add(keyword);
                }
            }

            return keywords;
        }
    }

    public bool HasRush => Keywords.Contains(CardKeywords.Rush);
    public bool HasBlocker => Keywords.Contains(CardKeywords.Blocker) && !HasFlag(EffectFlags.CannotBlock);
    public bool HasDoubleAttack => Keywords.Contains(CardKeywords.DoubleAttack);
    public bool HasBanish => Keywords.Contains(CardKeywords.Banish);

    public bool HasFlag(string flag)
    {
        return AllFlags().Any(entry => string.Equals(entry, flag, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> AllFlags()
    {
        return StaticFlags.Concat(Modifiers.Select(modifier => modifier.Flag).Where(flag => flag.Length > 0));
    }

    // Rule 6-5-5-2: each given DON!! adds 1000 power, but only during the owner's turn.
    // Effect modifiers always count. Rule 1-3-6-1: power can become negative.
    public int GetPower(bool isOwnersTurn)
    {
        return BasePower
            + (isOwnersTurn ? AttachedDon * 1000 : 0)
            + StaticPower
            + Modifiers.Sum(modifier => modifier.Power);
    }

    // Cost including effect modifiers ("-1 cost"). Cannot go below 0.
    public int CurrentCost => Math.Max(0, Cost + StaticCost + Modifiers.Sum(modifier => modifier.Cost));

    public bool HasTrigger => Script.Abilities.Any(ability => ability.Timing == EffectTiming.Trigger)
        || Text.Contains("[Trigger]", StringComparison.OrdinalIgnoreCase);

    // Resets all match state when a card leaves the field (KO, trash, back to hand...).
    public void ResetFieldState()
    {
        IsRested = false;
        AttachedDon = 0;
        PlayedOnTurn = 0;
        Modifiers.Clear();
        StaticPower = 0;
        StaticCost = 0;
        StaticFlags.Clear();
    }
}
