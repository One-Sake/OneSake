using System.Collections.Concurrent;
using System.Reflection;

namespace OneSake.Game.Effects;

// The script a card uses in a match, and where it came from.
public sealed record ResolvedCardScript(
    CardScript Script,
    // verified / generated / manual, or "" for a card without effect text.
    string Status,
    // "overrides", "generated" or "parser" (no file, parsed at runtime).
    string Origin,
    // The card text does not match the text the file was made for (errata).
    bool TextChanged = false);

// Card scripts are versioned JSON files (CardScripts/*, embedded in this assembly).
// Overrides written by hand win over generated files. Cards without a file (e.g. a new set)
// are parsed at runtime like before.
public static class CardScriptRegistry
{
    public const string OriginGenerated = "generated";
    public const string OriginOverrides = "overrides";
    public const string OriginParser = "parser";
    private const string ResourcePrefix = "CardScripts/";

    private static readonly Lazy<IReadOnlyDictionary<string, CardScriptFile>> Files =
        new(LoadEmbeddedFiles, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly ConcurrentDictionary<string, ResolvedCardScript> Resolved = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> Warned = new(StringComparer.Ordinal);

    // Warnings (missing file, changed card text). The server connects this to its ILogger.
    public static Action<string>? Warning { get; set; }

    // All loaded files by card ID (overrides already replaced the generated files).
    public static IReadOnlyDictionary<string, CardScriptFile> All => Files.Value;

    public static CardScriptFile? Find(string? cardId)
    {
        return string.IsNullOrWhiteSpace(cardId)
            ? null
            : Files.Value.GetValueOrDefault(cardId.Trim().ToUpperInvariant());
    }

    public static ResolvedCardScript Resolve(string? cardId, string? text, string category)
    {
        if (EffectParser.Clean(text).Length == 0)
        {
            return new ResolvedCardScript(CardScript.Empty, string.Empty, OriginParser);
        }

        return Resolved.GetOrAdd($"{cardId}|{category}|{text}", _ => ResolveUncached(cardId, text, category));
    }

    private static ResolvedCardScript ResolveUncached(string? cardId, string? text, string category)
    {
        var file = Find(cardId);
        if (file is null)
        {
            if (!string.IsNullOrWhiteSpace(cardId))
            {
                WarnOnce($"missing:{cardId}", $"No card script file for {cardId}. The card text is parsed at runtime.");
            }

            return new ResolvedCardScript(EffectParser.ParseCached(text, category), CardScriptStatus.Generated, OriginParser);
        }

        if (!string.Equals(file.SourceTextHash, CardScriptJson.HashText(text), StringComparison.OrdinalIgnoreCase))
        {
            WarnOnce(
                $"changed:{cardId}",
                $"The card text of {cardId} differs from its script file ({file.Origin}). The card is treated as manual.");
            return new ResolvedCardScript(CreateManualScript(text, category), CardScriptStatus.Manual, file.Origin, TextChanged: true);
        }

        return new ResolvedCardScript(file.Script, file.Status, file.Origin);
    }

    // Nothing is automated except keywords like [Blocker]: every effect text gets the manual prompt.
    public static CardScript CreateManualScript(string? text, string category)
    {
        var parsed = EffectParser.Parse(text, category);
        var script = new CardScript();

        foreach (var ability in parsed.Abilities)
        {
            if (IsKeywordAbility(ability))
            {
                script.Abilities.Add(ability);
            }
            else if (!script.UnsupportedTexts.Contains(ability.Text))
            {
                script.UnsupportedTexts.Add(ability.Text);
            }
        }

        foreach (var unsupported in parsed.UnsupportedTexts.Where(entry => !script.UnsupportedTexts.Contains(entry)))
        {
            script.UnsupportedTexts.Add(unsupported);
        }

        return script;
    }

    private static bool IsKeywordAbility(Ability ability)
    {
        return ability.Timing == EffectTiming.Static
            && ability.Steps.Count > 0
            && ability.Steps.All(step => step.Kind == StepKind.GainFlag && step.Target?.Self == true);
    }

    private static void WarnOnce(string key, string message)
    {
        if (Warned.TryAdd(key, 0))
        {
            Warning?.Invoke(message);
        }
    }

    // Resource names are "CardScripts/generated/OP01/OP01-016.json" (see OneSake.Game.csproj).
    private static IReadOnlyDictionary<string, CardScriptFile> LoadEmbeddedFiles()
    {
        var assembly = typeof(CardScriptRegistry).Assembly;
        var files = new Dictionary<string, CardScriptFile>(StringComparer.OrdinalIgnoreCase);
        var resources = assembly.GetManifestResourceNames()
            .Select(name => (Name: name, Path: name.Replace('\\', '/')))
            .Where(entry => IsScriptResource(entry.Path))
            .ToList();

        // Generated first, then the overrides replace them.
        foreach (var origin in new[] { OriginGenerated, OriginOverrides })
        {
            foreach (var resource in resources.Where(entry => entry.Path.StartsWith($"{ResourcePrefix}{origin}/", StringComparison.Ordinal)))
            {
                var file = Read(assembly, resource.Name);
                file.Origin = origin;
                files[file.CardId.Trim().ToUpperInvariant()] = file;
            }
        }

        return files;
    }

    private static CardScriptFile Read(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Card script resource {resourceName} is missing.");
        using var reader = new StreamReader(stream);
        try
        {
            return CardScriptJson.Deserialize(reader.ReadToEnd());
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Card script {resourceName} cannot be read: {exception.Message}", exception);
        }
    }

    private static bool IsScriptResource(string path)
    {
        return path.EndsWith(".json", StringComparison.Ordinal)
            && (path.StartsWith($"{ResourcePrefix}{OriginGenerated}/", StringComparison.Ordinal)
                || path.StartsWith($"{ResourcePrefix}{OriginOverrides}/", StringComparison.Ordinal));
    }

    // Small text files next to the scripts (e.g. coverage-baseline.json), null when missing.
    public static string? ReadResourceText(string fileName)
    {
        var assembly = typeof(CardScriptRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // All embedded resource paths (for the coverage test: schema check of every file).
    public static IReadOnlyList<(string Path, string Json)> ReadAllRaw()
    {
        var assembly = typeof(CardScriptRegistry).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => IsScriptResource(name.Replace('\\', '/')))
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return (name.Replace('\\', '/'), reader.ReadToEnd());
            })
            .ToList();
    }
}
