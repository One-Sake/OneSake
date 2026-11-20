using OneSake.Domain;
using OneSake.Game.Randomness;
using OneSake.Game.Simulation;
using OneSake.Game.State;
using OneSake.Server.Simulation;
using OneSake.Tests.Replays;

namespace OneSake.Tests.Simulation;

// Every invariant must find a deliberately broken state.
public class InvariantTests
{
    private readonly PlayerState _p1 = CreatePlayer("p1");
    private readonly PlayerState _p2 = CreatePlayer("p2");

    private InvariantSnapshot Snapshot(MatchPhase phase = MatchPhase.Main, IReadOnlyList<ChoicePromptDto>? prompts = null, string active = "p1") => new()
    {
        Players = [_p1, _p2],
        ExpectedCardCounts = new Dictionary<string, int> { ["p1"] = 11, ["p2"] = 11 },
        Phase = phase,
        ActivePlayerId = active,
        OpenPrompts = prompts ?? [new ChoicePromptDto { ChoiceId = "c1", PlayerId = "p1", Kind = "MAIN_ACTION", Options = ["END_TURN"] }]
    };

    private IReadOnlyList<string> Broken(InvariantSnapshot? snapshot = null)
    {
        return InvariantChecker.Check(snapshot ?? Snapshot()).Select(violation => violation.Invariant).ToList();
    }

    [Fact]
    public void ValidState_HasNoViolation()
    {
        Assert.Empty(Broken());
    }

    [Fact]
    public void LostCard_IsFound()
    {
        _p1.DrawDeck.RemoveAt(0);
        Assert.Contains(InvariantNames.CardConservation, Broken());
    }

    [Fact]
    public void CardInResolution_IsCounted()
    {
        var card = _p1.LifeCards[0];
        _p1.LifeCards.RemoveAt(0);

        Assert.Contains(InvariantNames.CardConservation, Broken());
        Assert.Empty(InvariantChecker.Check(Snapshot() with { CardsInResolution = [card] }));
    }

    [Fact]
    public void DuplicatedCard_IsFound()
    {
        _p1.Hand.Add(_p1.DrawDeck[0]);
        _p1.DrawDeck.RemoveAt(1);
        Assert.Contains(InvariantNames.UniqueCards, Broken());
    }

    [Fact]
    public void WrongDonCount_IsFound()
    {
        _p1.ActiveDon += 1;
        Assert.Contains(InvariantNames.DonCount, Broken());

        _p2.Leader!.AttachedDon = 2;
        _p2.DonDeckCount -= 2;
        Assert.DoesNotContain(InvariantNames.DonCount, InvariantChecker.Check(Snapshot()).Where(violation => violation.Message.StartsWith("p2")).Select(violation => violation.Invariant));
    }

    [Fact]
    public void NegativeDon_IsFound()
    {
        _p1.RestedDon = -1;
        _p1.DonDeckCount += 1;
        Assert.Contains(InvariantNames.NegativeValue, Broken());
    }

    [Fact]
    public void SixthCharacter_IsFound()
    {
        for (var index = 0; index < 6; index += 1)
        {
            _p1.Characters.Add(_p1.DrawDeck[0]);
            _p1.DrawDeck.RemoveAt(0);
        }

        Assert.Equal([InvariantNames.CharacterLimit], Broken());
    }

    [Fact]
    public void NegativePower_IsAllowed_NegativeCostIsNot()
    {
        // Rule 1-3-6-1 / 1-3-6-2: CurrentCost is never below 0, power can be.
        _p1.Leader!.Modifiers.Add(new CardModifier { Power = -9000 });
        Assert.Empty(Broken());
    }

    [Fact]
    public void LeaderOutsideTheLeaderArea_IsFound()
    {
        var fakeLeader = new CardInstance { InstanceId = "p1-fake-leader", Category = CardInstance.LeaderCategory, OwnerId = "p1" };
        _p1.Hand.Add(fakeLeader);
        _p1.DrawDeck.RemoveAt(0);
        Assert.Contains(InvariantNames.UniqueCards, Broken());
    }

    [Fact]
    public void RunningPhaseWithoutTurnPlayer_IsFound()
    {
        Assert.Contains(InvariantNames.Phase, Broken(Snapshot(active: "nobody")));
    }

    [Fact]
    public void BrokenPrompts_AreFound()
    {
        Assert.Contains(InvariantNames.Prompts, Broken(Snapshot(prompts: [new ChoicePromptDto { ChoiceId = "c1", PlayerId = "ghost", Kind = "MAIN_ACTION", Options = ["END_TURN"] }])));
        Assert.Contains(InvariantNames.Prompts, Broken(Snapshot(prompts: [new ChoicePromptDto { ChoiceId = "c1", PlayerId = "p1", Kind = "MAIN_ACTION", Options = [] }])));
        Assert.Contains(InvariantNames.Prompts, Broken(Snapshot(MatchPhase.GameOver)));
        Assert.Contains(InvariantNames.NoOpenPrompt, Broken(Snapshot(prompts: [])));
        Assert.Empty(Broken(Snapshot(MatchPhase.GameOver, prompts: [])));
    }

    [Fact]
    public void RandomBot_AlwaysGivesValidDecisions()
    {
        // 1000+ random prompts of real matches: the server accepts every decision of the bot.
        var steps = 0;
        var rejections = 0;
        for (var seed = 1UL; steps < 1000; seed += 1)
        {
            var result = MatchSimulator.Play(seed, MatchBot.Deck(), MatchBot.Deck(), random => new RandomBot(random), random => new RandomBot(random));
            Assert.False(result.Failed, string.Join(" | ", result.Violations));
            steps += result.Steps;
            rejections += result.Rejections;
        }

        Assert.Equal(0, rejections);
    }

    private static PlayerState CreatePlayer(string id)
    {
        var player = new PlayerState { PlayerId = id, DisplayName = id, DonDeckCount = 8, ActiveDon = 1, RestedDon = 1 };
        player.Leader = new CardInstance { InstanceId = $"{id}-leader", Category = CardInstance.LeaderCategory, OwnerId = id };
        for (var index = 0; index < 10; index += 1)
        {
            var card = new CardInstance { InstanceId = $"{id}-{index}", OwnerId = id, Cost = 1 };
            if (index < 6)
            {
                player.DrawDeck.Add(card);
            }
            else if (index < 8)
            {
                player.Hand.Add(card);
            }
            else
            {
                player.LifeCards.Add(card);
            }
        }

        return player;
    }
}
