using System.Reflection;
using OneSake.Game.Effects;
using Xunit.Abstractions;

namespace OneSake.Tests.Blocks;

// Every building block of the effect model must have its own test ([BlockTest("Step.KO")]).
// A card is only as correct as its blocks (docs/spiellogik-plan.md, A11).
public class BlockCoverageTests
{
    // Blocks that do not have a test yet. Goes down with every block test, never up.
    // 0 means: every block of the catalog is tested on its own.
    private const int MaxBlocksWithoutTest = 0;

    private readonly ITestOutputHelper _output;

    public BlockCoverageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EveryBlock_HasATest()
    {
        var tested = TestedBlocks();
        var missing = BlockCatalog.All.Where(block => !tested.ContainsKey(block)).ToList();

        _output.WriteLine($"{BlockCatalog.All.Count} blocks, {BlockCatalog.All.Count - missing.Count} with a test.");
        foreach (var block in missing)
        {
            _output.WriteLine($"  no test: {block}");
        }

        Assert.True(
            missing.Count <= MaxBlocksWithoutTest,
            $"{missing.Count} blocks without [BlockTest] (allowed: {MaxBlocksWithoutTest}): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryBlockTest_PointsToAKnownBlock()
    {
        var unknown = TestedBlocks().Keys.Where(block => !BlockCatalog.All.Contains(block)).ToList();

        Assert.True(unknown.Count == 0, $"[BlockTest] with an unknown block: {string.Join(", ", unknown)}");
    }

    // docs/effect-building-blocks.md lists every block: `Step.KO`, or as a short name in a
    // combined row ("`Condition.LifeAtMost` / `LifeAtLeast`").
    [Fact]
    public void EveryBlock_IsDocumented()
    {
        var doc = FindRepositoryFile(Path.Combine("docs", "effect-building-blocks.md"));
        if (doc is null)
        {
            // The test output folder is not inside a checkout (e.g. a copied build): nothing to check.
            _output.WriteLine("docs/effect-building-blocks.md not found - skipped.");
            return;
        }

        var text = File.ReadAllText(doc);
        var missing = BlockCatalog.All
            .Where(block => !text.Contains($"`{block}`") && !text.Contains($"`{block[(block.LastIndexOf('.') + 1)..]}`"))
            .ToList();

        Assert.True(missing.Count == 0, $"Blocks missing in docs/effect-building-blocks.md: {string.Join(", ", missing)}");
    }

    private static string? FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    [Fact]
    public void NoCardScript_UsesAZoneThatIsNoBlock()
    {
        var used = CardScriptRegistry.ReadAllRaw()
            .Where(file => file.Json.Contains("\"zone\": \"Deck\"") || file.Json.Contains("\"zone\": \"Life\""))
            .Select(file => file.Path)
            .ToList();

        Assert.True(used.Count == 0, $"Scripts with zone Deck/Life (the engine ignores it): {string.Join(", ", used)}");
    }

    // Block -> names of the tests that check it.
    public static Dictionary<string, List<string>> TestedBlocks()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var methods = typeof(BlockCoverageTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in methods)
        {
            foreach (var attribute in method.GetCustomAttributes<BlockTestAttribute>())
            {
                if (!result.TryGetValue(attribute.BlockId, out var tests))
                {
                    tests = [];
                    result[attribute.BlockId] = tests;
                }

                tests.Add($"{method.DeclaringType?.Name}.{method.Name}");
            }
        }

        return result;
    }
}
