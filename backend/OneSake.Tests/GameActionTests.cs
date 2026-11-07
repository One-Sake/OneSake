using OneSake.Domain;
using OneSake.Game.Actions;
using OneSake.Game.State;

namespace OneSake.Tests;

// Tests for the single rule actions in OneSake.Game without a full match room.
public class GameActionTests
{
    private const int Turn = 3;

    [Fact]
    public void PlayCard_FailsWhenThereIsNotEnoughDon()
    {
        var player = CreatePlayer("p1", activeDon: 2);
        var card = AddToHand(player, Character("expensive", cost: 5));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PlayCardAction().Execute(player, MatchPhase.Main, "p1", card.InstanceId, null, Turn));

        Assert.StartsWith("Not enough DON!!", exception.Message);
        Assert.Contains(card, player.Hand);
    }

    [Fact]
    public void PlayCard_WithFullCharacterArea_RequiresAndTrashesReplacedCharacter()
    {
        var player = CreatePlayer("p1", activeDon: 5);
        for (var index = 0; index < 5; index += 1)
        {
            player.Characters.Add(Character($"old-{index}"));
        }

        player.Characters[0].AttachedDon = 2;
        var card = AddToHand(player, Character("new", cost: 1));

        Assert.Throws<InvalidOperationException>(() =>
            new PlayCardAction().Execute(player, MatchPhase.Main, "p1", card.InstanceId, null, Turn));

        new PlayCardAction().Execute(player, MatchPhase.Main, "p1", card.InstanceId, "old-0", Turn);

        Assert.Equal(5, player.Characters.Count);
        Assert.Contains(card, player.Characters);
        Assert.Contains(player.TrashCards, trashed => trashed.InstanceId == "old-0");
        // 1 DON!! paid + 2 DON!! returned from the trashed Character.
        Assert.Equal(3, player.RestedDon);
    }

    [Fact]
    public void PlayCard_EventGoesToTrash()
    {
        var player = CreatePlayer("p1", activeDon: 2);
        var card = AddToHand(player, new CardInstance
        {
            InstanceId = "event",
            Name = "Event",
            Category = CardInstance.EventCategory,
            Cost = 1
        });

        new PlayCardAction().Execute(player, MatchPhase.Main, "p1", card.InstanceId, null, Turn);

        Assert.Contains(card, player.TrashCards);
        Assert.Empty(player.Characters);
    }

    [Fact]
    public void Attack_CharacterPlayedThisTurnNeedsRush()
    {
        var attacker = CreatePlayer("p1");
        var defender = CreatePlayer("p2");
        var fresh = Character("fresh");
        fresh.PlayedOnTurn = Turn;
        var rusher = Character("rusher", text: "[Rush] (This card can attack on the turn in which it is played.)");
        rusher.PlayedOnTurn = Turn;
        attacker.Characters.AddRange([fresh, rusher]);

        Assert.False(AttackAction.CanAttack(fresh, attacker, Turn));
        Assert.True(AttackAction.CanAttack(rusher, attacker, Turn));
        Assert.Throws<InvalidOperationException>(() =>
            new AttackAction().Execute(attacker, defender, MatchPhase.Main, "p1", "fresh", defender.Leader!.InstanceId, Turn));
    }

    [Fact]
    public void Attack_ActiveCharacterCannotBeTargeted()
    {
        var attacker = CreatePlayer("p1");
        var defender = CreatePlayer("p2");
        defender.Characters.Add(Character("target"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttackAction().Execute(attacker, defender, MatchPhase.Main, "p1", attacker.Leader!.InstanceId, "target", Turn));

        Assert.Equal("You can only attack rested Characters.", exception.Message);
    }

    [Fact]
    public void Battle_KnocksOutRestedCharacterWhenPowerIsHighEnough()
    {
        var attacker = CreatePlayer("p1");
        var defender = CreatePlayer("p2");
        var target = Character("target", power: 5000);
        target.IsRested = true;
        target.AttachedDon = 1;
        defender.Characters.Add(target);

        var battle = new AttackAction().Execute(
            attacker, defender, MatchPhase.Main, "p1", attacker.Leader!.InstanceId, "target", Turn);
        var result = new DamageStepAction().Execute(battle, attacker, defender);

        Assert.True(result.CharacterKnockedOut);
        Assert.Empty(defender.Characters);
        Assert.Contains(target, defender.TrashCards);
        Assert.Equal(1, defender.RestedDon);
    }

    [Fact]
    public void Battle_BlockerBecomesTheNewTarget()
    {
        var attacker = CreatePlayer("p1");
        var defender = CreatePlayer("p2");
        var blocker = Character("blocker", power: 6000, text: "[Blocker] (After your opponent declares an attack...)");
        defender.Characters.Add(blocker);

        var battle = new AttackAction().Execute(
            attacker, defender, MatchPhase.Main, "p1", attacker.Leader!.InstanceId, defender.Leader!.InstanceId, Turn);
        Assert.True(BlockAction.HasPossibleBlocker(defender, battle));

        new BlockAction().Execute(battle, defender, "blocker");
        battle.Step = BattleStep.Counter;
        var result = new DamageStepAction().Execute(battle, attacker, defender);

        Assert.True(blocker.IsRested);
        Assert.False(result.AttackerWon);
        Assert.Equal(5, defender.LifeCount);
    }

    [Fact]
    public void Battle_DoubleAttackTakesTwoLifeAndBanishTrashesThem()
    {
        var attacker = CreatePlayer("p1");
        var defender = CreatePlayer("p2");
        var striker = Character("striker", power: 7000, text: "[Double Attack] [Banish]");
        attacker.Characters.Add(striker);

        var battle = new AttackAction().Execute(
            attacker, defender, MatchPhase.Main, "p1", "striker", defender.Leader!.InstanceId, Turn);
        var result = new DamageStepAction().Execute(battle, attacker, defender);

        Assert.Equal(2, result.LifeCardsLost);
        Assert.Equal(3, defender.LifeCount);
        Assert.Equal(2, defender.TrashCards.Count);
        Assert.Empty(defender.Hand);
    }

    [Fact]
    public void Counter_OnlyAcceptsCardsWithCounterValue()
    {
        var defender = CreatePlayer("p2");
        var noCounter = AddToHand(defender, Character("no-counter", counter: 0));
        var counter = AddToHand(defender, Character("counter", counter: 2000));
        var battle = new BattleState { Step = BattleStep.Counter, Target = defender.Leader! };

        Assert.Throws<InvalidOperationException>(() => new CounterAction().Execute(battle, defender, noCounter.InstanceId));
        new CounterAction().Execute(battle, defender, counter.InstanceId);

        Assert.Equal(2000, battle.CounterBonus);
        Assert.Contains(counter, defender.TrashCards);
    }

    [Fact]
    public void AttachDon_AddsThousandPowerOnlyDuringOwnersTurn()
    {
        var player = CreatePlayer("p1", activeDon: 1);

        new AttachDonAction().Execute(player, MatchPhase.Main, "p1", player.Leader!.InstanceId);

        Assert.Equal(0, player.ActiveDon);
        Assert.Equal(6000, player.Leader.GetPower(isOwnersTurn: true));
        Assert.Equal(5000, player.Leader.GetPower(isOwnersTurn: false));
    }

    [Theory]
    [InlineData("[Rush] (This card can attack on the turn in which it is played.)", "Rush")]
    [InlineData("[Blocker] [On Play] Draw 1 card.", "Blocker")]
    [InlineData("[DON!! x1] This Character gains [Rush].", null)]
    [InlineData("", null)]
    public void Keywords_AreOnlyReadFromTheStartOfTheText(string text, string? expectedKeyword)
    {
        var keywords = CardKeywords.Parse(text);

        if (expectedKeyword is null)
        {
            Assert.Empty(keywords);
        }
        else
        {
            Assert.Equal([expectedKeyword], keywords);
        }
    }

    private static PlayerState CreatePlayer(string playerId, int activeDon = 0)
    {
        var player = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = playerId,
            ActiveDon = activeDon,
            TurnsStarted = 2,
            Leader = new CardInstance
            {
                InstanceId = $"{playerId}-leader",
                Name = "Leader",
                Category = CardInstance.LeaderCategory,
                BasePower = 5000,
                Life = 5
            }
        };

        for (var index = 0; index < 5; index += 1)
        {
            player.LifeCards.Add(Character($"{playerId}-life-{index}"));
        }

        return player;
    }

    private static CardInstance Character(string id, int cost = 1, int power = 3000, int counter = 1000, string text = "")
    {
        return new CardInstance
        {
            InstanceId = id,
            CardId = id,
            Name = id,
            Category = CardInstance.CharacterCategory,
            Cost = cost,
            BasePower = power,
            Counter = counter,
            Text = text
        };
    }

    private static CardInstance AddToHand(PlayerState player, CardInstance card)
    {
        player.Hand.Add(card);
        return card;
    }
}
