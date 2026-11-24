using OneSake.Domain;
using OneSake.Game.Randomness;

namespace OneSake.Game.Simulation;

// Chooses evenly among ALL valid decisions (targets, costs, order, counter, block, mulligan,
// turn order). All randomness comes from the seeded IRandom.
public sealed class RandomBot : IBotPlayer
{
    private readonly IRandom _random;

    public RandomBot(IRandom random)
    {
        _random = random;
    }

    public string Name => "random";

    public BotDecision Decide(BotView view, IReadOnlyCollection<string> rejected)
    {
        var candidates = ValidDecisions.For(view).Where(decision => !rejected.Contains(decision.Key)).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No valid decision left for {view.Prompt.Kind}.");
        }

        var decision = candidates[_random.Next(candidates.Count)];

        // Any order of simultaneous effects.
        if (view.Prompt.Kind == "ORDER_TRIGGERS" && decision.Choice?.OrderedIds is { } ids)
        {
            var order = ids.ToList();
            order.Shuffle(_random);
            decision = BotDecision.Answer(view.Prompt, "ORDER", order: order);
        }

        return decision;
    }
}

// A simple player for realistic matches (not a strong one): plays the most expensive card it can
// pay, gives DON!! to the Leader, attacks when the power is enough, blocks a lethal attack and uses
// Counters when the Leader would be hit otherwise.
public sealed class GreedyBot : IBotPlayer
{
    private readonly IRandom _random;

    public GreedyBot(IRandom random)
    {
        _random = random;
    }

    public string Name => "greedy";

    public BotDecision Decide(BotView view, IReadOnlyCollection<string> rejected)
    {
        var candidates = ValidDecisions.For(view).Where(decision => !rejected.Contains(decision.Key)).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No valid decision left for {view.Prompt.Kind}.");
        }

        var preferred = view.Prompt.Kind switch
        {
            "TURN_ORDER_DECISION" => Pick(candidates, "GO_FIRST"),
            "MULLIGAN_DECISION" => Pick(candidates, view.Me.HandCards.Count(card => (card.Cost) <= 3) >= 2 ? "KEEP" : "MULLIGAN"),
            "MAIN_ACTION" => MainAction(view, candidates),
            "BLOCK_STEP" => Block(view, candidates),
            "COUNTER_STEP" => Counter(view, candidates),
            "TRIGGER_DECISION" => Pick(candidates, "ACTIVATE"),
            "EFFECT_CHOICE" => EffectChoice(view, candidates),
            _ => null
        };

        return preferred ?? candidates[_random.Next(candidates.Count)];
    }

    private static BotDecision? Pick(List<BotDecision> candidates, string option)
    {
        return candidates.FirstOrDefault(decision => decision.Choice?.SelectedOption == option);
    }

    private static BotDecision? MainAction(BotView view, List<BotDecision> candidates)
    {
        var me = view.Me;
        var cards = me.HandCards.ToDictionary(card => card.InstanceId);

        // 1. The most expensive playable card (on a full board: replace the weakest Character).
        var play = candidates
            .Where(decision => decision.Choice?.SelectedOption == "PLAY_CARD" && cards.ContainsKey(decision.Choice.SelectedCardInstanceId!))
            .OrderByDescending(decision => cards[decision.Choice!.SelectedCardInstanceId!].Cost)
            .ThenBy(decision => me.BoardCards.FirstOrDefault(card => card.InstanceId == decision.Choice!.TargetCardInstanceId)?.Power ?? 0)
            .FirstOrDefault();
        if (play is not null)
        {
            return play;
        }

        // 2. Remaining DON!! to the Leader.
        var don = candidates.FirstOrDefault(decision => decision.Choice?.SelectedOption == "ATTACH_DON"
            && decision.Choice.SelectedCardInstanceId == me.Leader?.InstanceId);
        if (don is not null)
        {
            return don;
        }

        // 3. Attack when the power is enough: the Leader first, else a rested Character.
        var opponent = view.Opponent;
        var attack = candidates
            .Where(decision => decision.Choice?.SelectedOption == "ATTACK")
            .Select(decision => (Decision: decision, Attacker: FindCard(me, decision.Choice!.SelectedCardInstanceId), Target: FindCard(opponent, decision.Choice.TargetCardInstanceId)))
            .Where(entry => entry.Attacker is not null && entry.Target is not null && (entry.Attacker.Power) >= (entry.Target.Power))
            .OrderByDescending(entry => entry.Target!.Category == "Leader")
            .ThenByDescending(entry => entry.Target!.Power)
            .Select(entry => entry.Decision)
            .FirstOrDefault();

        return attack ?? Pick(candidates, "END_TURN");
    }

    // Blocks only when the attack would end the game.
    private static BotDecision? Block(BotView view, List<BotDecision> candidates)
    {
        var battle = view.State.Battle;
        var lethal = battle is not null
            && battle.TargetInstanceId == view.Me.Leader?.InstanceId
            && view.Me.LifeCount == 0
            && battle.AttackerPower >= battle.TargetPower;

        return lethal
            ? candidates.FirstOrDefault(decision => decision.Choice?.SelectedOption == "BLOCK") ?? Pick(candidates, "NO_BLOCK")
            : Pick(candidates, "NO_BLOCK");
    }

    // Uses the smallest Counter that is still needed when the Leader (or a strong Character) would lose.
    private static BotDecision? Counter(BotView view, List<BotDecision> candidates)
    {
        var battle = view.State.Battle;
        if (battle is null || battle.AttackerPower < battle.TargetPower)
        {
            return Pick(candidates, "PASS");
        }

        var leaderAttacked = battle.TargetInstanceId == view.Me.Leader?.InstanceId;
        if (!leaderAttacked || view.Me.LifeCount > 2)
        {
            return Pick(candidates, "PASS");
        }

        var cards = view.Me.HandCards.ToDictionary(card => card.InstanceId);
        return candidates
            .Where(decision => decision.Choice?.SelectedOption == "COUNTER" && cards.ContainsKey(decision.Choice.SelectedCardInstanceId!))
            .OrderBy(decision => cards[decision.Choice!.SelectedCardInstanceId!].Counter)
            .FirstOrDefault()
            ?? Pick(candidates, "PASS");
    }

    // Pays optional costs, chooses the opponent's strongest card or else the first one.
    private static BotDecision? EffectChoice(BotView view, List<BotDecision> candidates)
    {
        var yes = Pick(candidates, "YES");
        if (yes is not null)
        {
            return yes;
        }

        var opponentCards = new[] { view.Opponent.Leader, view.Opponent.Stage }.Concat(view.Opponent.BoardCards)
            .Where(card => card is not null)
            .ToDictionary(card => card!.InstanceId, card => card!);
        return candidates
            .Where(decision => decision.Choice?.SelectedOption == "SELECT")
            .OrderByDescending(decision => opponentCards.TryGetValue(decision.Choice!.SelectedCardInstanceId!, out var card) ? (card.Power) + 100_000 : 0)
            .FirstOrDefault()
            ?? Pick(candidates, "DONE");
    }

    private static CardInstanceDto? FindCard(PlayerStateDto player, string? instanceId)
    {
        return player.Leader?.InstanceId == instanceId
            ? player.Leader
            : player.BoardCards.FirstOrDefault(card => card.InstanceId == instanceId);
    }
}
