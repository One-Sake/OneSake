using System.Text.Json;
using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests;

// Marks a test that checks the script of a real card. A card script may only have the status
// "verified" when at least one test carries this attribute (CardScriptCoverageTests checks it).
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CardTestAttribute : Attribute
{
    public CardTestAttribute(string cardId)
    {
        CardId = cardId;
    }

    public string CardId { get; }
}

// Small game situation for card script tests: two players, an effect engine, real cards.
//
//   var game = new CardScenario();
//   var nami = game.AddCharacter(game.P1, "OP01-016");
//   var request = game.Trigger(nami, EffectTiming.OnPlay);
//   game.Answer(EffectChoiceRequest.Select, someCard.InstanceId);
public sealed class CardScenario
{
    private static readonly Lazy<Dictionary<string, JsonElement>> RealCards = new(LoadRealCards);
    private int _nextId;

    public CardScenario()
    {
        P1 = CreatePlayer("p1");
        P2 = CreatePlayer("p2");
        Context.PlayerList.AddRange([P1, P2]);
    }

    public ScenarioContext Context { get; } = new();
    public EffectEngine Engine { get; } = new();
    public PlayerState P1 { get; }
    public PlayerState P2 { get; }

    // A real card with its official text (TestData/card-script-cards.json), so the registry
    // uses the script file of that card ID.
    public CardInstance Real(string cardId, PlayerState owner)
    {
        var data = RealCards.Value[cardId];
        return new CardInstance
        {
            InstanceId = $"{cardId}#{++_nextId}",
            CardId = cardId,
            OwnerId = owner.PlayerId,
            Name = Text(data, "card_name"),
            Category = Text(data, "card_type") is "Event" or "Stage" or "Leader" ? Text(data, "card_type") : CardInstance.CharacterCategory,
            Text = Text(data, "card_text"),
            Types = Text(data, "sub_types"),
            Attribute = Text(data, "attribute"),
            Color = Text(data, "card_color"),
            Cost = Number(data, "card_cost"),
            BasePower = Number(data, "card_power"),
            Counter = Number(data, "counter_amount")
        };
    }

    // A plain Character without effect text (target or filler card).
    public CardInstance Plain(PlayerState owner, int cost = 1, int power = 2000, string types = "")
    {
        return new CardInstance
        {
            InstanceId = $"plain#{++_nextId}",
            CardId = $"PLAIN-{_nextId}",
            OwnerId = owner.PlayerId,
            Name = $"Plain {_nextId}",
            Category = CardInstance.CharacterCategory,
            Cost = cost,
            BasePower = power,
            Types = types
        };
    }

    public CardInstance AddCharacter(PlayerState owner, string cardId)
    {
        var card = Real(cardId, owner);
        owner.Characters.Add(card);
        return card;
    }

    public CardInstance AddPlain(PlayerState owner, int cost = 1, int power = 2000)
    {
        var card = Plain(owner, cost, power);
        owner.Characters.Add(card);
        return card;
    }

    // Queues the card's abilities of this timing like the match does and runs the engine.
    // Returns the first question of the effect, or null when it finished on its own.
    public EffectChoiceRequest? Trigger(CardInstance card, EffectTiming timing, bool startedByPlayer = false)
    {
        var owner = card.OwnerId;
        if (startedByPlayer)
        {
            foreach (var ability in card.Script.WithTiming(timing))
            {
                Engine.Enqueue(ability, card, owner, alreadyChosen: true);
            }
        }
        else
        {
            Engine.EnqueueTriggered(card, owner, timing, Context);
        }

        return Engine.Run(Context);
    }

    public EffectChoiceRequest? Answer(string option, string? cardInstanceId = null)
    {
        return Engine.Answer(Context, option, cardInstanceId);
    }

    public void Recalculate()
    {
        StaticEffects.Recalculate(Context);
    }

    // The test must run the checked file, not a runtime parse of the text.
    public static void AssertVerifiedScript(CardInstance card)
    {
        var info = card.ScriptInfo;
        Assert.False(info.TextChanged, $"{card.CardId}: the test text does not match the script file.");
        Assert.NotEqual(CardScriptRegistry.OriginParser, info.Origin);
        Assert.Equal(CardScriptStatus.Verified, info.Status);
    }

    private static PlayerState CreatePlayer(string id)
    {
        var player = new PlayerState
        {
            PlayerId = id,
            DisplayName = id,
            DonDeckCount = 5,
            Leader = new CardInstance
            {
                InstanceId = $"{id}-leader",
                CardId = $"{id}-leader",
                Name = $"{id} Leader",
                Category = CardInstance.LeaderCategory,
                OwnerId = id,
                BasePower = 5000
            }
        };

        for (var index = 0; index < 10; index += 1)
        {
            player.DrawDeck.Add(new CardInstance { InstanceId = $"{id}-deck-{index}", CardId = $"{id}-deck-{index}", Name = $"Deck {index}", OwnerId = id });
        }

        for (var index = 0; index < 3; index += 1)
        {
            player.Hand.Add(new CardInstance { InstanceId = $"{id}-hand-{index}", CardId = $"{id}-hand-{index}", Name = $"Hand {index}", OwnerId = id });
        }

        return player;
    }

    private static Dictionary<string, JsonElement> LoadRealCards()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "card-script-cards.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray()
            .ToDictionary(element => element.GetProperty("card_set_id").GetString()!, element => element.Clone());
    }

    private static string Text(JsonElement data, string name)
    {
        return data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
    }

    private static int Number(JsonElement data, string name)
    {
        // The API sends numbers sometimes as strings ("5000").
        if (!data.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => 0
        };
    }
}

public sealed class ScenarioContext : IGameContext
{
    public List<PlayerState> PlayerList { get; } = [];
    public List<string> Logs { get; } = [];
    public IReadOnlyList<PlayerState> Players => PlayerList;
    public int TurnNumber { get; set; } = 3;
    public string ActivePlayerId { get; set; } = "p1";
    public BattleState? Battle { get; set; }

    public void Log(string type, string text) => Logs.Add($"{type}: {text}");
}
