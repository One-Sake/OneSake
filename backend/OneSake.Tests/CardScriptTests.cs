using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests;

// Tests for real card scripts (CardScripts/generated and overrides). A card gets the status
// "verified" only together with a test here. See docs/card-scripts.md.
public class CardScriptTests
{
    private readonly CardScenario _game = new();

    #region ON PLAY

    [Fact]
    [CardTest("OP01-016")]
    public void Nami_LooksAtFive_AndOffersOnlyOtherStrawHatCharacters()
    {
        var p1 = _game.P1;
        p1.DrawDeck.Clear();
        var navy = _game.Plain(p1, types: "Navy");
        var zoro = _game.Plain(p1, types: "Straw Hat Crew");
        var otherNami = _game.Real("OP01-016", p1);
        var navy2 = _game.Plain(p1, types: "Navy");
        var navy3 = _game.Plain(p1, types: "Navy");
        var sixth = _game.Plain(p1, types: "Straw Hat Crew");
        p1.DrawDeck.AddRange([navy, zoro, otherNami, navy2, navy3, sixth]);
        var nami = _game.AddCharacter(p1, "OP01-016");
        CardScenario.AssertVerifiedScript(nami);

        var request = _game.Trigger(nami, EffectTiming.OnPlay)!;

        // Only the top 5, only {Straw Hat Crew} Characters, not [Nami].
        Assert.Equal([zoro.InstanceId], request.Cards.Select(card => card.InstanceId));
        Assert.Null(_game.Answer(EffectChoiceRequest.Select, zoro.InstanceId));
        Assert.Contains(zoro, p1.Hand);
        Assert.Equal(sixth, p1.DrawDeck[0]);
        Assert.Equal(5, p1.DrawDeck.Count);
    }

    [Fact]
    [CardTest("EB01-049")]
    public void TBone_KOsAnOpponentCharacterWithCostTwoOrLess()
    {
        var small = _game.AddPlain(_game.P2, cost: 2);
        var big = _game.AddPlain(_game.P2, cost: 3);
        var tBone = _game.AddCharacter(_game.P1, "EB01-049");
        CardScenario.AssertVerifiedScript(tBone);

        var request = _game.Trigger(tBone, EffectTiming.OnPlay)!;

        Assert.Equal([small.InstanceId], request.Cards.Select(card => card.InstanceId));
        Assert.Contains(EffectChoiceRequest.Done, request.Options);
        _game.Answer(EffectChoiceRequest.Select, small.InstanceId);
        Assert.Contains(small, _game.P2.TrashCards);
        Assert.Contains(big, _game.P2.Characters);
    }

    [Fact]
    [CardTest("OP03-044")]
    public void Kaya_DrawsTwo_ThenTrashesTwo()
    {
        var p1 = _game.P1;
        var kaya = _game.AddCharacter(p1, "OP03-044");
        CardScenario.AssertVerifiedScript(kaya);

        var request = _game.Trigger(kaya, EffectTiming.OnPlay);
        Assert.Equal(5, p1.Hand.Count);
        for (var guard = 0; request is not null && guard < 5; guard += 1)
        {
            Assert.DoesNotContain(EffectChoiceRequest.Done, request.Options);
            request = _game.Answer(EffectChoiceRequest.Select, request.Cards[0].InstanceId);
        }

        Assert.Equal(3, p1.Hand.Count);
        Assert.Equal(2, p1.TrashCards.Count);
        Assert.Equal(8, p1.DrawDeck.Count);
    }

    #endregion

    #region WHEN ATTACKING / ACTIVATE: MAIN

    [Fact]
    [CardTest("OP01-022")]
    public void Brook_WithOneDon_GivesUpToTwoOpponentCharactersMinus2000()
    {
        var first = _game.AddPlain(_game.P2, power: 5000);
        var second = _game.AddPlain(_game.P2, power: 5000);
        var brook = _game.AddCharacter(_game.P1, "OP01-022");
        CardScenario.AssertVerifiedScript(brook);

        // [DON!! x1]: nothing happens without a given DON!!.
        Assert.Null(_game.Trigger(brook, EffectTiming.WhenAttacking));

        brook.AttachedDon = 1;
        var request = _game.Trigger(brook, EffectTiming.WhenAttacking);
        foreach (var target in new[] { first, second })
        {
            Assert.NotNull(request);
            request = _game.Answer(EffectChoiceRequest.Select, target.InstanceId);
        }

        if (request is not null)
        {
            _game.Answer(EffectChoiceRequest.Done);
        }

        Assert.All([first, second], card =>
        {
            var modifier = Assert.Single(card.Modifiers);
            Assert.Equal(-2000, modifier.Power);
            Assert.Equal(Duration.ThisTurn, modifier.Duration);
        });
    }

    [Fact]
    [CardTest("OP16-049")]
    public void Ace_RestsItselfToDrawOneCard()
    {
        var ace = _game.AddCharacter(_game.P1, "OP16-049");
        CardScenario.AssertVerifiedScript(ace);
        var ability = Assert.Single(ace.Script.WithTiming(EffectTiming.ActivateMain));
        Assert.True(EffectEngine.CanActivate(ability, ace, "p1", _game.Context));

        var request = _game.Trigger(ace, EffectTiming.ActivateMain, startedByPlayer: true);
        if (request is not null)
        {
            Assert.Null(_game.Answer(EffectChoiceRequest.Yes));
        }

        Assert.True(ace.IsRested);
        Assert.Equal(4, _game.P1.Hand.Count);
        // The cost cannot be paid a second time.
        Assert.False(EffectEngine.CanActivate(ability, ace, "p1", _game.Context));
    }

    #endregion

    #region TRIGGER / COUNTER

    [Fact]
    [CardTest("OP01-009")]
    public void Carrot_TriggerPlaysTheLifeCard()
    {
        var carrot = _game.Real("OP01-009", _game.P2);
        CardScenario.AssertVerifiedScript(carrot);

        Assert.Null(_game.Trigger(carrot, EffectTiming.Trigger, startedByPlayer: true));

        Assert.Contains(carrot, _game.P2.Characters);
    }

    [Fact]
    [CardTest("ST01-014")]
    public void GuardPoint_CounterGivesPlus3000ForTheBattle_TriggerPlus1000ForTheTurn()
    {
        var leader = _game.P1.Leader!;
        var guardPoint = _game.Real("ST01-014", _game.P1);
        CardScenario.AssertVerifiedScript(guardPoint);

        var request = _game.Trigger(guardPoint, EffectTiming.EventCounter, startedByPlayer: true)!;
        Assert.Contains(request.Cards, card => card == leader);
        _game.Answer(EffectChoiceRequest.Select, leader.InstanceId);

        var counter = Assert.Single(leader.Modifiers);
        Assert.Equal(3000, counter.Power);
        Assert.Equal(Duration.ThisBattle, counter.Duration);

        _game.Trigger(guardPoint, EffectTiming.Trigger, startedByPlayer: true);
        _game.Answer(EffectChoiceRequest.Select, leader.InstanceId);
        Assert.Contains(leader.Modifiers, modifier => modifier.Power == 1000 && modifier.Duration == Duration.ThisTurn);
    }

    #endregion

    #region STATIC / ON K.O.

    [Fact]
    [CardTest("ST01-013")]
    public void Zoro_WithOneDon_Gains1000Power()
    {
        var zoro = _game.AddCharacter(_game.P1, "ST01-013");
        CardScenario.AssertVerifiedScript(zoro);

        _game.Recalculate();
        Assert.Equal(5000, zoro.GetPower(isOwnersTurn: true));

        zoro.AttachedDon = 1;
        _game.Recalculate();
        // 5000 + 1000 (given DON!!) + 1000 (effect).
        Assert.Equal(7000, zoro.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [CardTest("OP04-049")]
    public void Jack_OnKO_DrawsOneCard()
    {
        var jack = _game.AddCharacter(_game.P2, "OP04-049");
        CardScenario.AssertVerifiedScript(jack);

        Assert.True(_game.Engine.KnockOut(jack, _game.Context, byEffect: true));
        Assert.Null(_game.Engine.Run(_game.Context));

        Assert.Contains(jack, _game.P2.TrashCards);
        Assert.Equal(4, _game.P2.Hand.Count);
    }

    [Fact]
    [CardTest("OP10-005")]
    public void Sanji_Gains3000OnlyOnHisTurn_AndDrawsWhenKOd()
    {
        var sanji = _game.AddCharacter(_game.P1, "OP10-005");
        CardScenario.AssertVerifiedScript(sanji);

        _game.Recalculate();
        Assert.Equal(6000, sanji.GetPower(isOwnersTurn: true));

        _game.Context.ActivePlayerId = "p2";
        _game.Recalculate();
        Assert.Equal(3000, sanji.GetPower(isOwnersTurn: false));

        _game.Engine.KnockOut(sanji, _game.Context, byEffect: false);
        _game.Engine.Run(_game.Context);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    #endregion

    #region OVERRIDES (the parser does not understand these texts)

    [Fact]
    [CardTest("OP01-080")]
    public void MissDoublefinger_Override_DrawsACardWhenKOd()
    {
        var card = _game.AddCharacter(_game.P1, "OP01-080");
        CardScenario.AssertVerifiedScript(card);
        Assert.Equal(CardScriptRegistry.OriginOverrides, card.ScriptInfo.Origin);

        _game.Engine.KnockOut(card, _game.Context, byEffect: true);
        _game.Engine.Run(_game.Context);

        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [CardTest("OP06-089")]
    public void Taralan_Override_TrashesThreeOnPlayAndOnKO()
    {
        var p1 = _game.P1;
        var card = _game.AddCharacter(p1, "OP06-089");
        CardScenario.AssertVerifiedScript(card);
        Assert.Equal(CardScriptRegistry.OriginOverrides, card.ScriptInfo.Origin);

        _game.Trigger(card, EffectTiming.OnPlay);
        Assert.Equal(7, p1.DrawDeck.Count);
        Assert.Equal(3, p1.TrashCards.Count);

        _game.Engine.KnockOut(card, _game.Context, byEffect: true);
        _game.Engine.Run(_game.Context);
        Assert.Equal(4, p1.DrawDeck.Count);
        // 3 + 3 from the deck + the K.O.'d card itself.
        Assert.Equal(7, p1.TrashCards.Count);
    }

    [Fact]
    [CardTest("OP16-069")]
    public void Doflamingo_Override_AddsAnActiveDonOnPlayAndWhenAttacking()
    {
        var p1 = _game.P1;
        var card = _game.AddCharacter(p1, "OP16-069");
        CardScenario.AssertVerifiedScript(card);
        Assert.Equal(CardScriptRegistry.OriginOverrides, card.ScriptInfo.Origin);

        _game.Trigger(card, EffectTiming.OnPlay);
        Assert.Equal(4, p1.DonDeckCount);
        Assert.Equal(1, p1.ActiveDon);

        _game.Trigger(card, EffectTiming.WhenAttacking);
        Assert.Equal(3, p1.DonDeckCount);
        Assert.Equal(2, p1.ActiveDon);
    }

    #endregion

    // Errata check: the same card ID with a different text is treated as manual.
    [Fact]
    public void ChangedCardText_MakesTheCardManual()
    {
        var card = new CardInstance { CardId = "OP04-049", Text = "[On K.O.] Draw 2 cards.", Category = CardInstance.CharacterCategory };

        var info = card.ScriptInfo;

        Assert.True(info.TextChanged);
        Assert.Equal(CardScriptStatus.Manual, info.Status);
        Assert.Empty(info.Script.Abilities);
        Assert.Contains("[On K.O.] Draw 2 cards.", info.Script.UnsupportedTexts);
    }
}
