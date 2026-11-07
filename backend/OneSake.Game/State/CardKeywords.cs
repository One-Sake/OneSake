namespace OneSake.Game.State;

// Reads the always-on keywords from the start of a card text, e.g.
// "[Rush] (This card can attack on the turn in which it is played.)" or "[Blocker] [On Play] ...".
// Only keywords at the very beginning count, so "[DON!! x1] This Character gains [Rush]." is ignored.
public static class CardKeywords
{
    public const string Rush = "Rush";
    public const string Blocker = "Blocker";
    public const string DoubleAttack = "Double Attack";
    public const string Banish = "Banish";

    private static readonly string[] KnownKeywords = [Rush, Blocker, DoubleAttack, Banish];

    public static IReadOnlyList<string> Parse(string? text)
    {
        var keywords = new List<string>();
        var rest = (text ?? string.Empty).TrimStart();

        while (rest.StartsWith('['))
        {
            var closingIndex = rest.IndexOf(']');
            if (closingIndex < 0)
            {
                break;
            }

            var token = rest[1..closingIndex].Trim();
            var keyword = KnownKeywords.FirstOrDefault(known =>
                string.Equals(known, token, StringComparison.OrdinalIgnoreCase));

            if (keyword is null)
            {
                // Another bracket like [On Play] or [DON!! x1] starts a normal effect - stop here.
                break;
            }

            keywords.Add(keyword);
            rest = rest[(closingIndex + 1)..].TrimStart();

            // Skip the reminder text in parentheses after a keyword.
            if (rest.StartsWith('('))
            {
                var closingParenthesis = rest.IndexOf(')');
                rest = closingParenthesis < 0 ? string.Empty : rest[(closingParenthesis + 1)..].TrimStart();
            }
        }

        return keywords;
    }
}
