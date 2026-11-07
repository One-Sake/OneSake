using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests;

public class MatchRoomTests
{
    #region SETUP AND MULLIGAN

    [Fact]
    public void StartMatch_InitializesMulliganState()
    {
        var room = CreateRoomWithDecks();

        var update = room.StartMatch();

        Assert.Equal("ABCD", update.StateSnapshot.RoomCode);
        Assert.Equal(MatchPhase.Mulligan, update.StateSnapshot.Phase);
        Assert.Equal(1, update.StateSnapshot.TurnNumber);
        Assert.Equal("p1", update.StateSnapshot.ActivePlayerId);
        Assert.Equal(2, update.StateSnapshot.Players.Count);

        foreach (var player in update.StateSnapshot.Players)
        {
            Assert.Equal(45, player.DeckCount);
            Assert.Equal(5, player.HandCount);
            // Life cards are placed after the mulligan.
            Assert.Equal(0, player.LifeCount);
            Assert.Equal(10, player.DonDeckCount);
            Assert.Equal(0, player.BoardCount);
            Assert.NotNull(player.Leader);
            Assert.Empty(player.HandCards);
        }

        var p1PrivatePlayer = GetPlayer(update, "p1", "p1");
        Assert.Equal(5, p1PrivatePlayer.HandCards.Count);
        Assert.All(p1PrivatePlayer.HandCards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.InstanceId));
            Assert.False(string.IsNullOrWhiteSpace(card.CardId));
            Assert.False(string.IsNullOrWhiteSpace(card.Name));
            Assert.False(string.IsNullOrWhiteSpace(card.ImageUrl));
        });
        Assert.Equal(5, p1PrivatePlayer.HandCards.Select(card => card.InstanceId).Distinct().Count());
        Assert.Empty(GetPlayer(update, "p1", "p2").HandCards);
        Assert.Empty(GetPlayer(update, "p2", "p1").HandCards);

        Assert.Equal(2, update.ChoicePrompts.Count);
        Assert.All(update.ChoicePrompts, prompt => Assert.Equal("MULLIGAN_DECISION", prompt.Kind));
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "START_MATCH");
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "SHUFFLE_DECKS");
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "DRAW_OPENING_HAND");
    }

    [Fact]
    public void StartMatch_FailsWhenPlayersHaveNoDecks()
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");

        var exception = Assert.Throws<InvalidOperationException>(() => room.StartMatch());

        Assert.Equal("Both players must select a deck before starting.", exception.Message);
    }

    [Fact]
    public void SetPlayerDeck_StoresDeckDataInStateSnapshot()
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice");

        var update = room.SetPlayerDeck("p1", CreateDeck());
        var player = Assert.Single(update.StateSnapshot.Players);

        Assert.Equal("Test Deck", player.DeckName);
        Assert.Equal("OP01-001", player.LeaderCardId);
        Assert.Equal(50, player.MainDeckCount);
        Assert.True(player.HasDeck);
    }

    [Fact]
    public void MulliganTaken_RedrawsOpeningHandAndKeepsCountsCorrect()
    {
        var room = CreateRoomWithDecks();
        var startUpdate = room.StartMatch();

        var update = Submit(room, GetPrompt(startUpdate, "p1"), "MULLIGAN");
        var player = GetPlayer(update, "p1", "p1");

        Assert.Equal(MatchPhase.Mulligan, update.StateSnapshot.Phase);
        Assert.Equal(45, player.DeckCount);
        Assert.Equal(5, player.HandCount);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "MULLIGAN_TAKEN");
    }

    [Fact]
    public void Mulligan_ResolvesOnlyAfterBothPlayersDecide_ThenPlacesLifeAndStartsFirstTurn()
    {
        var room = CreateRoomWithDecks();
        var startUpdate = room.StartMatch();

        var firstDecision = Submit(room, GetPrompt(startUpdate, "p1"), "KEEP");
        Assert.Equal(MatchPhase.Mulligan, firstDecision.StateSnapshot.Phase);
        Assert.Empty(firstDecision.ChoicePrompts);

        var update = Submit(room, GetPrompt(startUpdate, "p2"), "KEEP");
        var p1 = GetPlayer(update, "p1", "p1");
        var p2 = GetPlayer(update, "p2", "p2");

        Assert.Equal(MatchPhase.Main, update.StateSnapshot.Phase);
        Assert.Equal("p1", update.StateSnapshot.ActivePlayerId);
        // Leader life is 5 -> 5 Life cards from the deck.
        Assert.Equal(5, p1.LifeCount);
        Assert.Equal(5, p2.LifeCount);
        // The first player does not draw on turn 1 and only gets 1 DON!!.
        Assert.Equal(5, p1.HandCount);
        Assert.Equal(40, p1.DeckCount);
        Assert.Equal(1, p1.ActiveDonCount);
        Assert.Equal(9, p1.DonDeckCount);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "SKIP_DRAW");

        var mainPrompt = Assert.Single(update.ChoicePrompts);
        Assert.Equal("p1", mainPrompt.PlayerId);
        Assert.Equal("MAIN_ACTION", mainPrompt.Kind);
        Assert.Contains("PLAY_CARD", mainPrompt.Options);
        Assert.Contains("ATTACH_DON", mainPrompt.Options);
        Assert.Contains("END_TURN", mainPrompt.Options);
        // Rule 6-5-6-1: no battle on your first turn.
        Assert.DoesNotContain("ATTACK", mainPrompt.Options);
    }

    [Fact]
    public void Mulligan_PlayerWithTooSmallDeckLosesByDeckOut()
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");
        room.SetPlayerDeck("p1", CreateDeck(quantity: 8));
        room.SetPlayerDeck("p2", CreateDeck());
        var startUpdate = room.StartMatch();

        Submit(room, GetPrompt(startUpdate, "p1"), "KEEP");
        var update = Submit(room, GetPrompt(startUpdate, "p2"), "KEEP");

        Assert.Equal(MatchPhase.GameOver, update.StateSnapshot.Phase);
        Assert.Equal("p2", update.StateSnapshot.WinnerPlayerId);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "DECK_OUT");
    }

    #endregion

    #region TURN FLOW

    [Fact]
    public void EndTurn_SecondPlayerDrawsAndGetsTwoDon()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();

        var update = Submit(room, prompt, "END_TURN");
        var p2 = GetPlayer(update, "p2", "p2");

        Assert.Equal(MatchPhase.Main, update.StateSnapshot.Phase);
        Assert.Equal(2, update.StateSnapshot.TurnNumber);
        Assert.Equal("p2", update.StateSnapshot.ActivePlayerId);
        Assert.Equal(6, p2.HandCount);
        Assert.Equal(39, p2.DeckCount);
        Assert.Equal(2, p2.ActiveDonCount);
        Assert.Equal("p2", Assert.Single(update.ChoicePrompts).PlayerId);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "TURN_END");
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "DRAW_CARD");
    }

    [Fact]
    public void RefreshPhase_ReturnsGivenDonAndSetsEverythingActive()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();
        var leaderId = GetPlayer(room.CreateStateSnapshot("p1"), "p1").Leader!.InstanceId;

        var update = Submit(room, prompt, "ATTACH_DON", leaderId);
        var p1 = GetPlayer(update, "p1", "p1");
        Assert.Equal(1, p1.Leader!.AttachedDon);
        Assert.Equal(6000, p1.Leader.Power);
        Assert.Equal(0, p1.ActiveDonCount);

        // p1 -> p2 -> p1 again
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");
        p1 = GetPlayer(update, "p1", "p1");

        Assert.Equal(0, p1.Leader!.AttachedDon);
        // 1 returned DON!! + 2 new DON!!
        Assert.Equal(3, p1.ActiveDonCount);
        Assert.Equal(0, p1.RestedDonCount);
    }

    #endregion

    #region PLAY CARD

    [Fact]
    public void PlayCard_PaysCostAndPutsCharacterOnTheBoard()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();
        var card = GetPlayer(room.CreateStateSnapshot("p1"), "p1").HandCards[0];

        var update = Submit(room, prompt, "PLAY_CARD", card.InstanceId);
        var p1 = GetPlayer(update, "p1", "p1");

        Assert.Equal(4, p1.HandCount);
        Assert.Equal(1, p1.BoardCount);
        Assert.Equal(card.InstanceId, p1.BoardCards[0].InstanceId);
        Assert.Equal(0, p1.ActiveDonCount);
        Assert.Equal(1, p1.RestedDonCount);
        // The opponent sees the Character on the board, but not the hand.
        Assert.Equal(card.InstanceId, GetPlayer(update, "p2", "p1").BoardCards[0].InstanceId);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "PLAY_CARD");

        // No DON!! left -> the next prompt cannot offer PLAY_CARD.
        var nextPrompt = GetPrompt(update, "p1");
        Assert.DoesNotContain("PLAY_CARD", nextPrompt.Options);
        Assert.Contains("END_TURN", nextPrompt.Options);
    }

    [Fact]
    public void PlayCard_IsNotOfferedWhenEveryCardIsTooExpensive()
    {
        var (_, prompt) = CreateStartedMainPhaseRoom(cost: 4);

        Assert.DoesNotContain("PLAY_CARD", prompt.Options);
        Assert.Contains("ATTACH_DON", prompt.Options);
    }

    [Fact]
    public void PlayCard_FailsWithoutSelectedCardInstanceId()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();

        var exception = Assert.Throws<InvalidOperationException>(() => Submit(room, prompt, "PLAY_CARD"));

        Assert.Equal("Playing a card requires selectedCardInstanceId.", exception.Message);
    }

    [Fact]
    public void PlayCard_FailsForNonActivePlayer()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            room.SubmitChoice(new ChoiceSubmissionDto
            {
                ChoiceId = prompt.ChoiceId,
                PlayerId = "p2",
                SelectedOption = "PLAY_CARD"
            }));

        Assert.Equal("Choice prompt does not belong to this player.", exception.Message);
    }

    #endregion

    #region BATTLE

    [Fact]
    public void Attack_IsRejectedOnSecondPlayersFirstTurn()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();

        var update = Submit(room, prompt, "END_TURN");

        Assert.DoesNotContain("ATTACK", GetPrompt(update, "p2").Options);
    }

    [Fact]
    public void Attack_LeaderHitsLeaderAndDefenderTakesLifeCardIntoHand()
    {
        var (room, update) = CreateRoomOnThirdTurn();
        var p1Leader = GetPlayer(update, "p1", "p1").Leader!;
        var p2Leader = GetPlayer(update, "p1", "p2").Leader!;
        var mainPrompt = GetPrompt(update, "p1");
        Assert.Contains("ATTACK", mainPrompt.Options);

        update = Submit(room, mainPrompt, "ATTACK", p1Leader.InstanceId, p2Leader.InstanceId);

        // p2 has counter cards in hand and gets the counter step.
        Assert.NotNull(update.StateSnapshot.Battle);
        var counterPrompt = GetPrompt(update, "p2");
        Assert.Equal("COUNTER_STEP", counterPrompt.Kind);

        update = Submit(room, counterPrompt, "PASS");
        var p2 = GetPlayer(update, "p2", "p2");

        Assert.Null(update.StateSnapshot.Battle);
        Assert.Equal(4, p2.LifeCount);
        Assert.Equal(7, p2.HandCount);
        Assert.True(GetPlayer(update, "p1", "p1").Leader!.IsRested);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "LIFE_LOST");

        var nextPrompt = GetPrompt(update, "p1");
        Assert.Equal("MAIN_ACTION", nextPrompt.Kind);
        Assert.DoesNotContain("ATTACK", nextPrompt.Options);
    }

    [Fact]
    public void Attack_CounterMakesTheAttackFail()
    {
        var (room, update) = CreateRoomOnThirdTurn();
        var p1Leader = GetPlayer(update, "p1", "p1").Leader!;
        var p2Leader = GetPlayer(update, "p1", "p2").Leader!;

        update = Submit(room, GetPrompt(update, "p1"), "ATTACK", p1Leader.InstanceId, p2Leader.InstanceId);
        var counterCard = GetPlayer(update, "p2", "p2").HandCards[0];

        update = Submit(room, GetPrompt(update, "p2"), "COUNTER", counterCard.InstanceId);
        Assert.Equal(6000, update.StateSnapshot.Battle!.TargetPower);

        update = Submit(room, GetPrompt(update, "p2"), "PASS");
        var p2 = GetPlayer(update, "p2", "p2");

        Assert.Equal(5, p2.LifeCount);
        Assert.Equal(5, p2.HandCount);
        Assert.Equal(1, p2.TrashCount);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "ATTACK_FAILED");
    }

    [Fact]
    public void Attack_LeaderWithZeroLifeLosesTheGame()
    {
        var (room, update) = CreateRoomOnThirdTurn(leaderLife: 1);

        // Turn 3: p1 hits p2 -> 0 Life.
        update = AttackLeaderAndPass(room, update, "p1", "p2");
        Assert.Equal(0, GetPlayer(update, "p2", "p2").LifeCount);

        // Turn 4 (p2) and turn 5 (p1): p1 hits again and wins.
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");
        update = AttackLeaderAndPass(room, update, "p1", "p2");

        Assert.Equal(MatchPhase.GameOver, update.StateSnapshot.Phase);
        Assert.Equal("p1", update.StateSnapshot.WinnerPlayerId);
        Assert.Empty(update.ChoicePrompts);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "GAME_OVER");
    }

    [Fact]
    public void Attack_IsRejectedOnTheFirstTurn()
    {
        var (room, prompt) = CreateStartedMainPhaseRoom();

        var exception = Assert.Throws<InvalidOperationException>(() => Submit(room, prompt, "ATTACK"));

        Assert.Equal("Selected option is invalid.", exception.Message);
    }

    #endregion

    #region HELPERS

    private static MatchRoom CreateRoomWithDecks(int cost = 1, int? leaderLife = 5)
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");
        room.SetPlayerDeck("p1", CreateDeck(cost: cost, leaderLife: leaderLife));
        room.SetPlayerDeck("p2", CreateDeck(cost: cost, leaderLife: leaderLife));
        return room;
    }

    private static (MatchRoom Room, ChoicePromptDto MainPrompt) CreateStartedMainPhaseRoom(
        int cost = 1,
        int? leaderLife = 5)
    {
        var room = CreateRoomWithDecks(cost, leaderLife);
        var startUpdate = room.StartMatch();
        Submit(room, GetPrompt(startUpdate, "p1"), "KEEP");
        var mainUpdate = Submit(room, GetPrompt(startUpdate, "p2"), "KEEP");
        return (room, GetPrompt(mainUpdate, "p1"));
    }

    // Returns the room at the start of p1's main phase in turn 3 (first turn where attacks are allowed).
    private static (MatchRoom Room, MatchUpdate Update) CreateRoomOnThirdTurn(int? leaderLife = 5)
    {
        var (room, prompt) = CreateStartedMainPhaseRoom(leaderLife: leaderLife);
        var update = Submit(room, prompt, "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");
        return (room, update);
    }

    private static MatchUpdate AttackLeaderAndPass(MatchRoom room, MatchUpdate update, string attackerId, string defenderId)
    {
        var attackerLeader = GetPlayer(update, attackerId, attackerId).Leader!;
        var defenderLeader = GetPlayer(update, attackerId, defenderId).Leader!;
        update = Submit(room, GetPrompt(update, attackerId), "ATTACK", attackerLeader.InstanceId, defenderLeader.InstanceId);

        var counterPrompt = update.ChoicePrompts.FirstOrDefault(prompt => prompt.Kind == "COUNTER_STEP");
        return counterPrompt is null ? update : Submit(room, counterPrompt, "PASS");
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
        return update.ChoicePrompts.Single(prompt => prompt.PlayerId == playerId);
    }

    private static PlayerStateDto GetPlayer(MatchUpdate update, string viewerId, string playerId)
    {
        return GetPlayer(update.StateSnapshots[viewerId], playerId);
    }

    private static PlayerStateDto GetPlayer(GameStateDto state, string playerId)
    {
        return state.Players.Single(player => player.PlayerId == playerId);
    }

    private static PlayerDeckSubmissionDto CreateDeck(int quantity = 50, int cost = 1, int? leaderLife = 5)
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = "deck-1",
            DeckName = "Test Deck",
            LeaderCardId = "OP01-001",
            LeaderName = "Test Leader",
            LeaderPower = 5000,
            LeaderLife = leaderLife,
            Cards =
            [
                new PlayerDeckCardDto
                {
                    CardId = "OP01-016",
                    Name = "Test Character",
                    Quantity = quantity,
                    Category = "Character",
                    Cost = cost,
                    Power = 3000,
                    Counter = 1000,
                    ImageUrl = "https://example.com/OP01-016.png"
                }
            ]
        };
    }

    #endregion
}
