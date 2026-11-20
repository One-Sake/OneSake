namespace OneSake.Game.Rules;

// Decides which effect is resolved next (rule 8-6). The EffectEngine then resolves that one effect.
//
// Effects that become pending "at the same time" (by the same action, or while one effect
// resolves - rule 8-6-3) are collected and then added as one batch:
//   - Rule 8-6-1: first the turn player's effects, then the non-turn player's.
//   - A batch is added behind everything that is already waiting. So when the turn player's
//     effect A creates effect C, the non-turn player's waiting effects and the turn player's
//     effect B come first (rules 8-6-1 and 8-6-1-1).
//   - If one player has more than one effect in a batch, that player chooses the order
//     (rule 6-6-1-1-3 / 6-6-1-1-4 for the End Phase; see docs/rules-open-questions.md).
// Effects the player starts on purpose (Event, [Counter], [Trigger], [Activate: Main]) are
// resolved at once (rule 8-4-1), before the waiting ones.
public sealed class TriggerQueue
{
    private readonly List<PendingTrigger> _collecting = [];
    private readonly List<TriggerGroup> _groups = [];

    public bool IsEmpty => _collecting.Count == 0 && _groups.Count == 0;

    // Every effect that is still waiting (for the client and tests).
    public IEnumerable<PendingTrigger> Waiting => _groups.SelectMany(group => group.Triggers).Concat(_collecting);

    public void Collect(IEnumerable<PendingTrigger> triggers)
    {
        _collecting.AddRange(triggers);
    }

    // Player-started effects: resolved next, in the order given (rule 1-3-7: card text order).
    public void ResolveNow(IReadOnlyList<PendingTrigger> triggers)
    {
        if (triggers.Count > 0)
        {
            _groups.Insert(0, new TriggerGroup(triggers[0].ControllerId, triggers.ToList(), Ordered: true));
        }
    }

    // Ends the current "at the same time" batch and puts it behind the waiting effects.
    public void CloseBatch(string turnPlayerId)
    {
        if (_collecting.Count == 0)
        {
            return;
        }

        // Rule 8-6-1 (and 1-3-10): turn player first, then the non-turn player.
        var turnPlayer = _collecting.Where(trigger => trigger.ControllerId == turnPlayerId).ToList();
        var otherPlayer = _collecting.Where(trigger => trigger.ControllerId != turnPlayerId).ToList();
        _collecting.Clear();

        foreach (var triggers in new[] { turnPlayer, otherPlayer })
        {
            if (triggers.Count > 0)
            {
                _groups.Add(new TriggerGroup(triggers[0].ControllerId, triggers, Ordered: triggers.Count == 1));
            }
        }
    }

    // The next step: an effect to resolve, a player who has to choose the order, or nothing.
    public TriggerQueueStep? Next()
    {
        while (_groups.Count > 0)
        {
            var group = _groups[0];
            if (group.Triggers.Count == 0)
            {
                _groups.RemoveAt(0);
                continue;
            }

            if (!group.Ordered)
            {
                return new TriggerQueueStep(null, group);
            }

            var trigger = group.Triggers[0];
            group.Triggers.RemoveAt(0);
            group.Resolved += 1;
            return new TriggerQueueStep(trigger, group);
        }

        return null;
    }

    // The player's answer to "in which order?": all IDs of the group, first = resolved first.
    public void SetOrder(string playerId, IReadOnlyList<string> orderedIds)
    {
        var group = _groups.FirstOrDefault()
            ?? throw new InvalidOperationException("There is no order to choose.");

        if (group.Ordered || group.PlayerId != playerId)
        {
            throw new InvalidOperationException("You cannot choose the order of these effects.");
        }

        var ids = group.Triggers.Select(trigger => trigger.Id).ToHashSet(StringComparer.Ordinal);
        if (orderedIds.Count != ids.Count || !orderedIds.All(ids.Contains) || orderedIds.Distinct().Count() != ids.Count)
        {
            throw new InvalidOperationException("Put every effect in the order exactly once.");
        }

        var byId = group.Triggers.ToDictionary(trigger => trigger.Id, StringComparer.Ordinal);
        group.Triggers.Clear();
        group.Triggers.AddRange(orderedIds.Select(id => byId[id]));
        group.Ordered = true;
    }

    public void Clear()
    {
        _collecting.Clear();
        _groups.Clear();
    }
}

// The effects of one player from one batch. Size/Resolved are used for "1/3" in the log.
public sealed class TriggerGroup
{
    public TriggerGroup(string playerId, List<PendingTrigger> triggers, bool Ordered)
    {
        PlayerId = playerId;
        Triggers = triggers;
        this.Ordered = Ordered;
        Size = triggers.Count;
    }

    public string PlayerId { get; }
    public List<PendingTrigger> Triggers { get; }
    public bool Ordered { get; set; }
    public int Size { get; }
    public int Resolved { get; set; }
}

// Trigger = null: the group's player has to choose the order first.
public sealed record TriggerQueueStep(PendingTrigger? Trigger, TriggerGroup Group);
