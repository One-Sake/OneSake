using System.Text.Json;
using OneSake.Game.Effects;

// Card script generator (see docs/card-scripts.md).
//
//   dotnet run --project backend/OneSake.CardScriptGen -- --cards <all_cards.json or optcgapi URL> [--cards ...] [--set OP01] [--only-missing] [--out <CardScripts folder>]
//
// Reads card data in the optcgapi format (card_set_id, card_text, card_type), runs the EffectParser
// and writes CardScripts/generated/<SET>/<CARD-ID>.json. Overrides are never touched.

const string GeneratorVersion = "cardscriptgen-1";

var options = GeneratorOptions.Parse(args);
if (options is null)
{
    Console.WriteLine("Usage: --cards <file or URL> [--cards ...] [--set OP01] [--only-missing] [--out <CardScripts folder>]");
    return 1;
}

var cards = await CardSource.LoadAsync(options.CardSources);
var outputRoot = options.OutputFolder ?? FindCardScriptsFolder();
var generatedRoot = Path.Combine(outputRoot, "generated");
var overridesRoot = Path.Combine(outputRoot, "overrides");
Console.WriteLine($"{cards.Count} cards loaded. Writing to {generatedRoot}");

var stats = new Statistics();
foreach (var card in cards.Values.OrderBy(entry => entry.CardId, StringComparer.Ordinal))
{
    var set = CardScriptJson.SetOf(card.CardId);
    if (options.Set is not null && !string.Equals(set, options.Set, StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    if (EffectParser.Clean(card.Text).Length == 0)
    {
        continue;
    }

    var path = Path.Combine(generatedRoot, set, $"{card.CardId}.json");
    var hasOverride = File.Exists(Path.Combine(overridesRoot, set, $"{card.CardId}.json"));
    var hash = CardScriptJson.HashText(card.Text);
    var existing = File.Exists(path) ? CardScriptJson.Deserialize(File.ReadAllText(path)) : null;

    if (existing is not null && options.OnlyMissing)
    {
        stats.Add(set, existing.Status, hasOverride, "kept");
        continue;
    }

    // A verified script stays as long as the card text is the same.
    if (existing?.Status == CardScriptStatus.Verified && existing.SourceTextHash == hash)
    {
        stats.Add(set, existing.Status, hasOverride, "kept");
        continue;
    }

    if (existing?.Status == CardScriptStatus.Verified)
    {
        Console.WriteLine($"  {card.CardId}: card text changed, verified -> generated. Please check it again.");
    }

    var script = EffectParser.Parse(card.Text, card.Category);
    var file = new CardScriptFile
    {
        CardId = card.CardId,
        Status = script.IsFullySupported ? CardScriptStatus.Generated : CardScriptStatus.Manual,
        SourceTextHash = hash,
        ParserVersion = GeneratorVersion,
        Notes = script.IsFullySupported ? null : "Not automated: " + string.Join(" | ", script.UnsupportedTexts),
        Script = script
    };

    var json = CardScriptJson.Serialize(file);
    // Safety check: what the server loads must be exactly what the parser produced.
    if (CardScriptJson.Serialize(CardScriptJson.Deserialize(json)) != json)
    {
        throw new InvalidOperationException($"{card.CardId}: the script does not survive a JSON round trip.");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    // Git may check the files out with CRLF line endings: that is not a change.
    var changed = !File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != json;
    if (changed)
    {
        File.WriteAllText(path, json);
    }

    stats.Add(set, file.Status, hasOverride, existing is null ? "new" : changed ? "updated" : "unchanged");
}

stats.Print();
return 0;

// Walks up from the current folder to backend/OneSake.Game/CardScripts.
static string FindCardScriptsFolder()
{
    for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
    {
        foreach (var candidate in new[] { Path.Combine(folder.FullName, "backend", "OneSake.Game", "CardScripts"), Path.Combine(folder.FullName, "OneSake.Game", "CardScripts") })
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    throw new DirectoryNotFoundException("CardScripts folder not found. Use --out <folder>.");
}

sealed record GeneratorOptions(IReadOnlyList<string> CardSources, string? Set, bool OnlyMissing, string? OutputFolder)
{
    public static GeneratorOptions? Parse(string[] args)
    {
        var sources = new List<string>();
        string? set = null;
        string? output = null;
        var onlyMissing = false;

        for (var index = 0; index < args.Length; index += 1)
        {
            switch (args[index])
            {
                case "--cards" when index + 1 < args.Length:
                    sources.Add(args[++index]);
                    break;
                case "--set" when index + 1 < args.Length:
                    set = args[++index];
                    break;
                case "--out" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--only-missing":
                    onlyMissing = true;
                    break;
                default:
                    Console.WriteLine($"Unknown argument: {args[index]}");
                    return null;
            }
        }

        return sources.Count == 0 ? null : new GeneratorOptions(sources, set, onlyMissing, output);
    }
}

sealed record CardData(string CardId, string Text, string Category);

static class CardSource
{
    // optcgapi lists every print (parallels too), so one card ID can appear several times.
    public static async Task<Dictionary<string, CardData>> LoadAsync(IEnumerable<string> sources)
    {
        var cards = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        foreach (var source in sources)
        {
            var json = source.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? await http.GetStringAsync(source)
                : await File.ReadAllTextAsync(source);

            using var document = JsonDocument.Parse(json);
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = Read(element, "card_set_id").Trim().ToUpperInvariant();
                var text = Read(element, "card_text");
                if (id.Length == 0 || (cards.TryGetValue(id, out var known) && known.Text.Length > 0))
                {
                    continue;
                }

                cards[id] = new CardData(id, text, NormalizeCategory(Read(element, "card_type")));
            }
        }

        return cards;
    }

    private static string Read(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    // Same categories as MatchRoom uses for deck cards.
    private static string NormalizeCategory(string type)
    {
        foreach (var category in new[] { "Leader", "Event", "Stage" })
        {
            if (type.Trim().Equals(category, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return "Character";
    }
}

sealed class Statistics
{
    private readonly SortedDictionary<string, Dictionary<string, int>> _bySet = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _changes = new(StringComparer.Ordinal);
    private int _overrides;

    public void Add(string set, string status, bool hasOverride, string change)
    {
        if (!_bySet.TryGetValue(set, out var counts))
        {
            counts = new Dictionary<string, int>(StringComparer.Ordinal);
            _bySet[set] = counts;
        }

        counts[status] = counts.GetValueOrDefault(status) + 1;
        _changes[change] = _changes.GetValueOrDefault(change) + 1;
        _overrides += hasOverride ? 1 : 0;
    }

    public void Print()
    {
        var statuses = CardScriptStatus.All;
        Console.WriteLine();
        Console.WriteLine($"{"Set",-8}{string.Join("", statuses.Select(status => $"{status,11}"))}{"total",8}");
        foreach (var (set, counts) in _bySet)
        {
            Console.WriteLine($"{set,-8}{string.Join("", statuses.Select(status => $"{counts.GetValueOrDefault(status),11}"))}{counts.Values.Sum(),8}");
        }

        var totals = statuses.Select(status => _bySet.Values.Sum(counts => counts.GetValueOrDefault(status))).ToList();
        var all = Math.Max(1, totals.Sum());
        Console.WriteLine($"{"all",-8}{string.Join("", totals.Select(total => $"{total,11}"))}{totals.Sum(),8}");
        Console.WriteLine(string.Join(", ", statuses.Select((status, index) => $"{status} {100.0 * totals[index] / all:0.0}%")));
        Console.WriteLine($"Files: {string.Join(", ", _changes.Select(entry => $"{entry.Key} {entry.Value}"))}. Cards with an override: {_overrides}.");
    }
}
