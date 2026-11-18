using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneSake.Server.Services.Meta;

// Reads one Card Kaizoku stats snapshot and turns it into the provider-independent model.
//
// Real format (checked against stats_eb05_lw_p_top20_20261118.json): the file is a JSON ARRAY,
// one entry per leader:
//   { "leader", "leaderKey": "OP13-001", "variantName", "leaderName": "Monkey.D.Luffy",
//     "wins", "number_of_matches", "total_matches", "raw_win_rate", "play_rate",
//     "weighted_win_rate", "first_win_rate", "second_win_rate",
//     "matchups": [ { "opponent": "OP09-001", "opponentKey": "OP09-001", "wins", "total_games",
//                     "matchup_win_rate", "first_wins", "first_games", "first_win_rate",
//                     "second_wins", "second_games", "second_win_rate" } ] }
// Differences to the first description we had:
//   - "opponent" is the opponent's KEY (card ID), not a name. The name comes from the
//     leader entry with that key.
//   - Rates are fractions 0..1 and are null when there are no games in that slot.
//   - leaderKey is the leader card ID, so it is used as LeaderCardId directly.
//   - leaderName can be null upstream (unnamed promo leaders); then the key is the name.
//   - There are no tiers in the data, so OneSake does not show tiers.
// The leader totals in the file are NOT used: Games and WinRate are derived from the matchups
// (sum of games, rate weighted by games), as agreed.
public static partial class CardKaizokuParser
{
    [GeneratedRegex("^[A-Z]{1,4}\\d{0,3}-\\d{1,4}$")]
    private static partial Regex CardIdPattern();

    public static bool LooksLikeCardId(string key) => CardIdPattern().IsMatch(key);

    public static IReadOnlyList<MetaLeader> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new MetaFormatException($"Invalid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                throw new MetaFormatException("Expected a non-empty array of leaders.");
            }

            var rawLeaders = root.EnumerateArray().Select(ReadLeader).ToList();
            var scale = DetectScale(rawLeaders);
            var names = rawLeaders
                .GroupBy(leader => leader.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

            return rawLeaders.Select(leader => Normalize(leader, scale, names)).ToList();
        }
    }

    private sealed record RawMatchup(string Key, double? First, double? Second, int FirstGames, int SecondGames);

    private sealed record RawLeader(string Key, string Name, List<RawMatchup> Matchups);

    private static RawLeader ReadLeader(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MetaFormatException("Leader entry is not an object.");
        }

        var key = RequireString(element, "leaderKey");
        var name = element.TryGetProperty("leaderName", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()!
            : key;

        if (!element.TryGetProperty("matchups", out var matchups) || matchups.ValueKind != JsonValueKind.Array)
        {
            throw new MetaFormatException($"Leader {key} has no matchups array.");
        }

        var list = matchups.EnumerateArray().Select(matchup =>
        {
            if (matchup.ValueKind != JsonValueKind.Object)
            {
                throw new MetaFormatException($"Matchup of {key} is not an object.");
            }

            var opponentKey = matchup.TryGetProperty("opponentKey", out var keyElement) && keyElement.ValueKind == JsonValueKind.String
                ? keyElement.GetString()!
                : RequireString(matchup, "opponent");

            return new RawMatchup(
                opponentKey,
                ReadRate(matchup, "first_win_rate", key),
                ReadRate(matchup, "second_win_rate", key),
                ReadGames(matchup, "first_games", key),
                ReadGames(matchup, "second_games", key));
        }).ToList();

        return new RawLeader(key, string.IsNullOrWhiteSpace(name) ? key : name.Trim(), list);
    }

    // Rates are usually 0..1; a provider might send percent (0..100). Anything else is invalid.
    private static double DetectScale(IEnumerable<RawLeader> leaders)
    {
        var max = leaders
            .SelectMany(leader => leader.Matchups)
            .SelectMany(matchup => new[] { matchup.First, matchup.Second })
            .Where(rate => rate.HasValue)
            .Select(rate => rate!.Value)
            .DefaultIfEmpty(0)
            .Max();

        if (max > 100)
        {
            throw new MetaFormatException("Win rate above 100.");
        }

        return max > 1 ? 100 : 1;
    }

    private static MetaLeader Normalize(RawLeader leader, double scale, IReadOnlyDictionary<string, string> names)
    {
        var matchups = leader.Matchups.Select(matchup =>
        {
            var first = matchup.First / scale;
            var second = matchup.Second / scale;
            var games = matchup.FirstGames + matchup.SecondGames;
            return new MetaMatchup(
                matchup.Key,
                names.TryGetValue(matchup.Key, out var opponentName) ? opponentName : matchup.Key,
                LooksLikeCardId(matchup.Key) ? matchup.Key : null,
                first,
                second,
                matchup.FirstGames,
                matchup.SecondGames,
                games,
                Weighted([(first, matchup.FirstGames), (second, matchup.SecondGames)]));
        }).ToList();

        return new MetaLeader(
            leader.Key,
            leader.Name,
            LooksLikeCardId(leader.Key) ? leader.Key : null,
            matchups.Sum(matchup => matchup.Games),
            Weighted(matchups.Select(matchup => (matchup.WinRate, matchup.Games))),
            Weighted(matchups.Select(matchup => (matchup.FirstWinRate, matchup.FirstGames))),
            Weighted(matchups.Select(matchup => (matchup.SecondWinRate, matchup.SecondGames))),
            matchups);
    }

    // Average weighted by the number of games. Null when there are no games.
    public static double? Weighted(IEnumerable<(double? Rate, int Games)> parts)
    {
        double sum = 0;
        var games = 0;
        foreach (var (rate, count) in parts)
        {
            if (rate is null || count <= 0)
            {
                continue;
            }

            sum += rate.Value * count;
            games += count;
        }

        return games == 0 ? null : sum / games;
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new MetaFormatException($"Missing text field '{name}'.");
        }

        return value.GetString()!.Trim();
    }

    private static double? ReadRate(JsonElement element, string name, string leaderKey)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var rate) || double.IsNaN(rate) || rate < 0)
        {
            throw new MetaFormatException($"Invalid '{name}' in a matchup of {leaderKey}.");
        }

        return rate;
    }

    private static int ReadGames(JsonElement element, string name, string leaderKey)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var games) || games < 0)
        {
            throw new MetaFormatException($"Invalid '{name}' in a matchup of {leaderKey}.");
        }

        return games;
    }
}
