using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests.Rules;

// Rules in a full MatchRoom (prompts, battle flow).
public class MatchRuleTests
{
    private const string DrawTrigger = "[Trigger] Draw 1 card.";
    private const string CounterEvent = "[Counter] Up to 1 of your Leader or Character cards gains +3000 power during this battle.";

    [Fact]
    public void LifeTrigger_ThePlayerChoosesToActivateIt()
    {
        // Rule 7-1-4-1-1-2 / 10-1-5: a Life card with [Trigger] can be activated instead of added to the hand.
        var (room, update) = StartOnTurnThree(p2Card: Card("Trigger Card", DrawTrigger));
        var handBefore = Me(update, "p2").HandCount;

        update = Attack(room, update);
        var decision = GetPrompt(update, "p2");
        Assert.Equal("TRIGGER_DECISION", decision.Kind);

        update = Submit(room, decision, "ACTIVATE");

        // The [Trigger] draws 1; the Life card is trashed afterwards (rule 10-1-5-3).
        Assert.Equal(handBefore + 1, Me(update, "p2").HandCount);
        Assert.Equal(4, Me(update, "p2").LifeCount);
        Assert.Equal(1, Me(update, "p2").TrashCount);
        Assert.Equal("MAIN_ACTION", GetPrompt(update, "p1").Kind);
    }

    [Fact]
    public void LifeTrigger_CanBeDeclined()
    {
        // Rule 10-1-5-2: not activated -> the card is added to the hand.
        var (room, update) = StartOnTurnThree(p2Card: Card("Trigger Card", DrawTrigger));
        var handBefore = Me(update, "p2").HandCount;

        update = Attack(room, update);
        update = Submit(room, GetPrompt(update, "p2"), "DECLINE");

        Assert.Equal(handBefore + 1, Me(update, "p2").HandCount);
        Assert.Equal(0, Me(update, "p2").TrashCount);
    }

    [Fact]
    public void CounterEvent_GivesPowerUntilTheEndOfTheBattle()
    {
        // Rule 7-1-3-1-2 / 10-2-4: [Counter] Event in the Counter Step; rule 7-1-5-4: the power ends with the battle.
        var (room, update) = StartOnTurnThree(p2Card: Card("Guard", CounterEvent, category: "Event", cost: 0, counter: 0));
        var leader = Me(update, "p2").Leader!.InstanceId;

        update = Attack(room, update);
        var counter = GetPrompt(update, "p2");
        Assert.Equal("COUNTER_STEP", counter.Kind);

        update = Submit(room, counter, "COUNTER", Me(update, "p2").HandCards[0].InstanceId);
        update = Submit(room, GetPrompt(update, "p2"), "SELECT", leader);
        Assert.Equal(8000, update.StateSnapshot.Battle!.TargetPower);

        update = Submit(room, GetPrompt(update, "p2"), "PASS");

        Assert.Contains(update.LogEvents, entry => entry.Type == "ATTACK_FAILED");
        Assert.Null(update.StateSnapshot.Battle);
        Assert.Equal(5000, Me(update, "p2").Leader!.Power);
        Assert.Equal(5, Me(update, "p2").LifeCount);
    }

    [Fact]
    public void TwoEndOfTurnEffects_OpenAnOrderPrompt()
    {
        // Rule 6-6-1-1-3: the turn player chooses the order of several [End of Your Turn] effects.
        var (room, update) = Start(p1Card: Card("Ender", "[End of Your Turn] Draw 1 card."));
        foreach (var card in Me(update, "p1").HandCards.Take(2).ToList())
        {
            update = room.ManualAction("p1", new ManualActionDto { Kind = "MOVE_CARD", CardInstanceId = card.InstanceId, ToZone = "character" });
        }

        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        var order = GetPrompt(update, "p1");
        Assert.Equal("ORDER_TRIGGERS", order.Kind);
        Assert.Equal(2, order.Triggers.Count);
        Assert.All(order.Triggers, trigger => Assert.Contains("[End of Your Turn]", trigger.Text));

        // A wrong answer keeps the prompt open.
        Assert.Throws<InvalidOperationException>(() => room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = order.ChoiceId,
            PlayerId = "p1",
            SelectedOption = "ORDER",
            OrderedIds = [order.Triggers[0].Id]
        }));

        update = room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = order.ChoiceId,
            PlayerId = "p1",
            SelectedOption = "ORDER",
            OrderedIds = order.Triggers.Select(trigger => trigger.Id).Reverse().ToList()
        });

        Assert.Contains(update.LogEvents, entry => entry.Type == "EFFECT_ORDER" && entry.Text.StartsWith("1/2: "));
        Assert.Contains(update.LogEvents, entry => entry.Type == "EFFECT_ORDER" && entry.Text.StartsWith("2/2: "));
        Assert.Equal(5, Me(update, "p1").HandCount);
        Assert.Equal("MAIN_ACTION", GetPrompt(update, "p2").Kind);
    }

    [Fact]
    public void EffectsCausedDuringDamage_AreActivatedAfterTheDamageProcessing()
    {
        // Rule 8-6-2: p1 attacks with [Double Attack]. The first Life card's [Trigger] K.O.s the
        // attacker; its [On K.O.] waits until the second damage is processed.
        var (room, update) = Start(
            p1Card: Card("Double", "[Double Attack] [On K.O.] Draw 1 card.", cost: 1),
            p2Card: Card("KO Trigger", "[Trigger] K.O. up to 1 of your opponent's Characters with a cost of 3 or less."));
        var attacker = Me(update, "p1").HandCards[0].InstanceId;
        update = room.ManualAction("p1", new ManualActionDto { Kind = "MOVE_CARD", CardInstanceId = attacker, ToZone = "character" });
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");

        // 3000 + 2 DON!! = 5000 power against the 5000 Leader.
        update = Submit(room, GetPrompt(update, "p1"), "ATTACH_DON", attacker);
        update = Submit(room, GetPrompt(update, "p1"), "ATTACH_DON", attacker);
        update = Submit(room, GetPrompt(update, "p1"), "ATTACK", attacker, Me(update, "p2").Leader!.InstanceId);
        var log = new List<LogEventDto>(update.LogEvents);
        update = Submit(room, GetPrompt(update, "p2"), "ACTIVATE");
        log.AddRange(update.LogEvents);
        update = Submit(room, GetPrompt(update, "p2"), "SELECT", attacker);
        log.AddRange(update.LogEvents);
        // The second damage: decline the second [Trigger].
        update = Submit(room, GetPrompt(update, "p2"), "DECLINE");
        log.AddRange(update.LogEvents);

        var secondDamage = log.FindIndex(entry => entry.Type == "LIFE_LOST");
        var onKoDraw = log.FindIndex(entry => entry.Text.Contains("Double: Alice draws"));
        Assert.True(secondDamage >= 0 && onKoDraw > secondDamage, string.Join(" | ", log.Select(entry => entry.Text)));
        Assert.Equal(3, Me(update, "p2").LifeCount);
    }

    #region HELPERS

    private static PlayerDeckCardDto Card(string name, string text = "", string category = "Character", int cost = 1, int counter = 0)
    {
        return new PlayerDeckCardDto
        {
            CardId = $"RULE-{name.Replace(' ', '-')}",
            Name = name,
            Quantity = 50,
            Category = category,
            Cost = cost,
            Power = 3000,
            Counter = counter,
            Text = text,
            ImageUrl = "https://example.com/card.png"
        };
    }

    // p1 goes first and is in the main phase of turn 1.
    private static (MatchRoom Room, MatchUpdate Update) Start(PlayerDeckCardDto? p1Card = null, PlayerDeckCardDto? p2Card = null)
    {
        var room = new MatchRoom("RULE", diceRoller: new FixedDiceRoller([6, 1]));
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");
        room.SetPlayerDeck("p1", Deck(p1Card ?? Card("Plain")));
        room.SetPlayerDeck("p2", Deck(p2Card ?? Card("Plain")));

        var update = Submit(room, Assert.Single(room.StartMatch().ChoicePrompts), "GO_FIRST");
        Submit(room, GetPrompt(update, "p1"), "KEEP");
        update = Submit(room, GetPrompt(update, "p2"), "KEEP");
        return (room, update);
    }

    // p1's main phase in turn 3 (attacks are allowed).
    private static (MatchRoom Room, MatchUpdate Update) StartOnTurnThree(PlayerDeckCardDto? p1Card = null, PlayerDeckCardDto? p2Card = null)
    {
        var (room, update) = Start(p1Card, p2Card);
        update = Submit(room, GetPrompt(update, "p1"), "END_TURN");
        update = Submit(room, GetPrompt(update, "p2"), "END_TURN");
        return (room, update);
    }

    private static MatchUpdate Attack(MatchRoom room, MatchUpdate update)
    {
        return Submit(room, GetPrompt(update, "p1"), "ATTACK", Me(update, "p1").Leader!.InstanceId, Me(update, "p2").Leader!.InstanceId);
    }

    private static PlayerDeckSubmissionDto Deck(PlayerDeckCardDto card)
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = "rules",
            DeckName = "Rules",
            LeaderCardId = "RULE-LEADER",
            LeaderName = "Leader",
            LeaderPower = 5000,
            LeaderLife = 5,
            Cards = [card]
        };
    }

    private static MatchUpdate Submit(MatchRoom room, ChoicePromptDto prompt, string option, string? selected = null, string? target = null)
    {
        return room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = prompt.ChoiceId,
            PlayerId = prompt.PlayerId,
            SelectedOption = option,
            SelectedCardInstanceId = selected,
            TargetCardInstanceId = target
        });
    }

    private static ChoicePromptDto GetPrompt(MatchUpdate update, string playerId)
    {
        return update.ChoicePrompts.Last(prompt => prompt.PlayerId == playerId);
    }

    private static PlayerStateDto Me(MatchUpdate update, string playerId)
    {
        return update.StateSnapshots[playerId].Players.Single(player => player.PlayerId == playerId);
    }

    #endregion
}
