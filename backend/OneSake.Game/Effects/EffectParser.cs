using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using OneSake.Game.State;

namespace OneSake.Game.Effects;

// Turns the English card text from the API into a CardScript.
//
// How it works:
//   1. Clean the text (remove reminder texts in brackets, disclaimers, unify spelling).
//   2. Split it into abilities at the timing tags ([On Play], [When Attacking], ...).
//   3. Split every ability into "costs : effect" and the effect into sentences.
//   4. Every sentence is matched against a list of known patterns (K.O., draw, rest, power...).
//
// If one sentence of an ability is unknown, the whole ability is marked as unsupported.
// It is better to do nothing (and tell the players) than to run half an effect.
public static partial class EffectParser
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly ConcurrentDictionary<string, CardScript> Cache = new(StringComparer.Ordinal);

    public static CardScript ParseCached(string? text, string category)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return CardScript.Empty;
        }

        return Cache.GetOrAdd($"{category}|{text}", _ => Parse(text, category));
    }

    public static CardScript Parse(string? text, string category = CardInstance.CharacterCategory)
    {
        var script = new CardScript();
        var cleaned = Clean(text);
        if (cleaned.Length == 0)
        {
            return script;
        }

        var index = 0;
        foreach (var block in SplitAbilities(cleaned))
        {
            LastFailedPart = null;
            var keywords = new List<string>();
            var ability = ParseAbility(block, keywords);

            if (keywords.Count > 0)
            {
                // Keywords become a static "this card has [Blocker]" ability (with the same DON!!/turn condition).
                var keywordAbility = new Ability
                {
                    Timing = EffectTiming.Static,
                    Text = string.Join(" ", keywords.Select(keyword => $"[{keyword}]")),
                    RequiredDon = ability?.Timing == EffectTiming.Static ? ability.RequiredDon : 0,
                    OnlyYourTurn = ability?.Timing == EffectTiming.Static && ability.OnlyYourTurn,
                    OnlyOpponentsTurn = ability?.Timing == EffectTiming.Static && ability.OnlyOpponentsTurn,
                    Index = index++
                };

                foreach (var keyword in keywords)
                {
                    keywordAbility.Steps.Add(new EffectStep
                    {
                        Kind = StepKind.GainFlag,
                        Target = new TargetSpec { Self = true, UpTo = false },
                        Flag = keyword,
                        Duration = Duration.Permanent,
                        Text = $"[{keyword}]"
                    });
                }

                script.Abilities.Add(keywordAbility);
            }

            if (ability is null)
            {
                script.UnsupportedTexts.Add(block.FullText);
                script.FailedParts.Add(LastFailedPart ?? block.FullText);
                continue;
            }

            if (ability.Timing == EffectTiming.Static && ability.Steps.Count == 0)
            {
                continue;
            }

            // Always-on abilities must be something StaticEffects can calculate (power, cost, flags).
            if (ability.Timing == EffectTiming.Static && !StaticEffects.IsSupported(ability))
            {
                script.UnsupportedTexts.Add(block.FullText);
                script.FailedParts.Add("STATIC: " + block.Body);
                continue;
            }

            ability.Index = index++;
            script.Abilities.Add(ability);
        }

        return script;
    }

    #region CLEANING

    public static string Clean(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // Disclaimers and errata notes are not part of the effect.
        value = Regex.Replace(value, @"(DISCLAIMER|Disclaimer|The main difference between|While the original print|This card is a reprint).*$", string.Empty, Options | RegexOptions.Singleline);
        value = Regex.Replace(value, @"This card has been officially errata'd\.?", string.Empty, Options);

        value = value
            .Replace('−', '-')
            .Replace('–', '-')
            .Replace('×', 'x')
            .Replace('’', '\'')
            .Replace("“", "\"")
            .Replace("”", "\"");

        // "(1)" in front of the reminder text means: rest 1 DON!!.
        value = Regex.Replace(value, @"[①-⑩]", match => $"({match.Value[0] - '①' + 1})");
        value = Regex.Replace(value, @"\((\d+)\)", "RESTDON$1", Options);

        // Reminder texts in brackets, e.g. "(This card can attack on the turn in which it is played.)".
        // Nested brackets do not exist, so a simple pattern is enough.
        for (var round = 0; round < 3; round += 1)
        {
            value = Regex.Replace(value, @"\s*\([^()]*\)", string.Empty);
        }

        value = Regex.Replace(value, @"\[Activate\s*:\s*Main\]", "[Activate: Main]", Options);
        value = Regex.Replace(value, @"\[DON!!\s*x\s*(\d+)\]", "[DON!! x$1]", Options);
        value = Regex.Replace(value, @"DON!!\s*-\s*(\d+)", "DON!! -$1", Options);
        value = Regex.Replace(value, @"\]\s*\[", "] [");
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = value.Replace("•", ".");

        // Types are written as {Type}, "Type" or [Type] - unify to {Type}.
        value = Regex.Replace(value, @"""([^""]+)""\s+type", "{$1} type", Options);
        value = Regex.Replace(value, @"\[([^\]]+)\]\s+type", "{$1} type", Options);
        value = Regex.Replace(value, @"type including ""([^""]+)""", "type including {$1}", Options);
        value = Regex.Replace(value, @"type including \[([^\]]+)\]", "type including {$1}", Options);

        return value.Trim();
    }

    #endregion

    #region ABILITY SPLITTING

    private sealed record AbilityBlock(List<string> Tags, string Body, string FullText);

    private static readonly string[] HeaderTags =
    [
        "On Play", "When Attacking", "On Block", "On K.O.", "Activate: Main", "Main", "Counter", "Trigger",
        "End of Your Turn", "End of Your Opponent's Turn", "On Your Opponent's Attack", "Your Turn",
        "Opponent's Turn", "Once Per Turn", "Rush", "Rush: Character", "Unblockable", "Blocker", "Double Attack", "Banish",
    ];

    private static bool IsHeaderTag(string tag)
    {
        return HeaderTags.Any(known => known.Equals(tag, StringComparison.OrdinalIgnoreCase))
            || Regex.IsMatch(tag, @"^DON!! x\d+$", Options);
    }

    // Splits "[On Play] Draw 1 card. [Trigger] Play this card." into two blocks.
    // A tag only starts a new block at the start of the text or after the end of a sentence,
    // so "K.O. up to 1 of your opponent's [Blocker] Characters" stays one block.
    private static List<AbilityBlock> SplitAbilities(string text)
    {
        var blocks = new List<AbilityBlock>();
        var position = 0;
        var currentTags = new List<string>();
        var bodyStart = 0;
        var blockStart = 0;

        while (position < text.Length)
        {
            var atBoundary = position == 0 || IsSentenceEnd(text, position);
            if (text[position] == '[' && atBoundary && TryReadTag(text, position, out var tag, out var end) && IsHeaderTag(tag))
            {
                // Finish the previous block.
                var previousBody = text[bodyStart..position].Trim();
                if (previousBody.Length > 0 || currentTags.Count > 0)
                {
                    blocks.Add(new AbilityBlock(currentTags, previousBody, text[blockStart..position].Trim()));
                }

                blockStart = position;
                currentTags = [tag];
                position = end;

                // Read all tags that follow directly: "[DON!! x1] [When Attacking] [Once Per Turn]".
                while (true)
                {
                    var next = SkipSpaces(text, position);
                    if (next < text.Length && text[next] == '[' && TryReadTag(text, next, out var nextTag, out var nextEnd) && IsHeaderTag(nextTag))
                    {
                        currentTags.Add(nextTag);
                        position = nextEnd;
                        continue;
                    }

                    break;
                }

                bodyStart = position;
                continue;
            }

            position += 1;
        }

        var lastBody = text[bodyStart..].Trim();
        if (lastBody.Length > 0 || currentTags.Count > 0)
        {
            blocks.Add(new AbilityBlock(currentTags, lastBody, text[blockStart..].Trim()));
        }

        return blocks;
    }

    private static bool IsSentenceEnd(string text, int position)
    {
        var index = position - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index -= 1;
        }

        if (index < 0)
        {
            return true;
        }

        // "DON!!" ends with "!" but is no sentence end.
        if (text[index] == '!' && index >= 4 && text.Substring(index - 4, 5) == "DON!!")
        {
            return false;
        }

        return text[index] is '.' or '!' or ']';
    }

    private static int SkipSpaces(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position += 1;
        }

        return position;
    }

    private static bool TryReadTag(string text, int start, out string tag, out int end)
    {
        var close = text.IndexOf(']', start);
        if (close < 0)
        {
            tag = string.Empty;
            end = start;
            return false;
        }

        tag = text[(start + 1)..close].Trim();
        end = close + 1;
        return true;
    }

    #endregion

    #region ABILITY PARSING

    private static Ability? ParseAbility(AbilityBlock block, List<string> keywords)
    {
        var ability = new Ability { Text = block.FullText };
        var timingSet = false;

        foreach (var tag in block.Tags)
        {
            var donMatch = Regex.Match(tag, @"^DON!! x(\d+)$", Options);
            if (donMatch.Success)
            {
                ability.RequiredDon = int.Parse(donMatch.Groups[1].Value);
                continue;
            }

            if (tag.Equals("Rush: Character", StringComparison.OrdinalIgnoreCase))
            {
                keywords.Add(EffectFlags.RushCharacter);
                continue;
            }

            if (tag.Equals("Unblockable", StringComparison.OrdinalIgnoreCase))
            {
                keywords.Add(EffectFlags.CannotBeBlocked);
                continue;
            }

            if (CardKeywords.Normalize(tag) is { } keyword)
            {
                // "[Blocker]" etc. - becomes a static flag of the card.
                keywords.Add(keyword);
                continue;
            }

            var timing = tag.ToLowerInvariant() switch
            {
                "on play" => EffectTiming.OnPlay,
                "when attacking" => EffectTiming.WhenAttacking,
                "on block" => EffectTiming.OnBlock,
                "on k.o." => EffectTiming.OnKO,
                "activate: main" => EffectTiming.ActivateMain,
                "main" => EffectTiming.EventMain,
                "counter" => EffectTiming.EventCounter,
                "trigger" => EffectTiming.Trigger,
                "end of your turn" => EffectTiming.EndOfYourTurn,
                "end of your opponent's turn" => EffectTiming.EndOfOpponentsTurn,
                "on your opponent's attack" => EffectTiming.OnOpponentsAttack,
                _ => (EffectTiming?)null
            };

            if (timing is not null)
            {
                if (timingSet)
                {
                    // Two timings in one block are not supported.
                    return null;
                }

                ability.Timing = timing.Value;
                timingSet = true;
                continue;
            }

            switch (tag.ToLowerInvariant())
            {
                case "once per turn":
                    ability.OncePerTurn = true;
                    break;
                case "your turn":
                    ability.OnlyYourTurn = true;
                    break;
                case "opponent's turn":
                    ability.OnlyOpponentsTurn = true;
                    break;
            }
        }

        if (!timingSet)
        {
            ability.Timing = EffectTiming.Static;
        }

        var body = block.Body.Trim();
        if (body.Length == 0)
        {
            // Only keywords like "[Blocker]" - nothing else to do. A timing without text is broken.
            return ability.Timing == EffectTiming.Static ? ability : null;
        }

        var colon = FindCostColon(body);
        if (colon >= 0)
        {
            var costText = body[..colon].Trim();
            body = body[(colon + 1)..].Trim();
            if (!TryParseCosts(costText, ability.Costs))
            {
                LastFailedPart = "COST: " + costText;
                return null;
            }
        }

        if (!TryParseEffect(body, ability.Steps, ability.Timing == EffectTiming.Static))
        {
            return null;
        }

        // In static abilities a leading "If" belongs to the ability, not to one step.
        if (ability.Timing == EffectTiming.Static)
        {
            foreach (var step in ability.Steps)
            {
                ability.Conditions.AddRange(step.Conditions);
                step.Conditions.Clear();
            }
        }

        return ability;
    }

    // Finds the ":" that separates costs and effect. Ignores ":" inside brackets.
    private static int FindCostColon(string body)
    {
        var depth = 0;
        for (var index = 0; index < body.Length; index += 1)
        {
            var character = body[index];
            if (character is '[' or '{')
            {
                depth += 1;
            }
            else if (character is ']' or '}')
            {
                depth -= 1;
            }
            else if (character == ':' && depth == 0)
            {
                // Only a cost colon when nothing like a full sentence is in front of it.
                var before = body[..index];
                return before.Contains(". ") ? -1 : index;
            }
        }

        return -1;
    }

    #endregion

    #region COSTS

    private static bool TryParseCosts(string text, List<EffectCost> costs)
    {
        var rest = text.Trim();
        const string Lead = @"^(?:and |, )?(?:you may )?";

        while (rest.Length > 0)
        {
            Match match;
            CardFilter? filter = null;

            // Newer cards in the API lost the minus sign: "DON!! 2:" means "DON!! -2".
            if ((match = Regex.Match(rest, @"^(?:, )?DON!! -?(\d+)\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.ReturnDon, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, @"^(?:and |, )?RESTDON(\d+)\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.RestDon, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"(?:rest )?this (?:Character|Leader|Stage|card)\s*", Options)).Success
                && (match.Value.Contains("rest", StringComparison.OrdinalIgnoreCase) || costs.Any(cost => cost.Kind is CostKind.RestDon or CostKind.RestOwnCards)))
            {
                // "rest 1 of your DON!! cards and this Character" - "this Character" is rested, too.
                costs.Add(new EffectCost { Kind = CostKind.RestThis });
            }
            else if ((match = Regex.Match(rest, Lead + @"trash this (?:Character|Stage|card)\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashThis });
            }
            else if ((match = Regex.Match(rest, Lead + @"return this (?:Character|card) to (?:the owner's|your) hand\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.ReturnThis });
            }
            else if ((match = Regex.Match(rest, Lead + @"rest (\d+) of your DON!! cards?\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.RestDon, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"trash (\d+) cards? from the top of your deck\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashTopOfDeck, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"trash (\d+) cards? from the top (?:or bottom )?of your Life cards\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashLife, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"turn (\d+) cards? from the top of your Life cards face-(?:up|down)\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.TurnLife, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"trash (\d+) (.+?) from your hand\s*", Options)).Success
                && TryCardDescription(match.Groups[2].Value, out filter))
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashFromHand, Amount = int.Parse(match.Groups[1].Value), Filter = filter });
            }
            else if ((match = Regex.Match(rest, Lead + @"reveal (\d+) (.+?) from your hand\s*", Options)).Success
                && TryCardDescription(match.Groups[2].Value, out filter))
            {
                costs.Add(new EffectCost { Kind = CostKind.RevealFromHand, Amount = int.Parse(match.Groups[1].Value), Filter = filter });
            }
            else if ((match = Regex.Match(rest, Lead + @"add (\d+) cards? from (?:the top (?:or bottom )?of )?your Life (?:cards|area) to your hand\s*", Options)).Success)
            {
                costs.Add(new EffectCost { Kind = CostKind.LifeToHand, Amount = int.Parse(match.Groups[1].Value) });
            }
            else if ((match = Regex.Match(rest, Lead + @"rest (\d+) of your (.+?)(?=$|, | and )", Options)).Success
                && ParseFieldDescription(match.Groups[2].Value, out _) is { } restFilter)
            {
                costs.Add(new EffectCost { Kind = CostKind.RestOwnCards, Amount = int.Parse(match.Groups[1].Value), Filter = restFilter });
            }
            else if ((match = Regex.Match(rest, Lead + @"return (\d+) of your (.+?) to (?:the owner's|your) hand\s*", Options)).Success
                && ParseFieldDescription(match.Groups[2].Value, out _) is { } returnFilter)
            {
                costs.Add(new EffectCost { Kind = CostKind.ReturnOwnCharacter, Amount = int.Parse(match.Groups[1].Value), Filter = returnFilter });
            }
            else if ((match = Regex.Match(rest, Lead + @"trash (\d+) of your (.+?)(?=$|, | and )", Options)).Success
                && ParseFieldDescription(match.Groups[2].Value, out _) is { } trashFilter)
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashOwnCharacter, Amount = int.Parse(match.Groups[1].Value), Filter = trashFilter });
            }
            else if ((match = Regex.Match(rest, Lead + @"(?:place|return) (\d+) (.+?) from your trash (?:at|to) the bottom of your deck(?: in any order)?\s*", Options)).Success
                && TryCardDescription(match.Groups[2].Value, out filter))
            {
                costs.Add(new EffectCost { Kind = CostKind.TrashToDeckBottom, Amount = int.Parse(match.Groups[1].Value), Filter = filter });
            }
            else if ((match = Regex.Match(rest, @"^(?:and|,)\s*", Options)).Success && match.Length > 0)
            {
                // Separator between two costs.
            }
            else
            {
                return false;
            }

            rest = rest[match.Length..].Trim();
        }

        return costs.Count > 0;
    }

    private static bool TryCardDescription(string text, out CardFilter filter)
    {
        filter = ParseCardDescription(text.Trim(), out var ok);
        return ok;
    }

    #endregion

    #region EFFECT SENTENCES

    private static bool TryParseEffect(string body, List<EffectStep> steps, bool isStatic)
    {
        foreach (var rawSentence in SplitSentences(body))
        {
            var sentence = rawSentence.Trim().TrimEnd('.').Trim();
            if (sentence.Length == 0)
            {
                continue;
            }

            // "Then, ..." just continues the effect.
            sentence = Regex.Replace(sentence, @"^Then,?\s*", string.Empty, Options);
            if (sentence.Length > 0)
            {
                sentence = char.ToUpperInvariant(sentence[0]) + sentence[1..];
            }

            // Rest of a "Look at" effect: modifies the step before.
            var restMatch = Regex.Match(sentence, @"^(?:Place|Put) the rest (?:at the (bottom|top or bottom) of (?:your|the) deck|on the bottom of your deck)(?: in any order)?(?: and (.+))?$", Options);
            if (!restMatch.Success)
            {
                restMatch = Regex.Match(sentence, @"^(Trash) the rest(?: and (.+))?$", Options);
            }

            if (restMatch.Success && steps.Count > 0 && steps[^1].Kind == StepKind.LookAndReveal)
            {
                var destination = restMatch.Groups[1].Value.ToLowerInvariant();
                steps[^1].RestDestination = destination switch
                {
                    "trash" => "trash",
                    "top or bottom" => "bottom",
                    _ => "bottom"
                };

                if (restMatch.Groups[2].Success && restMatch.Groups[2].Value.Length > 0)
                {
                    if (!TryParseSentence(restMatch.Groups[2].Value, steps, isStatic))
                    {
                        return false;
                    }
                }

                continue;
            }

            if (Regex.IsMatch(sentence, @"^Shuffle your deck$", Options))
            {
                steps.Add(new EffectStep { Kind = StepKind.ShuffleDeck, Text = sentence });
                continue;
            }

            if (!TryParseSentence(sentence, steps, isStatic))
            {
                return false;
            }
        }

        return steps.Count > 0;
    }

    // Splits at ". " but not inside brackets ("K.O." contains dots, too).
    private static List<string> SplitSentences(string body)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var index = 0; index < body.Length; index += 1)
        {
            var character = body[index];
            if (character is '[' or '{')
            {
                depth += 1;
            }
            else if (character is ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (character == '.' && depth == 0)
            {
                var isEnd = index == body.Length - 1 || body[index + 1] == ' ';
                // "K.O." / "K.O.'d" are not the end of a sentence.
                var isKo = index >= 3 && body.Substring(index - 3, 4).Equals("K.O.", StringComparison.OrdinalIgnoreCase);
                var koFollowedBySentence = isKo && index + 1 < body.Length && IsKoSentenceEnd(body, index);
                if (isEnd && (!isKo || koFollowedBySentence || index == body.Length - 1))
                {
                    parts.Add(body[start..(index + 1)]);
                    start = index + 1;
                }
            }
        }

        if (start < body.Length)
        {
            parts.Add(body[start..]);
        }

        return parts.Where(part => part.Trim().Length > 0).ToList();
    }

    // "... is K.O.'d. Then, draw" vs "K.O. up to 1": a "K.O." that ends a sentence is followed
    // by an upper-case word, a "K.O." that starts an effect is followed by "up to", "all"...
    private static bool IsKoSentenceEnd(string body, int dotIndex)
    {
        var next = body[(dotIndex + 1)..].TrimStart();
        if (next.Length == 0)
        {
            return true;
        }

        return char.IsUpper(next[0]) || next[0] == '[';
    }

    // Only for the coverage report: the last sentence that could not be parsed.
    [ThreadStatic] public static string? LastFailedPart;

    private static bool TryParseSentence(string sentence, List<EffectStep> steps, bool isStatic)
    {
        sentence = sentence.Trim().TrimEnd('.').Trim();
        var conditions = new List<EffectCondition>();

        // "If your Leader has the {X} type, draw 1 card."
        var ifMatch = Regex.Match(sentence, @"^If (.+?), (?=[a-z])(.+)$", Options);
        if (ifMatch.Success && TryParseConditions(ifMatch.Groups[1].Value, conditions))
        {
            sentence = ifMatch.Groups[2].Value;
        }
        else if (ifMatch.Success)
        {
            // The condition may contain commas: try the last comma as well.
            var lastComma = sentence.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma < 0 || !TryParseConditions(sentence[3..lastComma], conditions))
            {
                LastFailedPart = "IF: " + sentence;
                return false;
            }

            sentence = sentence[(lastComma + 2)..];
        }

        // "Draw 1 card if you have 5 or less cards in your hand."
        var trailingIf = Regex.Match(sentence, @"^(.+?) if (you|your|there|this).+$", Options);
        if (trailingIf.Success)
        {
            var conditionText = sentence[(trailingIf.Groups[1].Value.Length + 4)..];
            var trailingConditions = new List<EffectCondition>();
            if (TryParseConditions(conditionText, trailingConditions))
            {
                conditions.AddRange(trailingConditions);
                sentence = trailingIf.Groups[1].Value;
            }
        }

        var optional = false;
        var mayMatch = Regex.Match(sentence, @"^You may (.+)$", Options);
        if (mayMatch.Success)
        {
            optional = true;
            sentence = char.ToUpperInvariant(mayMatch.Groups[1].Value[0]) + mayMatch.Groups[1].Value[1..];
        }

        sentence = char.ToUpperInvariant(sentence[0]) + sentence[1..];

        var parsed = new List<EffectStep>();
        if (!TryParseAction(sentence, parsed, isStatic, optional))
        {
            // "A and B": try both halves on their own.
            if (!TrySplitAnd(sentence, parsed, isStatic, optional))
            {
                LastFailedPart = sentence;
                return false;
            }
        }

        if (conditions.Count > 0 && parsed.Count > 0)
        {
            foreach (var step in parsed)
            {
                step.Conditions.AddRange(conditions);
            }
        }

        steps.AddRange(parsed);
        return true;
    }

    private static bool TrySplitAnd(string sentence, List<EffectStep> parsed, bool isStatic, bool optional)
    {
        // Split only at " and " / ", and " / ", then " that is followed by a verb.
        var matches = Regex.Matches(sentence, @"(?:,? and |, then,? )(?=(?:draw|trash|rest|K\.O\.|play|add|give|set|return|place|look|this|your|up to|reveal)\b)", Options);
        foreach (Match match in matches)
        {
            var left = sentence[..match.Index];
            var right = sentence[(match.Index + match.Length)..];
            right = char.ToUpperInvariant(right[0]) + right[1..];

            // "K.O. up to 1 X and up to 1 Y" -> "K.O. up to 1 Y" for the right part.
            var verb = Regex.Match(left, @"^(K\.O\.|Rest|Return|Give|Play|Trash)\s", Options);
            if (verb.Success && right.StartsWith("Up to", StringComparison.OrdinalIgnoreCase))
            {
                right = $"{verb.Groups[1].Value} up to{right[5..]}";
                if (verb.Groups[1].Value.Equals("Return", StringComparison.OrdinalIgnoreCase)
                    && !right.Contains("hand", StringComparison.OrdinalIgnoreCase))
                {
                    right += " to the owner's hand";
                }
            }

            var leftSteps = new List<EffectStep>();
            var rightSteps = new List<EffectStep>();
            if (TryParseAction(left, leftSteps, isStatic, optional)
                && (TryParseAction(right, rightSteps, isStatic, false) || TrySplitAnd(right, rightSteps, isStatic, false)))
            {
                parsed.AddRange(leftSteps);
                parsed.AddRange(rightSteps);
                return true;
            }
        }

        // "This Character gains +1000 power and [Rush]" / "gains [Blocker] and +1000 power"
        var gains = Regex.Match(sentence, @"^(.+?) gains? (.+?) and (.+?)( during this turn| during this battle| until .+)?$", Options);
        if (gains.Success)
        {
            var suffix = gains.Groups[4].Value;
            var first = new List<EffectStep>();
            var second = new List<EffectStep>();
            if (TryParseAction($"{gains.Groups[1].Value} gains {gains.Groups[2].Value}{suffix}", first, isStatic, optional)
                && first.Count == 1
                && first[0].Target is { } target
                && TryParseAction($"{gains.Groups[1].Value} gains {gains.Groups[3].Value}{suffix}", second, isStatic, optional))
            {
                parsed.AddRange(first);
                // The second part affects the same cards as the first one.
                foreach (var step in second)
                {
                    parsed.Add(CopyWithTarget(step, target.Self || target.YourLeader || target.All
                        ? step.Target!
                        : new TargetSpec { Previous = true, Filter = target.Filter }));
                }

                return true;
            }
        }

        return false;
    }

    private static EffectStep CopyWithTarget(EffectStep step, TargetSpec target)
    {
        return new EffectStep
        {
            Kind = step.Kind,
            Amount = step.Amount,
            SecondAmount = step.SecondAmount,
            Optional = step.Optional,
            Target = target,
            Filter = step.Filter,
            Zone = step.Zone,
            Duration = step.Duration,
            Flag = step.Flag,
            Rested = step.Rested,
            RestDestination = step.RestDestination,
            PlayRevealed = step.PlayRevealed,
            Position = step.Position,
            Side = step.Side,
            Text = step.Text
        };
    }

    private static Duration ParseDuration(string text, bool isStatic)
    {
        var value = text.Trim().ToLowerInvariant();
        if (value.Contains("during this battle"))
        {
            return Duration.ThisBattle;
        }

        if (value.Contains("during this turn") || value.Contains("at the end of this turn"))
        {
            return Duration.ThisTurn;
        }

        if (value.Contains("until the end of your opponent's next") || value.Contains("until the start of your next turn"))
        {
            return Duration.UntilEndOfOpponentsNextTurn;
        }

        return isStatic ? Duration.Permanent : Duration.Permanent;
    }

    private const string DurationPattern = @"(?<duration> during this turn| during this battle| until the end of your opponent's next (?:turn|End Phase)| until the start of your next turn)?";

    private static bool TryParseAction(string sentence, List<EffectStep> steps, bool isStatic, bool optional)
    {
        Match match;
        var s = sentence.Trim().TrimEnd('.');

        EffectStep Step(StepKind kind, TargetSpec? target = null, int amount = 0, Duration duration = Duration.ThisTurn,
            string flag = "", CardFilter? filter = null, bool rested = false, CardZone zone = CardZone.Field,
            string position = "top", int secondAmount = 0, bool playRevealed = false, PlayerSide side = PlayerSide.You)
        {
            return new EffectStep
            {
                Kind = kind,
                Target = target,
                Amount = amount,
                SecondAmount = secondAmount,
                Duration = duration,
                Flag = flag,
                Filter = filter,
                Rested = rested,
                Zone = zone,
                Position = position,
                PlayRevealed = playRevealed,
                Side = side,
                Optional = optional,
                Text = sentence
            };
        }

        bool Add(EffectStep step)
        {
            steps.Add(step);
            return true;
        }

        // ---------- Card actions with a target ----------

        if ((match = Regex.Match(s, @"^K\.O\. (.+)$", Options)).Success && ParseTarget(match.Groups[1].Value) is { } koTarget)
        {
            return Add(Step(StepKind.KO, koTarget));
        }

        if ((match = Regex.Match(s, @"^Rest (.+)$", Options)).Success && ParseTarget(match.Groups[1].Value) is { } restTarget)
        {
            return Add(Step(StepKind.Rest, restTarget));
        }

        if ((match = Regex.Match(s, @"^Set (.+) as active$", Options)).Success)
        {
            var donActive = Regex.Match(match.Groups[1].Value, @"^up to (\d+) of your DON!! cards?$", Options);
            if (donActive.Success)
            {
                return Add(Step(StepKind.SetDonActive, amount: int.Parse(donActive.Groups[1].Value)));
            }

            if (ParseTarget(match.Groups[1].Value) is { } activeTarget)
            {
                return Add(Step(StepKind.SetActive, activeTarget));
            }
        }

        if ((match = Regex.Match(s, @"^Return (.+?) to (?:the owner's|their owner's|your|its owner's) hand$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } handTarget)
        {
            return Add(Step(StepKind.ReturnToHand, handTarget));
        }

        if ((match = Regex.Match(s, @"^Place (.+?) at the (bottom|top) of (?:the owner's|their owner's|your|its owner's) deck$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } deckTarget)
        {
            return Add(Step(match.Groups[2].Value.Equals("top", StringComparison.OrdinalIgnoreCase) ? StepKind.TopOfDeck : StepKind.BottomOfDeck, deckTarget));
        }

        if ((match = Regex.Match(s, @"^Select (.+)$", Options)).Success && ParseTarget(match.Groups[1].Value) is { } selectTarget)
        {
            return Add(Step(StepKind.Select, selectTarget));
        }

        // "Give up to 1 of your opponent's Characters -2000 power during this turn"
        // Newer cards lost the minus sign in the API, so a number without a sign is negative for the opponent.
        if ((match = Regex.Match(s, @"^Give (.+?) ([+-]?\d+) power" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } giveTarget)
        {
            return Add(Step(StepKind.PowerModifier, giveTarget, SignedAmount(match.Groups[2].Value, giveTarget), ParseDuration(match.Groups["duration"].Value, isStatic)));
        }

        if ((match = Regex.Match(s, @"^Give (.+?) ([+-]?\d+) cost" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } giveCostTarget)
        {
            return Add(Step(StepKind.CostModifier, giveCostTarget, SignedAmount(match.Groups[2].Value, giveCostTarget), ParseDuration(match.Groups["duration"].Value, isStatic)));
        }

        // "Give your Leader or 1 of your Characters up to 1 rested DON!! card"
        if ((match = Regex.Match(s, @"^Give (.+?) up to (\d+) rested DON!! cards?$", Options)).Success
            && ParseDonTarget(match.Groups[1].Value) is { } donTarget1)
        {
            return Add(Step(StepKind.GiveRestedDon, donTarget1, int.Parse(match.Groups[2].Value)));
        }

        if ((match = Regex.Match(s, @"^Give up to (\d+) rested DON!! cards? to (.+)$", Options)).Success
            && ParseDonTarget(match.Groups[2].Value) is { } donTarget2)
        {
            return Add(Step(StepKind.GiveRestedDon, donTarget2, int.Parse(match.Groups[1].Value)));
        }

        // "Up to 1 of your Leader or Character cards gains +1000 power during this turn"
        if ((match = Regex.Match(s, @"^(.+?) (?:gains?|get) (?:an additional )?([+-]?\d+) power" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } gainsTarget)
        {
            return Add(Step(StepKind.PowerModifier, gainsTarget, int.Parse(match.Groups[2].Value), ParseDuration(match.Groups["duration"].Value, isStatic)));
        }

        if ((match = Regex.Match(s, @"^(.+?) (?:gains?|get) ([+-]?\d+) cost" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } costTarget)
        {
            return Add(Step(StepKind.CostModifier, costTarget, int.Parse(match.Groups[2].Value), ParseDuration(match.Groups["duration"].Value, isStatic)));
        }

        if ((match = Regex.Match(s, @"^(.+?) gains? \[(Rush|Blocker|Double Attack|Banish|Unblockable)\]" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } keywordTarget)
        {
            // [Unblockable] = the opponent cannot block this card.
            var keywordFlag = match.Groups[2].Value.Equals("Unblockable", StringComparison.OrdinalIgnoreCase)
                ? EffectFlags.CannotBeBlocked
                : CardKeywords.Normalize(match.Groups[2].Value)!;
            return Add(Step(StepKind.GainFlag, keywordTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic),
                flag: keywordFlag));
        }

        if ((match = Regex.Match(s, @"^(.+?) cannot be K\.O\.'d in battle" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } noKoTarget)
        {
            return Add(Step(StepKind.GainFlag, noKoTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotBeKOdInBattle));
        }

        if ((match = Regex.Match(s, @"^(.+?) cannot be K\.O\.'d by (?:your opponent's )?effects" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } noEffectKoTarget)
        {
            return Add(Step(StepKind.GainFlag, noEffectKoTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotBeKOdByEffects));
        }

        if ((match = Regex.Match(s, @"^(.+?) cannot be rested by (?:your opponent's )?effects" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } noRestTarget)
        {
            return Add(Step(StepKind.GainFlag, noRestTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotBeRestedByEffects));
        }

        if ((match = Regex.Match(s, @"^(.+?) cannot attack" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } noAttackTarget)
        {
            return Add(Step(StepKind.GainFlag, noAttackTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotAttack));
        }

        if ((match = Regex.Match(s, @"^(.+?) can also attack (?:your opponent's )?active Characters" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } activeAttackTarget)
        {
            return Add(Step(StepKind.GainFlag, activeAttackTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CanAttackActiveCharacters));
        }

        if ((match = Regex.Match(s, @"^(.+?) will not become active in your opponent's next Refresh Phase$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } refreshTarget)
        {
            return Add(Step(StepKind.SkipRefresh, refreshTarget));
        }

        if ((match = Regex.Match(s, @"^Your opponent cannot activate \[Blocker\]" + DurationPattern + "$", Options)).Success)
        {
            return Add(Step(StepKind.NegateBlockers, duration: ParseDuration(match.Groups["duration"].Value, isStatic)));
        }

        // "Your opponent cannot activate [Blocker] if that Leader or Character attacks during this turn"
        if ((match = Regex.Match(s, @"^Your opponent cannot activate (?:a )?\[Blocker\](?: Character)? (?:if|when|whenever) (.+?) attacks" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } unblockTarget)
        {
            return Add(Step(StepKind.GainFlag, unblockTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotBeBlocked));
        }

        if ((match = Regex.Match(s, @"^Trash (.+)$", Options)).Success
            && !Regex.IsMatch(match.Groups[1].Value, @"from (your|their) (hand|deck)|from the top|Life|^\d+ cards?$|the rest", Options)
            && ParseTarget(match.Groups[1].Value) is { } trashTarget)
        {
            return Add(Step(StepKind.TrashCard, trashTarget));
        }

        // ---------- Player actions ----------

        if ((match = Regex.Match(s, @"^Draw (\d+) cards?$", Options)).Success)
        {
            return Add(Step(StepKind.Draw, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Draw (\d+) cards? and trash (\d+) cards? from your hand$", Options)).Success)
        {
            steps.Add(Step(StepKind.Draw, amount: int.Parse(match.Groups[1].Value)));
            optional = false;
            return Add(Step(StepKind.TrashFromHand, amount: int.Parse(match.Groups[2].Value), filter: new CardFilter { AllowCharacter = true }));
        }

        if ((match = Regex.Match(s, @"^Trash (\d+) (.*?)cards? from your hand$", Options)).Success)
        {
            var filter = ParseCardDescription(match.Groups[2].Value.Trim(), out var ok);
            if (ok)
            {
                return Add(Step(StepKind.TrashFromHand, amount: int.Parse(match.Groups[1].Value), filter: filter));
            }
        }

        if ((match = Regex.Match(s, @"^Your opponent trashes (\d+) cards? from their hand$", Options)).Success)
        {
            return Add(Step(StepKind.OpponentTrashesFromHand, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Add up to (\d+) DON!! cards? from your DON!! deck and (rest (?:it|them)|set (?:it|them) as active)$", Options)).Success)
        {
            return Add(Step(StepKind.AddDonFromDeck, amount: int.Parse(match.Groups[1].Value),
                rested: match.Groups[2].Value.StartsWith("rest", StringComparison.OrdinalIgnoreCase)));
        }

        if ((match = Regex.Match(s, @"^Return (\d+) DON!! cards? from your field to your DON!! deck$", Options)).Success)
        {
            return Add(Step(StepKind.ReturnDonToDeck, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Trash (\d+) cards? from the top of your deck$", Options)).Success)
        {
            return Add(Step(StepKind.TrashTopOfDeck, amount: int.Parse(match.Groups[1].Value)));
        }

        // "Look at 5 cards from the top of your deck; reveal up to 1 {Straw Hat Crew} type card and add it to your hand"
        if ((match = Regex.Match(s, @"^Look at (\d+) cards from the top of your deck; reveal up to (\d+) (.+?) and (add (?:it|them) to your hand|play (?:it|them))(?: rested)?$", Options)).Success)
        {
            var filter = ParseCardDescription(match.Groups[3].Value.Trim(), out var ok);
            if (ok)
            {
                return Add(Step(StepKind.LookAndReveal, amount: int.Parse(match.Groups[1].Value), secondAmount: int.Parse(match.Groups[2].Value),
                    filter: filter, playRevealed: match.Groups[4].Value.StartsWith("play", StringComparison.OrdinalIgnoreCase)));
            }
        }

        if ((match = Regex.Match(s, @"^Look at (\d+) cards from the top of your deck and place them at the top or bottom of (?:the|your) deck in any order$", Options)).Success)
        {
            return Add(Step(StepKind.LookAndArrange, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Look at (\d+) cards from the top of your deck and place them at the top of your deck in any order$", Options)).Success)
        {
            return Add(Step(StepKind.LookAndArrange, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Add (?:up to )?(\d+) cards? from the top (?:or bottom )?of your Life cards to your hand$", Options)).Success)
        {
            return Add(Step(StepKind.LifeToHand, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Add (?:up to )?(\d+) cards? from the top of your deck to the top of your Life cards$", Options)).Success)
        {
            return Add(Step(StepKind.DeckTopToLife, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Add (?:up to )?(\d+) cards? from your hand to the top of your Life cards$", Options)).Success)
        {
            return Add(Step(StepKind.HandToLife, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Trash (?:up to )?(\d+) cards? from the top of your opponent's Life cards$", Options)).Success)
        {
            return Add(Step(StepKind.TrashLife, amount: int.Parse(match.Groups[1].Value), side: PlayerSide.Opponent));
        }

        if ((match = Regex.Match(s, @"^Trash (?:up to )?(\d+) cards? from the top of your Life cards$", Options)).Success)
        {
            return Add(Step(StepKind.TrashLife, amount: int.Parse(match.Groups[1].Value)));
        }

        if (Regex.IsMatch(s, @"^Play this (?:card|Character|Stage)$", Options))
        {
            return Add(Step(StepKind.PlayThisCard));
        }

        if ((match = Regex.Match(s, @"^Play this (?:card|Character) rested$", Options)).Success)
        {
            return Add(Step(StepKind.PlayThisCard, rested: true));
        }

        if (Regex.IsMatch(s, @"^Activate this card's \[Main\] effect$", Options))
        {
            return Add(Step(StepKind.ActivateOwnAbility, flag: "Main"));
        }

        // "Play up to 1 {Straw Hat Crew} type Character card with a cost of 3 or less from your hand"
        if ((match = Regex.Match(s, @"^Play up to (\d+) (.+?) from your (hand|trash)( rested)?$", Options)).Success)
        {
            var filter = ParseCardDescription(match.Groups[2].Value.Trim(), out var ok);
            if (ok)
            {
                var zone = match.Groups[3].Value.Equals("trash", StringComparison.OrdinalIgnoreCase) ? CardZone.Trash : CardZone.Hand;
                return Add(Step(StepKind.PlayCard, amount: int.Parse(match.Groups[1].Value), filter: filter, zone: zone,
                    rested: match.Groups[4].Success && match.Groups[4].Value.Length > 0));
            }
        }

        if ((match = Regex.Match(s, @"^Play up to (\d+) (.+?) from your (hand|trash) rested$", Options)).Success)
        {
            var filter = ParseCardDescription(match.Groups[2].Value.Trim(), out var ok);
            if (ok)
            {
                return Add(Step(StepKind.PlayCard, amount: int.Parse(match.Groups[1].Value), filter: filter,
                    zone: match.Groups[3].Value.Equals("trash", StringComparison.OrdinalIgnoreCase) ? CardZone.Trash : CardZone.Hand, rested: true));
            }
        }

        if ((match = Regex.Match(s, @"^Activate this card's \[(Main|On Play|On K\.O\.|Counter|When Attacking)\] effect$", Options)).Success)
        {
            return Add(Step(StepKind.ActivateOwnAbility, flag: match.Groups[1].Value));
        }

        if ((match = Regex.Match(s, @"^Set up to (\d+) of your DON!! cards as active at the end of this turn$", Options)).Success)
        {
            return Add(Step(StepKind.SetDonActiveAtEndOfTurn, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Rest up to (\d+) of your opponent's DON!! cards?$", Options)).Success)
        {
            return Add(Step(StepKind.RestOpponentDon, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Also treat this card's name as \[([^\]]+)\] according to the rules$", Options)).Success)
        {
            return Add(Step(StepKind.NameAlias, flag: match.Groups[1].Value));
        }

        if ((match = Regex.Match(s, @"^Under the rules of this game, you may have any number of this card in your deck$", Options)).Success)
        {
            // Deck building rule only - nothing happens in the match.
            return Add(Step(StepKind.NameAlias, flag: string.Empty));
        }

        if ((match = Regex.Match(s, @"^Draw (\d+) cards? and place (\d+) cards? from your hand at the (?:top or )?bottom of your deck(?: in any order)?$", Options)).Success)
        {
            steps.Add(Step(StepKind.Draw, amount: int.Parse(match.Groups[1].Value)));
            return Add(Step(StepKind.HandToDeckBottom, amount: int.Parse(match.Groups[2].Value)));
        }

        if ((match = Regex.Match(s, @"^Place (\d+) cards? from your hand at the (?:top or )?bottom of your deck(?: in any order)?$", Options)).Success)
        {
            return Add(Step(StepKind.HandToDeckBottom, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Your opponent places (\d+) cards? from their hand at the bottom of their deck(?: in any order)?$", Options)).Success)
        {
            return Add(Step(StepKind.OpponentHandToDeckBottom, amount: int.Parse(match.Groups[1].Value)));
        }

        if ((match = Regex.Match(s, @"^Add up to (\d+) (.+?) from your trash to your hand$", Options)).Success
            && TryCardDescription(match.Groups[2].Value, out var trashFilter))
        {
            return Add(Step(StepKind.TrashToHand, amount: int.Parse(match.Groups[1].Value), filter: trashFilter, zone: CardZone.Trash));
        }

        if ((match = Regex.Match(s, @"^Trash up to (\d+) cards? from your hand$", Options)).Success)
        {
            optional = true;
            return Add(Step(StepKind.TrashFromHand, amount: int.Parse(match.Groups[1].Value), filter: new CardFilter()));
        }

        if ((match = Regex.Match(s, @"^Your opponent returns (\d+) DON!! cards? from their field to their DON!! deck$", Options)).Success)
        {
            return Add(Step(StepKind.ReturnDonToDeck, amount: int.Parse(match.Groups[1].Value), side: PlayerSide.Opponent));
        }

        if ((match = Regex.Match(s, @"^Add (?:up to )?(\d+) cards? from the top of your opponent's Life cards to (?:the owner's|their) hand$", Options)).Success)
        {
            return Add(Step(StepKind.LifeToHand, amount: int.Parse(match.Groups[1].Value), side: PlayerSide.Opponent));
        }

        if ((match = Regex.Match(s, @"^(.+?) cannot be rested" + DurationPattern + "$", Options)).Success
            && ParseTarget(match.Groups[1].Value) is { } noRestAnyTarget)
        {
            // The opponent's Characters cannot be rested -> they can neither attack nor block.
            steps.Add(Step(StepKind.GainFlag, noRestAnyTarget, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotAttack));
            return Add(Step(StepKind.GainFlag, new TargetSpec { Previous = true }, duration: ParseDuration(match.Groups["duration"].Value, isStatic), flag: EffectFlags.CannotBlock));
        }

        if ((match = Regex.Match(s, @"^Look at (\d+) cards from the top of your deck and add up to (\d+) cards? to your hand$", Options)).Success)
        {
            return Add(Step(StepKind.LookAndReveal, amount: int.Parse(match.Groups[1].Value), secondAmount: int.Parse(match.Groups[2].Value), filter: new CardFilter()));
        }

        if ((match = Regex.Match(s, @"^Play up to (\d+) (.+?) from your hand or trash$", Options)).Success
            && TryCardDescription(match.Groups[2].Value, out var handOrTrashFilter))
        {
            return Add(Step(StepKind.PlayCard, amount: int.Parse(match.Groups[1].Value), filter: handOrTrashFilter, zone: CardZone.HandOrTrash));
        }

        if ((match = Regex.Match(s, @"^This (?:Character|card) can attack Characters on the turn in which it is played$", Options)).Success)
        {
            return Add(Step(StepKind.GainFlag, new TargetSpec { Self = true, UpTo = false }, duration: Duration.Permanent, flag: EffectFlags.RushCharacter));
        }

        return false;
    }

    // "2000" without a sign: negative when it targets the opponent's cards (the API lost the "-").
    private static int SignedAmount(string value, TargetSpec target)
    {
        var amount = int.Parse(value);
        var hasSign = value.StartsWith('+') || value.StartsWith('-');
        return !hasSign && target.Filter.Owner == PlayerSide.Opponent ? -amount : amount;
    }

    #endregion

    #region TARGETS

    private static TargetSpec? ParseDonTarget(string text)
    {
        var value = text.Trim();
        if (Regex.IsMatch(value, @"^(?:your Leader or|your Leader and) (?:1|up to 1) of your Characters$", Options))
        {
            return new TargetSpec { Count = 1, UpTo = false, Filter = new CardFilter { Owner = PlayerSide.You, AllowLeader = true } };
        }

        return ParseTarget(value);
    }

    // Parses texts like:
    //   "up to 1 of your opponent's Characters with a cost of 3 or less"
    //   "all of your {Straw Hat Crew} type Characters"
    //   "this Character" / "your Leader" / "that Character"
    public static TargetSpec? ParseTarget(string text)
    {
        var value = text.Trim().TrimEnd('.');

        if (Regex.IsMatch(value, @"^this (Character|Leader|Stage|card)$", Options))
        {
            return new TargetSpec { Self = true, UpTo = false };
        }

        if (Regex.IsMatch(value, @"^your Leader$", Options))
        {
            return new TargetSpec { YourLeader = true, UpTo = false };
        }

        if (Regex.IsMatch(value, @"^(that|the selected|the played|the revealed) (Character|Leader or Character|card|Leader)$", Options))
        {
            return new TargetSpec { Previous = true, UpTo = false };
        }

        if (Regex.IsMatch(value, @"^your opponent's Leader$", Options))
        {
            return new TargetSpec
            {
                All = true,
                UpTo = false,
                Filter = new CardFilter { Owner = PlayerSide.Opponent, AllowLeader = true, AllowCharacter = false }
            };
        }

        if (Regex.IsMatch(value, @"^your Leader and all of your Characters$", Options))
        {
            return new TargetSpec { All = true, UpTo = false, Filter = new CardFilter { Owner = PlayerSide.You, AllowLeader = true } };
        }

        var match = Regex.Match(value,
            @"^(?:(?<all>all)(?: of)?|up to (?:a total of )?(?<upto>\d+)(?: of)?|(?<exact>\d+) of|(?<one>a|an))?\s*(?<owner>your opponent's|your|the opponent's)?\s*(?<desc>.*)$",
            Options);
        if (!match.Success)
        {
            return null;
        }

        var hasCount = match.Groups["all"].Success || match.Groups["upto"].Success || match.Groups["exact"].Success || match.Groups["one"].Success;
        var owner = match.Groups["owner"].Value.ToLowerInvariant() switch
        {
            "your opponent's" or "the opponent's" => PlayerSide.Opponent,
            "your" => PlayerSide.You,
            _ => PlayerSide.Any
        };

        var description = match.Groups["desc"].Value.Trim();
        var filter = ParseFieldDescription(description, out var plural);
        if (filter is null)
        {
            return null;
        }

        filter.Owner = owner;

        // "your {X} type Characters" without a number means all of them.
        var all = match.Groups["all"].Success || (!hasCount && plural);
        if (!hasCount && !plural)
        {
            // "K.O. Character with ..." without a number is unusual - do not guess.
            return null;
        }

        var count = match.Groups["upto"].Success ? int.Parse(match.Groups["upto"].Value)
            : match.Groups["exact"].Success ? int.Parse(match.Groups["exact"].Value)
            : 1;

        return new TargetSpec
        {
            All = all,
            Count = all ? int.MaxValue : count,
            UpTo = !match.Groups["exact"].Success && !match.Groups["one"].Success,
            Filter = filter
        };
    }

    // "rested Characters with a cost of 3 or less" -> filter for field cards.
    private static CardFilter? ParseFieldDescription(string text, out bool plural)
    {
        plural = false;
        var filter = new CardFilter();
        var rest = text.Trim();

        rest = ReadAdjectives(rest, filter);

        var kind = Regex.Match(rest,
            @"^(?<kind>Leader or Character cards?|Leaders? or Characters?|Leader and Character cards|Leader or Stage cards?|Character or Stage cards?|Character cards?|Characters?|Leaders?|Stages?|cards?|DON!! cards?)(?<rest>.*)$",
            Options);

        if (!kind.Success)
        {
            // "[Name]" as target: "up to 1 [Monkey.D.Luffy]"
            var nameMatch = Regex.Match(rest, @"^\[(?<name>[^\]]+)\](?<rest>.*)$", Options);
            if (!nameMatch.Success)
            {
                return null;
            }

            filter.Name = nameMatch.Groups["name"].Value;
            return ReadFilterClauses(nameMatch.Groups["rest"].Value, filter) ? filter : null;
        }

        var kindText = kind.Groups["kind"].Value.ToLowerInvariant();
        if (kindText.StartsWith("don"))
        {
            return null;
        }

        plural = kindText.EndsWith('s');
        filter.AllowLeader = kindText.Contains("leader") || kindText.StartsWith("card");
        filter.AllowCharacter = kindText.Contains("character") || kindText.StartsWith("card");
        filter.AllowStage = kindText.Contains("stage") || kindText.StartsWith("card");
        if (kindText.StartsWith("leader") && !kindText.Contains("character"))
        {
            filter.AllowCharacter = false;
        }

        return ReadFilterClauses(kind.Groups["rest"].Value, filter) ? filter : null;
    }

    private static readonly string[] Colors = ["red", "green", "blue", "purple", "black", "yellow"];

    private static string ReadAdjectives(string text, CardFilter filter)
    {
        var rest = text.Trim();
        while (true)
        {
            Match match;
            if ((match = Regex.Match(rest, @"^rested\s+", Options)).Success)
            {
                filter.Rested = true;
            }
            else if ((match = Regex.Match(rest, @"^active\s+", Options)).Success)
            {
                filter.Rested = false;
            }
            else if ((match = Regex.Match(rest, @"^\{([^}]+)\} or \{([^}]+)\} type(?:\s+|$)", Options)).Success)
            {
                // "{A} or {B} type": one of both types.
                filter.TypeIncludes = $"{match.Groups[1].Value}|{match.Groups[2].Value}";
            }
            else if ((match = Regex.Match(rest, @"^\[([^\]]+)\] or \{([^}]+)\} type(?:\s+|$)", Options)).Success)
            {
                // "[Nami] or {Straw Hat Crew} type card": the name OR the type.
                filter.Name = match.Groups[1].Value;
                filter.TypeIncludes = match.Groups[2].Value;
                filter.NameOrType = true;
            }
            else if ((match = Regex.Match(rest, @"^\{([^}]+)\} type(?:\s+|$)", Options)).Success)
            {
                filter.TypeIncludes = match.Groups[1].Value;
            }
            else if ((match = Regex.Match(rest, @"^\[([^\]]+)\]\s+(?=cards?\b|Characters?\b)", Options)).Success
                && CardKeywords.Normalize(match.Groups[1].Value) is null)
            {
                // "your [Monkey.D.Luffy] cards"
                filter.Name = match.Groups[1].Value;
            }
            else if ((match = Regex.Match(rest, @"^\[(Blocker|Rush|Double Attack|Banish)\]\s+", Options)).Success)
            {
                filter.Keyword = CardKeywords.Normalize(match.Groups[1].Value)!;
            }
            else if ((match = Regex.Match(rest, @"^(red|green|blue|purple|black|yellow)\s+", Options)).Success)
            {
                filter.Color = match.Groups[1].Value;
            }
            else if ((match = Regex.Match(rest, @"^<([^>]+)> attribute\s+", Options)).Success)
            {
                filter.Attribute = match.Groups[1].Value;
            }
            else
            {
                return rest;
            }

            rest = rest[match.Length..];
        }
    }

    // "with a cost of 3 or less", "with 5000 power or less", "other than this Character"...
    private static bool ReadFilterClauses(string text, CardFilter filter)
    {
        var rest = text.Trim();
        while (rest.Length > 0)
        {
            Match match;
            if ((match = Regex.Match(rest, @"^(?:with|and) a (base )?cost of (\d+) or less\s*", Options)).Success)
            {
                filter.MaxCost = int.Parse(match.Groups[2].Value);
                filter.UseBaseValues |= match.Groups[1].Success && match.Groups[1].Value.Length > 0;
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) a (base )?cost of (\d+) or more\s*", Options)).Success)
            {
                filter.MinCost = int.Parse(match.Groups[2].Value);
                filter.UseBaseValues |= match.Groups[1].Success && match.Groups[1].Value.Length > 0;
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) a (base )?cost of (\d+)\s*", Options)).Success)
            {
                filter.MaxCost = int.Parse(match.Groups[2].Value);
                filter.MinCost = filter.MaxCost;
                filter.UseBaseValues |= match.Groups[1].Success && match.Groups[1].Value.Length > 0;
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) (?:a )?(base )?(?:power of )?(\d+)(?: base)? power or less\s*", Options)).Success
                || (match = Regex.Match(rest, @"^(?:with|and) (?:a )?(base )?power of (\d+) or less\s*", Options)).Success)
            {
                filter.MaxPower = int.Parse(match.Groups[2].Value);
                filter.UseBaseValues |= match.Groups[1].Success && match.Groups[1].Value.Length > 0 || match.Value.Contains("base", StringComparison.OrdinalIgnoreCase);
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) (?:a )?(base )?(\d+)(?: base)? power or more\s*", Options)).Success)
            {
                filter.MinPower = int.Parse(match.Groups[2].Value);
                filter.UseBaseValues |= match.Value.Contains("base", StringComparison.OrdinalIgnoreCase);
            }
            else if ((match = Regex.Match(rest, @"^with a cost equal to or less than the number of DON!! cards on your field\s*", Options)).Success)
            {
                filter.MaxCostFrom = "yourDon";
            }
            else if ((match = Regex.Match(rest, @"^with a cost equal to or less than the number of your opponent's Life cards\s*", Options)).Success)
            {
                filter.MaxCostFrom = "opponentLife";
            }
            else if ((match = Regex.Match(rest, @"^other than this (?:Character|card|Leader)\s*", Options)).Success)
            {
                filter.ExcludeSource = true;
            }
            else if ((match = Regex.Match(rest, @"^other than \[([^\]]+)\]\s*", Options)).Success)
            {
                filter.NameNot = match.Groups[1].Value;
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) (\d+) (base )?power\s*(?=$|and |with |other )", Options)).Success)
            {
                // Exact power: "with 6000 power"
                filter.MinPower = int.Parse(match.Groups[1].Value);
                filter.MaxPower = filter.MinPower;
                filter.UseBaseValues |= match.Groups[2].Success && match.Groups[2].Value.Length > 0;
            }
            else if ((match = Regex.Match(rest, @"^(?:with|and) a \[Trigger\]\s*", Options)).Success)
            {
                filter.RequiresTrigger = true;
            }
            else if ((match = Regex.Match(rest, @"^with a type including \{([^}]+)\}\s*", Options)).Success)
            {
                filter.TypeIncludes = match.Groups[1].Value;
            }
            else if ((match = Regex.Match(rest, @"^that (?:is|are) rested\s*", Options)).Success)
            {
                filter.Rested = true;
            }
            else
            {
                return false;
            }

            rest = rest[match.Length..].Trim();
        }

        return true;
    }

    // Describes cards in hand/deck/trash: "{Straw Hat Crew} type card other than [Nami]",
    // "Character card with a cost of 3 or less", "red Event", "[Monkey.D.Luffy]".
    public static CardFilter ParseCardDescription(string text, out bool ok)
    {
        var filter = new CardFilter { AllowCharacter = true, AllowStage = true, AllowLeader = false };
        var rest = ReadAdjectives(text.Trim(), filter);
        ok = true;

        if (rest.Length == 0)
        {
            return filter;
        }

        var nameMatch = Regex.Match(rest, @"^\[(?<name>[^\]]+)\](?: or \[(?<name2>[^\]]+)\])?\s*(?<rest>.*)$", Options);
        if (nameMatch.Success && !nameMatch.Groups["name2"].Success)
        {
            filter.Name = nameMatch.Groups["name"].Value;
            rest = nameMatch.Groups["rest"].Value;
        }

        var kind = Regex.Match(rest, @"^(?<kind>Character cards?|Characters?|Event cards?|Events?|Stage cards?|Stages?|cards?)?\s*(?<rest>.*)$", Options);
        var kindText = kind.Groups["kind"].Value.ToLowerInvariant();
        if (kindText.StartsWith("character"))
        {
            filter.Category = CardInstance.CharacterCategory;
        }
        else if (kindText.StartsWith("event"))
        {
            filter.Category = CardInstance.EventCategory;
        }
        else if (kindText.StartsWith("stage"))
        {
            filter.Category = CardInstance.StageCategory;
        }

        ok = ReadFilterClauses(kind.Groups["rest"].Value, filter);
        return filter;
    }

    #endregion

    #region CONDITIONS

    private static bool TryParseConditions(string text, List<EffectCondition> conditions)
    {
        var parts = Regex.Split(text.Trim(), @",? and (?=(?:you|your|there|this)\b)", Options);
        foreach (var part in parts)
        {
            var condition = ParseCondition(part.Trim());
            if (condition is null)
            {
                return false;
            }

            conditions.Add(condition);
        }

        return conditions.Count > 0;
    }

    private static EffectCondition? ParseCondition(string text)
    {
        Match match;
        EffectCondition Make(ConditionKind kind, int amount = 0, CardFilter? filter = null) =>
            new() { Kind = kind, Amount = amount, Filter = filter, Text = text };

        if ((match = Regex.Match(text, @"^your Leader (?:has the|has) \{([^}]+)\} type$", Options)).Success
            || (match = Regex.Match(text, @"^your Leader's type includes \{([^}]+)\}$", Options)).Success)
        {
            return Make(ConditionKind.LeaderHasType, filter: new CardFilter { TypeIncludes = match.Groups[1].Value });
        }

        if ((match = Regex.Match(text, @"^your Leader is \[([^\]]+)\]$", Options)).Success)
        {
            return Make(ConditionKind.LeaderIsName, filter: new CardFilter { Name = match.Groups[1].Value });
        }

        if (Regex.IsMatch(text, @"^your Leader is multicolou?red$", Options))
        {
            return Make(ConditionKind.LeaderIsMulticolored);
        }

        if ((match = Regex.Match(text, @"^your Leader is (red|green|blue|purple|black|yellow)$", Options)).Success)
        {
            return Make(ConditionKind.LeaderColor, filter: new CardFilter { Color = match.Groups[1].Value });
        }

        if ((match = Regex.Match(text, @"^you have (\d+) or (less|fewer|more) Life cards$", Options)).Success)
        {
            return Make(match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase) ? ConditionKind.LifeAtLeast : ConditionKind.LifeAtMost, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^your opponent has (\d+) or (less|fewer|more) Life cards$", Options)).Success)
        {
            return Make(match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase) ? ConditionKind.OpponentLifeAtLeast : ConditionKind.OpponentLifeAtMost, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^you have (\d+) or (less|fewer|more) cards in your hand$", Options)).Success)
        {
            return Make(match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase) ? ConditionKind.HandAtLeast : ConditionKind.HandAtMost, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^your opponent has (\d+) or (less|fewer|more) cards in (?:their|his or her) hand$", Options)).Success)
        {
            return Make(match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase) ? ConditionKind.OpponentHandAtLeast : ConditionKind.OpponentHandAtMost, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^(?:you have|there are) (\d+) or (less|fewer|more) DON!! cards on your field$", Options)).Success)
        {
            return Make(match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase) ? ConditionKind.DonOnFieldAtLeast : ConditionKind.DonOnFieldAtMost, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^your opponent has (\d+) or more DON!! cards on their field$", Options)).Success)
        {
            return Make(ConditionKind.OpponentDonOnFieldAtLeast, int.Parse(match.Groups[1].Value));
        }

        if (Regex.IsMatch(text, @"^your opponent has more DON!! cards on their field than you$", Options))
        {
            return Make(ConditionKind.OpponentHasMoreDon);
        }

        if (Regex.IsMatch(text, @"^the number of DON!! cards on your field is equal to or less than the number on your opponent's field$", Options))
        {
            return Make(ConditionKind.DonNotMoreThanOpponent);
        }

        if (Regex.IsMatch(text, @"^you have less Life cards than your opponent$", Options))
        {
            return Make(ConditionKind.LifeLessThanOpponent);
        }

        if ((match = Regex.Match(text, @"^you have (\d+) or more cards in your trash$", Options)).Success)
        {
            return Make(ConditionKind.TrashAtLeast, int.Parse(match.Groups[1].Value));
        }

        if ((match = Regex.Match(text, @"^(?:you have|there are) (\d+) or (less|fewer) cards in your deck$", Options)).Success)
        {
            return Make(ConditionKind.DeckAtMost, int.Parse(match.Groups[1].Value));
        }

        if (Regex.IsMatch(text, @"^this (Character|Leader|card) is rested$", Options))
        {
            return Make(ConditionKind.ThisIsRested);
        }

        if (Regex.IsMatch(text, @"^this (Character|Leader|card) is active$", Options))
        {
            return Make(ConditionKind.ThisIsActive);
        }

        if ((match = Regex.Match(text, @"^this (?:Character|Leader) has (\d+) or more DON!! cards given$", Options)).Success)
        {
            return Make(ConditionKind.GivenDonAtLeast, int.Parse(match.Groups[1].Value));
        }

        // "you have 2 or more Characters" / "you have 3 or more {X} type Characters"
        if ((match = Regex.Match(text, @"^you have (\d+) or (more|less|fewer) (.+)$", Options)).Success)
        {
            var filter = ParseFieldDescription(match.Groups[3].Value, out _);
            if (filter is not null)
            {
                filter.Owner = PlayerSide.You;
                var more = match.Groups[2].Value.Equals("more", StringComparison.OrdinalIgnoreCase);
                return Make(more ? ConditionKind.CharactersAtLeast : ConditionKind.CharactersAtMost, int.Parse(match.Groups[1].Value), filter);
            }
        }

        // "you have a {X} type Character" / "you have [Name]"
        if ((match = Regex.Match(text, @"^you (have|don't have|do not have) (?:a |an )?(.+)$", Options)).Success)
        {
            var filter = ParseFieldDescription(match.Groups[2].Value, out _);
            if (filter is not null)
            {
                filter.Owner = PlayerSide.You;
                filter.AllowStage = true;
                return Make(match.Groups[1].Value.Equals("have", StringComparison.OrdinalIgnoreCase) ? ConditionKind.HasCard : ConditionKind.HasNoCard, 1, filter);
            }
        }

        // "your opponent has a Character with a cost of 5 or more"
        if ((match = Regex.Match(text, @"^your opponent has (?:a |an |(\d+) or more )?(.+)$", Options)).Success)
        {
            var filter = ParseFieldDescription(match.Groups[2].Value, out _);
            if (filter is not null)
            {
                filter.Owner = PlayerSide.Opponent;
                var amount = match.Groups[1].Success && match.Groups[1].Value.Length > 0 ? int.Parse(match.Groups[1].Value) : 1;
                return Make(ConditionKind.OpponentHasCard, amount, filter);
            }
        }

        // "there is a Character with a cost of 8 or more"
        if ((match = Regex.Match(text, @"^there is (?:a |an )(.+)$", Options)).Success)
        {
            var filter = ParseFieldDescription(match.Groups[1].Value, out _);
            if (filter is not null)
            {
                filter.Owner = PlayerSide.Any;
                return Make(ConditionKind.HasCard, 1, filter);
            }
        }

        return null;
    }

    #endregion
}
