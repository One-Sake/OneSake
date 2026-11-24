using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: everything in front of the ":" of an ability. Each cost is tested with
// "[Activate: Main] <cost>: Draw 1 card." - the draw shows that the effect ran after paying.
public class CostBlockTests
{
    private readonly BlockGame _game = new();

    private CardInstance WithCost(string cost, PlayerState? owner = null)
    {
        var card = _game.OnField(owner ?? _game.P1, $"[Activate: Main] {cost}: Draw 1 card.");
        Assert.Single(BlockGame.Ability(card).Steps);
        return card;
    }

    private static EffectCost OnlyCost(CardInstance card) => Assert.Single(BlockGame.Ability(card).Costs);

    private bool CanActivate(CardInstance card) =>
        EffectEngine.CanActivate(BlockGame.Ability(card), card, card.OwnerId, _game.Scenario);

    [Fact]
    [BlockTest("Cost.ReturnDon")]
    public void ReturnDon_PutsDonBackIntoTheDonDeck()
    {
        var card = WithCost("DON!! -2");
        Assert.Equal(CostKind.ReturnDon, OnlyCost(card).Kind);
        Assert.Equal(2, OnlyCost(card).Amount);

        _game.P1.ActiveDon = 1;
        Assert.False(CanActivate(card));

        _game.P1.ActiveDon = 1;
        _game.P1.RestedDon = 2;
        _game.ActivateMain(card);

        // Rested DON!! are returned first.
        Assert.Equal(1, _game.P1.ActiveDon);
        Assert.Equal(0, _game.P1.RestedDon);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.ReturnDon")]
    public void ReturnDon_WithoutMinusSign_IsTheSameCost()
    {
        // Newer card texts in the API lost the minus sign.
        var card = WithCost("DON!! 1");

        Assert.Equal(CostKind.ReturnDon, OnlyCost(card).Kind);
        Assert.Equal(1, OnlyCost(card).Amount);
    }

    [Fact]
    [BlockTest("Cost.RestDon")]
    public void RestDon_CircledNumber_RestsActiveDon()
    {
        var card = WithCost("(2)");
        Assert.Equal(CostKind.RestDon, OnlyCost(card).Kind);

        _game.P1.ActiveDon = 1;
        Assert.False(CanActivate(card));

        _game.P1.ActiveDon = 3;
        _game.ActivateMain(card);

        Assert.Equal(1, _game.P1.ActiveDon);
        Assert.Equal(2, _game.P1.RestedDon);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.RestDon")]
    public void RestDon_WrittenOut_IsTheSameCost()
    {
        var card = WithCost("You may rest 1 of your DON!! cards");

        Assert.Equal(CostKind.RestDon, OnlyCost(card).Kind);
        Assert.Equal(1, OnlyCost(card).Amount);
    }

    [Fact]
    [BlockTest("Cost.RestThis")]
    public void RestThis_CannotBePaidWhenTheCardIsAlreadyRested()
    {
        var card = WithCost("You may rest this Character");
        Assert.Equal(CostKind.RestThis, OnlyCost(card).Kind);

        _game.ActivateMain(card);
        Assert.True(card.IsRested);
        Assert.Equal(4, _game.P1.Hand.Count);

        Assert.False(CanActivate(card));
    }

    [Fact]
    [BlockTest("Cost.RestThis")]
    [BlockTest("Cost.RestDon")]
    public void RestDonAndThisCharacter_AreTwoCosts()
    {
        var card = WithCost("(1) and rest this Character");
        var costs = BlockGame.Ability(card).Costs;

        Assert.Equal([CostKind.RestDon, CostKind.RestThis], costs.Select(cost => cost.Kind));
    }

    [Fact]
    [BlockTest("Cost.TrashThis")]
    public void TrashThis_MovesTheCardToTheTrash()
    {
        var card = WithCost("You may trash this Character");
        Assert.Equal(CostKind.TrashThis, OnlyCost(card).Kind);

        _game.ActivateMain(card);

        Assert.DoesNotContain(card, _game.P1.Characters);
        Assert.Contains(card, _game.P1.TrashCards);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.ReturnThis")]
    public void ReturnThis_MovesTheCardToTheHand()
    {
        var card = WithCost("You may return this Character to the owner's hand");
        Assert.Equal(CostKind.ReturnThis, OnlyCost(card).Kind);

        _game.ActivateMain(card);

        Assert.DoesNotContain(card, _game.P1.Characters);
        Assert.Contains(card, _game.P1.Hand);
        // 3 + the returned card + the drawn card
        Assert.Equal(5, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashFromHand")]
    public void TrashFromHand_ThePlayerChoosesTheCard()
    {
        var card = WithCost("You may trash 1 card from your hand");
        Assert.Equal(CostKind.TrashFromHand, OnlyCost(card).Kind);
        var chosen = _game.P1.Hand[1];

        _game.ActivateMain(card);
        Assert.Equal(3, _game.Offered.Count);
        Assert.Equal([EffectChoiceRequest.Select], _game.Choice.Options);
        _game.Select(chosen);

        Assert.Contains(chosen, _game.P1.TrashCards);
        Assert.Equal(3, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashFromHand")]
    public void TrashFromHand_WithAFilter_OnlyOffersMatchingCards()
    {
        var card = WithCost("You may trash 1 {Navy} type card from your hand");
        var navy = _game.InHand(_game.P1, types: "Navy");
        Assert.Equal("Navy", OnlyCost(card).Filter!.TypeIncludes);

        _game.ActivateMain(card);

        Assert.Equal(navy, Assert.Single(_game.Offered));
    }

    [Fact]
    [BlockTest("Cost.TrashFromHand")]
    public void TrashFromHand_CannotBePaidWithTooFewCards()
    {
        var card = WithCost("You may trash 2 cards from your hand");
        _game.P1.Hand.RemoveRange(0, 2);

        Assert.False(CanActivate(card));
    }

    [Fact]
    [BlockTest("Cost.RevealFromHand")]
    public void RevealFromHand_KeepsTheCardInTheHand()
    {
        var card = WithCost("You may reveal 1 {Navy} type card from your hand");
        Assert.Equal(CostKind.RevealFromHand, OnlyCost(card).Kind);
        Assert.False(CanActivate(card));

        var navy = _game.InHand(_game.P1, types: "Navy", name: "Navy card");
        _game.ActivateMain(card);

        Assert.Contains(navy, _game.P1.Hand);
        Assert.Equal(5, _game.P1.Hand.Count);
        Assert.Contains(_game.Logs, line => line.Contains("reveals Navy card"));
    }

    [Fact]
    [BlockTest("Cost.LifeToHand")]
    public void LifeToHand_AddsTheTopLifeCardToTheHand()
    {
        var card = WithCost("You may add 1 card from the top of your Life cards to your hand");
        Assert.Equal(CostKind.LifeToHand, OnlyCost(card).Kind);
        var top = _game.P1.LifeCards[^1];

        _game.ActivateMain(card);

        Assert.Equal(4, _game.P1.LifeCount);
        Assert.Contains(top, _game.P1.Hand);
        Assert.Equal(5, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashLife")]
    public void TrashLife_TrashesTheTopLifeCard()
    {
        var card = WithCost("You may trash 1 card from the top of your Life cards");
        Assert.Equal(CostKind.TrashLife, OnlyCost(card).Kind);
        var top = _game.P1.LifeCards[^1];

        _game.ActivateMain(card);

        Assert.Equal(4, _game.P1.LifeCount);
        Assert.Contains(top, _game.P1.TrashCards);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TurnLife")]
    public void TurnLife_NeedsEnoughLifeCards()
    {
        // Face-up Life cards are not shown differently yet: the cost only checks the number.
        var card = WithCost("You may turn 2 cards from the top of your Life cards face-up");
        Assert.Equal(CostKind.TurnLife, OnlyCost(card).Kind);

        _game.P1.LifeCards.RemoveRange(0, 4);
        Assert.False(CanActivate(card));

        _game.P1.LifeCards.Add(_game.Card(_game.P1));
        _game.ActivateMain(card);
        Assert.Equal(2, _game.P1.LifeCount);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashTopOfDeck")]
    public void TrashTopOfDeck_TrashesFromTheTopOfTheDeck()
    {
        var card = WithCost("You may trash 2 cards from the top of your deck");
        Assert.Equal(CostKind.TrashTopOfDeck, OnlyCost(card).Kind);
        var top = _game.P1.DrawDeck.Take(2).ToList();

        _game.ActivateMain(card);

        Assert.All(top, trashed => Assert.Contains(trashed, _game.P1.TrashCards));
        // 10 - 2 trashed - 1 drawn
        Assert.Equal(7, _game.P1.DrawDeck.Count);
    }

    [Fact]
    [BlockTest("Cost.RestOwnCards")]
    public void RestOwnCards_OnlyOffersActiveCards()
    {
        var card = WithCost("You may rest 1 of your Characters");
        Assert.Equal(CostKind.RestOwnCards, OnlyCost(card).Kind);
        var active = _game.OnField(_game.P1);
        var rested = _game.OnField(_game.P1);
        rested.IsRested = true;

        _game.ActivateMain(card);
        Assert.DoesNotContain(rested, _game.Offered);
        _game.Select(active);

        Assert.True(active.IsRested);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.ReturnOwnCharacter")]
    public void ReturnOwnCharacter_MovesTheChosenCharacterToTheHand()
    {
        var card = WithCost("You may return 1 of your Characters to the owner's hand");
        Assert.Equal(CostKind.ReturnOwnCharacter, OnlyCost(card).Kind);
        var other = _game.OnField(_game.P1);

        _game.ActivateMain(card);
        Assert.DoesNotContain(_game.P1.Leader!, _game.Offered);
        _game.Select(other);

        Assert.Contains(other, _game.P1.Hand);
        Assert.Equal(5, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashOwnCharacter")]
    public void TrashOwnCharacter_MovesTheChosenCharacterToTheTrash()
    {
        var card = WithCost("You may trash 1 of your Characters");
        Assert.Equal(CostKind.TrashOwnCharacter, OnlyCost(card).Kind);
        var other = _game.OnField(_game.P1);

        _game.ActivateMain(card);
        _game.Select(other);

        Assert.Contains(other, _game.P1.TrashCards);
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Cost.TrashToDeckBottom")]
    public void TrashToDeckBottom_PlacesTrashCardsUnderTheDeck()
    {
        var card = WithCost("You may place 2 cards from your trash at the bottom of your deck in any order");
        Assert.Equal(CostKind.TrashToDeckBottom, OnlyCost(card).Kind);
        var first = _game.InTrash(_game.P1);
        Assert.False(CanActivate(card));
        var second = _game.InTrash(_game.P1);

        _game.ActivateMain(card);
        _game.Select(first);
        _game.Select(second);

        Assert.Empty(_game.P1.TrashCards);
        Assert.Equal([first, second], _game.P1.DrawDeck.TakeLast(2));
    }

    [Fact]
    [BlockTest("Option.Optional")]
    public void CostOfAnAutoEffect_AsksTheController_AndNoPaysNothing()
    {
        var card = _game.OnField(_game.P1, "[On Play] You may trash this Character: Draw 2 cards.");

        _game.Play(card);
        Assert.Equal([EffectChoiceRequest.Yes, EffectChoiceRequest.No], _game.Choice.Options);
        Assert.StartsWith("Use the effect of", _game.Choice.Title);
        _game.No();

        Assert.Contains(card, _game.P1.Characters);
        Assert.Equal(3, _game.P1.Hand.Count);
    }

    [Fact]
    public void CostOfAnAutoEffect_YesPaysAndRunsTheEffect()
    {
        var card = _game.OnField(_game.P1, "[On Play] You may trash this Character: Draw 2 cards.");

        _game.Play(card);
        _game.Yes();

        Assert.Contains(card, _game.P1.TrashCards);
        Assert.Equal(5, _game.P1.Hand.Count);
    }
}
