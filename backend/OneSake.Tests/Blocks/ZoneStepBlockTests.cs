using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: actions with the hand, the deck, the Life cards, the trash and DON!!.
// The effects are [On Play] abilities of p1's Character "Source" unless said otherwise.
public class ZoneStepBlockTests
{
    private readonly BlockGame _game = new();

    private CardInstance Source(string effect, int stepCount = 1)
    {
        var card = _game.OnField(_game.P1, $"[On Play] {effect}", name: "Source");
        Assert.Equal(stepCount, BlockGame.Ability(card).Steps.Count);
        return card;
    }

    // Five named cards on top of p1's deck: Top1 (first) ... Top5.
    private List<CardInstance> NamedDeckTop(params string[] types)
    {
        var cards = Enumerable.Range(1, 5)
            .Select(index => _game.Card(_game.P1, name: $"Top{index}", types: index - 1 < types.Length ? types[index - 1] : ""))
            .ToList();
        _game.DeckTop(_game.P1, [.. cards]);
        return cards;
    }

    // ---------- Hand ----------

    [Fact]
    [BlockTest("Step.Draw")]
    public void Draw_TakesTheTopCardsOfTheDeck()
    {
        var source = Source("Draw 2 cards.");
        Assert.Equal(StepKind.Draw, BlockGame.Step(source).Kind);
        var top = NamedDeckTop();

        _game.Play(source);

        Assert.Contains(top[0], _game.P1.Hand);
        Assert.Contains(top[1], _game.P1.Hand);
        Assert.Equal(13, _game.P1.DrawDeck.Count);
    }

    [Fact]
    [BlockTest("Step.TrashFromHand")]
    public void TrashFromHand_AfterDrawing_MustBeDone()
    {
        var source = Source("Draw 2 cards and trash 1 card from your hand.", 2);
        Assert.Equal(StepKind.TrashFromHand, BlockGame.Step(source, stepIndex: 1).Kind);

        _game.Play(source);
        Assert.Equal([EffectChoiceRequest.Select], _game.Choice.Options);
        _game.Select(_game.Offered[0]);

        Assert.Equal(4, _game.P1.Hand.Count);
        Assert.Single(_game.P1.TrashCards);
    }

    [Fact]
    [BlockTest("Option.Optional")]
    public void YouMay_AsksYesOrNo()
    {
        var source = Source("You may draw 1 card.");
        Assert.True(BlockGame.Step(source).Optional);

        _game.Play(source);
        Assert.Equal([EffectChoiceRequest.Yes, EffectChoiceRequest.No], _game.Choice.Options);
        _game.No();
        Assert.Equal(3, _game.P1.Hand.Count);

        _game.Play(source);
        _game.Yes();
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Option.Optional")]
    public void UpTo_DoesNotAskYesOrNo_BecauseChoosingNothingIsPossible()
    {
        var source = Source("You may K.O. up to 1 of your opponent's Characters.");
        var target = _game.OnField(_game.P2);

        _game.Play(source);

        Assert.Equal([EffectChoiceRequest.Select, EffectChoiceRequest.Done], _game.Choice.Options);
        _game.Done();
        Assert.Contains(target, _game.P2.Characters);
    }

    [Fact]
    [BlockTest("Step.OpponentTrashesFromHand")]
    public void OpponentTrashesFromHand_TheOpponentChooses()
    {
        var source = Source("Your opponent trashes 1 card from their hand.");
        Assert.Equal(StepKind.OpponentTrashesFromHand, BlockGame.Step(source).Kind);

        _game.Play(source);
        Assert.Equal("p2", _game.Choice.PlayerId);
        _game.Select(_game.P2.Hand[2]);

        Assert.Equal(2, _game.P2.Hand.Count);
        Assert.Single(_game.P2.TrashCards);
    }

    [Fact]
    [BlockTest("Step.HandToDeckBottom")]
    public void HandToDeckBottom_AfterDrawing()
    {
        var source = Source("Draw 2 cards and place 2 cards from your hand at the bottom of your deck in any order.", 2);
        Assert.Equal(StepKind.HandToDeckBottom, BlockGame.Step(source, stepIndex: 1).Kind);
        var first = _game.P1.Hand[0];
        var second = _game.P1.Hand[1];

        _game.Play(source);
        _game.Select(first);
        _game.Select(second);

        Assert.Equal(3, _game.P1.Hand.Count);
        Assert.Equal([first, second], _game.P1.DrawDeck.TakeLast(2));
    }

    [Fact]
    [BlockTest("Step.OpponentHandToDeckBottom")]
    public void OpponentHandToDeckBottom_TheOpponentChooses()
    {
        var source = Source("Your opponent places 1 card from their hand at the bottom of their deck.");
        Assert.Equal(StepKind.OpponentHandToDeckBottom, BlockGame.Step(source).Kind);
        var chosen = _game.P2.Hand[0];

        _game.Play(source);
        Assert.Equal("p2", _game.Choice.PlayerId);
        _game.Select(chosen);

        Assert.Equal(chosen, _game.P2.DrawDeck[^1]);
        Assert.Equal(2, _game.P2.Hand.Count);
    }

    // ---------- Trash ----------

    [Fact]
    [BlockTest("Step.TrashToHand")]
    [BlockTest("Option.Zone.Trash")]
    public void TrashToHand_OnlyOffersMatchingTrashCards()
    {
        var source = Source("Add up to 1 {Navy} type card from your trash to your hand.");
        Assert.Equal(StepKind.TrashToHand, BlockGame.Step(source).Kind);
        Assert.Equal(CardZone.Trash, BlockGame.Step(source).Zone);
        var navy = _game.InTrash(_game.P1, types: "Navy");
        _game.InTrash(_game.P1, types: "Straw Hat Crew");

        _game.Play(source);
        Assert.Equal(navy, Assert.Single(_game.Offered));
        _game.Select(navy);

        Assert.Contains(navy, _game.P1.Hand);
        Assert.Single(_game.P1.TrashCards);
    }

    // ---------- Deck ----------

    [Fact]
    [BlockTest("Step.TrashTopOfDeck")]
    public void TrashTopOfDeck()
    {
        var source = Source("Trash 2 cards from the top of your deck.");
        Assert.Equal(StepKind.TrashTopOfDeck, BlockGame.Step(source).Kind);
        var top = NamedDeckTop();

        _game.Play(source);

        Assert.Equal([top[0], top[1]], _game.P1.TrashCards);
    }

    [Fact]
    [BlockTest("Step.ShuffleDeck")]
    public void ShuffleDeck_KeepsTheSameCards()
    {
        var source = Source("Shuffle your deck.");
        Assert.Equal(StepKind.ShuffleDeck, BlockGame.Step(source).Kind);
        NamedDeckTop();
        var before = _game.P1.DrawDeck.ToList();

        _game.Play(source);

        Assert.Equal(before.OrderBy(card => card.InstanceId), _game.P1.DrawDeck.OrderBy(card => card.InstanceId));
        Assert.NotEqual(before, _game.P1.DrawDeck);
    }

    [Fact]
    [BlockTest("Step.LookAndReveal")]
    [BlockTest("Option.RestDestination.bottom")]
    public void LookAndReveal_AddsTheRevealedCard_AndTheRestGoesToTheBottom()
    {
        var source = Source("Look at 5 cards from the top of your deck; reveal up to 1 {Navy} type card and add it to your hand. Then, place the rest at the bottom of your deck in any order.");
        var step = BlockGame.Step(source);
        Assert.Equal(StepKind.LookAndReveal, step.Kind);
        Assert.Equal("bottom", step.RestDestination);
        var top = NamedDeckTop("", "Navy");

        _game.Play(source);
        Assert.Equal(top[1], Assert.Single(_game.Offered));
        _game.Select(top[1]);

        Assert.Contains(top[1], _game.P1.Hand);
        Assert.Equal([top[0], top[2], top[3], top[4]], _game.P1.DrawDeck.TakeLast(4));
    }

    [Fact]
    [BlockTest("Step.LookAndReveal")]
    public void LookAndReveal_WithoutAMatchingCard_AsksNothing()
    {
        var source = Source("Look at 5 cards from the top of your deck; reveal up to 1 {Navy} type card and add it to your hand. Then, place the rest at the bottom of your deck in any order.");
        NamedDeckTop();

        Assert.Null(_game.Play(source));
        Assert.Equal(3, _game.P1.Hand.Count);
        Assert.Contains(_game.Logs, line => line.Contains("did not reveal a card"));
    }

    [Fact]
    [BlockTest("Option.RestDestination.trash")]
    public void LookAndReveal_TrashTheRest()
    {
        var source = Source("Look at 3 cards from the top of your deck; reveal up to 1 {Navy} type card and add it to your hand. Then, trash the rest.");
        Assert.Equal("trash", BlockGame.Step(source).RestDestination);
        var top = NamedDeckTop("Navy");

        _game.Play(source);
        _game.Select(top[0]);

        Assert.Equal([top[1], top[2]], _game.P1.TrashCards);
    }

    [Fact]
    [BlockTest("Option.RestDestination.top")]
    public void LookAndReveal_RestOnTop_EngineOnly()
    {
        // No card text produces "top" yet ("top or bottom" becomes "bottom", see the block catalog),
        // so this tests the engine with a hand-made step.
        var source = _game.OnField(_game.P1, name: "Source");
        var top = NamedDeckTop("Navy");
        var ability = new Ability { Timing = EffectTiming.OnPlay };
        ability.Steps.Add(new EffectStep
        {
            Kind = StepKind.LookAndReveal,
            Amount = 3,
            SecondAmount = 1,
            Filter = new CardFilter { TypeIncludes = "Navy" },
            RestDestination = "top",
            Text = "test"
        });

        var engine = _game.Scenario.Resolver.Engine;
        engine.Enqueue(ability, source, "p1", alreadyChosen: true);
        Assert.NotNull(engine.Run(_game.Scenario));
        engine.Answer(_game.Scenario, EffectChoiceRequest.Select, top[0].InstanceId);

        // "in any order": the engine does not ask for the order, both cards are on top.
        Assert.Equal(new[] { top[1], top[2] }.ToHashSet(), _game.P1.DrawDeck.Take(2).ToHashSet());
    }

    [Fact]
    [BlockTest("Option.PlayRevealed")]
    public void LookAndReveal_PlayIt_PlaysTheRevealedCharacter()
    {
        var source = Source("Look at 5 cards from the top of your deck; reveal up to 1 Character card with a cost of 2 or less and play it. Then, place the rest at the bottom of your deck in any order.");
        Assert.True(BlockGame.Step(source).PlayRevealed);
        var top = NamedDeckTop();

        _game.Play(source);
        _game.Select(top[2]);

        Assert.Contains(top[2], _game.P1.Characters);
        Assert.DoesNotContain(top[2], _game.P1.Hand);
    }

    [Fact]
    [BlockTest("Step.LookAndArrange")]
    public void LookAndArrange_ChosenCardsGoToTheBottom_TheOthersStayOnTop()
    {
        var source = Source("Look at 3 cards from the top of your deck and place them at the top or bottom of the deck in any order.");
        Assert.Equal(StepKind.LookAndArrange, BlockGame.Step(source).Kind);
        var top = NamedDeckTop();

        _game.Play(source);
        Assert.Equal(3, _game.Offered.Count);
        _game.Select(top[1]);
        _game.Done();

        Assert.Equal([top[0], top[2]], _game.P1.DrawDeck.Take(2));
        Assert.Equal(top[1], _game.P1.DrawDeck[^1]);
    }

    // ---------- Life ----------

    [Fact]
    [BlockTest("Step.LifeToHand")]
    public void LifeToHand_TakesTheTopLifeCard()
    {
        var source = Source("Add 1 card from the top of your Life cards to your hand.");
        Assert.Equal(StepKind.LifeToHand, BlockGame.Step(source).Kind);
        var top = _game.P1.LifeCards[^1];

        _game.Play(source);

        Assert.Contains(top, _game.P1.Hand);
        Assert.Equal(4, _game.P1.LifeCount);
    }

    [Fact]
    [BlockTest("Step.LifeToHand")]
    [BlockTest("Option.Side.Opponent")]
    public void LifeToHand_OfTheOpponent()
    {
        var source = Source("Add 1 card from the top of your opponent's Life cards to the owner's hand.");
        Assert.Equal(PlayerSide.Opponent, BlockGame.Step(source).Side);

        _game.Play(source);

        Assert.Equal(4, _game.P2.LifeCount);
        Assert.Equal(4, _game.P2.Hand.Count);
        Assert.Equal(5, _game.P1.LifeCount);
    }

    [Fact]
    [BlockTest("Step.TrashLife")]
    [BlockTest("Option.Side.Opponent")]
    public void TrashLife_OfTheOpponent()
    {
        var source = Source("Trash up to 1 card from the top of your opponent's Life cards.");
        Assert.Equal(StepKind.TrashLife, BlockGame.Step(source).Kind);
        var top = _game.P2.LifeCards[^1];

        _game.Play(source);

        Assert.Contains(top, _game.P2.TrashCards);
        Assert.Equal(4, _game.P2.LifeCount);
    }

    [Fact]
    [BlockTest("Step.TrashLife")]
    public void TrashLife_Own()
    {
        var source = Source("Trash 1 card from the top of your Life cards.");
        Assert.Equal(PlayerSide.You, BlockGame.Step(source).Side);

        _game.Play(source);

        Assert.Equal(4, _game.P1.LifeCount);
        Assert.Single(_game.P1.TrashCards);
    }

    [Fact]
    [BlockTest("Step.DeckTopToLife")]
    public void DeckTopToLife_PutsTheTopDeckCardOnTopOfTheLife()
    {
        var source = Source("Add up to 1 card from the top of your deck to the top of your Life cards.");
        Assert.Equal(StepKind.DeckTopToLife, BlockGame.Step(source).Kind);
        var top = NamedDeckTop();

        _game.Play(source);

        Assert.Equal(top[0], _game.P1.LifeCards[^1]);
        Assert.Equal(6, _game.P1.LifeCount);
    }

    [Fact]
    [BlockTest("Step.HandToLife")]
    public void HandToLife_ThePlayerChoosesTheCard()
    {
        var source = Source("Add up to 1 card from your hand to the top of your Life cards.");
        Assert.Equal(StepKind.HandToLife, BlockGame.Step(source).Kind);
        var chosen = _game.P1.Hand[1];

        _game.Play(source);
        Assert.Contains(EffectChoiceRequest.Done, _game.Choice.Options);
        _game.Select(chosen);

        Assert.Equal(chosen, _game.P1.LifeCards[^1]);
        Assert.Equal(2, _game.P1.Hand.Count);
    }

    // ---------- DON!! ----------

    [Fact]
    [BlockTest("Step.AddDonFromDeck")]
    [BlockTest("Option.Rested")]
    public void AddDonFromDeck_Rested()
    {
        var source = Source("Add up to 1 DON!! card from your DON!! deck and rest it.");
        Assert.Equal(StepKind.AddDonFromDeck, BlockGame.Step(source).Kind);
        Assert.True(BlockGame.Step(source).Rested);

        _game.Play(source);

        Assert.Equal(4, _game.P1.DonDeckCount);
        Assert.Equal(1, _game.P1.RestedDon);
        Assert.Equal(0, _game.P1.ActiveDon);
    }

    [Fact]
    [BlockTest("Step.AddDonFromDeck")]
    public void AddDonFromDeck_Active_OnlyAsManyAsAreLeft()
    {
        var source = Source("Add up to 2 DON!! cards from your DON!! deck and set them as active.");
        _game.P1.DonDeckCount = 1;

        _game.Play(source);

        Assert.Equal(0, _game.P1.DonDeckCount);
        Assert.Equal(1, _game.P1.ActiveDon);
    }

    [Fact]
    [BlockTest("Step.SetDonActive")]
    public void SetDonActive()
    {
        var source = Source("Set up to 2 of your DON!! cards as active.");
        Assert.Equal(StepKind.SetDonActive, BlockGame.Step(source).Kind);
        _game.P1.RestedDon = 3;

        _game.Play(source);

        Assert.Equal(2, _game.P1.ActiveDon);
        Assert.Equal(1, _game.P1.RestedDon);
    }

    [Fact]
    [BlockTest("Step.SetDonActiveAtEndOfTurn")]
    public void SetDonActiveAtEndOfTurn_IsRememberedForTheEndPhase()
    {
        var source = Source("Set up to 2 of your DON!! cards as active at the end of this turn.");
        Assert.Equal(StepKind.SetDonActiveAtEndOfTurn, BlockGame.Step(source).Kind);

        _game.Play(source);

        // The MatchRoom sets them active in the End Phase.
        Assert.Equal(2, _game.P1.DonToActivateAtEndOfTurn);
    }

    [Fact]
    [BlockTest("Step.ReturnDonToDeck")]
    public void ReturnDonToDeck_Own()
    {
        var source = Source("Return 1 DON!! card from your field to your DON!! deck.");
        Assert.Equal(StepKind.ReturnDonToDeck, BlockGame.Step(source).Kind);
        _game.P1.ActiveDon = 2;

        _game.Play(source);

        Assert.Equal(1, _game.P1.DonOnField);
    }

    [Fact]
    [BlockTest("Step.ReturnDonToDeck")]
    [BlockTest("Option.Side.Opponent")]
    public void ReturnDonToDeck_Opponent()
    {
        var source = Source("Your opponent returns 1 DON!! card from their field to their DON!! deck.");
        Assert.Equal(PlayerSide.Opponent, BlockGame.Step(source).Side);
        _game.P2.ActiveDon = 2;

        _game.Play(source);

        Assert.Equal(1, _game.P2.DonOnField);
    }

    [Fact]
    [BlockTest("Step.RestOpponentDon")]
    public void RestOpponentDon()
    {
        var source = Source("Rest up to 1 of your opponent's DON!! cards.");
        Assert.Equal(StepKind.RestOpponentDon, BlockGame.Step(source).Kind);
        _game.P2.ActiveDon = 2;

        _game.Play(source);

        Assert.Equal(1, _game.P2.ActiveDon);
        Assert.Equal(1, _game.P2.RestedDon);
    }

    // ---------- Playing cards ----------

    [Fact]
    [BlockTest("Step.PlayCard")]
    [BlockTest("Option.Zone.Hand")]
    public void PlayCard_FromTheHand_WithAFilter()
    {
        var source = Source("Play up to 1 {Navy} type Character card with a cost of 3 or less from your hand.");
        Assert.Equal(StepKind.PlayCard, BlockGame.Step(source).Kind);
        Assert.Equal(CardZone.Hand, BlockGame.Step(source).Zone);
        var navy = _game.InHand(_game.P1, "[On Play] Draw 1 card.", cost: 3, types: "Navy");
        _game.InHand(_game.P1, cost: 4, types: "Navy");

        _game.Play(source);
        Assert.Equal(navy, Assert.Single(_game.Offered));
        _game.Select(navy);

        Assert.Contains(navy, _game.P1.Characters);
        // The played card's [On Play] runs, too: 3 + 2 new cards - 1 played + 1 drawn.
        Assert.Equal(5, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Step.PlayCard")]
    [BlockTest("Option.Zone.Trash")]
    [BlockTest("Option.Rested")]
    public void PlayCard_FromTheTrash_Rested()
    {
        var source = Source("Play up to 1 Character card with a cost of 2 or less from your trash rested.");
        var step = BlockGame.Step(source);
        Assert.Equal(CardZone.Trash, step.Zone);
        Assert.True(step.Rested);
        var card = _game.InTrash(_game.P1, cost: 2);

        _game.Play(source);
        _game.Select(card);

        Assert.Contains(card, _game.P1.Characters);
        Assert.True(card.IsRested);
    }

    [Fact]
    [BlockTest("Option.Zone.HandOrTrash")]
    public void PlayCard_FromTheHandOrTrash_OffersBoth()
    {
        var source = Source("Play up to 1 {Navy} type Character card from your hand or trash.");
        Assert.Equal(CardZone.HandOrTrash, BlockGame.Step(source).Zone);
        var inHand = _game.InHand(_game.P1, types: "Navy");
        var inTrash = _game.InTrash(_game.P1, types: "Navy");

        _game.Play(source);

        Assert.Equal([inHand, inTrash], _game.Offered);
    }

    [Fact]
    [BlockTest("Step.PlayCard")]
    public void PlayCard_WithAFullCharacterArea_OffersNoCharacter()
    {
        var source = Source("Play up to 1 Character card from your hand.");
        for (var index = 0; index < 4; index += 1)
        {
            _game.OnField(_game.P1);
        }

        _game.InHand(_game.P1);

        Assert.Null(_game.Play(source));
    }

    [Fact]
    [BlockTest("Step.PlayThisCard")]
    public void PlayThisCard_FromATrigger()
    {
        var card = _game.InHand(_game.P1, "[On Play] Draw 1 card. [Trigger] Play this card.");
        Assert.Equal(StepKind.PlayThisCard, BlockGame.Step(card, abilityIndex: 1).Kind);

        _game.Activate(card, EffectTiming.Trigger);

        Assert.Contains(card, _game.P1.Characters);
        Assert.Equal(card.PlayedOnTurn, _game.Scenario.TurnNumber);
        // Played by an effect: its [On Play] runs (3 - 1 played + 1 drawn).
        Assert.Equal(4, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Step.ActivateOwnAbility")]
    public void ActivateOwnAbility_RunsTheMainEffectOfTheEvent()
    {
        var card = _game.InHand(_game.P1, "[Main] Draw 2 cards. [Trigger] Activate this card's [Main] effect.", category: CardInstance.EventCategory);
        Assert.Equal(StepKind.ActivateOwnAbility, BlockGame.Step(card, abilityIndex: 1).Kind);

        _game.Activate(card, EffectTiming.Trigger);

        Assert.Equal(6, _game.P1.Hand.Count);
    }

    // ---------- Rules texts ----------

    [Fact]
    [BlockTest("Step.NameAlias")]
    public void NameAlias_CountsForNameChecks()
    {
        var alias = _game.OnField(_game.P1, "Also treat this card's name as [Nami] according to the rules.", name: "Not Nami");
        Assert.Equal(StepKind.NameAlias, BlockGame.Step(alias).Kind);
        var checker = _game.OnField(_game.P1, "If you have [Nami], this Character gains +1000 power.");

        _game.Recalculate();

        Assert.True(EffectQueries.HasName(alias, "Nami"));
        Assert.Equal(3000, checker.GetPower(isOwnersTurn: true));
    }

    [Fact]
    [BlockTest("Step.NameAlias")]
    public void AnyNumberInTheDeck_IsADeckRule_AndDoesNothingInTheGame()
    {
        var card = _game.OnField(_game.P1, "Under the rules of this game, you may have any number of this card in your deck.");
        var step = BlockGame.Step(card);

        Assert.Equal(StepKind.NameAlias, step.Kind);
        Assert.Equal(string.Empty, step.Flag);
        Assert.Empty(card.StaticFlags);
    }
}
