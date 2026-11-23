using System.Collections.Concurrent;
using System.Net;
using System.Text;
using OneSake.Game.Effects;

// Phase 0 of docs/spiellogik-plan.md: why do card effects not run in real matches?
//
//   dotnet run --project backend/OneSake.CardScriptGen -- --check-backend https://api.onesake.world [--set OP04] [--report <file>]
//   dotnet run --project backend/OneSake.CardScriptGen -- --check-texts <cards.json or optcgapi URL> [--set OP04] [--report <file>]
//
// In a match the browser loads every deck card from GET /cards/{id} and sends its text to the
// server. The server only runs a card script if the hash of that text matches the script file
// (CardScriptRegistry.Resolve). This check does the same for every card script and counts what
// the game really does with each card. It only reads (GET), it writes nothing into CardScripts.
static class TextCheck
{
    public static bool IsCheckCommand(string argument) =>
        argument is "--check-backend" or "--check-texts";

    public static async Task<int> RunAsync(string[] args)
    {
        string? backend = null;
        string? textSource = null;
        string? set = null;
        var reportPath = "card-text-check.md";

        for (var index = 0; index < args.Length; index += 1)
        {
            switch (args[index])
            {
                case "--check-backend" when index + 1 < args.Length:
                    backend = args[++index].TrimEnd('/');
                    break;
                case "--check-texts" when index + 1 < args.Length:
                    textSource = args[++index];
                    break;
                case "--set" when index + 1 < args.Length:
                    set = args[++index];
                    break;
                case "--report" when index + 1 < args.Length:
                    reportPath = args[++index];
                    break;
                default:
                    Console.WriteLine($"Unknown argument: {args[index]}");
                    Console.WriteLine("Usage: --check-backend <backend URL> | --check-texts <file or URL> [--set OP04] [--report <file>]");
                    return 1;
            }
        }

        var scripts = CardScriptRegistry.All.Values
            .Where(file => set is null || string.Equals(CardScriptJson.SetOf(file.CardId), set, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.CardId, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"{scripts.Count} card scripts to check.");

        var delivered = backend is not null
            ? await LoadFromBackendAsync(backend, scripts.Select(file => file.CardId).ToList())
            : await LoadFromTextsAsync(textSource!);

        var results = scripts.Select(file => Classify(file, delivered.GetValueOrDefault(file.CardId))).ToList();
        var report = BuildReport(backend ?? textSource!, results);
        await File.WriteAllTextAsync(reportPath, report);

        PrintSummary(results);
        Console.WriteLine();
        Console.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
        return 0;
    }

    private enum Outcome
    {
        // Text matches: the script runs with its status (verified / generated / manual).
        Same,
        // Text differs: CardScriptRegistry plays the card as manual.
        TextChanged,
        // The backend knows the card but delivers no text: the card has no effect at all.
        EmptyText,
        // The backend does not know the card: the browser sends no text, no effect at all.
        NotDelivered,
    }

    private sealed record Result(CardScriptFile File, Outcome Outcome, string DeliveredText);

    private static Result Classify(CardScriptFile file, CardData? card)
    {
        if (card is null)
        {
            return new Result(file, Outcome.NotDelivered, string.Empty);
        }

        if (EffectParser.Clean(card.Text).Length == 0)
        {
            return new Result(file, Outcome.EmptyText, card.Text);
        }

        var same = string.Equals(file.SourceTextHash, CardScriptJson.HashText(card.Text), StringComparison.OrdinalIgnoreCase);
        return new Result(file, same ? Outcome.Same : Outcome.TextChanged, card.Text);
    }

    // What the game does with the card in a real match.
    private static string EffectiveStatus(Result result) => result.Outcome switch
    {
        Outcome.Same => result.File.Status,
        Outcome.TextChanged => CardScriptStatus.Manual,
        _ => "no effect",
    };

    private static async Task<Dictionary<string, CardData>> LoadFromTextsAsync(string source)
    {
        return await CardSource.LoadAsync([source]);
    }

    // The same request the browser makes for every deck card (matchDeckMapper.ts).
    private static async Task<Dictionary<string, CardData>> LoadFromBackendAsync(string backend, IReadOnlyList<string> cardIds)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var bodies = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var failures = new ConcurrentBag<string>();
        var done = 0;

        await Parallel.ForEachAsync(cardIds, new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (cardId, token) =>
        {
            try
            {
                using var response = await http.GetAsync($"{backend}/cards/{Uri.EscapeDataString(cardId)}", token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bodies[cardId] = await response.Content.ReadAsStringAsync(token);
                }
                else if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    failures.Add($"{cardId}: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                failures.Add($"{cardId}: {exception.Message}");
            }

            var count = Interlocked.Increment(ref done);
            if (count % 250 == 0)
            {
                Console.WriteLine($"  {count}/{cardIds.Count} cards loaded");
            }
        });

        if (!failures.IsEmpty)
        {
            Console.WriteLine($"{failures.Count} requests failed (counted as 'not delivered'), e.g.:");
            foreach (var failure in failures.Take(5))
            {
                Console.WriteLine($"  {failure}");
            }
        }

        var cards = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cardId, body) in bodies)
        {
            var single = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);
            CardSource.AddCards(single, body);
            if (single.TryGetValue(cardId, out var card))
            {
                cards[cardId] = card;
            }
        }

        return cards;
    }

    private static void PrintSummary(IReadOnlyList<Result> results)
    {
        Console.WriteLine();
        Console.WriteLine("Card text check:");
        foreach (var group in results.GroupBy(result => result.Outcome).OrderBy(group => group.Key))
        {
            Console.WriteLine($"  {Describe(group.Key),-60}{group.Count(),6}");
        }

        Console.WriteLine();
        Console.WriteLine("What the game really does with these cards:");
        foreach (var group in results.GroupBy(EffectiveStatus).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {group.Key,-60}{group.Count(),6}");
        }
    }

    private static string Describe(Outcome outcome) => outcome switch
    {
        Outcome.Same => "text matches the script (script runs)",
        Outcome.TextChanged => "text differs (played as manual)",
        Outcome.EmptyText => "no card text delivered (no effect at all)",
        _ => "card not delivered (no effect at all)",
    };

    private static string BuildReport(string source, IReadOnlyList<Result> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Kartentext-Prüfung");
        builder.AppendLine();
        builder.AppendLine($"Quelle: `{source}` · {DateTime.Now:yyyy-MM-dd HH:mm} · {results.Count} Kartenskripte");
        builder.AppendLine();
        builder.AppendLine("| Ergebnis | Karten |");
        builder.AppendLine("|---|---|");
        foreach (var group in results.GroupBy(result => result.Outcome).OrderBy(group => group.Key))
        {
            builder.AppendLine($"| {Describe(group.Key)} | {group.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Was das Spiel wirklich macht | Karten |");
        builder.AppendLine("|---|---|");
        foreach (var group in results.GroupBy(EffectiveStatus).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {group.Key} | {group.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Set | gleich | Text anders | kein Text | nicht geliefert |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var group in results.GroupBy(result => CardScriptJson.SetOf(result.File.CardId)).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            int Count(Outcome outcome) => group.Count(result => result.Outcome == outcome);
            builder.AppendLine($"| {group.Key} | {Count(Outcome.Same)} | {Count(Outcome.TextChanged)} | {Count(Outcome.EmptyText)} | {Count(Outcome.NotDelivered)} |");
        }

        AppendDifferences(builder, results.Where(result => result.Outcome == Outcome.TextChanged).ToList());
        AppendIds(builder, "Kein Kartentext geliefert", results.Where(result => result.Outcome == Outcome.EmptyText));
        AppendIds(builder, "Karte nicht geliefert", results.Where(result => result.Outcome == Outcome.NotDelivered));
        return builder.ToString();
    }

    // The script files do not contain the original text, only the texts of its abilities.
    private static void AppendDifferences(StringBuilder builder, IReadOnlyList<Result> changed)
    {
        if (changed.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"## Text anders ({changed.Count})");
        builder.AppendLine();
        builder.AppendLine("Oben der Text aus dem Skript (Fähigkeiten und nicht unterstützte Teile), unten der gelieferte Text (bereinigt).");
        foreach (var result in changed)
        {
            var script = result.File.Script;
            var scriptText = string.Join(" ", script.Abilities.Select(ability => ability.Text).Concat(script.UnsupportedTexts).Distinct());
            builder.AppendLine();
            builder.AppendLine($"**{result.File.CardId}** ({result.File.Status})");
            builder.AppendLine($"- Skript: {scriptText}");
            builder.AppendLine($"- Geliefert: {EffectParser.Clean(result.DeliveredText)}");
        }
    }

    private static void AppendIds(StringBuilder builder, string title, IEnumerable<Result> results)
    {
        var ids = results.Select(result => result.File.CardId).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"## {title} ({ids.Count})");
        builder.AppendLine();
        builder.AppendLine(string.Join(", ", ids));
    }
}
