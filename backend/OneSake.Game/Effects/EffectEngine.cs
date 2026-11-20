using OneSake.Game.State;

using OneSake.Game.Rules;

namespace OneSake.Game.Effects;

// A question the engine asks a player while an effect resolves.
public sealed class EffectChoiceRequest
{
    public const string Select = "SELECT";
    public const string Done = "DONE";
    public const string Yes = "YES";
    public const string No = "NO";

    public string PlayerId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string AbilityText { get; init; } = string.Empty;
    // Cards the player can pick with "SELECT" (field, hand, trash or looked-at cards).
    public IReadOnlyList<CardInstance> Cards { get; init; } = [];
    public IReadOnlyList<string> Options { get; init; } = [];
}

// Resolves card abilities one after another. When a player has to decide something,
// Run() returns an EffectChoiceRequest and the engine waits for Answer().
//
// MatchRoom usage:
//   engine.Enqueue(...);                     // e.g. [On Play] of the played card
//   var request = engine.Run(context);       // null = everything resolved
//   ... later: request = engine.Answer(context, option, cardId);
public sealed class EffectEngine
{
    private readonly List<EffectFrame> _queue = [];
    private EffectChoiceRequest? _pendingChoice;

    public bool IsBusy => _queue.Count > 0 || _pendingChoice is not null;
    public EffectChoiceRequest? PendingChoice => _pendingChoice;

    public void Clear()
    {
        _queue.Clear();
        _pendingChoice = null;
    }

    #region QUEUE

    // Adds an ability to the end of the queue.
    // alreadyChosen: the player started it on purpose ([Activate: Main], Event, Counter), so no "Use it?" question.
    public void Enqueue(Ability ability, CardInstance source, string controllerId, bool alreadyChosen = false)
    {
        _queue.Add(new EffectFrame(ability, source, controllerId) { SkipOptionalQuestion = alreadyChosen });
    }

    // Queues all abilities of a card with the given timing (e.g. all [On Play] abilities).
    public int EnqueueTriggered(CardInstance source, string controllerId, EffectTiming timing, IGameContext context)
    {
        var count = 0;
        foreach (var ability in source.Script.WithTiming(timing))
        {
            if (!EffectQueries.AbilityIsActive(ability, source, controllerId, context) || IsUsedThisTurn(ability, source, context))
            {
                continue;
            }

            Enqueue(ability, source, controllerId);
            count += 1;
        }

        return count;
    }

    private void InsertNext(EffectFrame frame)
    {
        // Abilities that are triggered while an effect resolves wait until it is finished.
        var index = _queue.Count > 0 ? 1 : 0;
        while (index < _queue.Count && _queue[index].InsertedByTrigger)
        {
            index += 1;
        }

        frame.InsertedByTrigger = true;
        _queue.Insert(Math.Min(index, _queue.Count), frame);
    }

    public static bool IsUsedThisTurn(Ability ability, CardInstance source, IGameContext context)
    {
        return ability.OncePerTurn && source.UsedOncePerTurn.Contains($"{context.TurnNumber}:{ability.Index}");
    }

    // Can the player start this [Activate: Main] ability right now?
    public static bool CanActivate(Ability ability, CardInstance source, string controllerId, IGameContext context)
    {
        return ability.Timing == EffectTiming.ActivateMain
            && EffectQueries.AbilityIsActive(ability, source, controllerId, context)
            && !IsUsedThisTurn(ability, source, context)
            && CanPayCosts(ability, source, controllerId, context);
    }

    #endregion

    #region RUN

    public EffectChoiceRequest? Run(IGameContext context)
    {
        return Answer(context, null, null);
    }

    public EffectChoiceRequest? Answer(IGameContext context, string? option, string? cardInstanceId)
    {
        var answer = option is null ? null : new EffectAnswer(option.ToUpperInvariant(), cardInstanceId);

        if (answer is not null)
        {
            ValidateAnswer(answer);
        }

        _pendingChoice = null;

        // Safety limit against endless loops in broken card definitions.
        for (var guard = 0; guard < 500 && _queue.Count > 0; guard += 1)
        {
            var frame = _queue[0];
            var request = Advance(frame, context, answer);
            answer = null;

            StaticEffects.Recalculate(context);

            if (request is not null)
            {
                _pendingChoice = request;
                return request;
            }

            if (frame.Finished)
            {
                _queue.Remove(frame);
            }
        }

        if (_queue.Count > 0)
        {
            context.Log("EFFECT_ERROR", "An effect could not be finished and was stopped.");
            _queue.Clear();
        }

        return null;
    }

    private void ValidateAnswer(EffectAnswer answer)
    {
        var pending = _pendingChoice ?? throw new InvalidOperationException("No effect is waiting for a decision.");

        if (!pending.Options.Contains(answer.Option))
        {
            throw new InvalidOperationException("This option is not possible right now.");
        }

        if (answer.Option == EffectChoiceRequest.Select
            && pending.Cards.All(card => card.InstanceId != answer.CardInstanceId))
        {
            throw new InvalidOperationException("This card cannot be chosen.");
        }
    }

    // Moves one frame forward. Returns a request when a player has to decide.
    private EffectChoiceRequest? Advance(EffectFrame frame, IGameContext context, EffectAnswer? answer)
    {
        while (!frame.Finished)
        {
            switch (frame.Stage)
            {
                case FrameStage.Start:
                {
                    // [DON!! xN] and [Your Turn] were checked when the ability was queued
                    // (an [On K.O.] card is already in the trash now). "If ..." is checked again.
                    if (!frame.Ability.Conditions.All(condition => EffectQueries.Check(condition, frame.Source, frame.ControllerId, context)))
                    {
                        context.Log("EFFECT", $"{frame.Source.Name}: the conditions are not met.");
                        frame.Finished = true;
                        break;
                    }

                    if (frame.Ability.Costs.Count > 0)
                    {
                        frame.Stage = FrameStage.AskCost;
                    }
                    else
                    {
                        MarkUsed(frame, context);
                        frame.Stage = FrameStage.Steps;
                    }

                    break;
                }

                case FrameStage.AskCost:
                {
                    if (!CanPayCosts(frame.Ability, frame.Source, frame.ControllerId, context))
                    {
                        context.Log("EFFECT", $"{frame.Source.Name}: the cost cannot be paid.");
                        frame.Finished = true;
                        break;
                    }

                    if (!frame.SkipOptionalQuestion)
                    {
                        if (answer is null)
                        {
                            return Ask(frame, context, $"Use the effect of {frame.Source.Name}? Cost: {DescribeCosts(frame.Ability)}",
                                [EffectChoiceRequest.Yes, EffectChoiceRequest.No]);
                        }

                        if (answer.Option != EffectChoiceRequest.Yes)
                        {
                            frame.Finished = true;
                            break;
                        }

                        answer = null;
                    }

                    MarkUsed(frame, context);
                    frame.Stage = FrameStage.PayCost;
                    break;
                }

                case FrameStage.PayCost:
                {
                    while (frame.CostIndex < frame.Ability.Costs.Count)
                    {
                        var request = PayCost(frame, frame.Ability.Costs[frame.CostIndex], context, answer);
                        answer = null;
                        if (request is not null)
                        {
                            return request;
                        }

                        frame.CostIndex += 1;
                        frame.Scratch.Clear();
                    }

                    context.Log("EFFECT_COST", $"{frame.Source.Name}: paid {DescribeCosts(frame.Ability)}.");
                    frame.Stage = FrameStage.Steps;
                    break;
                }

                case FrameStage.Steps:
                {
                    if (frame.StepIndex >= frame.Ability.Steps.Count)
                    {
                        frame.Finished = true;
                        break;
                    }

                    var step = frame.Ability.Steps[frame.StepIndex];
                    var request = RunStep(frame, step, context, answer);
                    answer = null;
                    if (request is not null)
                    {
                        return request;
                    }

                    frame.StepIndex += 1;
                    frame.Scratch.Clear();
                    break;
                }
            }
        }

        return null;
    }

    private static void MarkUsed(EffectFrame frame, IGameContext context)
    {
        if (frame.Ability.OncePerTurn)
        {
            frame.Source.UsedOncePerTurn.Add($"{context.TurnNumber}:{frame.Ability.Index}");
        }
    }

    private static EffectChoiceRequest Ask(EffectFrame frame, IGameContext context, string title, IReadOnlyList<string> options,
        IReadOnlyList<CardInstance>? cards = null, string? playerId = null)
    {
        _ = context;
        return new EffectChoiceRequest
        {
            PlayerId = playerId ?? frame.ControllerId,
            Title = title,
            SourceName = frame.Source.Name,
            AbilityText = frame.Ability.Text,
            Cards = cards ?? [],
            Options = options
        };
    }

    #endregion

    #region COSTS

    public static bool CanPayCosts(Ability ability, CardInstance source, string controllerId, IGameContext context)
    {
        var player = context.GetPlayer(controllerId);
        var restDonTotal = ability.Costs.Where(cost => cost.Kind == CostKind.RestDon).Sum(cost => cost.Amount);
        if (player.ActiveDon < restDonTotal)
        {
            return false;
        }

        foreach (var cost in ability.Costs)
        {
            var payable = cost.Kind switch
            {
                CostKind.ReturnDon => player.DonOnField >= cost.Amount,
                CostKind.RestDon => true,
                CostKind.RestThis => !source.IsRested && IsOnField(source, player),
                CostKind.TrashThis or CostKind.ReturnThis => player.Characters.Contains(source) || player.Stage == source,
                CostKind.TrashFromHand => HandCards(player, cost.Filter, source, controllerId, context).Count >= cost.Amount,
                CostKind.RevealFromHand => HandCards(player, cost.Filter, source, controllerId, context).Count >= cost.Amount,
                CostKind.LifeToHand or CostKind.TrashLife or CostKind.TurnLife => player.LifeCount >= cost.Amount,
                CostKind.TrashTopOfDeck => player.DrawDeck.Count >= cost.Amount,
                CostKind.RestOwnCards => OwnFieldCards(cost, source, controllerId, context, mustBeActive: true).Count >= cost.Amount,
                CostKind.ReturnOwnCharacter or CostKind.TrashOwnCharacter => OwnFieldCards(cost, source, controllerId, context, mustBeActive: false).Count >= cost.Amount,
                CostKind.TrashToDeckBottom => player.TrashCards.Count(card => cost.Filter is null || EffectQueries.Matches(card, cost.Filter, source, controllerId, context)) >= cost.Amount,
                _ => false
            };

            if (!payable)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOnField(CardInstance card, PlayerState player)
    {
        return player.Leader == card || player.Stage == card || player.Characters.Contains(card);
    }

    private static List<CardInstance> HandCards(PlayerState player, CardFilter? filter, CardInstance source, string controllerId, IGameContext context)
    {
        return player.Hand
            .Where(card => card != source && (filter is null || EffectQueries.Matches(card, filter, source, controllerId, context)))
            .ToList();
    }

    private static List<CardInstance> OwnFieldCards(EffectCost cost, CardInstance source, string controllerId, IGameContext context, bool mustBeActive)
    {
        var filter = cost.Filter?.Clone() ?? new CardFilter();
        filter.Owner = PlayerSide.You;
        return EffectQueries.FieldCandidates(filter, source, controllerId, context)
            .Where(card => !mustBeActive || !card.IsRested)
            .ToList();
    }

    private static string DescribeCosts(Ability ability)
    {
        return string.Join(", ", ability.Costs.Select(cost => cost.Kind switch
        {
            CostKind.ReturnDon => $"DON!! -{cost.Amount}",
            CostKind.RestDon => $"rest {cost.Amount} DON!!",
            CostKind.RestThis => "rest this card",
            CostKind.TrashThis => "trash this card",
            CostKind.ReturnThis => "return this card to your hand",
            CostKind.TrashFromHand => $"trash {cost.Amount} card(s) from your hand",
            CostKind.RevealFromHand => $"reveal {cost.Amount} card(s) from your hand",
            CostKind.LifeToHand => $"add {cost.Amount} Life card(s) to your hand",
            CostKind.TrashLife => $"trash {cost.Amount} Life card(s)",
            CostKind.TurnLife => $"turn {cost.Amount} Life card(s)",
            CostKind.TrashTopOfDeck => $"trash the top {cost.Amount} card(s) of your deck",
            CostKind.RestOwnCards => $"rest {cost.Amount} of your cards",
            CostKind.ReturnOwnCharacter => $"return {cost.Amount} of your Characters to your hand",
            CostKind.TrashOwnCharacter => $"trash {cost.Amount} of your Characters",
            CostKind.TrashToDeckBottom => $"place {cost.Amount} card(s) from your trash at the bottom of your deck",
            _ => cost.Kind.ToString()
        }));
    }

    private EffectChoiceRequest? PayCost(EffectFrame frame, EffectCost cost, IGameContext context, EffectAnswer? answer)
    {
        var player = context.GetPlayer(frame.ControllerId);

        switch (cost.Kind)
        {
            case CostKind.ReturnDon:
                ReturnDon(player, cost.Amount);
                return null;

            case CostKind.RestDon:
            {
                var amount = Math.Min(cost.Amount, player.ActiveDon);
                player.ActiveDon -= amount;
                player.RestedDon += amount;
                return null;
            }

            case CostKind.RestThis:
                frame.Source.IsRested = true;
                return null;

            case CostKind.TrashThis:
                player.RemoveFromField(frame.Source);
                player.TrashCards.Add(frame.Source);
                return null;

            case CostKind.ReturnThis:
                player.RemoveFromField(frame.Source);
                player.Hand.Add(frame.Source);
                return null;

            case CostKind.LifeToHand:
                MoveLifeToHand(player, cost.Amount);
                return null;

            case CostKind.TrashLife:
                for (var index = 0; index < cost.Amount && player.LifeCards.Count > 0; index += 1)
                {
                    var life = player.LifeCards[^1];
                    player.LifeCards.RemoveAt(player.LifeCards.Count - 1);
                    player.TrashCards.Add(life);
                }

                return null;

            case CostKind.TurnLife:
                // Face-up/face-down Life cards are not shown differently yet.
                return null;

            case CostKind.TrashTopOfDeck:
                TrashTopOfDeck(player, cost.Amount);
                return null;

            case CostKind.RevealFromHand:
            {
                var revealed = HandCards(player, cost.Filter, frame.Source, frame.ControllerId, context).Take(cost.Amount);
                context.Log("EFFECT", $"{player.DisplayName} reveals {string.Join(", ", revealed.Select(card => card.Name))}.");
                return null;
            }

            case CostKind.TrashFromHand:
                return ChooseAndMove(frame, context, answer, cost.Amount, exact: true,
                    () => HandCards(player, cost.Filter, frame.Source, frame.ControllerId, context),
                    $"Trash {cost.Amount} card(s) from your hand",
                    card =>
                    {
                        player.Hand.Remove(card);
                        player.TrashCards.Add(card);
                    });

            case CostKind.RestOwnCards:
                return ChooseAndMove(frame, context, answer, cost.Amount, exact: true,
                    () => OwnFieldCards(cost, frame.Source, frame.ControllerId, context, mustBeActive: true),
                    $"Rest {cost.Amount} of your cards",
                    card => card.IsRested = true);

            case CostKind.ReturnOwnCharacter:
                return ChooseAndMove(frame, context, answer, cost.Amount, exact: true,
                    () => OwnFieldCards(cost, frame.Source, frame.ControllerId, context, mustBeActive: false).Where(card => !card.IsLeader).ToList(),
                    $"Return {cost.Amount} of your Characters to your hand",
                    card =>
                    {
                        player.RemoveFromField(card);
                        player.Hand.Add(card);
                    });

            case CostKind.TrashOwnCharacter:
                return ChooseAndMove(frame, context, answer, cost.Amount, exact: true,
                    () => OwnFieldCards(cost, frame.Source, frame.ControllerId, context, mustBeActive: false).Where(card => !card.IsLeader).ToList(),
                    $"Trash {cost.Amount} of your Characters",
                    card =>
                    {
                        player.RemoveFromField(card);
                        player.TrashCards.Add(card);
                    });

            case CostKind.TrashToDeckBottom:
                return ChooseAndMove(frame, context, answer, cost.Amount, exact: true,
                    () => player.TrashCards.Where(card => cost.Filter is null || EffectQueries.Matches(card, cost.Filter, frame.Source, frame.ControllerId, context)).ToList(),
                    $"Place {cost.Amount} card(s) from your trash at the bottom of your deck",
                    card =>
                    {
                        player.TrashCards.Remove(card);
                        player.DrawDeck.Add(card);
                    });

            default:
                return null;
        }
    }

    #endregion

    #region STEPS

    private EffectChoiceRequest? RunStep(EffectFrame frame, EffectStep step, IGameContext context, EffectAnswer? answer)
    {
        var player = context.GetPlayer(frame.ControllerId);
        var opponent = context.GetOpponent(frame.ControllerId);

        if (!frame.Scratch.ConditionsChecked)
        {
            frame.Scratch.ConditionsChecked = true;
            if (!step.Conditions.All(condition => EffectQueries.Check(condition, frame.Source, frame.ControllerId, context)))
            {
                frame.Scratch.Skipped = true;
            }
        }

        if (frame.Scratch.Skipped)
        {
            return null;
        }

        // "You may draw 1 card." - steps with "up to" targets already allow choosing nothing.
        if (step.Optional && !frame.Scratch.OptionalAnswered && !HasUpToChoice(step))
        {
            if (answer is null)
            {
                return Ask(frame, context, $"{frame.Source.Name}: {step.Text}?", [EffectChoiceRequest.Yes, EffectChoiceRequest.No]);
            }

            frame.Scratch.OptionalAnswered = true;
            if (answer.Option != EffectChoiceRequest.Yes)
            {
                return null;
            }

            answer = null;
        }

        switch (step.Kind)
        {
            case StepKind.Select:
            case StepKind.KO:
            case StepKind.Rest:
            case StepKind.SetActive:
            case StepKind.ReturnToHand:
            case StepKind.BottomOfDeck:
            case StepKind.TopOfDeck:
            case StepKind.TrashCard:
            case StepKind.PowerModifier:
            case StepKind.CostModifier:
            case StepKind.GainFlag:
            case StepKind.SkipRefresh:
            case StepKind.GiveRestedDon:
                return RunTargetStep(frame, step, context, answer);

            case StepKind.Draw:
                player.DrawCards(step.Amount);
                context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} draws {step.Amount} card(s).");
                return null;

            case StepKind.TrashFromHand:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: !step.Optional,
                    () => player.Hand.Where(card => step.Filter is null || EffectQueries.Matches(card, step.Filter, frame.Source, frame.ControllerId, context)).ToList(),
                    $"{frame.Source.Name}: trash {step.Amount} card(s) from your hand",
                    card =>
                    {
                        player.Hand.Remove(card);
                        player.TrashCards.Add(card);
                        context.Log("EFFECT", $"{player.DisplayName} trashed {card.Name} from their hand.");
                    });

            case StepKind.OpponentTrashesFromHand:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: true,
                    () => opponent.Hand.ToList(),
                    $"{frame.Source.Name}: trash {step.Amount} card(s) from your hand",
                    card =>
                    {
                        opponent.Hand.Remove(card);
                        opponent.TrashCards.Add(card);
                        context.Log("EFFECT", $"{opponent.DisplayName} trashed {card.Name} from their hand.");
                    },
                    opponent.PlayerId);

            case StepKind.HandToDeckBottom:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: true,
                    () => player.Hand.ToList(),
                    $"{frame.Source.Name}: place {step.Amount} card(s) from your hand at the bottom of your deck",
                    card =>
                    {
                        player.Hand.Remove(card);
                        player.DrawDeck.Add(card);
                    });

            case StepKind.OpponentHandToDeckBottom:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: true,
                    () => opponent.Hand.ToList(),
                    $"{frame.Source.Name}: place {step.Amount} card(s) from your hand at the bottom of your deck",
                    card =>
                    {
                        opponent.Hand.Remove(card);
                        opponent.DrawDeck.Add(card);
                    },
                    opponent.PlayerId);

            case StepKind.HandToLife:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: false,
                    () => player.Hand.Where(card => step.Filter is null || EffectQueries.Matches(card, step.Filter, frame.Source, frame.ControllerId, context)).ToList(),
                    $"{frame.Source.Name}: add up to {step.Amount} card(s) from your hand to the top of your Life cards",
                    card =>
                    {
                        player.Hand.Remove(card);
                        player.LifeCards.Add(card);
                    });

            case StepKind.TrashToHand:
                return ChooseAndMove(frame, context, answer, step.Amount, exact: false,
                    () => player.TrashCards.Where(card => step.Filter is null || EffectQueries.Matches(card, step.Filter, frame.Source, frame.ControllerId, context)).ToList(),
                    $"{frame.Source.Name}: add up to {step.Amount} card(s) from your trash to your hand",
                    card =>
                    {
                        player.TrashCards.Remove(card);
                        player.Hand.Add(card);
                        context.Log("EFFECT", $"{player.DisplayName} added {card.Name} from the trash to their hand.");
                    });

            case StepKind.AddDonFromDeck:
            {
                var amount = Math.Min(step.Amount, player.DonDeckCount);
                player.DonDeckCount -= amount;
                if (step.Rested)
                {
                    player.RestedDon += amount;
                }
                else
                {
                    player.ActiveDon += amount;
                }

                context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} adds {amount} DON!! ({(step.Rested ? "rested" : "active")}).");
                return null;
            }

            case StepKind.SetDonActive:
            {
                var amount = Math.Min(step.Amount, player.RestedDon);
                player.RestedDon -= amount;
                player.ActiveDon += amount;
                context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} sets {amount} DON!! as active.");
                return null;
            }

            case StepKind.SetDonActiveAtEndOfTurn:
                player.DonToActivateAtEndOfTurn += step.Amount;
                return null;

            case StepKind.ReturnDonToDeck:
            {
                var target = step.Side == PlayerSide.Opponent ? opponent : player;
                ReturnDon(target, step.Amount);
                context.Log("EFFECT", $"{frame.Source.Name}: {target.DisplayName} returns {step.Amount} DON!! to the DON!! deck.");
                return null;
            }

            case StepKind.RestOpponentDon:
            {
                var amount = Math.Min(step.Amount, opponent.ActiveDon);
                opponent.ActiveDon -= amount;
                opponent.RestedDon += amount;
                context.Log("EFFECT", $"{frame.Source.Name}: rests {amount} of {opponent.DisplayName}'s DON!!.");
                return null;
            }

            case StepKind.TrashTopOfDeck:
                TrashTopOfDeck(player, step.Amount);
                context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} trashes the top {step.Amount} card(s) of their deck.");
                return null;

            case StepKind.LifeToHand:
            {
                var target = step.Side == PlayerSide.Opponent ? opponent : player;
                MoveLifeToHand(target, step.Amount);
                context.Log("EFFECT", $"{frame.Source.Name}: {target.DisplayName} adds {step.Amount} Life card(s) to their hand.");
                return null;
            }

            case StepKind.TrashLife:
            {
                var target = step.Side == PlayerSide.Opponent ? opponent : player;
                for (var index = 0; index < step.Amount && target.LifeCards.Count > 0; index += 1)
                {
                    var life = target.LifeCards[^1];
                    target.LifeCards.RemoveAt(target.LifeCards.Count - 1);
                    target.TrashCards.Add(life);
                }

                context.Log("EFFECT", $"{frame.Source.Name}: {target.DisplayName} trashes {step.Amount} Life card(s).");
                return null;
            }

            case StepKind.DeckTopToLife:
                for (var index = 0; index < step.Amount && player.DrawDeck.Count > 0; index += 1)
                {
                    player.LifeCards.Add(player.DrawDeck[0]);
                    player.DrawDeck.RemoveAt(0);
                }

                context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} adds {step.Amount} card(s) from the deck to their Life.");
                return null;

            case StepKind.ShuffleDeck:
                Shuffle(player.DrawDeck);
                return null;

            case StepKind.LookAndReveal:
                return RunLookAndReveal(frame, step, context, answer);

            case StepKind.LookAndArrange:
                return RunLookAndArrange(frame, step, context, answer);

            case StepKind.PlayCard:
                return RunPlayCard(frame, step, context, answer);

            case StepKind.PlayThisCard:
                PlayCharacterOrStage(frame.Source, player, context, step.Rested);
                return null;

            case StepKind.ActivateOwnAbility:
            {
                var timing = step.Flag.ToLowerInvariant() switch
                {
                    "main" => EffectTiming.EventMain,
                    "on play" => EffectTiming.OnPlay,
                    "on k.o." => EffectTiming.OnKO,
                    "counter" => EffectTiming.EventCounter,
                    "when attacking" => EffectTiming.WhenAttacking,
                    _ => EffectTiming.EventMain
                };

                foreach (var ability in frame.Source.Script.WithTiming(timing))
                {
                    InsertNext(new EffectFrame(ability, frame.Source, frame.ControllerId) { SkipOptionalQuestion = true });
                }

                return null;
            }

            case StepKind.NegateBlockers:
                if (context.Battle is { } battle && step.Duration == Duration.ThisBattle)
                {
                    battle.Attacker.Modifiers.Add(new CardModifier { Flag = EffectFlags.CannotBeBlocked, Duration = Duration.ThisBattle, CreatedOnTurn = context.TurnNumber });
                }
                else
                {
                    player.TurnFlags.Add(EffectFlags.CannotBeBlocked);
                }

                context.Log("EFFECT", $"{frame.Source.Name}: {opponent.DisplayName} cannot activate [Blocker].");
                return null;

            case StepKind.NameAlias:
                return null;

            default:
                context.Log("EFFECT", $"{frame.Source.Name}: '{step.Text}' must be resolved manually.");
                return null;
        }
    }

    private static bool HasUpToChoice(EffectStep step)
    {
        if (step.Target is { } target)
        {
            return target.UpTo && !target.All && !target.Self && !target.YourLeader && !target.Previous;
        }

        return step.Kind is StepKind.LookAndReveal or StepKind.PlayCard or StepKind.TrashToHand or StepKind.HandToLife;
    }

    // Chooses the targets of a step (if needed) and applies the step to them.
    private EffectChoiceRequest? RunTargetStep(EffectFrame frame, EffectStep step, IGameContext context, EffectAnswer? answer)
    {
        var target = step.Target ?? new TargetSpec { Self = true };
        var player = context.GetPlayer(frame.ControllerId);

        List<CardInstance> chosen;
        if (target.Self)
        {
            chosen = IsOnFieldAnywhere(frame.Source, context) ? [frame.Source] : [];
        }
        else if (target.YourLeader)
        {
            chosen = player.Leader is null ? [] : [player.Leader];
        }
        else if (target.Previous)
        {
            chosen = frame.Previous.Where(card => IsOnFieldAnywhere(card, context)).ToList();
        }
        else
        {
            var candidates = EffectQueries.FieldCandidates(target.Filter, frame.Source, frame.ControllerId, context)
                .Where(card => !frame.Scratch.Chosen.Contains(card))
                .Where(card => CanBeAffected(card, step, frame, context))
                .ToList();

            if (target.All)
            {
                chosen = candidates;
            }
            else
            {
                if (answer is not null)
                {
                    if (answer.Option == EffectChoiceRequest.Done)
                    {
                        frame.Scratch.ChoiceDone = true;
                    }
                    else if (candidates.FirstOrDefault(card => card.InstanceId == answer.CardInstanceId) is { } picked)
                    {
                        frame.Scratch.Chosen.Add(picked);
                        candidates.Remove(picked);
                    }
                }

                var maxCount = target.Count;
                var needMore = !frame.Scratch.ChoiceDone && frame.Scratch.Chosen.Count < maxCount && candidates.Count > 0;
                if (needMore)
                {
                    // Exact counts ("K.O. 1 of your Characters") must be chosen if possible.
                    var canStop = target.UpTo || step.Optional;
                    var options = canStop
                        ? new[] { EffectChoiceRequest.Select, EffectChoiceRequest.Done }
                        : new[] { EffectChoiceRequest.Select };
                    var left = maxCount - frame.Scratch.Chosen.Count;
                    return Ask(frame, context, $"{frame.Source.Name}: {step.Text} ({left} left)", options, candidates);
                }

                chosen = frame.Scratch.Chosen.ToList();
            }
        }

        foreach (var card in chosen)
        {
            ApplyToCard(frame, step, card, context);
        }

        frame.Previous = chosen;
        return null;
    }

    private static bool IsOnFieldAnywhere(CardInstance card, IGameContext context)
    {
        return context.Players.Any(player => player.Leader == card || player.Stage == card || player.Characters.Contains(card));
    }

    // Protection flags like "cannot be K.O.'d by effects" only protect against the opponent.
    private static bool CanBeAffected(CardInstance card, EffectStep step, EffectFrame frame, IGameContext context)
    {
        var ownerId = context.FindOwner(card)?.PlayerId;
        var byOpponent = ownerId != frame.ControllerId;

        return step.Kind switch
        {
            StepKind.KO => !(byOpponent && card.HasFlag(EffectFlags.CannotBeKOdByEffects)) && !card.IsLeader,
            StepKind.Rest => !(byOpponent && card.HasFlag(EffectFlags.CannotBeRestedByEffects)),
            StepKind.ReturnToHand or StepKind.BottomOfDeck or StepKind.TopOfDeck or StepKind.TrashCard => !card.IsLeader,
            StepKind.GiveRestedDon => !card.IsStage,
            _ => true
        };
    }

    private void ApplyToCard(EffectFrame frame, EffectStep step, CardInstance card, IGameContext context)
    {
        var owner = context.FindOwner(card);
        if (owner is null)
        {
            return;
        }

        var source = frame.Source.Name;
        switch (step.Kind)
        {
            case StepKind.Select:
                return;

            case StepKind.KO:
                if (KnockOut(card, context, byEffect: true))
                {
                    context.Log("EFFECT_KO", $"{source}: {owner.DisplayName}'s {card.Name} was K.O.'d.");
                }

                return;

            case StepKind.Rest:
                card.IsRested = true;
                context.Log("EFFECT", $"{source}: {card.Name} was rested.");
                return;

            case StepKind.SetActive:
                card.IsRested = false;
                context.Log("EFFECT", $"{source}: {card.Name} was set as active.");
                return;

            case StepKind.ReturnToHand:
                if (owner.RemoveFromField(card))
                {
                    owner.Hand.Add(card);
                    context.Log("EFFECT", $"{source}: {card.Name} returned to {owner.DisplayName}'s hand.");
                }

                return;

            case StepKind.BottomOfDeck:
            case StepKind.TopOfDeck:
                if (owner.RemoveFromField(card))
                {
                    if (step.Kind == StepKind.TopOfDeck)
                    {
                        owner.DrawDeck.Insert(0, card);
                    }
                    else
                    {
                        owner.DrawDeck.Add(card);
                    }

                    context.Log("EFFECT", $"{source}: {card.Name} was placed at the {(step.Kind == StepKind.TopOfDeck ? "top" : "bottom")} of the deck.");
                }

                return;

            case StepKind.TrashCard:
                if (owner.RemoveFromField(card))
                {
                    owner.TrashCards.Add(card);
                    context.Log("EFFECT", $"{source}: {card.Name} was trashed.");
                }

                return;

            case StepKind.PowerModifier:
                card.Modifiers.Add(new CardModifier { Power = step.Amount, Duration = step.Duration, CreatedOnTurn = context.TurnNumber });
                context.Log("EFFECT", $"{source}: {card.Name} {(step.Amount >= 0 ? "+" : string.Empty)}{step.Amount} power ({DescribeDuration(step.Duration)}).");
                return;

            case StepKind.CostModifier:
                card.Modifiers.Add(new CardModifier { Cost = step.Amount, Duration = step.Duration, CreatedOnTurn = context.TurnNumber });
                context.Log("EFFECT", $"{source}: {card.Name} {(step.Amount >= 0 ? "+" : string.Empty)}{step.Amount} cost ({DescribeDuration(step.Duration)}).");
                return;

            case StepKind.GainFlag:
                card.Modifiers.Add(new CardModifier { Flag = step.Flag, Duration = step.Duration, CreatedOnTurn = context.TurnNumber });
                context.Log("EFFECT", $"{source}: {card.Name} gains {DescribeFlag(step.Flag)} ({DescribeDuration(step.Duration)}).");
                return;

            case StepKind.SkipRefresh:
                card.Modifiers.Add(new CardModifier { Flag = EffectFlags.SkipNextRefresh, Duration = Duration.Permanent, CreatedOnTurn = context.TurnNumber });
                context.Log("EFFECT", $"{source}: {card.Name} will not become active in the next Refresh Phase.");
                return;

            case StepKind.GiveRestedDon:
            {
                var player = context.GetPlayer(frame.ControllerId);
                var amount = Math.Min(step.Amount, player.RestedDon);
                player.RestedDon -= amount;
                card.AttachedDon += amount;
                context.Log("EFFECT", $"{source}: {card.Name} was given {amount} rested DON!!.");
                return;
            }
        }
    }

    private static string DescribeDuration(Duration duration) => duration switch
    {
        Duration.ThisBattle => "this battle",
        Duration.ThisTurn => "this turn",
        Duration.UntilEndOfOpponentsNextTurn => "until the end of the opponent's next turn",
        _ => "while on the field"
    };

    private static string DescribeFlag(string flag) => flag switch
    {
        EffectFlags.CannotBeKOdInBattle => "\"cannot be K.O.'d in battle\"",
        EffectFlags.CannotBeKOdByEffects => "\"cannot be K.O.'d by effects\"",
        EffectFlags.CannotAttack => "\"cannot attack\"",
        EffectFlags.CannotBlock => "\"cannot block\"",
        EffectFlags.CannotBeBlocked => "\"cannot be blocked\"",
        EffectFlags.CanAttackActiveCharacters => "\"can attack active Characters\"",
        _ => $"[{flag}]"
    };

    private EffectChoiceRequest? RunLookAndReveal(EffectFrame frame, EffectStep step, IGameContext context, EffectAnswer? answer)
    {
        var player = context.GetPlayer(frame.ControllerId);

        if (!frame.Scratch.Looked)
        {
            frame.Scratch.Looked = true;
            frame.Scratch.LookedCards = player.DrawDeck.Take(step.Amount).ToList();
            player.DrawDeck.RemoveRange(0, frame.Scratch.LookedCards.Count);
        }

        var looked = frame.Scratch.LookedCards;
        var matching = looked
            .Where(card => !frame.Scratch.Chosen.Contains(card))
            .Where(card => step.Filter is null || EffectQueries.Matches(card, step.Filter, frame.Source, frame.ControllerId, context))
            .ToList();

        if (answer is not null)
        {
            if (answer.Option == EffectChoiceRequest.Done)
            {
                frame.Scratch.ChoiceDone = true;
            }
            else if (matching.FirstOrDefault(card => card.InstanceId == answer.CardInstanceId) is { } picked)
            {
                frame.Scratch.Chosen.Add(picked);
                matching.Remove(picked);
            }
        }

        if (!frame.Scratch.ChoiceDone && frame.Scratch.Chosen.Count < step.SecondAmount && matching.Count > 0)
        {
            var names = string.Join(", ", looked.Select(card => card.Name));
            return Ask(frame, context, $"{frame.Source.Name}: you look at {names}. Choose up to {step.SecondAmount - frame.Scratch.Chosen.Count} card(s) to reveal.",
                [EffectChoiceRequest.Select, EffectChoiceRequest.Done], matching);
        }

        foreach (var card in frame.Scratch.Chosen)
        {
            looked.Remove(card);
            context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} reveals {card.Name}.");
            if (step.PlayRevealed && (card.IsCharacter || card.IsStage))
            {
                PlayCharacterOrStage(card, player, context, step.Rested);
            }
            else
            {
                player.Hand.Add(card);
            }
        }

        foreach (var card in looked)
        {
            if (step.RestDestination == "trash")
            {
                player.TrashCards.Add(card);
            }
            else if (step.RestDestination == "top")
            {
                player.DrawDeck.Insert(0, card);
            }
            else
            {
                player.DrawDeck.Add(card);
            }
        }

        if (frame.Scratch.Chosen.Count == 0)
        {
            context.Log("EFFECT", $"{frame.Source.Name}: {player.DisplayName} did not reveal a card.");
        }

        frame.Previous = frame.Scratch.Chosen.ToList();
        return null;
    }

    // "Look at 3 cards and place them at the top or bottom of the deck": the player picks the
    // cards for the bottom, the others stay on top in the same order.
    private EffectChoiceRequest? RunLookAndArrange(EffectFrame frame, EffectStep step, IGameContext context, EffectAnswer? answer)
    {
        var player = context.GetPlayer(frame.ControllerId);

        if (!frame.Scratch.Looked)
        {
            frame.Scratch.Looked = true;
            frame.Scratch.LookedCards = player.DrawDeck.Take(step.Amount).ToList();
        }

        var remaining = frame.Scratch.LookedCards.Where(card => !frame.Scratch.Chosen.Contains(card)).ToList();
        if (answer is not null)
        {
            if (answer.Option == EffectChoiceRequest.Done)
            {
                frame.Scratch.ChoiceDone = true;
            }
            else if (remaining.FirstOrDefault(card => card.InstanceId == answer.CardInstanceId) is { } picked)
            {
                frame.Scratch.Chosen.Add(picked);
                remaining.Remove(picked);
            }
        }

        if (!frame.Scratch.ChoiceDone && remaining.Count > 0)
        {
            return Ask(frame, context, $"{frame.Source.Name}: choose cards to place at the BOTTOM of your deck (the others stay on top).",
                [EffectChoiceRequest.Select, EffectChoiceRequest.Done], remaining);
        }

        foreach (var card in frame.Scratch.Chosen)
        {
            player.DrawDeck.Remove(card);
            player.DrawDeck.Add(card);
        }

        return null;
    }

    private EffectChoiceRequest? RunPlayCard(EffectFrame frame, EffectStep step, IGameContext context, EffectAnswer? answer)
    {
        var player = context.GetPlayer(frame.ControllerId);

        IEnumerable<CardInstance> Zone() => step.Zone switch
        {
            CardZone.Trash => player.TrashCards,
            CardZone.HandOrTrash => player.Hand.Concat(player.TrashCards),
            _ => player.Hand
        };

        var candidates = Zone()
            .Where(card => card != frame.Source && (card.IsCharacter || card.IsStage))
            .Where(card => step.Filter is null || EffectQueries.Matches(card, step.Filter, frame.Source, frame.ControllerId, context))
            .Where(card => !frame.Scratch.Chosen.Contains(card))
            .ToList();

        if (player.Characters.Count >= PlayerState.MaxCharacters)
        {
            // A full Character area would need a trash choice - only Stages can still be played.
            candidates = candidates.Where(card => card.IsStage).ToList();
        }

        if (answer is not null)
        {
            if (answer.Option == EffectChoiceRequest.Done)
            {
                frame.Scratch.ChoiceDone = true;
            }
            else if (candidates.FirstOrDefault(card => card.InstanceId == answer.CardInstanceId) is { } picked)
            {
                frame.Scratch.Chosen.Add(picked);
                PlayCharacterOrStage(picked, player, context, step.Rested);
                candidates = candidates.Where(card => card != picked).ToList();
            }
        }

        if (!frame.Scratch.ChoiceDone && frame.Scratch.Chosen.Count < step.Amount && candidates.Count > 0)
        {
            return Ask(frame, context, $"{frame.Source.Name}: {step.Text}", [EffectChoiceRequest.Select, EffectChoiceRequest.Done], candidates);
        }

        frame.Previous = frame.Scratch.Chosen.ToList();
        return null;
    }

    // Generic "choose N cards and do something with each of them".
    private EffectChoiceRequest? ChooseAndMove(EffectFrame frame, IGameContext context, EffectAnswer? answer, int amount, bool exact,
        Func<List<CardInstance>> candidatesFactory, string title, Action<CardInstance> apply, string? playerId = null)
    {
        var candidates = candidatesFactory().Where(card => !frame.Scratch.Chosen.Contains(card)).ToList();

        if (answer is not null)
        {
            if (answer.Option == EffectChoiceRequest.Done)
            {
                frame.Scratch.ChoiceDone = true;
            }
            else if (candidates.FirstOrDefault(card => card.InstanceId == answer.CardInstanceId) is { } picked)
            {
                frame.Scratch.Chosen.Add(picked);
                apply(picked);
                candidates.Remove(picked);
            }
        }

        if (!frame.Scratch.ChoiceDone && frame.Scratch.Chosen.Count < amount && candidates.Count > 0)
        {
            var options = exact
                ? new[] { EffectChoiceRequest.Select }
                : new[] { EffectChoiceRequest.Select, EffectChoiceRequest.Done };
            return Ask(frame, context, $"{title} ({amount - frame.Scratch.Chosen.Count} left)", options, candidates, playerId);
        }

        return null;
    }

    #endregion

    #region SHARED GAME ACTIONS

    // K.O.: the Character goes to the trash and its [On K.O.] abilities are queued.
    // Returns false when the card could not be K.O.'d.
    public bool KnockOut(CardInstance card, IGameContext context, bool byEffect)
    {
        var owner = context.FindOwner(card);
        if (owner is null || !owner.Characters.Contains(card))
        {
            return false;
        }

        if (!byEffect && card.HasFlag(EffectFlags.CannotBeKOdInBattle))
        {
            return false;
        }

        // Rule 10-2-17-1: [On K.O.] is checked while the card is still on the field, then it is trashed.
        if (context.Events is { } events)
        {
            events.Publish(new CharacterKOd(card, owner.PlayerId, byEffect));
            owner.TrashCharacter(card);
            return true;
        }

        // Without an event bus: queue the abilities here (DON!! requirement, conditions).
        var onKoAbilities = card.Script.WithTiming(EffectTiming.OnKO)
            .Where(ability => EffectQueries.AbilityIsActive(ability, card, owner.PlayerId, context))
            .ToList();

        owner.TrashCharacter(card);

        foreach (var ability in onKoAbilities)
        {
            InsertNext(new EffectFrame(ability, card, owner.PlayerId));
        }

        context.RequestManualEffect(card, owner.PlayerId, EffectTiming.OnKO);

        return true;
    }

    // Plays a Character or Stage without paying its cost and queues its [On Play] abilities.
    public void PlayCharacterOrStage(CardInstance card, PlayerState player, IGameContext context, bool rested = false)
    {
        player.Hand.Remove(card);
        player.TrashCards.Remove(card);
        player.DrawDeck.Remove(card);
        player.LifeCards.Remove(card);

        card.ResetFieldState();
        card.PlayedOnTurn = context.TurnNumber;
        card.IsRested = rested;

        if (card.IsStage)
        {
            if (player.Stage is not null)
            {
                player.TrashCards.Add(player.Stage);
                player.Stage.ResetFieldState();
            }

            player.Stage = card;
        }
        else
        {
            if (player.Characters.Count >= PlayerState.MaxCharacters)
            {
                player.TrashCards.Add(card);
                context.Log("EFFECT", $"{card.Name} could not be played because the Character area is full.");
                return;
            }

            player.Characters.Add(card);
        }

        context.Log("EFFECT_PLAY", $"{player.DisplayName} played {card.Name}{(rested ? " rested" : string.Empty)}.");

        if (context.Events is { } events)
        {
            events.Publish(new CardPlayed(card, player.PlayerId, ByEffect: true));
            return;
        }

        foreach (var ability in card.Script.WithTiming(EffectTiming.OnPlay))
        {
            if (EffectQueries.AbilityIsActive(ability, card, player.PlayerId, context))
            {
                InsertNext(new EffectFrame(ability, card, player.PlayerId));
            }
        }

        context.RequestManualEffect(card, player.PlayerId, EffectTiming.OnPlay);
    }

    private static void ReturnDon(PlayerState player, int amount)
    {
        // Prefer rested DON!!, then active DON!!, then DON!! given to cards.
        var left = amount;
        var fromRested = Math.Min(left, player.RestedDon);
        player.RestedDon -= fromRested;
        left -= fromRested;

        var fromActive = Math.Min(left, player.ActiveDon);
        player.ActiveDon -= fromActive;
        left -= fromActive;

        foreach (var card in player.FieldCards().OrderBy(card => card.IsLeader ? 1 : 0))
        {
            if (left == 0)
            {
                break;
            }

            var fromCard = Math.Min(left, card.AttachedDon);
            card.AttachedDon -= fromCard;
            left -= fromCard;
        }

        player.DonDeckCount += amount - left;
    }

    private static void MoveLifeToHand(PlayerState player, int amount)
    {
        for (var index = 0; index < amount && player.LifeCards.Count > 0; index += 1)
        {
            var life = player.LifeCards[^1];
            player.LifeCards.RemoveAt(player.LifeCards.Count - 1);
            player.Hand.Add(life);
        }
    }

    private static void TrashTopOfDeck(PlayerState player, int amount)
    {
        for (var index = 0; index < amount && player.DrawDeck.Count > 0; index += 1)
        {
            player.TrashCards.Add(player.DrawDeck[0]);
            player.DrawDeck.RemoveAt(0);
        }
    }

    private static void Shuffle(IList<CardInstance> cards)
    {
        for (var index = cards.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
        }
    }

    #endregion

    #region FRAME

    private enum FrameStage
    {
        Start,
        AskCost,
        PayCost,
        Steps,
    }

    private sealed record EffectAnswer(string Option, string? CardInstanceId);

    // Progress inside one step (chosen cards, looked-at cards...). Cleared before the next step.
    private sealed class StepScratch
    {
        public bool ConditionsChecked { get; set; }
        public bool Skipped { get; set; }
        public bool OptionalAnswered { get; set; }
        public bool ChoiceDone { get; set; }
        public bool Looked { get; set; }
        public List<CardInstance> Chosen { get; } = [];
        public List<CardInstance> LookedCards { get; set; } = [];

        public void Clear()
        {
            ConditionsChecked = false;
            Skipped = false;
            OptionalAnswered = false;
            ChoiceDone = false;
            Looked = false;
            Chosen.Clear();
            LookedCards = [];
        }
    }

    // One ability that is resolving.
    private sealed class EffectFrame
    {
        public EffectFrame(Ability ability, CardInstance source, string controllerId)
        {
            Ability = ability;
            Source = source;
            ControllerId = controllerId;
        }

        public Ability Ability { get; }
        public CardInstance Source { get; }
        public string ControllerId { get; }
        public FrameStage Stage { get; set; } = FrameStage.Start;
        public bool SkipOptionalQuestion { get; init; }
        public bool InsertedByTrigger { get; set; }
        public bool Finished { get; set; }
        public int CostIndex { get; set; }
        public int StepIndex { get; set; }
        public StepScratch Scratch { get; } = new();
        // Cards chosen by the step before ("that Character").
        public List<CardInstance> Previous { get; set; } = [];
    }

    #endregion
}
