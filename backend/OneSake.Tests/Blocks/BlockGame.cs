using OneSake.Game.Effects;
using OneSake.Game.Rules;
using OneSake.Game.State;
using OneSake.Tests.Rules;

namespace OneSake.Tests.Blocks;

// A small game for building block tests. It uses the same way as a match: game event ->
// TriggerCollector -> TriggerQueue -> EffectEngine, with rule processing (RuleScenario).
// Cards get their text parsed at runtime, so a test shows which words become which block.
//
//   var game = new BlockGame();
//   var card = game.OnField(game.P1, "[On Play] Draw 1 card.");
//   game.Play(card);
//   Assert.Equal(4, game.P1.Hand.Count);
public sealed class BlockGame
{
    private int _nextId;

    public BlockGame(int deckSize = 10, int handSize = 3)
    {
        Scenario = new RuleScenario(deckSize, handSize);
    }

    public RuleScenario Scenario { get; }
    public PlayerState P1 => Scenario.P1;
    public PlayerState P2 => Scenario.P2;
    public List<string> Logs => Scenario.Logs;

    // The last question the effect asked (null when the effect finished).
    public ResolverRequest? Request { get; private set; }

    public EffectChoiceRequest Choice => Assert.IsType<EffectChoiceNeeded>(Request).Request;

    // ---------- Cards ----------

    public CardInstance Card(
        PlayerState owner,
        string text = "",
        int cost = 1,
        int power = 2000,
        string category = CardInstance.CharacterCategory,
        string? name = null,
        string types = "",
        string color = "Red",
        string attribute = "",
        int counter = 0)
    {
        _nextId += 1;
        return new CardInstance
        {
            InstanceId = $"{owner.PlayerId}-b{_nextId}",
            CardId = $"BLOCK-{_nextId}",
            Name = name ?? $"Card {_nextId}",
            OwnerId = owner.PlayerId,
            Category = category,
            Cost = cost,
            BasePower = power,
            Counter = counter,
            Text = text,
            Types = types,
            Color = color,
            Attribute = attribute
        };
    }

    // A Character in the Character area (or a Stage / the Leader for those categories).
    public CardInstance OnField(PlayerState owner, string text = "", int cost = 1, int power = 2000, string category = CardInstance.CharacterCategory,
        string? name = null, string types = "", string color = "Red", string attribute = "")
    {
        var card = Card(owner, text, cost, power, category, name, types, color, attribute);
        switch (category)
        {
            case CardInstance.LeaderCategory:
                owner.Leader = card;
                break;
            case CardInstance.StageCategory:
                owner.Stage = card;
                break;
            default:
                owner.Characters.Add(card);
                break;
        }

        Recalculate();
        return card;
    }

    public CardInstance InHand(PlayerState owner, string text = "", int cost = 1, int power = 2000, string category = CardInstance.CharacterCategory,
        string? name = null, string types = "", int counter = 0)
    {
        var card = Card(owner, text, cost, power, category, name, types, counter: counter);
        owner.Hand.Add(card);
        return card;
    }

    public CardInstance InTrash(PlayerState owner, string text = "", int cost = 1, int power = 2000, string category = CardInstance.CharacterCategory,
        string? name = null, string types = "")
    {
        var card = Card(owner, text, cost, power, category, name, types);
        owner.TrashCards.Add(card);
        return card;
    }

    // Puts the cards on top of the deck, the first one on the very top.
    public void DeckTop(PlayerState owner, params CardInstance[] cards)
    {
        owner.DrawDeck.InsertRange(0, cards);
    }

    // The parsed ability of a card text. Fails when the parser did not understand everything.
    public static Ability Ability(CardInstance card, int index = 0)
    {
        Assert.True(card.Script.IsFullySupported, $"Not understood: {string.Join(" | ", card.Script.UnsupportedTexts)}");
        return card.Script.Abilities[index];
    }

    public static EffectStep Step(CardInstance card, int abilityIndex = 0, int stepIndex = 0)
    {
        return Ability(card, abilityIndex).Steps[stepIndex];
    }

    // ---------- Running effects ----------

    public ResolverRequest? Publish(GameEvent gameEvent)
    {
        Scenario.Events.Publish(gameEvent);
        return Run();
    }

    // [On Play] like after playing the card from the hand.
    public ResolverRequest? Play(CardInstance card) => Publish(new CardPlayed(card, card.OwnerId, ByEffect: false));

    // An effect the player starts: Event [Main]/[Counter], [Trigger].
    public ResolverRequest? Activate(CardInstance card, EffectTiming timing)
    {
        Scenario.Resolver.Activate(card, card.OwnerId, timing);
        return Run();
    }

    // [Activate: Main] (the first ability with that timing).
    public ResolverRequest? ActivateMain(CardInstance card)
    {
        var ability = card.Script.WithTiming(EffectTiming.ActivateMain).First();
        Scenario.Resolver.Activate(card, card.OwnerId, ability);
        return Run();
    }

    public ResolverRequest? Run()
    {
        Request = Scenario.Resolver.Run();
        return Request;
    }

    public ResolverRequest? Answer(string option, CardInstance? card = null)
    {
        Request = Scenario.Resolver.AnswerEffect(option, card?.InstanceId);
        return Request;
    }

    public ResolverRequest? Select(CardInstance card) => Answer(EffectChoiceRequest.Select, card);
    public ResolverRequest? Done() => Answer(EffectChoiceRequest.Done);
    public ResolverRequest? Yes() => Answer(EffectChoiceRequest.Yes);
    public ResolverRequest? No() => Answer(EffectChoiceRequest.No);

    // Cards offered in the current question.
    public IReadOnlyList<CardInstance> Offered => Choice.Cards;

    public void Recalculate() => StaticEffects.Recalculate(Scenario);

    // A new turn of the given player (turn number + 1), like the match does it.
    public void NextTurn(PlayerState player)
    {
        EffectDurations.EndOfTurn(Scenario, Scenario.TurnNumber);
        Scenario.TurnNumber += 1;
        Scenario.ActivePlayerId = player.PlayerId;
        Recalculate();
    }
}
