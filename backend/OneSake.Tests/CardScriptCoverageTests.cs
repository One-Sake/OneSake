using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneSake.Game.Effects;
using Xunit.Abstractions;

namespace OneSake.Tests;

// Runs in the CI with the embedded card script files only (no external data).
public partial class CardScriptCoverageTests
{
    private readonly ITestOutputHelper _output;

    public CardScriptCoverageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EveryFile_CanBeLoaded_AndHasTheRequiredFields()
    {
        var files = CardScriptRegistry.ReadAllRaw();
        Assert.NotEmpty(files);
        var errors = new List<string>();

        foreach (var (path, json) in files)
        {
            CardScriptFile file;
            try
            {
                file = CardScriptJson.Deserialize(json);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                errors.Add($"{path}: {exception.Message}");
                continue;
            }

            // CardScripts/<origin>/<SET>/<CARD-ID>.json
            var parts = path.Split('/');
            if (parts.Length != 4 || parts[3] != $"{file.CardId}.json" || parts[2] != CardScriptJson.SetOf(file.CardId))
            {
                errors.Add($"{path}: file name or set folder does not match cardId '{file.CardId}'.");
            }

            if (!CardScriptStatus.All.Contains(file.Status))
            {
                errors.Add($"{path}: unknown status '{file.Status}'.");
            }

            if (!HashPattern().IsMatch(file.SourceTextHash))
            {
                errors.Add($"{path}: sourceTextHash must be a SHA-256 hex string.");
            }

            if (string.IsNullOrWhiteSpace(file.ParserVersion))
            {
                errors.Add($"{path}: parserVersion is missing.");
            }

            if (file.Script.Abilities.Any(ability => ability.Steps.Count == 0 && ability.Timing != EffectTiming.Static))
            {
                errors.Add($"{path}: an ability without steps.");
            }

            if (file.Status == CardScriptStatus.Verified && !file.Script.IsFullySupported)
            {
                errors.Add($"{path}: a verified script must not contain unsupported texts.");
            }
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void EveryVerifiedCard_HasATest()
    {
        var tested = TestedCardIds();
        var verified = CardScriptRegistry.ReadAllRaw()
            .Select(entry => CardScriptJson.Deserialize(entry.Json))
            .Where(file => file.Status == CardScriptStatus.Verified)
            .Select(file => file.CardId)
            .Distinct()
            .ToList();

        var missing = verified.Where(cardId => !tested.Contains(cardId)).ToList();
        Assert.True(missing.Count == 0, $"Verified without [CardTest]: {string.Join(", ", missing)}");
    }

    [Fact]
    public void VerifiedCards_ReachTheBaseline_AndSummaryIsPrinted()
    {
        var files = CardScriptRegistry.All.Values.ToList();
        var total = Math.Max(1, files.Count);
        var counts = CardScriptStatus.All.ToDictionary(status => status, status => files.Count(file => file.Status == status));
        var overrides = files.Count(file => file.Origin == CardScriptRegistry.OriginOverrides);

        _output.WriteLine($"Card scripts: {files.Count} cards ({overrides} with override)");
        foreach (var (status, count) in counts)
        {
            _output.WriteLine($"  {status,-10} {count,5}  ({100.0 * count / total:0.0}%)");
        }

        var baselineJson = CardScriptRegistry.ReadResourceText("coverage-baseline.json");
        Assert.NotNull(baselineJson);
        var baseline = JsonDocument.Parse(baselineJson!).RootElement.GetProperty("verified").GetInt32();

        Assert.True(
            counts[CardScriptStatus.Verified] >= baseline,
            $"Only {counts[CardScriptStatus.Verified]} verified card scripts, the baseline is {baseline}.");
    }

    [Fact]
    public void EveryCardTest_PointsToAVerifiedScript()
    {
        var notVerified = TestedCardIds()
            .Where(cardId => CardScriptRegistry.Find(cardId)?.Status != CardScriptStatus.Verified)
            .ToList();

        Assert.True(notVerified.Count == 0, $"[CardTest] for cards that are not verified: {string.Join(", ", notVerified)}");
    }

    private static HashSet<string> TestedCardIds()
    {
        return typeof(CardScriptCoverageTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(method => method.GetCustomAttributes<CardTestAttribute>())
            .Select(attribute => attribute.CardId.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashPattern();
}
