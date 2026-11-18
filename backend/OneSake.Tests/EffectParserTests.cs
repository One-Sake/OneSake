using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests;

// The parser turns real card texts (from optcgapi.com) into effect definitions.
public class EffectParserTests
{
    [Fact]
    public void OnPlay_KO_WithCostFilter()
    {
        var script = EffectParser.Parse("[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.");

        Assert.True(script.IsFullySupported);
        var ability = Assert.Single(script.Abilities);
        Assert.Equal(EffectTiming.OnPlay, ability.Timing);
        var step = Assert.Single(ability.Steps);
        Assert.Equal(StepKind.KO, step.Kind);
        Assert.Equal(1, step.Target!.Count);
        Assert.True(step.Target.UpTo);
        Assert.Equal(PlayerSide.Opponent, step.Target.Filter.Owner);
        Assert.Equal(3, step.Target.Filter.MaxCost);
        Assert.False(step.Target.Filter.AllowLeader);
    }

    [Theory]
    [InlineData("[DON!! x1] [When Attacking] Give up to 1 of your opponent's Characters -2000 power during this turn.")]
    // Newer cards in the API lost the minus sign.
    [InlineData("[DON!! x1][When Attacking] Give up to 1 of your opponent's Characters 2000 power during this turn.")]
    public void WhenAttacking_WithDonRequirement_GivesMinusPower(string text)
    {
        var ability = Assert.Single(EffectParser.Parse(text).Abilities);

        Assert.Equal(EffectTiming.WhenAttacking, ability.Timing);
        Assert.Equal(1, ability.RequiredDon);
        var step = Assert.Single(ability.Steps);
        Assert.Equal(StepKind.PowerModifier, step.Kind);
        Assert.Equal(-2000, step.Amount);
        Assert.Equal(Duration.ThisTurn, step.Duration);
    }

    [Fact]
    public void LookAtTopCards_RevealsTypeAndPlacesRestAtBottom()
    {
        var script = EffectParser.Parse(
            "[On Play] Look at 5 cards from the top of your deck; reveal up to 1 {Straw Hat Crew} type card other than [Nami] and add it to your hand. Then, place the rest at the bottom of your deck in any order.");

        var step = Assert.Single(Assert.Single(script.Abilities).Steps);
        Assert.Equal(StepKind.LookAndReveal, step.Kind);
        Assert.Equal(5, step.Amount);
        Assert.Equal(1, step.SecondAmount);
        Assert.Equal("Straw Hat Crew", step.Filter!.TypeIncludes);
        Assert.Equal("Nami", step.Filter.NameNot);
        Assert.Equal("bottom", step.RestDestination);
    }

    [Fact]
    public void StaticDonAbility_GivesPowerAndKeyword()
    {
        var script = EffectParser.Parse("[DON!! x1] This Character gains +1000 power. [DON!! x2] This Character gains [Rush].");

        Assert.True(script.IsFullySupported);
        Assert.Equal(2, script.Abilities.Count);
        Assert.All(script.Abilities, ability => Assert.Equal(EffectTiming.Static, ability.Timing));
        Assert.Equal(1000, script.Abilities[0].Steps[0].Amount);
        Assert.Equal(2, script.Abilities[1].RequiredDon);
        Assert.Equal(CardKeywords.Rush, script.Abilities[1].Steps[0].Flag);
    }

    [Fact]
    public void ActivateMain_WithRestDonAndRestThisCosts()
    {
        var script = EffectParser.Parse(
            "[Activate:Main] [Once Per Turn] (1) (You may rest the specified number of DON!! cards in your cost area) You may rest this Character: Draw 1 card.");

        var ability = Assert.Single(script.Abilities);
        Assert.Equal(EffectTiming.ActivateMain, ability.Timing);
        Assert.True(ability.OncePerTurn);
        Assert.Collection(ability.Costs,
            cost => Assert.Equal(CostKind.RestDon, cost.Kind),
            cost => Assert.Equal(CostKind.RestThis, cost.Kind));
        Assert.Equal(StepKind.Draw, Assert.Single(ability.Steps).Kind);
    }

    [Fact]
    public void Event_SplitsMainAndTrigger()
    {
        var script = EffectParser.Parse(
            "[Main] K.O. up to 1 of your opponent's Characters with 4000 power or less. [Trigger] Activate this card's [Main] effect.",
            CardInstance.EventCategory);

        Assert.Collection(script.Abilities,
            ability => Assert.Equal(EffectTiming.EventMain, ability.Timing),
            ability =>
            {
                Assert.Equal(EffectTiming.Trigger, ability.Timing);
                Assert.Equal(StepKind.ActivateOwnAbility, Assert.Single(ability.Steps).Kind);
            });
        Assert.Equal(4000, script.Abilities[0].Steps[0].Target!.Filter.MaxPower);
    }

    [Fact]
    public void KO_WithTwoTargets_BecomesTwoSteps()
    {
        var ability = Assert.Single(EffectParser.Parse(
            "[On Play] DON!! -2: K.O. up to 1 of your opponent's Characters with a cost of 3 or less and up to 1 of your opponent's Characters with a cost of 2 or less.").Abilities);

        Assert.Equal(CostKind.ReturnDon, Assert.Single(ability.Costs).Kind);
        Assert.Equal(2, ability.Steps.Count);
        Assert.Equal(3, ability.Steps[0].Target!.Filter.MaxCost);
        Assert.Equal(2, ability.Steps[1].Target!.Filter.MaxCost);
    }

    [Fact]
    public void LeaderCondition_IsAttachedToTheStep()
    {
        var step = Assert.Single(Assert.Single(EffectParser.Parse(
            "[On Play] If your Leader has the {Navy} type, draw 2 cards.").Abilities).Steps);

        Assert.Equal(StepKind.Draw, step.Kind);
        var condition = Assert.Single(step.Conditions);
        Assert.Equal(ConditionKind.LeaderHasType, condition.Kind);
        Assert.Equal("Navy", condition.Filter!.TypeIncludes);
    }

    [Fact]
    public void BlockerKeyword_AndReminderTextAreIgnored()
    {
        var script = EffectParser.Parse("[Blocker] (After your opponent declares an attack, you may rest this card to make it the new target of the attack.)");

        Assert.True(script.IsFullySupported);
        Assert.Equal(CardKeywords.Blocker, Assert.Single(Assert.Single(script.Abilities).Steps).Flag);
    }

    [Fact]
    public void UnknownText_IsMarkedAsManual()
    {
        var script = EffectParser.Parse("[On Play] Choose one: Draw 1 card or K.O. this Character.");

        Assert.False(script.IsFullySupported);
        Assert.Empty(script.Abilities);
        Assert.Single(script.UnsupportedTexts);
    }

    [Fact]
    public void DisclaimerText_IsNotAnEffect()
    {
        var script = EffectParser.Parse("[Rush] DISCLAIMER: This card is a reprint of the pre-existing card in the OP01 set.");

        Assert.True(script.IsFullySupported);
    }
}
