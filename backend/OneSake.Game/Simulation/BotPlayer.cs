using OneSake.Domain;

namespace OneSake.Game.Simulation;

// What a bot sees: the same state and prompt a real player gets over SignalR.
public sealed record BotView(GameStateDto State, ChoicePromptDto Prompt, string PlayerId)
{
    public PlayerStateDto Me => State.Players.Single(player => player.PlayerId == PlayerId);
    public PlayerStateDto Opponent => State.Players.Single(player => player.PlayerId != PlayerId);
    public bool IsMyTurn => State.ActivePlayerId == PlayerId;
}

// A decision: an answer to the open prompt.
public sealed record BotDecision
{
    public ChoiceSubmissionDto? Choice { get; init; }

    // Used to remember rejected decisions (records with lists do not compare by value).
    public string Key => Choice is { } choice
        ? $"{choice.SelectedOption}|{choice.SelectedCardInstanceId}|{choice.TargetCardInstanceId}|{string.Join(',', choice.OrderedIds ?? [])}"
        : string.Empty;

    public static BotDecision Answer(ChoicePromptDto prompt, string option, string? selected = null, string? target = null, IReadOnlyList<string>? order = null)
    {
        return new BotDecision
        {
            Choice = new ChoiceSubmissionDto
            {
                ChoiceId = prompt.ChoiceId,
                PlayerId = prompt.PlayerId,
                SelectedOption = option,
                SelectedCardInstanceId = selected,
                TargetCardInstanceId = target,
                OrderedIds = order
            }
        };
    }

    public override string ToString() => Key;
}

// A bot uses exactly the actions and prompts of a real player - no shortcuts past the logic.
public interface IBotPlayer
{
    string Name { get; }

    // rejected: decisions of this prompt the server did not accept (the bot must choose another one).
    BotDecision Decide(BotView view, IReadOnlyCollection<string> rejected);
}

// All decisions that are allowed for the open prompt, as far as a client can know it.
// The server checks everything again (e.g. "cannot attack" flags that a client does not see).
public static class ValidDecisions
{
    public static IReadOnlyList<BotDecision> For(BotView view)
    {
        var prompt = view.Prompt;
        var decisions = new List<BotDecision>();

        switch (prompt.Kind)
        {
            case "MAIN_ACTION":
                AddMainActions(view, decisions);
                break;

            case "BLOCK_STEP":
                decisions.Add(BotDecision.Answer(prompt, "NO_BLOCK"));
                foreach (var blocker in view.Me.BoardCards.Where(card => CanBlock(card, view.State.Battle)))
                {
                    decisions.Add(BotDecision.Answer(prompt, "BLOCK", blocker.InstanceId));
                }

                break;

            case "COUNTER_STEP":
                decisions.Add(BotDecision.Answer(prompt, "PASS"));
                foreach (var card in view.Me.HandCards.Where(card => IsUsableCounter(card, view.Me)))
                {
                    decisions.Add(BotDecision.Answer(prompt, "COUNTER", card.InstanceId));
                }

                break;

            case "EFFECT_CHOICE":
                foreach (var option in prompt.Options)
                {
                    if (option == "SELECT")
                    {
                        decisions.AddRange(prompt.Cards.Select(card => BotDecision.Answer(prompt, "SELECT", card.InstanceId)));
                    }
                    else
                    {
                        decisions.Add(BotDecision.Answer(prompt, option));
                    }
                }

                break;

            case "ORDER_TRIGGERS":
                decisions.Add(BotDecision.Answer(prompt, "ORDER", order: prompt.Triggers.Select(trigger => trigger.Id).ToList()));
                break;

            default:
                // Turn order, mulligan, [Trigger] decision, "resolve manually" (DONE) ...
                decisions.AddRange(prompt.Options.Select(option => BotDecision.Answer(prompt, option)));
                break;
        }

        return decisions;
    }

    public static bool CanPlay(CardInstanceDto card, PlayerStateDto me)
    {
        if ((card.Cost) > me.ActiveDonCount)
        {
            return false;
        }

        // Events need a [Main] effect to be played in the main phase.
        return card.Category != "Event" || (card.Text ?? string.Empty).Contains("[Main]", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUsableCounter(CardInstanceDto card, PlayerStateDto me)
    {
        return (card.Category == "Character" && (card.Counter) > 0)
            || (card.IsCounterEvent == true && (card.Cost) <= me.ActiveDonCount);
    }

    public static bool CanBlock(CardInstanceDto card, BattleDto? battle)
    {
        return card.Keywords?.Contains("Blocker") == true
            && card.IsRested != true
            && card.InstanceId != battle?.TargetInstanceId;
    }

    // Leader first, then the Characters.
    public static IEnumerable<CardInstanceDto> Attackers(PlayerStateDto me)
    {
        return (me.Leader is null ? me.BoardCards : me.BoardCards.Prepend(me.Leader)).Where(card => card.CanAttack);
    }

    public static IEnumerable<CardInstanceDto> AttackTargets(PlayerStateDto opponent, CardInstanceDto attacker)
    {
        return (opponent.Leader is null || attacker.CanAttackOnlyCharacters ? [] : new[] { opponent.Leader })
            .Concat(opponent.BoardCards.Where(card => card.IsRested == true));
    }

    private static void AddMainActions(BotView view, List<BotDecision> decisions)
    {
        var prompt = view.Prompt;
        var me = view.Me;

        if (prompt.Options.Contains("PLAY_CARD"))
        {
            foreach (var card in me.HandCards.Where(card => CanPlay(card, me)))
            {
                // Rule 3-7-6-1: with 5 Characters one of them is trashed for the new one.
                if (card.Category == "Character" && me.BoardCards.Count >= 5)
                {
                    decisions.AddRange(me.BoardCards.Select(old => BotDecision.Answer(prompt, "PLAY_CARD", card.InstanceId, old.InstanceId)));
                }
                else
                {
                    decisions.Add(BotDecision.Answer(prompt, "PLAY_CARD", card.InstanceId));
                }
            }
        }

        if (prompt.Options.Contains("ATTACH_DON") && me.ActiveDonCount > 0)
        {
            foreach (var target in (me.Leader is null ? me.BoardCards : me.BoardCards.Prepend(me.Leader)))
            {
                decisions.Add(BotDecision.Answer(prompt, "ATTACH_DON", target.InstanceId));
            }
        }

        if (prompt.Options.Contains("ATTACK"))
        {
            foreach (var attacker in Attackers(me))
            {
                decisions.AddRange(AttackTargets(view.Opponent, attacker).Select(target => BotDecision.Answer(prompt, "ATTACK", attacker.InstanceId, target.InstanceId)));
            }
        }

        if (prompt.Options.Contains("ACTIVATE"))
        {
            var field = new[] { me.Leader, me.Stage }.Concat(me.BoardCards).Where(card => card?.CanActivate == true);
            decisions.AddRange(field.Select(card => BotDecision.Answer(prompt, "ACTIVATE", card!.InstanceId)));
        }

        decisions.Add(BotDecision.Answer(prompt, "END_TURN"));
    }
}
