using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests;

// Manual mode: players resolve card effects by hand. The rules (turn flow, winning) stay automatic.
public class ManualActionTests
{
    private const string UnsupportedOnPlay = "[On Play] Choose one: Draw 1 card or K.O. this Character.";

    #region CARDS AND ZONES

    [Fact]
    public void Draw_AddsCardsToTheHand_AndIsLogged()
    {
        var (room, _) = StartMainPhase();

        var update = Manual(room, "p1", "DRAW", amount: 2);

        var me = Me(update, "p1");
        Assert.Equal(7, me.HandCount);
        Assert.Equal(38, me.DeckCount);
        var log = Assert.Single(update.LogEvents, entry => entry.Type == "MANUAL");
        Assert.Equal("✋ Alice (manuell): zieht 2 Karten", log.Text);
    }

    [Fact]
    public void MoveCard_HandToCharacterArea_PaysNoCost()
    {
        var (room, update) = StartMainPhase(cost: 5);
        var card = Me(update, "p1").HandCards[0];

        update = Manual(room, "p1", "MOVE_CARD", card.InstanceId, toZone: "character");

        var me = Me(update, "p1");
        Assert.Equal(card.InstanceId, Assert.Single(me.BoardCards).InstanceId);
        Assert.Equal(4, me.HandCount);
        Assert.Equal(1, me.ActiveDonCount);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): Test Character: Hand → Character-Bereich");
    }

    [Fact]
    public void MoveCard_SixthCharacter_IsRejected()
    {
        var (room, update) = StartMainPhase();
        update = Manual(room, "p1", "DRAW", amount: 1);

        foreach (var card in Me(update, "p1").HandCards.Take(5).ToList())
        {
            update = Manual(room, "p1", "MOVE_CARD", card.InstanceId, toZone: "character");
        }

        var sixth = Me(update, "p1").HandCards[0];
        var error = Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "MOVE_CARD", sixth.InstanceId, toZone: "character"));
        Assert.Contains("full", error.Message);
        Assert.Equal(5, Me(room, "p1").BoardCards.Count);
    }

    [Fact]
    public void MoveCard_SecondStage_IsRejected()
    {
        var (room, update) = StartMainPhase(category: "Stage");
        var hand = Me(update, "p1").HandCards;

        update = Manual(room, "p1", "MOVE_CARD", hand[0].InstanceId, toZone: "stage");
        Assert.Equal(hand[0].InstanceId, Me(update, "p1").Stage!.InstanceId);

        Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "MOVE_CARD", hand[1].InstanceId, toZone: "stage"));
    }

    [Fact]
    public void MoveCard_FromTheField_ReturnsGivenDonRested()
    {
        var (room, update) = StartMainPhase();
        var card = Me(update, "p1").HandCards[0];
        Manual(room, "p1", "MOVE_CARD", card.InstanceId, toZone: "character");
        update = Manual(room, "p1", "DON_ATTACH", card.InstanceId);
        Assert.Equal(1, Me(update, "p1").BoardCards[0].AttachedDon);
        Assert.Equal(0, Me(update, "p1").ActiveDonCount);

        update = Manual(room, "p1", "MOVE_CARD", card.InstanceId, toZone: "trash");

        var me = Me(update, "p1");
        Assert.Empty(me.BoardCards);
        Assert.Equal(card.InstanceId, Assert.Single(me.TrashCards).InstanceId);
        Assert.Equal(1, me.RestedDonCount);
        Assert.Equal(0, me.TrashCards[0].AttachedDon);
    }

    [Fact]
    public void MoveCard_HandToDeckBottom_DoesNotNameTheCard()
    {
        var (room, update) = StartMainPhase();
        var card = Me(update, "p1").HandCards[0];

        update = Manual(room, "p1", "MOVE_CARD", card.InstanceId, toZone: "deck", toPosition: "bottom");

        Assert.Equal(41, Me(update, "p1").DeckCount);
        var log = Assert.Single(update.LogEvents, entry => entry.Type == "MANUAL");
        Assert.Equal("✋ Alice (manuell): legt 1 Karte (Hand) unter das Deck", log.Text);
        Assert.DoesNotContain("Test Character", log.Text);
    }

    [Fact]
    public void KoCard_OpponentCharacter_GoesToTheTrash()
    {
        var (room, update, opponentCard) = StartWithOpponentCharacter();

        update = Manual(room, "p1", "KO_CARD", opponentCard);

        var opponent = Me(update, "p2");
        Assert.Empty(opponent.BoardCards);
        Assert.Contains(opponent.TrashCards, card => card.InstanceId == opponentCard);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): K.O. für Test Character (Bob)");
    }

    [Fact]
    public void ReturnToHand_And_ReturnToDeck_WorkOnOpponentCharacters()
    {
        var (room, update, opponentCard) = StartWithOpponentCharacter();
        var handBefore = Me(update, "p2").HandCount;

        update = Manual(room, "p1", "RETURN_TO_HAND", opponentCard);
        Assert.Empty(Me(update, "p2").BoardCards);
        Assert.Equal(handBefore + 1, Me(update, "p2").HandCount);

        // Back on the field, then to the bottom of the owner's deck.
        var (room2, update2, opponentCard2) = StartWithOpponentCharacter();
        var deckBefore = Me(update2, "p2").DeckCount;
        update2 = Manual(room2, "p1", "RETURN_TO_DECK", opponentCard2, toPosition: "bottom");
        Assert.Empty(Me(update2, "p2").BoardCards);
        Assert.Equal(deckBefore + 1, Me(update2, "p2").DeckCount);
    }

    [Fact]
    public void SetRest_And_SetActive_WorkOnTheOpponentLeader()
    {
        var (room, update) = StartMainPhase();
        var leader = Me(update, "p2").Leader!.InstanceId;

        update = Manual(room, "p1", "SET_REST", leader);
        Assert.True(Me(update, "p2").Leader!.IsRested);

        update = Manual(room, "p1", "SET_ACTIVE", leader);
        Assert.False(Me(update, "p2").Leader!.IsRested);
    }

    [Fact]
    public void ModifyPower_LastsUntilTheEndOfTheTurn()
    {
        var (room, update) = StartMainPhase();
        var leader = Me(update, "p1").Leader!.InstanceId;

        update = Manual(room, "p1", "MODIFY_POWER", leader, amount: 2000, duration: "turn");
        Assert.Equal(7000, Me(update, "p1").Leader!.Power);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): Test Leader +2000 Power (dieser Zug)");

        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        Assert.Equal(5000, Me(update, "p1").Leader!.Power);
    }

    [Fact]
    public void ModifyPower_ForTheBattle_NeedsABattle()
    {
        var (room, update) = StartMainPhase();

        Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "MODIFY_POWER", Me(update, "p1").Leader!.InstanceId, amount: 1000, duration: "battle"));
    }

    [Fact]
    public void RevealCard_ShowsTheCardToBothPlayers_AndTrashFromHandMovesIt()
    {
        var (room, update) = StartMainPhase();
        var card = Me(update, "p1").HandCards[0];

        update = Manual(room, "p1", "REVEAL_CARD", card.InstanceId);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): zeigt Test Character (OP01-016) aus der Hand");

        update = Manual(room, "p1", "TRASH_FROM_HAND", card.InstanceId);
        Assert.Equal(card.InstanceId, Assert.Single(Me(update, "p1").TrashCards).InstanceId);
        Assert.Equal(4, Me(update, "p1").HandCount);
    }

    [Fact]
    public void DonActions_MoveDonBetweenDeckAndCostArea()
    {
        var (room, _) = StartMainPhase();

        var update = Manual(room, "p1", "DON_ADD", amount: 2, toZone: "rested");
        var me = Me(update, "p1");
        Assert.Equal(7, me.DonDeckCount);
        Assert.Equal(1, me.ActiveDonCount);
        Assert.Equal(2, me.RestedDonCount);

        update = Manual(room, "p1", "DON_SET_ACTIVE", amount: 2);
        Assert.Equal(3, Me(update, "p1").ActiveDonCount);

        update = Manual(room, "p1", "DON_SET_REST", amount: 1);
        Assert.Equal(1, Me(update, "p1").RestedDonCount);

        update = Manual(room, "p1", "DON_RETURN", amount: 3);
        me = Me(update, "p1");
        Assert.Equal(10, me.DonDeckCount);
        Assert.Equal(0, me.ActiveDonCount + me.RestedDonCount);
    }

    #endregion

    #region DECK AND LIFE

    [Fact]
    public void LookTop_ShowsTheCardsOnlyToTheOwnPlayer()
    {
        var (room, _) = StartMainPhase(name: "Hidden Card");

        var update = Manual(room, "p1", "LOOK_TOP", amount: 3);

        var view = Assert.IsType<ManualViewDto>(Me(update, "p1").ManualView);
        Assert.Equal("LOOK", view.Kind);
        Assert.Equal(3, view.Cards.Count);
        Assert.Null(GetPlayer(update.StateSnapshots["p2"], "p1").ManualView);
        Assert.Null(GetPlayer(update.StateSnapshot, "p1").ManualView);
        Assert.All(update.LogEvents, entry => Assert.DoesNotContain("Hidden Card", entry.Text));
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): sieht sich die obersten 3 Karten des Decks an");

        // Distribute: one to the hand, one under the deck, one to the trash (public).
        update = Manual(room, "p1", "MOVE_CARD", view.Cards[0].InstanceId, toZone: "hand");
        update = Manual(room, "p1", "MOVE_CARD", view.Cards[1].InstanceId, toZone: "deck", toPosition: "bottom");
        Assert.All(update.LogEvents, entry => Assert.DoesNotContain("Hidden Card", entry.Text));
        update = Manual(room, "p1", "MOVE_CARD", view.Cards[2].InstanceId, toZone: "trash");
        Assert.Contains(update.LogEvents, entry => entry.Text.Contains("Hidden Card: Deck → Trash"));

        var me = Me(update, "p1");
        Assert.Equal(6, me.HandCount);
        Assert.Equal(38, me.DeckCount);
        Assert.Empty(me.ManualView!.Cards);

        update = Manual(room, "p1", "CLOSE_VIEW");
        Assert.Null(Me(update, "p1").ManualView);
    }

    [Fact]
    public void DeckCards_CanOnlyBeTakenWhileLookingAtThem()
    {
        var (room, update) = StartMainPhase();
        update = Manual(room, "p1", "LOOK_TOP", amount: 1);
        var looked = Me(update, "p1").ManualView!.Cards[0].InstanceId;
        Manual(room, "p1", "CLOSE_VIEW");

        Assert.Throws<InvalidOperationException>(() => Manual(room, "p1", "MOVE_CARD", looked, toZone: "hand"));
    }

    [Fact]
    public void SearchDeck_TakesOneCard_AndShufflesWhenClosed()
    {
        var (room, _) = StartMainPhase();

        var update = Manual(room, "p1", "SEARCH_DECK");
        var view = Me(update, "p1").ManualView!;
        Assert.Equal("SEARCH", view.Kind);
        Assert.Equal(40, view.Cards.Count);

        update = Manual(room, "p1", "MOVE_CARD", view.Cards[17].InstanceId, toZone: "hand");
        Assert.Equal(39, Me(update, "p1").ManualView!.Cards.Count);

        update = Manual(room, "p1", "CLOSE_VIEW");
        Assert.Null(Me(update, "p1").ManualView);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice (manuell): beendet die Suche und mischt das Deck");
    }

    [Fact]
    public void LifeToHand_DownToZero_DoesNotEndTheGame()
    {
        var (room, update) = StartMainPhase();

        for (var index = 0; index < 5; index += 1)
        {
            update = Manual(room, "p1", "LIFE_TO_HAND");
        }

        Assert.Equal(0, Me(update, "p1").LifeCount);
        Assert.Equal(10, Me(update, "p1").HandCount);
        Assert.Equal(MatchPhase.Main, update.StateSnapshot.Phase);
        Assert.Equal(string.Empty, update.StateSnapshot.WinnerPlayerId);
        Assert.Throws<InvalidOperationException>(() => Manual(room, "p1", "LIFE_TO_HAND"));
    }

    [Fact]
    public void Draw_TheLastCard_LosesByDeckOut()
    {
        var (room, update) = StartMainPhase();

        while (update.StateSnapshot.Phase != MatchPhase.GameOver)
        {
            update = Manual(room, "p1", "DRAW", amount: 10);
        }

        Assert.Equal(0, Me(update, "p1").DeckCount);
        Assert.Equal("p2", update.StateSnapshot.WinnerPlayerId);
        Assert.Contains(update.LogEvents, entry => entry.Type == "DECK_OUT");
        Assert.Throws<InvalidOperationException>(() => Manual(room, "p1", "DRAW"));
    }

    #endregion

    #region RIGHTS

    [Fact]
    public void ManualActions_AreRejected_WhenItIsNotYourTurn()
    {
        var (room, _) = StartMainPhase();

        var error = Assert.Throws<InvalidOperationException>(() => Manual(room, "p2", "DRAW"));
        Assert.Contains("your turn", error.Message);
    }

    [Fact]
    public void ManualActions_CannotUseTheOpponentsHand()
    {
        var (room, update) = StartMainPhase();
        var opponentHandCard = Me(update, "p2").HandCards[0].InstanceId;

        Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "MOVE_CARD", opponentHandCard, toZone: "trash"));
        Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "TRASH_FROM_HAND", opponentHandCard));
        Assert.Throws<InvalidOperationException>(
            () => Manual(room, "p1", "KO_CARD", opponentHandCard));
        Assert.Equal(5, Me(room, "p2").HandCount);
    }

    [Fact]
    public void Defender_CanUseManualActions_DuringTheCounterStep()
    {
        var (room, update) = StartMainPhase();
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");

        var attacker = Me(update, "p1").Leader!.InstanceId;
        var target = Me(update, "p2").Leader!.InstanceId;
        update = Submit(room, GetPrompt(update, "p1"), "ATTACK", attacker, target);
        Assert.Equal("COUNTER_STEP", GetPrompt(update, "p2").Kind);

        update = Manual(room, "p2", "MODIFY_POWER", target, amount: 2000, duration: "battle");

        Assert.Equal(7000, update.StateSnapshot.Battle!.TargetPower);
        // The open counter prompt is sent again so the client still has it.
        Assert.Equal("COUNTER_STEP", GetPrompt(update, "p2").Kind);
    }

    #endregion

    #region RESOLVE MANUALLY PROMPT

    [Fact]
    public void UnsupportedEffect_AsksThePlayerToResolveItManually()
    {
        var (room, update) = StartMainPhase(text: UnsupportedOnPlay);
        var card = Me(update, "p1").HandCards[0];

        update = Submit(room, GetPrompt(update, "p1"), "PLAY_CARD", card.InstanceId);

        var prompt = GetPrompt(update, "p1");
        Assert.Equal("MANUAL_EFFECT", prompt.Kind);
        Assert.Equal(["DONE"], prompt.Options);
        Assert.Equal("Effekt bitte manuell ausführen: " + UnsupportedOnPlay, prompt.Title);
        Assert.Equal(card.InstanceId, Assert.Single(prompt.Cards).InstanceId);

        // While the prompt is open the player resolves the effect by hand.
        update = Manual(room, "p1", "DRAW");
        Assert.Equal(5, Me(update, "p1").HandCount);
        Assert.DoesNotContain(update.ChoicePrompts, entry => entry.Kind == "MAIN_ACTION");

        update = Submit(room, prompt, "DONE");
        Assert.Equal("MAIN_ACTION", GetPrompt(update, "p1").Kind);
        Assert.Contains(update.LogEvents, entry => entry.Text == "✋ Alice: Effekt von Test Character erledigt.");
    }

    [Fact]
    public void UnsupportedTrigger_CanBeActivated_AndIsResolvedManually()
    {
        const string trigger = "[Trigger] Choose one: Draw 1 card or K.O. this Character.";
        var (room, update) = StartMainPhase(text: trigger);
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");

        var attacker = Me(update, "p1").Leader!.InstanceId;
        var target = Me(update, "p2").Leader!.InstanceId;
        update = Submit(room, GetPrompt(update, "p1"), "ATTACK", attacker, target);
        update = Submit(room, GetPrompt(update, "p2"), "PASS");

        var decision = GetPrompt(update, "p2");
        Assert.Equal("TRIGGER_DECISION", decision.Kind);
        Assert.Equal(trigger, decision.EffectText);

        update = Submit(room, decision, "ACTIVATE");
        var manual = GetPrompt(update, "p2");
        Assert.Equal("MANUAL_EFFECT", manual.Kind);

        update = Submit(room, manual, "DONE");
        Assert.Equal(1, Me(update, "p2").TrashCount);
        Assert.Equal(4, Me(update, "p2").LifeCount);
        Assert.Equal("MAIN_ACTION", GetPrompt(update, "p1").Kind);
    }

    [Fact]
    public void ManualAction_InTheMainPhase_SendsAFreshMainActionPrompt()
    {
        var (room, update) = StartMainPhase();
        var oldPrompt = GetPrompt(update, "p1");

        update = Manual(room, "p1", "DON_ADD", amount: 1);

        var newPrompt = GetPrompt(update, "p1");
        Assert.Equal("MAIN_ACTION", newPrompt.Kind);
        Assert.NotEqual(oldPrompt.ChoiceId, newPrompt.ChoiceId);
        Assert.Throws<InvalidOperationException>(() => Submit(room, oldPrompt, "END_TURN"));
    }

    #endregion

    #region HELPERS

    // p1 goes first and is in the main phase of turn 1 (5 hand cards, 5 Life, 1 active DON!!).
    private static (MatchRoom Room, MatchUpdate Update) StartMainPhase(
        int cost = 1,
        string category = "Character",
        string name = "Test Character",
        string text = "")
    {
        var room = new MatchRoom("ABCD", diceRoller: new FixedDiceRoller([6, 1]));
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");
        room.SetPlayerDeck("p1", CreateDeck(cost, category, name, text));
        room.SetPlayerDeck("p2", CreateDeck(cost, category, name, text));

        var turnOrder = Assert.Single(room.StartMatch().ChoicePrompts);
        var update = Submit(room, turnOrder, "GO_FIRST");
        Submit(room, GetPrompt(update, "p1"), "KEEP");
        update = Submit(room, GetPrompt(update, "p2"), "KEEP");
        return (room, update);
    }

    // p1's main phase in turn 3, p2 has one Character on the field.
    private static (MatchRoom Room, MatchUpdate Update, string OpponentCard) StartWithOpponentCharacter()
    {
        var (room, update) = StartMainPhase();
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");

        var card = Me(update, "p2").HandCards[0].InstanceId;
        update = Submit(room, GetPrompt(update, "p2"), "PLAY_CARD", card);
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");
        Assert.Single(Me(update, "p2").BoardCards);
        return (room, update, card);
    }

    private static MatchUpdate Manual(
        MatchRoom room,
        string playerId,
        string kind,
        string? cardInstanceId = null,
        int? amount = null,
        string? toZone = null,
        string? toPosition = null,
        string? duration = null)
    {
        return room.ManualAction(playerId, new ManualActionDto
        {
            Kind = kind,
            CardInstanceId = cardInstanceId,
            Amount = amount,
            ToZone = toZone,
            ToPosition = toPosition,
            Duration = duration
        });
    }

    private static MatchUpdate Submit(
        MatchRoom room,
        ChoicePromptDto prompt,
        string option,
        string? selectedCardInstanceId = null,
        string? targetCardInstanceId = null)
    {
        return room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = prompt.ChoiceId,
            PlayerId = prompt.PlayerId,
            SelectedOption = option,
            SelectedCardInstanceId = selectedCardInstanceId,
            TargetCardInstanceId = targetCardInstanceId
        });
    }

    private static ChoicePromptDto GetPrompt(MatchUpdate update, string playerId)
    {
        return update.ChoicePrompts.Last(prompt => prompt.PlayerId == playerId);
    }

    // The player as they see themselves (with hand cards and manual view).
    private static PlayerStateDto Me(MatchUpdate update, string playerId)
    {
        return GetPlayer(update.StateSnapshots[playerId], playerId);
    }

    private static PlayerStateDto Me(MatchRoom room, string playerId)
    {
        return GetPlayer(room.CreateStateSnapshot(playerId), playerId);
    }

    private static PlayerStateDto GetPlayer(GameStateDto state, string playerId)
    {
        return state.Players.Single(player => player.PlayerId == playerId);
    }

    private static PlayerDeckSubmissionDto CreateDeck(int cost, string category, string name, string text)
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = "deck-1",
            DeckName = "Test Deck",
            LeaderCardId = "OP01-001",
            LeaderName = "Test Leader",
            LeaderPower = 5000,
            LeaderLife = 5,
            Cards =
            [
                new PlayerDeckCardDto
                {
                    CardId = "OP01-016",
                    Name = name,
                    Quantity = 50,
                    Category = category,
                    Cost = cost,
                    Power = 3000,
                    Counter = 1000,
                    Text = text,
                    ImageUrl = "https://example.com/OP01-016.png"
                }
            ]
        };
    }

    #endregion
}
