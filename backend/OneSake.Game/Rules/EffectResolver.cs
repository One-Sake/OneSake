using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// What the resolver needs from a player before it can go on.
public abstract record ResolverRequest(string PlayerId);

// A choice inside one effect (target, cost, "use it?") - asked by the EffectEngine.
public sealed record EffectChoiceNeeded(EffectChoiceRequest Request) : ResolverRequest(Request.PlayerId);

// The player has several effects at the same time and chooses the order.
public sealed record OrderNeeded(string PlayerId, IReadOnlyList<PendingTrigger> Triggers) : ResolverRequest(PlayerId);

// An effect text the engine cannot automate: the player resolves it by hand, then says "done".
public sealed record ManualEffectNeeded(string PlayerId, PendingTrigger Trigger) : ResolverRequest(PlayerId);

// Runs all effects of a match: events -> TriggerCollector -> TriggerQueue -> EffectEngine.
// The EffectEngine resolves exactly one effect at a time; the queue decides which one is next.
public sealed class EffectResolver
{
    private const int MaxStepsPerRun = 1000;

    private readonly IGameContext _context;
    private PendingTrigger? _manualEffect;
    private bool _orderPending;

    public EffectResolver(IGameContext context, GameEventBus events)
    {
        _context = context;
        Events = events;
        Collector = new TriggerCollector(context);
        events.Subscribe(gameEvent => Queue.Collect(Collector.Collect(gameEvent)));
    }

    public GameEventBus Events { get; }
    public EffectEngine Engine { get; } = new();
    public TriggerQueue Queue { get; } = new();
    public TriggerCollector Collector { get; }

    public bool IsBusy => Engine.IsBusy || !Queue.IsEmpty || _manualEffect is not null || _orderPending;

    // Effects the player starts on purpose (Event [Main]/[Counter], [Trigger]).
    public void Activate(CardInstance card, string controllerId, EffectTiming timing)
    {
        Queue.ResolveNow(Collector.Activated(card, controllerId, timing));
    }

    // One chosen [Activate: Main] ability.
    public void Activate(CardInstance card, string controllerId, Ability ability)
    {
        Queue.ResolveNow([Collector.Activated(card, controllerId, ability)]);
    }

    // Resolves effects until a player has to decide something (returned) or nothing is left (null).
    public ResolverRequest? Run()
    {
        for (var guard = 0; guard < MaxStepsPerRun; guard += 1)
        {
            // Rule 9-1-2: rule processing happens before anything else goes on.
            if (_context.ProcessRules())
            {
                Clear();
                return null;
            }

            if (Engine.PendingChoice is { } pending)
            {
                return new EffectChoiceNeeded(pending);
            }

            if (_manualEffect is not null)
            {
                return new ManualEffectNeeded(_manualEffect.ControllerId, _manualEffect);
            }

            // Rule 8-6-3: everything that became pending while the last effect resolved is one batch.
            Queue.CloseBatch(_context.ActivePlayerId);
            var step = Queue.Next();
            if (step is null)
            {
                _orderPending = false;
                return null;
            }

            if (step.Trigger is null)
            {
                _orderPending = true;
                return new OrderNeeded(step.Group.PlayerId, step.Group.Triggers.ToList());
            }

            var trigger = step.Trigger;
            if (!CanStillActivate(trigger))
            {
                continue;
            }

            if (step.Group.Size > 1)
            {
                _context.Log("EFFECT_ORDER", $"{step.Group.Resolved}/{step.Group.Size}: {trigger.Source.Name} {trigger.Text}");
            }

            if (trigger.Ability is null)
            {
                _manualEffect = trigger;
                _context.Log("EFFECT_MANUAL", $"{trigger.Source.Name}: \"{trigger.ManualText}\" is not automated - please resolve it manually.");
                return new ManualEffectNeeded(trigger.ControllerId, trigger);
            }

            Engine.Enqueue(trigger.Ability, trigger.Source, trigger.ControllerId, alreadyChosen: trigger.StartedByPlayer);
            if (Engine.Run(_context) is { } request)
            {
                return _context.ProcessRules() ? Stop() : new EffectChoiceNeeded(request);
            }
        }

        _context.Log("EFFECT_ERROR", "Too many effects in a row - the remaining effects were stopped (rule 11-1).");
        Clear();
        return null;
    }

    public ResolverRequest? AnswerEffect(string option, string? cardInstanceId)
    {
        if (Engine.Answer(_context, option, cardInstanceId) is { } request)
        {
            return _context.ProcessRules() ? Stop() : new EffectChoiceNeeded(request);
        }

        return Run();
    }

    public ResolverRequest? AnswerOrder(string playerId, IReadOnlyList<string> orderedIds)
    {
        Queue.SetOrder(playerId, orderedIds);
        _orderPending = false;
        _context.Log("EFFECT_ORDER", $"{_context.GetPlayer(playerId).DisplayName} chose the order of {orderedIds.Count} effects.");
        return Run();
    }

    public ResolverRequest? ManualEffectDone(string playerId)
    {
        if (_manualEffect is null || _manualEffect.ControllerId != playerId)
        {
            throw new InvalidOperationException("There is no manual effect to finish.");
        }

        _manualEffect = null;
        return Run();
    }

    public void Clear()
    {
        Engine.Clear();
        Queue.Clear();
        _manualEffect = null;
        _orderPending = false;
    }

    private ResolverRequest? Stop()
    {
        Clear();
        return null;
    }

    // Checked again when it is the effect's turn to activate (rule 8-4-1-1).
    private bool CanStillActivate(PendingTrigger trigger)
    {
        var owner = _context.Players.FirstOrDefault(player => player.PlayerId == trigger.ControllerId);

        // Rule 8-1-3-1-3: the card left the field before the effect activated.
        if (trigger.NeedsSourceOnField && (owner is null || !owner.FieldCards().Contains(trigger.Source)))
        {
            _context.Log("EFFECT", $"{trigger.Source.Name} left the field - its effect does not activate.");
            return false;
        }

        if (trigger.Ability is null || trigger.StartedByPlayer)
        {
            return true;
        }

        // Rule 10-2-13-3: a [Once Per Turn] effect that was resolved this turn cannot activate again.
        if (EffectEngine.IsUsedThisTurn(trigger.Ability, trigger.Source, _context))
        {
            _context.Log("EFFECT", $"{trigger.Source.Name}: [Once Per Turn] was already used this turn.");
            return false;
        }

        // Rule 8-3-2-3 / 8-4-1-1: [DON!! xX], [Your Turn] and the other conditions must still be met.
        // [On K.O.] was checked on the field (rule 10-2-17-1); in the trash it has no DON!! any more.
        if (trigger.Timing != EffectTiming.OnKO
            && !EffectQueries.AbilityIsActive(trigger.Ability, trigger.Source, trigger.ControllerId, _context))
        {
            _context.Log("EFFECT", $"{trigger.Source.Name}: the conditions are no longer met - the effect does not activate.");
            return false;
        }

        return true;
    }
}
