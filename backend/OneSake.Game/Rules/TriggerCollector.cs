using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// An effect that is waiting to be activated and resolved.
public sealed class PendingTrigger
{
    public string Id { get; init; } = string.Empty;
    public CardInstance Source { get; init; } = new();
    public string ControllerId { get; init; } = string.Empty;
    public EffectTiming Timing { get; init; }
    // The automated ability, or null when the text has to be resolved manually.
    public Ability? Ability { get; init; }
    public string? ManualText { get; init; }
    // Started by the player on purpose (Event, [Counter], [Trigger], [Activate: Main]):
    // no "use it?" question, no checks of the activation timing.
    public bool StartedByPlayer { get; init; }
    // Rule 8-1-3-1-3: the effect does not activate when its card left the field in the meantime.
    // Not for [On K.O.] (rule 10-2-17-2) and player-started effects.
    public bool NeedsSourceOnField { get; init; }

    public string Text => Ability?.Text ?? ManualText ?? string.Empty;
}

// Rule 8-1-3-1: auto effects activate when their activation event happens.
// Turns one game event into the pending effects of all cards (timing, [DON!! xX], [Your Turn],
// "if" conditions, [Once Per Turn]). Nothing is resolved here.
public sealed class TriggerCollector
{
    private readonly IGameContext _context;
    private int _nextId;

    public TriggerCollector(IGameContext context)
    {
        _context = context;
    }

    public IReadOnlyList<PendingTrigger> Collect(GameEvent gameEvent)
    {
        var triggers = new List<PendingTrigger>();

        switch (gameEvent)
        {
            // Rule 10-2-6-1: [On Play] when the card is played.
            case CardPlayed played:
                AddAuto(triggers, played.Card, played.PlayerId, EffectTiming.OnPlay, needsSourceOnField: true);
                break;

            // Rule 7-1-1-3 / 10-2-5-1: [When Attacking] of the attacker. Rule 10-2-16-1:
            // [On Your Opponent's Attack] of the defender's cards.
            case AttackDeclared attack:
                AddAuto(triggers, attack.Attacker, attack.AttackerPlayerId, EffectTiming.WhenAttacking, needsSourceOnField: true);
                foreach (var card in _context.GetPlayer(attack.DefenderPlayerId).FieldCards().ToList())
                {
                    AddAuto(triggers, card, attack.DefenderPlayerId, EffectTiming.OnOpponentsAttack, needsSourceOnField: true);
                }

                break;

            // Rule 7-1-2-2 / 10-2-15-1: [On Block] when the [Blocker] is activated.
            case BlockerDeclared block:
                AddAuto(triggers, block.Blocker, block.PlayerId, EffectTiming.OnBlock, needsSourceOnField: true);
                break;

            // Rule 10-2-17-1: [On K.O.] is checked on the field and resolved from the trash.
            case CharacterKOd knockedOut:
                AddAuto(triggers, knockedOut.Card, knockedOut.OwnerId, EffectTiming.OnKO, needsSourceOnField: false);
                break;

            // Rule 6-6-1-1: [End of Your Turn] of the turn player, [End of Your Opponent's Turn]
            // of the other player.
            case TurnEnding ending:
                foreach (var player in _context.Players)
                {
                    var timing = player.PlayerId == ending.PlayerId ? EffectTiming.EndOfYourTurn : EffectTiming.EndOfOpponentsTurn;
                    foreach (var card in player.FieldCards().ToList())
                    {
                        AddAuto(triggers, card, player.PlayerId, timing, needsSourceOnField: true);
                    }
                }

                break;
        }

        return triggers;
    }

    // Effects the player starts on purpose: an Event ([Main]/[Counter]), a [Trigger] or the
    // [Main] of a card. Unsupported texts of that timing become manual effects.
    public IReadOnlyList<PendingTrigger> Activated(CardInstance card, string controllerId, EffectTiming timing)
    {
        var triggers = card.Script.WithTiming(timing)
            .Select(ability => Create(card, controllerId, timing, ability, null, startedByPlayer: true, needsSourceOnField: false))
            .ToList();
        triggers.AddRange(ManualTexts(card, timing)
            .Select(text => Create(card, controllerId, timing, null, text, startedByPlayer: true, needsSourceOnField: false)));
        return triggers;
    }

    // One chosen [Activate: Main] ability (rule 6-5-4).
    public PendingTrigger Activated(CardInstance card, string controllerId, Ability ability)
    {
        return Create(card, controllerId, ability.Timing, ability, null, startedByPlayer: true, needsSourceOnField: false);
    }

    private void AddAuto(List<PendingTrigger> triggers, CardInstance card, string controllerId, EffectTiming timing, bool needsSourceOnField)
    {
        foreach (var ability in card.Script.WithTiming(timing))
        {
            // Rule 8-3-2-3: [DON!! xX] (and the other conditions) must be met when the timing happens.
            // Rule 10-2-13-3: a used [Once Per Turn] effect cannot activate again this turn.
            if (!EffectQueries.AbilityIsActive(ability, card, controllerId, _context)
                || EffectEngine.IsUsedThisTurn(ability, card, _context))
            {
                continue;
            }

            triggers.Add(Create(card, controllerId, timing, ability, null, startedByPlayer: false, needsSourceOnField));
        }

        foreach (var text in ManualTexts(card, timing))
        {
            triggers.Add(Create(card, controllerId, timing, null, text, startedByPlayer: false, needsSourceOnField));
        }
    }

    private PendingTrigger Create(
        CardInstance card,
        string controllerId,
        EffectTiming timing,
        Ability? ability,
        string? manualText,
        bool startedByPlayer,
        bool needsSourceOnField)
    {
        _nextId += 1;
        return new PendingTrigger
        {
            Id = $"t{_nextId}",
            Source = card,
            ControllerId = controllerId,
            Timing = timing,
            Ability = ability,
            ManualText = manualText,
            StartedByPlayer = startedByPlayer,
            NeedsSourceOnField = needsSourceOnField
        };
    }

    // Unsupported texts of a card that belong to the timing (e.g. all "[On Play] ..." parts).
    public static IEnumerable<string> ManualTexts(CardInstance card, EffectTiming timing)
    {
        var tag = TimingTag(timing);
        return tag is null
            ? []
            : card.Script.UnsupportedTexts.Where(text => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
    }

    public static string? TimingTag(EffectTiming timing)
    {
        return timing switch
        {
            EffectTiming.OnPlay => "[On Play]",
            EffectTiming.WhenAttacking => "[When Attacking]",
            EffectTiming.OnBlock => "[On Block]",
            EffectTiming.OnKO => "[On K.O.]",
            EffectTiming.EventMain => "[Main]",
            EffectTiming.EventCounter => "[Counter]",
            EffectTiming.Trigger => "[Trigger]",
            EffectTiming.EndOfYourTurn => "[End of Your Turn]",
            EffectTiming.EndOfOpponentsTurn => "[End of Your Opponent's Turn]",
            EffectTiming.OnOpponentsAttack => "[On Your Opponent's Attack]",
            EffectTiming.ActivateMain => "[Activate: Main]",
            _ => null
        };
    }
}
