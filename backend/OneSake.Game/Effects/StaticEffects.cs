using OneSake.Game.State;

namespace OneSake.Game.Effects;

// Always-on abilities like "[DON!! x1] This Character gains +1000 power" or
// "[Your Turn] All of your {Straw Hat Crew} type Characters gain +1000 power".
// They are not stored as modifiers: every time something changes, all values are recalculated.
public static class StaticEffects
{
    public static void Recalculate(IGameContext context)
    {
        var fieldCards = context.Players.SelectMany(player => player.FieldCards()).ToList();

        foreach (var card in fieldCards)
        {
            card.StaticPower = 0;
            card.StaticCost = 0;
            card.StaticFlags.Clear();
        }

        // Two rounds: a flag from the first round (e.g. a name alias) can enable a condition in the second.
        for (var round = 0; round < 2; round += 1)
        {
            if (round == 1)
            {
                foreach (var card in fieldCards)
                {
                    card.StaticPower = 0;
                    card.StaticCost = 0;
                }
            }

            foreach (var player in context.Players)
            {
                foreach (var source in player.FieldCards().ToList())
                {
                    ApplyStaticAbilities(source, player, context);
                }
            }
        }
    }

    private static void ApplyStaticAbilities(CardInstance source, PlayerState owner, IGameContext context)
    {
        foreach (var ability in source.Script.WithTiming(EffectTiming.Static))
        {
            if (!EffectQueries.AbilityIsActive(ability, source, owner.PlayerId, context))
            {
                continue;
            }

            foreach (var step in ability.Steps)
            {
                foreach (var target in StaticTargets(step, source, owner, context))
                {
                    Apply(step, target);
                }
            }
        }
    }

    private static IEnumerable<CardInstance> StaticTargets(EffectStep step, CardInstance source, PlayerState owner, IGameContext context)
    {
        if (step.Kind == StepKind.NameAlias)
        {
            return [source];
        }

        var target = step.Target;
        if (target is null)
        {
            return [];
        }

        if (target.Self || target.Previous)
        {
            return [source];
        }

        if (target.YourLeader)
        {
            return owner.Leader is null ? [] : [owner.Leader];
        }

        // Static abilities with "up to 1" are rare and need a choice - only "all" is supported.
        return target.All ? EffectQueries.FieldCandidates(target.Filter, source, owner.PlayerId, context).ToList() : [];
    }

    private static void Apply(EffectStep step, CardInstance target)
    {
        switch (step.Kind)
        {
            case StepKind.PowerModifier:
                target.StaticPower += step.Amount;
                break;
            case StepKind.CostModifier:
                target.StaticCost += step.Amount;
                break;
            case StepKind.GainFlag:
                target.StaticFlags.Add(step.Flag);
                break;
            case StepKind.NameAlias when step.Flag.Length > 0:
                target.StaticFlags.Add(EffectFlags.NameAliasPrefix + step.Flag);
                break;
        }
    }

    // True when the static ability can be calculated by Recalculate. Other static texts
    // (like "Your opponent cannot ...") are shown to the players as "resolve manually".
    public static bool IsSupported(Ability ability)
    {
        return ability.Steps.All(step =>
            step.Kind is StepKind.PowerModifier or StepKind.CostModifier or StepKind.GainFlag or StepKind.NameAlias
            && (step.Kind == StepKind.NameAlias || step.Target is { } target && (target.Self || target.YourLeader || target.All || target.Previous)));
    }
}
