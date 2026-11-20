using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Tests.Rules;

// A small game for rule tests without a MatchRoom: two players, the event bus, the
// EffectResolver and rule processing. Cards without a script file are parsed from their text.
//
//   var game = new RuleScenario();
//   var card = game.AddCharacter(game.P1, "[On Play] Draw 1 card.");
//   game.Events.Publish(new CardPlayed(card, "p1", ByEffect: false));
//   game.Resolve();
public sealed class RuleScenario : IGameContext
{
    private int _nextId;

    public RuleScenario(int deckSize = 10, int handSize = 3)
    {
        Resolver = new EffectResolver(this, Events);
        P1 = CreatePlayer("p1", deckSize, handSize);
        P2 = CreatePlayer("p2", deckSize, handSize);
        PlayerList.AddRange([P1, P2]);
    }

    public List<PlayerState> PlayerList { get; } = [];
    public IReadOnlyList<PlayerState> Players => PlayerList;
    public int TurnNumber { get; set; } = 3;
    public string ActivePlayerId { get; set; } = "p1";
    public BattleState? Battle { get; set; }
    public GameEventBus Events { get; } = new();
    public EffectResolver Resolver { get; }
    public PlayerState P1 { get; }
    public PlayerState P2 { get; }
    public List<string> Logs { get; } = [];
    public IReadOnlyList<PlayerState> Losers { get; private set; } = [];
    public bool IsGameOver => Losers.Count > 0;

    // Every request the resolver asked, in order (for tests about the order of effects).
    public List<ResolverRequest> Requests { get; } = [];

    public void Log(string type, string text) => Logs.Add($"{type}: {text}");

    // Rule 9 like in the MatchRoom: defeat judgment, then static effects.
    public bool ProcessRules()
    {
        if (IsGameOver)
        {
            return true;
        }

        Losers = RulesProcessor.Process(this);
        return IsGameOver;
    }

    public CardInstance Card(PlayerState owner, string text = "", int cost = 1, int power = 2000, string category = CardInstance.CharacterCategory, string? name = null)
    {
        _nextId += 1;
        return new CardInstance
        {
            InstanceId = $"{owner.PlayerId}-c{_nextId}",
            CardId = $"RULE-{_nextId}",
            Name = name ?? $"Card {_nextId}",
            OwnerId = owner.PlayerId,
            Category = category,
            Cost = cost,
            BasePower = power,
            Text = text
        };
    }

    public CardInstance AddCharacter(PlayerState owner, string text = "", int cost = 1, int power = 2000, string? name = null)
    {
        var card = Card(owner, text, cost, power, name: name);
        owner.Characters.Add(card);
        return card;
    }

    // Resolves everything. Questions are answered automatically:
    //   effect choice: "Yes" / select the first card / "Done", order: as offered (or by `order`),
    //   manual effect: done.
    public void Resolve(Func<OrderNeeded, IReadOnlyList<string>>? order = null, Func<EffectChoiceNeeded, (string Option, string? CardId)>? choose = null)
    {
        var request = Resolver.Run();
        for (var guard = 0; request is not null && guard < 100; guard += 1)
        {
            Requests.Add(request);
            request = request switch
            {
                EffectChoiceNeeded effect => Answer(effect, choose),
                OrderNeeded orderNeeded => Resolver.AnswerOrder(orderNeeded.PlayerId, order?.Invoke(orderNeeded) ?? orderNeeded.Triggers.Select(trigger => trigger.Id).ToList()),
                ManualEffectNeeded manual => Resolver.ManualEffectDone(manual.PlayerId),
                _ => null
            };
        }
    }

    // The engine's "draws" log lines in order, e.g. ["Card 3", "Card 1"] - shows which effect ran first.
    public List<string> DrawSources()
    {
        return Logs
            .Where(line => line.StartsWith("EFFECT: ", StringComparison.Ordinal) && line.Contains(" draws "))
            .Select(line => line["EFFECT: ".Length..line.IndexOf(':', "EFFECT: ".Length)])
            .ToList();
    }

    private ResolverRequest? Answer(EffectChoiceNeeded effect, Func<EffectChoiceNeeded, (string Option, string? CardId)>? choose)
    {
        if (choose is not null)
        {
            var (option, cardId) = choose(effect);
            return Resolver.AnswerEffect(option, cardId);
        }

        var options = effect.Request.Options;
        if (options.Contains(EffectChoiceRequest.Yes))
        {
            return Resolver.AnswerEffect(EffectChoiceRequest.Yes, null);
        }

        if (options.Contains(EffectChoiceRequest.Select) && effect.Request.Cards.Count > 0)
        {
            return Resolver.AnswerEffect(EffectChoiceRequest.Select, effect.Request.Cards[0].InstanceId);
        }

        return Resolver.AnswerEffect(EffectChoiceRequest.Done, null);
    }

    private PlayerState CreatePlayer(string id, int deckSize, int handSize)
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

        for (var index = 0; index < deckSize; index += 1)
        {
            player.DrawDeck.Add(new CardInstance { InstanceId = $"{id}-deck-{index}", CardId = $"{id}-deck", Name = "Deck card", OwnerId = id });
        }

        for (var index = 0; index < handSize; index += 1)
        {
            player.Hand.Add(new CardInstance { InstanceId = $"{id}-hand-{index}", CardId = $"{id}-hand", Name = "Hand card", OwnerId = id });
        }

        for (var index = 0; index < 5; index += 1)
        {
            player.LifeCards.Add(new CardInstance { InstanceId = $"{id}-life-{index}", CardId = $"{id}-life", Name = "Life card", OwnerId = id });
        }

        return player;
    }
}
