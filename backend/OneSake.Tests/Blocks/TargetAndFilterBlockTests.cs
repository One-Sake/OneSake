using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: which cards a step affects ("up to 2", "all of", "this Character") and how cards
// are described ("with a cost of 3 or less", "{Navy} type", "rested" ...).
// Most tests look at the cards the effect offers: only matching cards may be offered.
public class TargetAndFilterBlockTests
{
    private readonly BlockGame _game = new();

    private CardInstance Source(string effect)
    {
        var card = _game.OnField(_game.P1, $"[On Play] {effect}", name: "Source");
        BlockGame.Ability(card);
        return card;
    }

    private IReadOnlyList<CardInstance> OfferedBy(string effect)
    {
        _game.Play(Source(effect));
        return _game.Offered;
    }

    // ---------- Targets ----------

    [Fact]
    [BlockTest("Target.Count")]
    [BlockTest("Target.UpTo")]
    public void UpToTwo_AsksTwice_AndAllowsStoppingEarly()
    {
        var source = Source("Rest up to 2 of your opponent's Characters.");
        var target = BlockGame.Step(source).Target!;
        Assert.Equal(2, target.Count);
        Assert.True(target.UpTo);
        var first = _game.OnField(_game.P2);
        var second = _game.OnField(_game.P2);
        var third = _game.OnField(_game.P2);

        _game.Play(source);
        Assert.Contains(EffectChoiceRequest.Done, _game.Choice.Options);
        _game.Select(first);
        Assert.DoesNotContain(first, _game.Offered);
        _game.Select(second);

        Assert.Null(_game.Request);
        Assert.True(first.IsRested && second.IsRested);
        Assert.False(third.IsRested);
    }

    [Fact]
    [BlockTest("Target.UpTo")]
    public void UpTo_DoneChoosesNothing()
    {
        var target = _game.OnField(_game.P2);

        OfferedBy("Rest up to 1 of your opponent's Characters.");
        _game.Done();

        Assert.False(target.IsRested);
    }

    [Fact]
    [BlockTest("Target.Exact")]
    public void ExactNumber_MustBeChosen()
    {
        var source = Source("K.O. 1 of your Characters.");
        Assert.False(BlockGame.Step(source).Target!.UpTo);
        var own = _game.OnField(_game.P1);

        _game.Play(source);

        Assert.Equal([EffectChoiceRequest.Select], _game.Choice.Options);
        _game.Select(own);
        Assert.Contains(own, _game.P1.TrashCards);
    }

    [Fact]
    [BlockTest("Target.All")]
    public void All_AffectsEveryMatchingCard_WithoutAChoice()
    {
        var source = Source("Give all of your opponent's Characters -1000 power during this turn.");
        Assert.True(BlockGame.Step(source).Target!.All);
        var first = _game.OnField(_game.P2);
        var second = _game.OnField(_game.P2);
        var own = _game.OnField(_game.P1);

        Assert.Null(_game.Play(source));

        Assert.Equal(1000, first.GetPower(isOwnersTurn: false));
        Assert.Equal(1000, second.GetPower(isOwnersTurn: false));
        Assert.Equal(2000, own.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Target.All")]
    public void PluralWithoutANumber_MeansAll()
    {
        var card = _game.OnField(_game.P1, "[Your Turn] Your {Navy} type Characters gain +1000 power.");
        var navy = _game.OnField(_game.P1, types: "Navy");
        var other = _game.OnField(_game.P1);

        Assert.True(BlockGame.Step(card).Target!.All);
        _game.Recalculate();
        Assert.Equal(3000, navy.GetPower(isOwnersTurn: true));
        Assert.Equal(2000, other.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Target.Self")]
    public void Self_DoesNothingWhenTheCardLeftTheField()
    {
        var source = Source("This Character gains +1000 power during this turn.");
        Assert.True(BlockGame.Step(source).Target!.Self);
        _game.P1.RemoveFromField(source);

        _game.Publish(new OneSake.Game.Rules.CardPlayed(source, "p1", ByEffect: false));

        Assert.Empty(source.Modifiers);
    }

    [Fact]
    [BlockTest("Target.YourLeader")]
    public void YourLeader_NeedsNoChoice()
    {
        var source = Source("Your Leader gains +2000 power during this turn.");
        Assert.True(BlockGame.Step(source).Target!.YourLeader);

        Assert.Null(_game.Play(source));
        Assert.Equal(7000, _game.P1.Leader!.GetPower(isOwnersTurn: true));
    }

    // ---------- Filters: whose cards and which kind ----------

    [Fact]
    [BlockTest("Filter.Owner")]
    public void Owner_YourOpponentsOrAny()
    {
        var own = _game.OnField(_game.P1);
        var theirs = _game.OnField(_game.P2);

        var opponent = OfferedBy("Rest up to 1 of your opponent's Characters.");
        Assert.Equal([theirs], opponent);
        _game.Done();

        var yours = OfferedBy("Rest up to 1 of your Characters.");
        Assert.Contains(own, yours);
        Assert.DoesNotContain(theirs, yours);
        _game.Done();

        var any = OfferedBy("Rest up to 1 Character.");
        Assert.Contains(own, any);
        Assert.Contains(theirs, any);
    }

    [Fact]
    [BlockTest("Filter.AllowLeader")]
    [BlockTest("Filter.AllowCharacter")]
    public void LeaderOrCharacter_OffersBoth_LeaderOnlyOffersNoCharacter()
    {
        var character = _game.OnField(_game.P2);

        var both = OfferedBy("Give up to 1 of your opponent's Leader or Character cards -1000 power during this turn.");
        Assert.Equal([_game.P2.Leader!, character], both);
        _game.Done();

        var source = Source("Give your opponent's Leader -1000 power during this turn.");
        var filter = BlockGame.Step(source).Target!.Filter;
        Assert.True(filter.AllowLeader);
        Assert.False(filter.AllowCharacter);
        _game.Play(source);
        Assert.Equal(4000, _game.P2.Leader!.GetPower(isOwnersTurn: false));
        Assert.Empty(character.Modifiers);
    }

    [Fact]
    [BlockTest("Filter.AllowStage")]
    public void Stages_AreOnlyOfferedWhenTheTextSaysSo()
    {
        var stage = _game.OnField(_game.P2, category: CardInstance.StageCategory, cost: 1);
        var character = _game.OnField(_game.P2, cost: 1);

        var stages = OfferedBy("Trash up to 1 of your opponent's Stages with a cost of 3 or less.");
        Assert.Equal([stage], stages);
        _game.Select(stage);
        Assert.Contains(stage, _game.P2.TrashCards);
        Assert.Null(_game.P2.Stage);

        var characters = OfferedBy("Rest up to 1 of your opponent's Characters.");
        Assert.Equal([character], characters);
    }

    [Fact]
    [BlockTest("Filter.Category")]
    public void Category_EventCard()
    {
        var eventCard = _game.InTrash(_game.P1, category: CardInstance.EventCategory);
        _game.InTrash(_game.P1);

        var offered = OfferedBy("Add up to 1 Event card from your trash to your hand.");

        Assert.Equal([eventCard], offered);
    }

    // ---------- Filters: values ----------

    [Fact]
    [BlockTest("Filter.MaxCost")]
    [BlockTest("Filter.MinCost")]
    public void Cost_OrLess_OrMore_OrExact()
    {
        var cheap = _game.OnField(_game.P2, cost: 2);
        var middle = _game.OnField(_game.P2, cost: 5);
        var expensive = _game.OnField(_game.P2, cost: 7);

        Assert.Equal([cheap, middle], OfferedBy("Rest up to 1 of your opponent's Characters with a cost of 5 or less."));
        _game.Done();
        Assert.Equal([middle, expensive], OfferedBy("Rest up to 1 of your opponent's Characters with a cost of 5 or more."));
        _game.Done();
        Assert.Equal([middle], OfferedBy("Rest up to 1 of your opponent's Characters with a cost of 5."));
    }

    [Fact]
    [BlockTest("Filter.MaxPower")]
    [BlockTest("Filter.MinPower")]
    public void Power_OrLess_OrMore_UsesTheCurrentPower()
    {
        var weak = _game.OnField(_game.P2, power: 3000);
        var strong = _game.OnField(_game.P2, power: 6000);
        strong.Modifiers.Add(new CardModifier { Power = -3000, Duration = Duration.ThisTurn });

        Assert.Equal([weak, strong], OfferedBy("Rest up to 1 of your opponent's Characters with 3000 power or less."));
        _game.Done();
        Assert.Equal([weak, strong], OfferedBy("Rest up to 1 of your opponent's Characters with 3000 power or more."));
        _game.Done();
        Assert.Empty(EffectQueries.FieldCandidates(
            BlockGame.Step(Source("Rest up to 1 of your opponent's Characters with 4000 power or more.")).Target!.Filter,
            null, "p1", _game.Scenario));
    }

    [Fact]
    [BlockTest("Filter.UseBaseValues")]
    public void BaseCost_IgnoresModifiers()
    {
        var card = _game.OnField(_game.P2, cost: 5);
        card.Modifiers.Add(new CardModifier { Cost = -3, Duration = Duration.ThisTurn });
        var source = Source("Rest up to 1 of your opponent's Characters with a base cost of 3 or less.");
        Assert.True(BlockGame.Step(source).Target!.Filter.UseBaseValues);

        Assert.Null(_game.Play(source));

        Assert.Equal([card], OfferedBy("Rest up to 1 of your opponent's Characters with a cost of 3 or less."));
    }

    [Fact]
    [BlockTest("Filter.Rested")]
    public void Rested_OnlyRestedCards()
    {
        var rested = _game.OnField(_game.P2);
        rested.IsRested = true;
        _game.OnField(_game.P2);

        Assert.Equal([rested], OfferedBy("K.O. up to 1 of your opponent's rested Characters."));
    }

    [Fact]
    [BlockTest("Filter.Rested")]
    public void Active_OnlyActiveCards()
    {
        var active = _game.OnField(_game.P2);
        _game.OnField(_game.P2).IsRested = true;

        Assert.Equal([active], OfferedBy("K.O. up to 1 of your opponent's active Characters."));
    }

    [Fact]
    [BlockTest("Filter.MaxCostFrom")]
    public void CostUpToYourDon()
    {
        _game.P1.ActiveDon = 3;
        var fits = _game.OnField(_game.P2, cost: 3);
        _game.OnField(_game.P2, cost: 4);

        Assert.Equal([fits], OfferedBy("K.O. up to 1 of your opponent's Characters with a cost equal to or less than the number of DON!! cards on your field."));
    }

    [Fact]
    [BlockTest("Filter.MaxCostFrom")]
    public void CostUpToTheOpponentsLife()
    {
        _game.P2.LifeCards.RemoveRange(0, 3);
        var fits = _game.OnField(_game.P2, cost: 2);
        _game.OnField(_game.P2, cost: 3);

        Assert.Equal([fits], OfferedBy("K.O. up to 1 of your opponent's Characters with a cost equal to or less than the number of your opponent's Life cards."));
    }

    // ---------- Filters: names, types, colors ... ----------

    [Fact]
    [BlockTest("Filter.TypeIncludes")]
    public void Type_OneOfSeveralTypes()
    {
        var navy = _game.OnField(_game.P2, types: "Navy");
        var both = _game.OnField(_game.P2, types: "Supernovas/Straw Hat Crew");
        _game.OnField(_game.P2, types: "Animal");

        Assert.Equal([navy], OfferedBy("Rest up to 1 of your opponent's {Navy} type Characters."));
        _game.Done();
        Assert.Equal([navy, both], OfferedBy("Rest up to 1 of your opponent's {Navy} or {Straw Hat Crew} type Characters."));
    }

    [Fact]
    [BlockTest("Filter.Name")]
    public void Name_FromTheTrash()
    {
        var nami = _game.InTrash(_game.P1, name: "Nami");
        _game.InTrash(_game.P1, name: "Usopp");

        Assert.Equal([nami], OfferedBy("Add up to 1 [Nami] from your trash to your hand."));
    }

    [Fact]
    [BlockTest("Filter.NameNot")]
    public void OtherThanName()
    {
        _game.InTrash(_game.P1, name: "Nami", types: "Straw Hat Crew");
        var usopp = _game.InTrash(_game.P1, name: "Usopp", types: "Straw Hat Crew");

        Assert.Equal([usopp], OfferedBy("Add up to 1 {Straw Hat Crew} type card other than [Nami] from your trash to your hand."));
    }

    [Fact]
    [BlockTest("Filter.NameOrType")]
    public void NameOrType_EitherIsEnough()
    {
        var nami = _game.InTrash(_game.P1, name: "Nami");
        var crew = _game.InTrash(_game.P1, types: "Straw Hat Crew");
        _game.InTrash(_game.P1);

        Assert.Equal([nami, crew], OfferedBy("Add up to 1 [Nami] or {Straw Hat Crew} type card from your trash to your hand."));
    }

    [Fact]
    [BlockTest("Filter.Color")]
    public void Color_MulticoloredCardsHaveBothColors()
    {
        var green = _game.InHand(_game.P1, cost: 2);
        var source = Source("Play up to 1 green Character card with a cost of 2 or less from your hand.");
        Assert.Equal("green", BlockGame.Step(source).Filter!.Color);
        var multi = _game.Card(_game.P1, cost: 2, color: "Red/Green");
        _game.P1.Hand.Add(multi);

        _game.Play(source);

        // "green" is not in the color of the first card (Red).
        Assert.Equal([multi], _game.Offered);
        Assert.DoesNotContain(green, _game.Offered);
    }

    [Fact]
    [BlockTest("Filter.Attribute")]
    public void Attribute()
    {
        var slash = _game.OnField(_game.P2, attribute: "Slash");
        _game.OnField(_game.P2, attribute: "Strike");

        Assert.Equal([slash], OfferedBy("Rest up to 1 of your opponent's <Slash> attribute Characters."));
    }

    [Fact]
    [BlockTest("Filter.Keyword")]
    public void Keyword()
    {
        var blocker = _game.OnField(_game.P2, "[Blocker]");
        _game.OnField(_game.P2);

        Assert.Equal([blocker], OfferedBy("K.O. up to 1 of your opponent's [Blocker] Characters."));
    }

    [Fact]
    [BlockTest("Filter.ExcludeSource")]
    public void OtherThanThisCharacter()
    {
        var other = _game.OnField(_game.P1);

        Assert.Equal([other], OfferedBy("Set up to 1 of your Characters other than this Character as active."));
    }

    [Fact]
    [BlockTest("Filter.RequiresTrigger")]
    public void WithATrigger()
    {
        var withTrigger = _game.InTrash(_game.P1, "[Trigger] Draw 1 card.");
        _game.InTrash(_game.P1);

        Assert.Equal([withTrigger], OfferedBy("Add up to 1 card with a [Trigger] from your trash to your hand."));
    }
}
