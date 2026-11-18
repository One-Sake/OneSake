using System.Text;
using System.Text.Json;
using OneSake.Game.Effects;
using Xunit.Abstractions;

namespace OneSake.Tests;

// Optional report: how many card effects the parser understands.
// Only runs when ONESAKE_CARDS_JSON points to a JSON file from the optcgapi
// (e.g. https://optcgapi.com/api/allSetCards/). Without the variable the test does nothing.
//
//   $env:ONESAKE_CARDS_JSON = "C:\path\all_cards.json"; dotnet test --filter EffectCoverageReport
public class EffectCoverageReport
{
    private readonly ITestOutputHelper _output;

    public EffectCoverageReport(ITestOutputHelper output)
    {
        _output = output;
    }

    // Debug helper: $env:ONESAKE_PARSE_TEXT = "[On Play] Draw 1 card."; dotnet test --filter PrintSingleParse
    [Fact]
    public void PrintSingleParse()
    {
        var text = Environment.GetEnvironmentVariable("ONESAKE_PARSE_TEXT");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var script = EffectParser.Parse(text);
        var json = JsonSerializer.Serialize(script, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
        File.WriteAllText(Environment.GetEnvironmentVariable("ONESAKE_COVERAGE_OUT") ?? "parse.json",
            $"{EffectParser.Clean(text)}\n{string.Join("\n", script.FailedParts)}\n{json}");
    }

    [Fact]
    public void PrintParserCoverage()
    {
        var path = Environment.GetEnvironmentVariable("ONESAKE_CARDS_JSON");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cards = new Dictionary<string, (string Text, string Type)>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var id = element.GetProperty("card_set_id").GetString() ?? string.Empty;
            var text = element.TryGetProperty("card_text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
            var type = element.TryGetProperty("card_type", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
            cards[id] = (text, type);
        }

        var fully = 0;
        var withEffect = 0;
        var unsupported = new List<string>();

        foreach (var (id, (text, type)) in cards.OrderBy(entry => entry.Key))
        {
            var script = EffectParser.Parse(text, type);
            if (EffectParser.Clean(text).Length == 0)
            {
                continue;
            }

            withEffect += 1;
            if (script.IsFullySupported)
            {
                fully += 1;
            }
            else
            {
                unsupported.AddRange(script.FailedParts.Select(part => $"{id}\t{part}"));
            }
        }

        var summary = $"Cards with effect text: {withEffect}, fully automated: {fully} ({100.0 * fully / Math.Max(1, withEffect):0.0}%)";
        _output.WriteLine(summary);

        var reportPath = Environment.GetEnvironmentVariable("ONESAKE_COVERAGE_OUT");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var builder = new StringBuilder();
            builder.AppendLine(summary);
            foreach (var line in unsupported)
            {
                builder.AppendLine(line);
            }

            File.WriteAllText(reportPath, builder.ToString());
        }
    }
}
