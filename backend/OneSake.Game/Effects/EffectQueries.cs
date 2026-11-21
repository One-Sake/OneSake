using OneSake.Game.State;

namespace OneSake.Game.Effects;

// Checks that are used by the static effects and by the effect engine:
// "does this card match the filter?", "is this condition true?".
public static class EffectQueries
{
    public static bool Matches(CardInstance card, CardFilter filter, CardInstance? source, string controllerId, IGameContext context)
    {
        if (filter.ExcludeSource && source is not null && card == source)
        {
            return false;
        }

        if (filter.Category.Length > 0 && !string.Equals(card.Category, filter.Category, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cost = filter.UseBaseValues ? card.Cost : card.CurrentCost;
        var power = filter.UseBaseValues ? card.BasePower : card.GetPower(context.IsOwnersTurn(card));

        if (filter.MaxCost is { } maxCost && cost > maxCost)
        {
            return false;
        }

        if (filter.MinCost is { } minCost && cost < minCost)
        {
            return false;
        }

        if (filter.MaxCostFrom.Length > 0 && cost > DynamicMaxCost(filter.MaxCostFrom, controllerId, context))
        {
            return false;
        }

        if (filter.MaxPower is { } maxPower && power > maxPower)
        {
            return false;
        }

        if (filter.MinPower is { } minPower && power < minPower)
        {
            return false;
        }

        if (filter.Rested is { } rested && card.IsRested != rested)
        {
            return false;
        }

        var nameOk = filter.Name.Length == 0 || HasName(card, filter.Name);
        var typeOk = filter.TypeIncludes.Length == 0 || HasType(card, filter.TypeIncludes);
        if (filter.NameOrType ? !(nameOk || typeOk) : !(nameOk && typeOk))
        {
            return false;
        }

        if (filter.NameNot.Length > 0 && HasName(card, filter.NameNot))
        {
            return false;
        }

        if (filter.Color.Length > 0 && !card.Color.Contains(filter.Color, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Attribute.Length > 0 && !card.Attribute.Contains(filter.Attribute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Keyword.Length > 0 && !card.Keywords.Contains(filter.Keyword))
        {
            return false;
        }

        if (filter.RequiresTrigger && !card.HasTrigger)
        {
            return false;
        }

        return true;
    }

    public static bool HasName(CardInstance card, string name)
    {
        return string.Equals(card.Name, name, StringComparison.OrdinalIgnoreCase)
            || card.HasFlag(EffectFlags.NameAliasPrefix + name);
    }

    // "Straw Hat Crew|Supernovas" -> true when the card has one of these types.
    public static bool HasType(CardInstance card, string types)
    {
        return types
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(type => card.Types.Contains(type, StringComparison.OrdinalIgnoreCase));
    }

    private static int DynamicMaxCost(string source, string controllerId, IGameContext context)
    {
        return source switch
        {
            "yourDon" => context.GetPlayer(controllerId).DonOnField,
            "opponentLife" => context.GetOpponent(controllerId).LifeCount,
            _ => int.MaxValue
        };
    }

    // Field cards (Leader, Characters, Stage) of the players described by the filter.
    public static IEnumerable<CardInstance> FieldCandidates(CardFilter filter, CardInstance? source, string controllerId, IGameContext context)
    {
        var owners = filter.Owner switch
        {
            PlayerSide.You => [context.GetPlayer(controllerId)],
            PlayerSide.Opponent => [context.GetOpponent(controllerId)],
            _ => context.Players.ToList()
        };

        foreach (var owner in owners)
        {
            if (filter.AllowLeader && owner.Leader is not null && Matches(owner.Leader, filter, source, controllerId, context))
            {
                yield return owner.Leader;
            }

            if (filter.AllowCharacter)
            {
                foreach (var character in owner.Characters.ToList())
                {
                    if (Matches(character, filter, source, controllerId, context))
                    {
                        yield return character;
                    }
                }
            }

            if (filter.AllowStage && owner.Stage is not null && Matches(owner.Stage, filter, source, controllerId, context))
            {
                yield return owner.Stage;
            }
        }
    }

    // [DON!! xN], [Your Turn], [Opponent's Turn] and "If ..." of a whole ability.
    public static bool AbilityIsActive(Ability ability, CardInstance source, string controllerId, IGameContext context)
    {
        if (ability.RequiredDon > 0 && source.AttachedDon < ability.RequiredDon)
        {
            return false;
        }

        var isYourTurn = context.ActivePlayerId == controllerId;
        if (ability.OnlyYourTurn && !isYourTurn)
        {
            return false;
        }

        if (ability.OnlyOpponentsTurn && isYourTurn)
        {
            return false;
        }

        return ability.Conditions.All(condition => Check(condition, source, controllerId, context));
    }

    public static bool Check(EffectCondition condition, CardInstance source, string controllerId, IGameContext context)
    {
        var you = context.GetPlayer(controllerId);
        var opponent = context.GetOpponent(controllerId);

        return condition.Kind switch
        {
            ConditionKind.LeaderHasType => you.Leader is not null && HasType(you.Leader, condition.Filter!.TypeIncludes),
            ConditionKind.LeaderIsName => you.Leader is not null && HasName(you.Leader, condition.Filter!.Name),
            ConditionKind.LeaderIsMulticolored => you.Leader is not null
                // Explicit array: older .NET 8 SDKs (as on the server) find the collection expression ambiguous.
                && you.Leader.Color.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries).Length > 1,
            ConditionKind.LeaderColor => you.Leader is not null
                && you.Leader.Color.Contains(condition.Filter!.Color, StringComparison.OrdinalIgnoreCase),
            ConditionKind.LifeAtMost => you.LifeCount <= condition.Amount,
            ConditionKind.LifeAtLeast => you.LifeCount >= condition.Amount,
            ConditionKind.OpponentLifeAtMost => opponent.LifeCount <= condition.Amount,
            ConditionKind.OpponentLifeAtLeast => opponent.LifeCount >= condition.Amount,
            ConditionKind.HandAtMost => you.HandCount <= condition.Amount,
            ConditionKind.HandAtLeast => you.HandCount >= condition.Amount,
            ConditionKind.OpponentHandAtLeast => opponent.HandCount >= condition.Amount,
            ConditionKind.OpponentHandAtMost => opponent.HandCount <= condition.Amount,
            ConditionKind.DonOnFieldAtLeast => you.DonOnField >= condition.Amount,
            ConditionKind.DonOnFieldAtMost => you.DonOnField <= condition.Amount,
            ConditionKind.OpponentDonOnFieldAtLeast => opponent.DonOnField >= condition.Amount,
            ConditionKind.OpponentHasMoreDon => opponent.DonOnField > you.DonOnField,
            ConditionKind.DonNotMoreThanOpponent => you.DonOnField <= opponent.DonOnField,
            ConditionKind.LifeLessThanOpponent => you.LifeCount < opponent.LifeCount,
            ConditionKind.TrashAtLeast => you.TrashCards.Count >= condition.Amount,
            ConditionKind.DeckAtMost => you.DrawDeck.Count <= condition.Amount,
            ConditionKind.ThisIsRested => source.IsRested,
            ConditionKind.ThisIsActive => !source.IsRested,
            ConditionKind.GivenDonAtLeast => source.AttachedDon >= condition.Amount,
            ConditionKind.CharactersAtLeast => FieldCandidates(condition.Filter!, source, controllerId, context).Count() >= condition.Amount,
            ConditionKind.CharactersAtMost => FieldCandidates(condition.Filter!, source, controllerId, context).Count() <= condition.Amount,
            ConditionKind.HasCard => FieldCandidates(condition.Filter!, source, controllerId, context).Any(),
            ConditionKind.HasNoCard => !FieldCandidates(condition.Filter!, source, controllerId, context).Any(),
            ConditionKind.OpponentHasCard => FieldCandidates(condition.Filter!, source, controllerId, context).Count() >= Math.Max(1, condition.Amount),
            _ => false
        };
    }
}
