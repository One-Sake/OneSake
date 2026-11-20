using System.Text.Json;
using OneSake.Domain;
using OneSake.Game.Randomness;

namespace OneSake.Server.Simulation;

// Card data for simulations in the optcgapi format (card_set_id, card_text, ...): the checked-in
// fixture backend/OneSake.Tests/Decks/cards-fixture.json or a full file (ONESAKE_CARDS_JSON).
public sealed class CardPool
{
    private CardPool(IReadOnlyDictionary<string, PlayerDeckCardDto> cards, IReadOnlyDictionary<string, PlayerDeckCardDto> leaders, IReadOnlyDictionary<string, int> leaderLife)
    {
        Cards = cards;
        Leaders = leaders;
        LeaderLife = leaderLife;
    }

    public IReadOnlyDictionary<string, PlayerDeckCardDto> Cards { get; }
    public IReadOnlyDictionary<string, PlayerDeckCardDto> Leaders { get; }
    private IReadOnlyDictionary<string, int> LeaderLife { get; }

    public static CardPool Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cards = new Dictionary<string, PlayerDeckCardDto>(StringComparer.OrdinalIgnoreCase);
        var leaders = new Dictionary<string, PlayerDeckCardDto>(StringComparer.OrdinalIgnoreCase);
        var life = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var basePrints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var id = Text(element, "card_set_id").Trim().ToUpperInvariant();
            if (id.Length == 0)
            {
                continue;
            }

            // Several prints per ID: the base print wins (same rule as the client and the generator).
            var isBase = Text(element, "card_image_id").Length == 0 || string.Equals(Text(element, "card_image_id"), id, StringComparison.OrdinalIgnoreCase);
            if (basePrints.Contains(id) || (cards.ContainsKey(id) && !isBase))
            {
                continue;
            }

            if (isBase)
            {
                basePrints.Add(id);
            }

            var type = Text(element, "card_type");
            var card = new PlayerDeckCardDto
            {
                CardId = id,
                Name = Text(element, "card_name"),
                Category = type,
                Color = Text(element, "card_color"),
                Cost = Number(element, "card_cost"),
                Power = Number(element, "card_power"),
                Counter = Number(element, "counter_amount"),
                Text = Text(element, "card_text"),
                Types = Text(element, "sub_types"),
                Attribute = Text(element, "attribute"),
                Quantity = 1
            };

            if (type.Equals("Leader", StringComparison.OrdinalIgnoreCase))
            {
                leaders[id] = card;
                life[id] = Number(element, "life") ?? 5;
            }
            else
            {
                cards[id] = card;
            }
        }

        return new CardPool(cards, leaders, life);
    }

    public PlayerDeckSubmissionDto CreateDeck(string name, string leaderId, IEnumerable<(string CardId, int Quantity)> cards)
    {
        var leader = Leaders.GetValueOrDefault(leaderId)
            ?? throw new InvalidOperationException($"Leader {leaderId} is not in the card data.");

        return new PlayerDeckSubmissionDto
        {
            DeckId = name,
            DeckName = name,
            LeaderCardId = leader.CardId,
            LeaderName = leader.Name,
            LeaderColor = leader.Color,
            LeaderPower = leader.Power,
            LeaderLife = LeaderLife.GetValueOrDefault(leader.CardId, 5),
            LeaderText = leader.Text,
            LeaderTypes = leader.Types,
            LeaderAttribute = leader.Attribute,
            Cards = cards
                .Select(entry => (Cards.GetValueOrDefault(entry.CardId)
                    ?? throw new InvalidOperationException($"Card {entry.CardId} is not in the card data.")) with { Quantity = entry.Quantity })
                .ToArray()
        };
    }

    private static string Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    // The API sends numbers as numbers, strings ("5000") or null.
    private static int? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }
}

// Deck lists in the export format: "1xOP13-001" (Leader) and "4xOP01-016" per line, "#" = comment.
public static class DeckList
{
    public static PlayerDeckSubmissionDto Load(string path, CardPool pool)
    {
        var entries = new List<(string CardId, int Quantity)>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('x', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var quantity))
            {
                throw new FormatException($"{Path.GetFileName(path)}: '{line}' is not in the format 4xOP01-016.");
            }

            entries.Add((parts[1].Trim().ToUpperInvariant(), quantity));
        }

        var leader = entries.FirstOrDefault(entry => pool.Leaders.ContainsKey(entry.CardId));
        if (leader.CardId is null)
        {
            throw new FormatException($"{Path.GetFileName(path)} has no Leader.");
        }

        var deck = pool.CreateDeck(Path.GetFileNameWithoutExtension(path), leader.CardId, entries.Where(entry => entry != leader));
        var size = deck.Cards.Sum(card => card.Quantity);
        return size == 50 ? deck : throw new FormatException($"{Path.GetFileName(path)} has {size} cards instead of 50.");
    }

    public static IReadOnlyList<PlayerDeckSubmissionDto> LoadFolder(string folder, CardPool pool)
    {
        return Directory.GetFiles(folder, "*.txt").Order(StringComparer.Ordinal).Select(path => Load(path, pool)).ToArray();
    }
}

// A random valid deck: a Leader and 50 cards of its colors (max. 4 copies each).
public static class RandomDeckBuilder
{
    public static PlayerDeckSubmissionDto Build(CardPool pool, IRandom random)
    {
        var leaders = pool.Leaders.Keys.Order(StringComparer.Ordinal).ToList();
        for (var attempt = 0; attempt < 20; attempt += 1)
        {
            var leader = pool.Leaders[leaders[random.Next(leaders.Count)]];
            var leaderColors = leader.Color.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = pool.Cards.Values
                .Where(card => card.Color.Length > 0 && card.Color.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(leaderColors.Contains))
                .OrderBy(card => card.CardId, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count < 13)
            {
                continue;
            }

            candidates.Shuffle(random);
            var entries = new List<(string CardId, int Quantity)>();
            var left = 50;
            foreach (var card in candidates)
            {
                var copies = Math.Min(left, 1 + random.Next(4));
                entries.Add((card.CardId, copies));
                left -= copies;
                if (left == 0)
                {
                    break;
                }
            }

            if (left == 0)
            {
                return pool.CreateDeck($"random-{leader.CardId}", leader.CardId, entries);
            }
        }

        throw new InvalidOperationException("The card data has no Leader with enough cards of its colors.");
    }
}
