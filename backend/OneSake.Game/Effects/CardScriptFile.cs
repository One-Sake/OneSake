using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneSake.Game.Effects;

// One card script file in CardScripts/generated or CardScripts/overrides (see docs/card-scripts.md).
public sealed class CardScriptFile
{
    public string CardId { get; set; } = string.Empty;
    // "verified" (has a test), "generated" (parser understood everything, not checked by hand)
    // or "manual" (not fully automated, the player resolves the rest by hand).
    public string Status { get; set; } = CardScriptStatus.Generated;
    // SHA-256 of the cleaned card text the script was made for. A different text means errata.
    public string SourceTextHash { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public CardScript Script { get; set; } = new();

    // "generated" or "overrides" - where the file was loaded from. Not part of the file.
    [JsonIgnore]
    public string Origin { get; set; } = string.Empty;
}

public static class CardScriptStatus
{
    public const string Verified = "verified";
    public const string Generated = "generated";
    public const string Manual = "manual";

    public static readonly IReadOnlyList<string> All = [Verified, Generated, Manual];
}

public static class CardScriptJson
{
    // Enums as strings, null values left out. The model classes use get-only lists,
    // "Populate" fills them instead of replacing them.
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(CardScriptFile file)
    {
        return JsonSerializer.Serialize(file, Options).ReplaceLineEndings("\n") + "\n";
    }

    public static CardScriptFile Deserialize(string json)
    {
        return JsonSerializer.Deserialize<CardScriptFile>(json, Options)
            ?? throw new JsonException("The card script file is empty.");
    }

    // SHA-256 (hex) of the cleaned card text, so small spelling differences do not count as errata.
    public static string HashText(string? text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(EffectParser.Clean(text)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // "OP01-016" -> "OP01", "P-001" -> "P".
    public static string SetOf(string cardId)
    {
        var dash = cardId.IndexOf('-');
        return dash > 0 ? cardId[..dash].ToUpperInvariant() : "MISC";
    }
}
