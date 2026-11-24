using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests.Blocks;

// Blocks: "If ..." conditions. Each one is tested as "If <condition>, this Character gains +1000 power."
// on a 2000 power Character, so 3000 power means "the condition is met". Every test checks both sides.
public class ConditionBlockTests
{
    private readonly BlockGame _game = new();

    // Starting state: both players have 3 cards in hand, 5 Life cards, 10 cards in the deck,
    // an empty trash and no DON!!.
    private CardInstance Conditional(string condition, ConditionKind kind)
    {
        var card = _game.OnField(_game.P1, $"If {condition}, this Character gains +1000 power.");
        var parsed = Assert.Single(BlockGame.Ability(card).Conditions);
        Assert.Equal(kind, parsed.Kind);
        return card;
    }

    private bool IsMet(CardInstance card)
    {
        _game.Recalculate();
        return card.GetPower(isOwnersTurn: true) - card.AttachedDon * 1000 == 3000;
    }

    private void SetLeader(PlayerState owner, string name = "Leader", string types = "", string color = "Red")
    {
        _game.OnField(owner, category: CardInstance.LeaderCategory, name: name, types: types, color: color, power: 5000);
    }

    [Fact]
    [BlockTest("Condition.LeaderHasType")]
    public void LeaderHasType()
    {
        var card = Conditional("your Leader has the {Straw Hat Crew} type", ConditionKind.LeaderHasType);
        SetLeader(_game.P1, types: "Navy");
        Assert.False(IsMet(card));

        SetLeader(_game.P1, types: "Supernovas/Straw Hat Crew");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LeaderHasType")]
    public void LeaderTypeIncludes_IsTheSameCondition()
    {
        Conditional("your Leader's type includes {Whitebeard Pirates}", ConditionKind.LeaderHasType);
    }

    [Fact]
    [BlockTest("Condition.LeaderIsName")]
    public void LeaderIsName()
    {
        var card = Conditional("your Leader is [Monkey.D.Luffy]", ConditionKind.LeaderIsName);
        SetLeader(_game.P1, name: "Roronoa Zoro");
        Assert.False(IsMet(card));

        SetLeader(_game.P1, name: "Monkey.D.Luffy");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LeaderIsMulticolored")]
    public void LeaderIsMulticolored()
    {
        var card = Conditional("your Leader is multicolored", ConditionKind.LeaderIsMulticolored);
        SetLeader(_game.P1, color: "Red");
        Assert.False(IsMet(card));

        SetLeader(_game.P1, color: "Red/Green");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LeaderColor")]
    public void LeaderColor()
    {
        var card = Conditional("your Leader is green", ConditionKind.LeaderColor);
        SetLeader(_game.P1, color: "Red");
        Assert.False(IsMet(card));

        SetLeader(_game.P1, color: "Red/Green");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LifeAtMost")]
    public void LifeAtMost()
    {
        var card = Conditional("you have 3 or less Life cards", ConditionKind.LifeAtMost);
        Assert.False(IsMet(card));

        _game.P1.LifeCards.RemoveRange(0, 2);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LifeAtLeast")]
    public void LifeAtLeast()
    {
        var card = Conditional("you have 5 or more Life cards", ConditionKind.LifeAtLeast);
        Assert.True(IsMet(card));

        _game.P1.LifeCards.RemoveAt(0);
        Assert.False(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentLifeAtMost")]
    public void OpponentLifeAtMost()
    {
        var card = Conditional("your opponent has 3 or less Life cards", ConditionKind.OpponentLifeAtMost);
        Assert.False(IsMet(card));

        _game.P2.LifeCards.RemoveRange(0, 2);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentLifeAtLeast")]
    public void OpponentLifeAtLeast()
    {
        var card = Conditional("your opponent has 4 or more Life cards", ConditionKind.OpponentLifeAtLeast);
        Assert.True(IsMet(card));

        _game.P2.LifeCards.RemoveRange(0, 2);
        Assert.False(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.HandAtMost")]
    public void HandAtMost()
    {
        var card = Conditional("you have 2 or less cards in your hand", ConditionKind.HandAtMost);
        Assert.False(IsMet(card));

        _game.P1.Hand.RemoveAt(0);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.HandAtLeast")]
    public void HandAtLeast()
    {
        var card = Conditional("you have 4 or more cards in your hand", ConditionKind.HandAtLeast);
        Assert.False(IsMet(card));

        _game.InHand(_game.P1);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentHandAtLeast")]
    public void OpponentHandAtLeast()
    {
        var card = Conditional("your opponent has 4 or more cards in their hand", ConditionKind.OpponentHandAtLeast);
        Assert.False(IsMet(card));

        _game.InHand(_game.P2);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentHandAtMost")]
    public void OpponentHandAtMost()
    {
        var card = Conditional("your opponent has 2 or less cards in their hand", ConditionKind.OpponentHandAtMost);
        Assert.False(IsMet(card));

        _game.P2.Hand.RemoveAt(0);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.DonOnFieldAtLeast")]
    public void DonOnFieldAtLeast_CountsActiveRestedAndGivenDon()
    {
        var card = Conditional("you have 3 or more DON!! cards on your field", ConditionKind.DonOnFieldAtLeast);
        _game.P1.ActiveDon = 1;
        _game.P1.RestedDon = 1;
        Assert.False(IsMet(card));

        _game.P1.Leader!.AttachedDon = 1;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.DonOnFieldAtMost")]
    public void DonOnFieldAtMost()
    {
        var card = Conditional("there are 2 or less DON!! cards on your field", ConditionKind.DonOnFieldAtMost);
        _game.P1.ActiveDon = 3;
        Assert.False(IsMet(card));

        _game.P1.ActiveDon = 2;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentDonOnFieldAtLeast")]
    public void OpponentDonOnFieldAtLeast()
    {
        var card = Conditional("your opponent has 2 or more DON!! cards on their field", ConditionKind.OpponentDonOnFieldAtLeast);
        _game.P2.ActiveDon = 1;
        Assert.False(IsMet(card));

        _game.P2.RestedDon = 1;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentHasMoreDon")]
    public void OpponentHasMoreDon()
    {
        var card = Conditional("your opponent has more DON!! cards on their field than you", ConditionKind.OpponentHasMoreDon);
        _game.P1.ActiveDon = 2;
        _game.P2.ActiveDon = 2;
        Assert.False(IsMet(card));

        _game.P2.ActiveDon = 3;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.DonNotMoreThanOpponent")]
    public void DonNotMoreThanOpponent()
    {
        var card = Conditional("the number of DON!! cards on your field is equal to or less than the number on your opponent's field",
            ConditionKind.DonNotMoreThanOpponent);
        _game.P1.ActiveDon = 3;
        _game.P2.ActiveDon = 2;
        Assert.False(IsMet(card));

        _game.P2.ActiveDon = 3;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.LifeLessThanOpponent")]
    public void LifeLessThanOpponent()
    {
        var card = Conditional("you have less Life cards than your opponent", ConditionKind.LifeLessThanOpponent);
        Assert.False(IsMet(card));

        _game.P1.LifeCards.RemoveAt(0);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.TrashAtLeast")]
    public void TrashAtLeast()
    {
        var card = Conditional("you have 2 or more cards in your trash", ConditionKind.TrashAtLeast);
        _game.InTrash(_game.P1);
        Assert.False(IsMet(card));

        _game.InTrash(_game.P1);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.DeckAtMost")]
    public void DeckAtMost()
    {
        var card = Conditional("there are 9 or less cards in your deck", ConditionKind.DeckAtMost);
        Assert.False(IsMet(card));

        _game.P1.DrawDeck.RemoveAt(0);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.ThisIsRested")]
    public void ThisIsRested()
    {
        var card = Conditional("this Character is rested", ConditionKind.ThisIsRested);
        Assert.False(IsMet(card));

        card.IsRested = true;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.ThisIsActive")]
    public void ThisIsActive()
    {
        var card = Conditional("this Character is active", ConditionKind.ThisIsActive);
        Assert.True(IsMet(card));

        card.IsRested = true;
        Assert.False(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.GivenDonAtLeast")]
    public void GivenDonAtLeast()
    {
        var card = Conditional("this Character has 2 or more DON!! cards given", ConditionKind.GivenDonAtLeast);
        card.AttachedDon = 1;
        Assert.False(IsMet(card));

        card.AttachedDon = 2;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.CharactersAtLeast")]
    public void CharactersAtLeast_CountsMatchingCharacters()
    {
        var card = Conditional("you have 2 or more {Navy} type Characters", ConditionKind.CharactersAtLeast);
        _game.OnField(_game.P1, types: "Navy");
        _game.OnField(_game.P1, types: "Straw Hat Crew");
        _game.OnField(_game.P2, types: "Navy");
        Assert.False(IsMet(card));

        _game.OnField(_game.P1, types: "Navy");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.CharactersAtMost")]
    public void CharactersAtMost_CountsTheCardItself()
    {
        var card = Conditional("you have 2 or less Characters", ConditionKind.CharactersAtMost);
        Assert.True(IsMet(card));

        _game.OnField(_game.P1);
        Assert.True(IsMet(card));

        _game.OnField(_game.P1);
        Assert.False(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.HasCard")]
    public void HasCard_ByName()
    {
        var card = Conditional("you have [Nami]", ConditionKind.HasCard);
        _game.OnField(_game.P2, name: "Nami");
        Assert.False(IsMet(card));

        _game.OnField(_game.P1, name: "Nami");
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.HasCard")]
    public void ThereIs_LooksAtBothFields()
    {
        var card = Conditional("there is a Character with a cost of 8 or more", ConditionKind.HasCard);
        Assert.False(IsMet(card));

        _game.OnField(_game.P2, cost: 8);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.HasNoCard")]
    public void HasNoCard()
    {
        var card = Conditional("you don't have [Nami]", ConditionKind.HasNoCard);
        Assert.True(IsMet(card));

        _game.OnField(_game.P1, name: "Nami");
        Assert.False(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentHasCard")]
    public void OpponentHasCard()
    {
        var card = Conditional("your opponent has a Character with a cost of 5 or more", ConditionKind.OpponentHasCard);
        _game.OnField(_game.P1, cost: 5);
        _game.OnField(_game.P2, cost: 4);
        Assert.False(IsMet(card));

        _game.OnField(_game.P2, cost: 5);
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Condition.OpponentHasCard")]
    public void OpponentHasCard_WithANumber()
    {
        var card = Conditional("your opponent has 2 or more rested Characters", ConditionKind.OpponentHasCard);
        _game.OnField(_game.P2).IsRested = true;
        Assert.False(IsMet(card));

        _game.OnField(_game.P2).IsRested = true;
        Assert.True(IsMet(card));
    }

    [Fact]
    [BlockTest("Option.StepConditions")]
    public void ConditionInsideAnEffect_OnlySkipsThatStep()
    {
        var card = _game.OnField(_game.P1, "[On Play] Draw 1 card. Then, if you have 2 or less Life cards, draw 1 card.");
        var steps = BlockGame.Ability(card).Steps;
        Assert.Empty(BlockGame.Ability(card).Conditions);
        Assert.Equal(ConditionKind.LifeAtMost, Assert.Single(steps[^1].Conditions).Kind);

        _game.Play(card);
        Assert.Equal(4, _game.P1.Hand.Count);

        _game.P1.LifeCards.RemoveRange(0, 3);
        _game.Play(card);
        Assert.Equal(6, _game.P1.Hand.Count);
    }

    [Fact]
    [BlockTest("Option.StepConditions")]
    public void LeadingIf_OfAnAutoEffect_IsCheckedWhenTheStepResolves()
    {
        // The parser puts the "If" of a triggered ability on its step: it is checked on resolution.
        var card = _game.OnField(_game.P1, "[On Play] If you have 2 or less Life cards, draw 1 card.");
        Assert.Equal(ConditionKind.LifeAtMost, Assert.Single(BlockGame.Step(card).Conditions).Kind);

        _game.Play(card);
        Assert.Equal(3, _game.P1.Hand.Count);

        _game.P1.LifeCards.RemoveRange(0, 3);
        _game.Play(card);
        Assert.Equal(4, _game.P1.Hand.Count);
    }
}
