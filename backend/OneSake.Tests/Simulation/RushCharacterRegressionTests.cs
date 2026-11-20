using OneSake.Domain;
using OneSake.Game.Simulation;
using OneSake.Server.Match;

namespace OneSake.Tests.Simulation;

// Finding F2 (docs/simulation-findings.md): a [Rush: Character] card showed "can attack" in the
// turn it was played, and the client offered the opponent's Leader as target. The server
// rejected that attack (95 of 95 rejected bot decisions in 1000 simulated matches).
public class RushCharacterRegressionTests
{
    [Fact]
    public void F2_RushCharacter_TellsTheClientThatOnlyCharactersAreTargets()
    {
        var room = new MatchRoom("RUSH", diceRoller: new FixedDiceRoller([6, 1]), seed: 3);
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");
        room.SetPlayerDeck("p1", Deck("[Rush: Character]"));
        room.SetPlayerDeck("p2", Deck(string.Empty));
        var update = Submit(room, Assert.Single(room.StartMatch().ChoicePrompts), "GO_FIRST");
        Submit(room, update.ChoicePrompts.Single(prompt => prompt.PlayerId == "p1"), "KEEP");
        update = Submit(room, update.ChoicePrompts.Single(prompt => prompt.PlayerId == "p2"), "KEEP");
        update = Submit(room, Prompt(update, "p1"), "END_TURN");
        update = Submit(room, Prompt(update, "p2"), "END_TURN");

        var hand = Me(update, "p1").HandCards[0];
        update = Submit(room, Prompt(update, "p1"), "PLAY_CARD", hand.InstanceId);
        var played = Assert.Single(Me(update, "p1").BoardCards);

        Assert.True(played.CanAttack);
        Assert.True(played.CanAttackOnlyCharacters);
        Assert.False(Me(update, "p1").Leader!.CanAttackOnlyCharacters);

        // The bots no longer offer the Leader as target for this card.
        var view = new BotView(update.StateSnapshots["p1"], Prompt(update, "p1"), "p1");
        Assert.DoesNotContain(ValidDecisions.For(view), decision =>
            decision.Choice?.SelectedOption == "ATTACK"
            && decision.Choice.SelectedCardInstanceId == played.InstanceId
            && decision.Choice.TargetCardInstanceId == Me(update, "p2").Leader!.InstanceId);

        // And the server still rejects it, as before.
        Assert.Throws<InvalidOperationException>(() => Submit(room, Prompt(update, "p1"), "ATTACK", played.InstanceId, Me(update, "p2").Leader!.InstanceId));
    }

    private static PlayerDeckSubmissionDto Deck(string text) => new()
    {
        DeckId = "rush",
        DeckName = "Rush",
        LeaderCardId = "RUSH-LEADER",
        LeaderName = "Leader",
        LeaderPower = 5000,
        LeaderLife = 5,
        Cards = [new PlayerDeckCardDto { CardId = "RUSH-001", Name = "Runner", Quantity = 50, Category = "Character", Cost = 1, Power = 3000, Text = text }]
    };

    private static MatchUpdate Submit(MatchRoom room, ChoicePromptDto prompt, string option, string? selected = null, string? target = null)
    {
        return room.SubmitChoice(new ChoiceSubmissionDto { ChoiceId = prompt.ChoiceId, PlayerId = prompt.PlayerId, SelectedOption = option, SelectedCardInstanceId = selected, TargetCardInstanceId = target });
    }

    private static ChoicePromptDto Prompt(MatchUpdate update, string playerId) => update.ChoicePrompts.Last(prompt => prompt.PlayerId == playerId);

    private static PlayerStateDto Me(MatchUpdate update, string playerId) => update.StateSnapshots[playerId].Players.Single(player => player.PlayerId == playerId);
}
