using OneSake.Game.State;

namespace OneSake.Game.Effects;

// What the client shows about a card's effects (inspect overlay, "resolve manually" list).
public sealed record CardEffectStatus(
    // "auto" (all effects automated), "partial", "manual" or "" (no effect text).
    string EffectStatus,
    // Status of the script file: "verified", "generated" (not checked by hand) or "manual".
    string ScriptStatus,
    IReadOnlyList<string> ManualEffects)
{
    public static CardEffectStatus For(CardInstance card)
    {
        var info = card.ScriptInfo;
        var script = info.Script;

        if (info.Status.Length == 0)
        {
            return new CardEffectStatus(string.Empty, string.Empty, []);
        }

        var effectStatus = script.IsFullySupported
            ? "auto"
            : script.Abilities.Count > 0 ? "partial" : "manual";

        return new CardEffectStatus(effectStatus, info.Status, script.UnsupportedTexts.ToArray());
    }
}
