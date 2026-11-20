using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Tests;

public class CardScriptRegistryTests
{
    // Texts with many different parts of the model (costs, conditions, filters, durations...).
    public static TheoryData<string, string> Texts => new()
    {
        { "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.", "Character" },
        { "[DON!! x1] [When Attacking] [Once Per Turn] Give up to 1 of your opponent's Characters -2000 power during this turn.", "Character" },
        { "[Activate: Main] [Once Per Turn] Give this Leader or 1 of your Characters up to 1 rested DON!! card.", "Leader" },
        { "[Blocker] (After your opponent declares an attack, you may rest this card to make it the new target of the attack.)", "Character" },
        { "[DON!! x1] This Character gains +1000 power.", "Character" },
        { "[Counter] Up to 1 of your Leader or Character cards gains +4000 power during this battle. [Trigger] Up to 1 of your Leader or Character cards gains +1000 power during this turn.", "Event" },
        { "[On Play] Look at 5 cards from the top of your deck; reveal up to 1 {Straw Hat Crew} type card other than [Nami] and add it to your hand. Then, place the rest at the bottom of your deck in any order.", "Character" },
        { "[On Play] If your Leader has the {Animal Kingdom Pirates} type, draw 2 cards and trash 1 card from your hand.", "Character" },
        { "[On Play] Choose one: Draw 1 card or K.O. this Character.", "Character" },
    };

    [Theory]
    [MemberData(nameof(Texts))]
    public void Scripts_SurviveAJsonRoundTrip(string text, string category)
    {
        var script = EffectParser.Parse(text, category);
        var file = new CardScriptFile
        {
            CardId = "TEST-001",
            Status = script.IsFullySupported ? CardScriptStatus.Generated : CardScriptStatus.Manual,
            SourceTextHash = CardScriptJson.HashText(text),
            ParserVersion = "test",
            Script = script
        };

        var json = CardScriptJson.Serialize(file);
        var loaded = CardScriptJson.Deserialize(json);

        Assert.Equal(json, CardScriptJson.Serialize(loaded));
        Assert.Equal(script.Abilities.Count, loaded.Script.Abilities.Count);
        Assert.Equal(script.UnsupportedTexts, loaded.Script.UnsupportedTexts);
        if (script.Abilities.Count > 0)
        {
            // Enums are written as strings.
            Assert.Contains("\"timing\": \"", json);
        }

        Assert.DoesNotContain("isFullySupported", json);
    }

    [Fact]
    public void Defaults_ThatDifferFromTheClrDefault_AreKeptAfterLoading()
    {
        // AllowCharacter defaults to true, UpTo to true and RestDestination to "bottom":
        // a stored "false"/other value must win, a stored "true" must stay true.
        var script = EffectParser.Parse("[On Play] K.O. up to 1 of your opponent's Leader or Character cards with a cost of 0.", "Character");
        var loaded = CardScriptJson.Deserialize(CardScriptJson.Serialize(new CardScriptFile { Script = script }));

        var original = script.Abilities.SelectMany(ability => ability.Steps).Select(step => step.Target?.Filter).ToList();
        var copy = loaded.Script.Abilities.SelectMany(ability => ability.Steps).Select(step => step.Target?.Filter).ToList();
        Assert.Equal(original.Select(filter => filter?.AllowCharacter), copy.Select(filter => filter?.AllowCharacter));
        Assert.Equal(original.Select(filter => filter?.AllowLeader), copy.Select(filter => filter?.AllowLeader));
    }

    [Fact]
    public void CardWithoutFile_IsParsedAtRuntime()
    {
        var card = new CardInstance { CardId = "NOFILE-001", Text = "[On Play] Draw 1 card.", Category = CardInstance.CharacterCategory };

        var info = card.ScriptInfo;

        Assert.Equal(CardScriptRegistry.OriginParser, info.Origin);
        Assert.Equal(CardScriptStatus.Generated, info.Status);
        Assert.Single(info.Script.Abilities);
    }

    [Fact]
    public void CardWithoutText_HasNoStatus()
    {
        var card = new CardInstance { CardId = "NOFILE-002", Text = string.Empty };

        Assert.Equal(string.Empty, card.ScriptInfo.Status);
        Assert.Empty(card.Script.Abilities);
    }

    [Fact]
    public void ManualScript_KeepsKeywords_AndMarksEveryEffectAsManual()
    {
        var script = CardScriptRegistry.CreateManualScript("[Blocker] [On Play] Draw 1 card.", CardInstance.CharacterCategory);

        var keyword = Assert.Single(script.Abilities);
        Assert.Equal(EffectTiming.Static, keyword.Timing);
        Assert.Contains(script.UnsupportedTexts, text => text.Contains("[On Play]"));
    }
}
