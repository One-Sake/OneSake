using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests;

// Tests for resolving card effects without a full match room.
public class EffectEngineTests
{
    private sealed class TestContext : IGameContext
    {
        public List<PlayerState> PlayerList { get; } = [];
        public List<string> Logs { get; } = [];
        public IReadOnlyList<PlayerState> Players => PlayerList;
        public int TurnNumber { get; set; } = 3;
        public string ActivePlayerId { get; set; } = "p1";
        public BattleState? Battle { get; set; }

        public void Log(string type, string text) => Logs.Add($"{type}: {text}");
    }

    private readonly TestContext _context = new();
    private readonly EffectEngine _engine = new();
    private readonly PlayerState _p1;
    private readonly PlayerState _p2;

    public EffectEngineTests()
    {
        _p1 = CreatePlayer("p1");
        _p2 = CreatePlayer("p2");
        _context.PlayerList.AddRange([_p1, _p2]);
    }

    [Fact]
    public void OnPlayKO_OffersOnlyMatchingTargets_AndTriggersOnKO()
    {
        var small = AddCharacter(_p2, "small", cost: 2, text: "[On K.O.] Draw 1 card.");
        AddCharacter(_p2, "big", cost: 5);
        var source = AddCharacter(_p1, "source", text: "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");
        var handBefore = _p2.Hand.Count;

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        var request = _engine.Run(_context);

        Assert.NotNull(request);
        Assert.Equal("p1", request!.PlayerId);
        Assert.Equal(["small"], request.Cards.Select(card => card.InstanceId));
        Assert.Contains(EffectChoiceRequest.Done, request.Options);

        Assert.Null(_engine.Answer(_context, EffectChoiceRequest.Select, "small"));

        Assert.Contains(small, _p2.TrashCards);
        Assert.DoesNotContain(small, _p2.Characters);
        // [On K.O.] of the K.O.'d card: its owner draws.
        Assert.Equal(handBefore + 1, _p2.Hand.Count);
        Assert.False(_engine.IsBusy);
    }

    [Fact]
    public void UpToTarget_CanBeSkipped()
    {
        var target = AddCharacter(_p2, "target", cost: 1);
        var source = AddCharacter(_p1, "source", text: "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        _engine.Run(_context);
        Assert.Null(_engine.Answer(_context, EffectChoiceRequest.Done, null));

        Assert.Contains(target, _p2.Characters);
    }

    [Fact]
    public void OptionalDonCost_AsksFirst_AndOnlyPaysOnYes()
    {
        var source = AddCharacter(_p1, "source", text: "[On Play] DON!! -1: Draw 2 cards.");
        _p1.ActiveDon = 2;
        var handBefore = _p1.Hand.Count;

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        var request = _engine.Run(_context)!;
        Assert.Equal([EffectChoiceRequest.Yes, EffectChoiceRequest.No], request.Options);

        Assert.Null(_engine.Answer(_context, EffectChoiceRequest.No, null));
        Assert.Equal(2, _p1.ActiveDon);
        Assert.Equal(handBefore, _p1.Hand.Count);

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        _engine.Run(_context);
        Assert.Null(_engine.Answer(_context, EffectChoiceRequest.Yes, null));
        Assert.Equal(1, _p1.ActiveDon);
        Assert.Equal(1, _p1.DonDeckCount);
        Assert.Equal(handBefore + 2, _p1.Hand.Count);
    }

    [Fact]
    public void CostThatCannotBePaid_SkipsTheEffect()
    {
        var source = AddCharacter(_p1, "source", text: "[On Play] DON!! -1: Draw 2 cards.");
        _p1.ActiveDon = 0;
        var handBefore = _p1.Hand.Count;

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);

        Assert.Null(_engine.Run(_context));
        Assert.Equal(handBefore, _p1.Hand.Count);
    }

    [Fact]
    public void DrawAndTrash_AsksForTheCardToTrash()
    {
        var source = AddCharacter(_p1, "source", text: "[On Play] Draw 2 cards and trash 1 card from your hand.");
        var handBefore = _p1.Hand.Count;

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        var request = _engine.Run(_context)!;

        Assert.Equal(handBefore + 2, _p1.Hand.Count);
        // Exactly 1 card must be trashed - no "Done" option.
        Assert.Equal([EffectChoiceRequest.Select], request.Options);

        var cardToTrash = request.Cards[0];
        Assert.Null(_engine.Answer(_context, EffectChoiceRequest.Select, cardToTrash.InstanceId));
        Assert.Equal(handBefore + 1, _p1.Hand.Count);
        Assert.Contains(cardToTrash, _p1.TrashCards);
    }

    [Fact]
    public void StaticDonAbility_ChangesPowerOnlyWithDon()
    {
        var card = AddCharacter(_p1, "card", power: 4000, text: "[DON!! x1] This Character gains +1000 power.");

        StaticEffects.Recalculate(_context);
        Assert.Equal(4000, card.GetPower(isOwnersTurn: false));

        card.AttachedDon = 1;
        StaticEffects.Recalculate(_context);
        // Base 4000 + 1000 from the static effect (DON!! power only counts in the owner's turn).
        Assert.Equal(5000, card.GetPower(isOwnersTurn: false));
        Assert.Equal(6000, card.GetPower(isOwnersTurn: true));
    }

    [Fact]
    public void StaticTypeBuff_AffectsAllMatchingCharacters()
    {
        _p1.Leader = new CardInstance
        {
            InstanceId = "leader",
            Name = "Leader",
            Category = CardInstance.LeaderCategory,
            OwnerId = "p1",
            BasePower = 5000,
            Text = "[Your Turn] All of your {Straw Hat Crew} type Characters gain +1000 power."
        };
        var crew = AddCharacter(_p1, "crew", power: 3000, types: "Straw Hat Crew");
        var other = AddCharacter(_p1, "other", power: 3000, types: "Navy");

        StaticEffects.Recalculate(_context);

        Assert.Equal(4000, crew.GetPower(isOwnersTurn: true));
        Assert.Equal(3000, other.GetPower(isOwnersTurn: true));

        _context.ActivePlayerId = "p2";
        StaticEffects.Recalculate(_context);
        Assert.Equal(3000, crew.GetPower(isOwnersTurn: false));
    }

    [Fact]
    public void CounterEvent_GivesPowerForThisBattle()
    {
        var leader = _p1.Leader!;
        var counter = new CardInstance
        {
            InstanceId = "counter",
            Name = "Guard Point",
            Category = CardInstance.EventCategory,
            OwnerId = "p1",
            Text = "[Counter] Up to 1 of your Leader or Character cards gains +3000 power during this battle."
        };

        foreach (var ability in counter.Script.WithTiming(EffectTiming.EventCounter))
        {
            _engine.Enqueue(ability, counter, "p1", alreadyChosen: true);
        }

        var request = _engine.Run(_context)!;
        Assert.Contains(request.Cards, card => card == leader);
        _engine.Answer(_context, EffectChoiceRequest.Select, leader.InstanceId);

        var modifier = Assert.Single(leader.Modifiers);
        Assert.Equal(3000, modifier.Power);
        Assert.Equal(Duration.ThisBattle, modifier.Duration);
    }

    [Fact]
    public void ActivateMain_OncePerTurn()
    {
        var card = AddCharacter(_p1, "card", text: "[Activate: Main] [Once Per Turn] Draw 1 card.");
        var ability = Assert.Single(card.Script.WithTiming(EffectTiming.ActivateMain));

        Assert.True(EffectEngine.CanActivate(ability, card, "p1", _context));
        _engine.Enqueue(ability, card, "p1", alreadyChosen: true);
        _engine.Run(_context);

        Assert.False(EffectEngine.CanActivate(ability, card, "p1", _context));
        _context.TurnNumber += 2;
        Assert.True(EffectEngine.CanActivate(ability, card, "p1", _context));
    }

    [Fact]
    public void LookAndReveal_OnlyOffersMatchingCards_RestGoesToBottom()
    {
        _p1.DrawDeck.Clear();
        _p1.DrawDeck.AddRange(
        [
            Card("navy1", types: "Navy"),
            Card("crew1", types: "Straw Hat Crew"),
            Card("navy2", types: "Navy"),
            Card("bottom", types: "Navy"),
        ]);
        var source = AddCharacter(_p1, "source",
            text: "[On Play] Look at 3 cards from the top of your deck; reveal up to 1 {Straw Hat Crew} type card and add it to your hand. Then, place the rest at the bottom of your deck in any order.");

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        var request = _engine.Run(_context)!;

        Assert.Equal(["crew1"], request.Cards.Select(card => card.InstanceId));
        _engine.Answer(_context, EffectChoiceRequest.Select, "crew1");

        Assert.Contains(_p1.Hand, card => card.InstanceId == "crew1");
        Assert.Equal(["bottom", "navy1", "navy2"], _p1.DrawDeck.Select(card => card.InstanceId));
    }

    [Fact]
    public void ProtectedCharacter_CannotBeChosenByOpponentKO()
    {
        AddCharacter(_p2, "protected", cost: 1, text: "This Character cannot be K.O.'d by effects.");
        var source = AddCharacter(_p1, "source", text: "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");
        StaticEffects.Recalculate(_context);

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);

        // No valid target -> nothing to ask.
        Assert.Null(_engine.Run(_context));
        Assert.Contains(_p2.Characters, card => card.InstanceId == "protected");
    }

    [Fact]
    public void InvalidAnswer_Throws_AndKeepsTheQuestion()
    {
        AddCharacter(_p2, "target", cost: 1);
        var source = AddCharacter(_p1, "source", text: "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        _engine.Run(_context);

        Assert.Throws<InvalidOperationException>(() => _engine.Answer(_context, EffectChoiceRequest.Select, "source"));
        Assert.NotNull(_engine.PendingChoice);
    }

    [Fact]
    public void TriggerPlayThisCard_PutsTheLifeCardOnTheField()
    {
        var lifeCard = Card("life", text: "[Trigger] Play this card.");
        lifeCard.OwnerId = "p2";

        foreach (var ability in lifeCard.Script.WithTiming(EffectTiming.Trigger))
        {
            _engine.Enqueue(ability, lifeCard, "p2", alreadyChosen: true);
        }

        _engine.Run(_context);

        Assert.Contains(lifeCard, _p2.Characters);
    }

    [Fact]
    public void OpponentTrashesFromHand_AsksTheOpponent()
    {
        var source = AddCharacter(_p1, "source", text: "[On Play] Your opponent trashes 1 card from their hand.");

        _engine.EnqueueTriggered(source, "p1", EffectTiming.OnPlay, _context);
        var request = _engine.Run(_context)!;

        Assert.Equal("p2", request.PlayerId);
        Assert.All(request.Cards, card => Assert.Contains(card, _p2.Hand));
    }

    // ---------- helpers ----------

    private static PlayerState CreatePlayer(string id)
    {
        var player = new PlayerState
        {
            PlayerId = id,
            DisplayName = id,
            DonDeckCount = 0,
            Leader = new CardInstance
            {
                InstanceId = $"{id}-leader",
                Name = $"{id} Leader",
                Category = CardInstance.LeaderCategory,
                OwnerId = id,
                BasePower = 5000
            }
        };

        for (var index = 0; index < 10; index += 1)
        {
            var card = Card($"{id}-deck-{index}");
            card.OwnerId = id;
            player.DrawDeck.Add(card);
        }

        for (var index = 0; index < 3; index += 1)
        {
            var card = Card($"{id}-hand-{index}");
            card.OwnerId = id;
            player.Hand.Add(card);
        }

        return player;
    }

    private static CardInstance Card(string id, int cost = 1, int power = 2000, string text = "", string types = "")
    {
        return new CardInstance
        {
            InstanceId = id,
            CardId = id,
            Name = id,
            Category = CardInstance.CharacterCategory,
            Cost = cost,
            BasePower = power,
            Text = text,
            Types = types
        };
    }

    private static CardInstance AddCharacter(PlayerState player, string id, int cost = 1, int power = 2000, string text = "", string types = "")
    {
        var card = Card(id, cost, power, text, types);
        card.OwnerId = player.PlayerId;
        player.Characters.Add(card);
        return card;
    }
}
