using System.Reflection;
using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Marks a test that checks one building block of the effect model (see docs/effect-building-blocks.md).
// A block is one word or phrase of a card text that the engine understands, e.g. "Step.KO" for
// "K.O. up to 1 of your opponent's Characters" or "Duration.ThisBattle" for "during this battle".
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BlockTestAttribute : Attribute
{
    public BlockTestAttribute(string blockId)
    {
        BlockId = blockId;
    }

    public string BlockId { get; }
}

// Every building block of the effect model. Most come straight from the enums and classes in
// OneSake.Game/Effects/EffectModel.cs, so a new enum value is a new block and needs a test
// (BlockCoverageTests).
public static class BlockCatalog
{
    // Enum values that are no building block: Field is the default of every target, Deck and Life
    // exist in the model but neither the parser nor the engine uses them (BlockCoverageTests checks
    // that no card script uses them either).
    public static IReadOnlyList<string> NotABlock { get; } = ["Zone.Field", "Zone.Deck", "Zone.Life"];

    public static IReadOnlyList<string> All { get; } = Build();

    private static List<string> Build()
    {
        var blocks = new List<string>();

        // When an ability is used: [On Play], [When Attacking], static abilities ...
        blocks.AddRange(Enum.GetNames<EffectTiming>().Select(name => $"Timing.{name}"));
        // Parts of an ability header: [DON!! x1], [Once Per Turn], [Your Turn], [Opponent's Turn], "If ..., ".
        blocks.AddRange(["Ability.RequiredDon", "Ability.OncePerTurn", "Ability.OnlyYourTurn", "Ability.OnlyOpponentsTurn", "Ability.Conditions"]);
        // Keywords at the start of a card text.
        blocks.AddRange(typeof(CardKeywords).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => $"Keyword.{field.Name}"));
        // Everything in front of the ":".
        blocks.AddRange(Enum.GetNames<CostKind>().Select(name => $"Cost.{name}"));
        // "If your Leader has the {X} type", "If you have 2 or less Life cards" ...
        blocks.AddRange(Enum.GetNames<ConditionKind>().Select(name => $"Condition.{name}"));
        // The actions.
        blocks.AddRange(Enum.GetNames<StepKind>().Select(name => $"Step.{name}"));
        // Options of a step: "You may", "rested", "from your trash", "trash the rest" ...
        blocks.AddRange([
            "Option.Optional",
            "Option.Rested",
            "Option.StepConditions",
            "Option.PlayRevealed",
            "Option.Side.Opponent",
            "Option.RestDestination.bottom",
            "Option.RestDestination.top",
            "Option.RestDestination.trash",
        ]);
        blocks.AddRange(Enum.GetNames<CardZone>().Where(name => !NotABlock.Contains($"Zone.{name}")).Select(name => $"Option.Zone.{name}"));
        // "during this turn", "during this battle" ...
        blocks.AddRange(Enum.GetNames<Duration>().Select(name => $"Duration.{name}"));
        // Flags a card can gain: "cannot attack", "cannot be K.O.'d in battle" ...
        blocks.AddRange(typeof(EffectFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.Name != nameof(EffectFlags.NameAliasPrefix))
            .Select(field => $"Flag.{field.Name}"));
        // Which cards a step affects: "up to 2", "all of", "this Character", "your Leader", "that Character".
        blocks.AddRange(["Target.Count", "Target.UpTo", "Target.Exact", "Target.All", "Target.Self", "Target.YourLeader", "Target.Previous"]);
        // How cards are described: "with a cost of 3 or less", "{Straw Hat Crew} type", "rested" ...
        blocks.AddRange(typeof(CardFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => $"Filter.{property.Name}"));

        return blocks;
    }
}
