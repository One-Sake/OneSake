using OneSake.Domain;
using OneSake.Game.State;

namespace OneSake.Game.Simulation;

// The complete state of a match for the invariant checks (MatchRoom.CreateInvariantSnapshot).
public sealed record InvariantSnapshot
{
    public IReadOnlyList<PlayerState> Players { get; init; } = [];
    // Leader + deck size per player ID: this many cards must exist the whole match.
    public IReadOnlyDictionary<string, int> ExpectedCardCounts { get; init; } = new Dictionary<string, int>();
    // Cards that belong to no area right now (rule 10-1-5-3: a [Trigger] card while it resolves).
    public IReadOnlyList<CardInstance> CardsInResolution { get; init; } = [];
    public MatchPhase Phase { get; init; }
    public string ActivePlayerId { get; init; } = string.Empty;
    public string WinnerPlayerId { get; init; } = string.Empty;
    public IReadOnlyList<ChoicePromptDto> OpenPrompts { get; init; } = [];
}

public sealed record InvariantViolation(string Invariant, string Message)
{
    public override string ToString() => $"{Invariant}: {Message}";
}

public static class InvariantNames
{
    public const string CardConservation = "CardConservation";
    public const string UniqueCards = "UniqueCards";
    public const string DonCount = "DonCount";
    public const string CharacterLimit = "CharacterLimit";
    public const string NegativeValue = "NegativeValue";
    public const string Phase = "Phase";
    public const string Prompts = "Prompts";
    public const string NoOpenPrompt = "NoOpenPrompt";
    // Found by the simulator, not by the snapshot check:
    public const string Exception = "Exception";
    public const string NoValidDecision = "NoValidDecision";
    public const string EffectLoop = "EffectLoop";
    public const string Hang = "Hang";
    public const string ActionAfterGameOver = "ActionAfterGameOver";
    public const string Determinism = "Determinism";
}

// Basic rules that must hold after EVERY step of a match.
public static class InvariantChecker
{
    private static readonly MatchPhase[] RunningPhases = [MatchPhase.Refresh, MatchPhase.Draw, MatchPhase.Don, MatchPhase.Main, MatchPhase.End];

    public static IReadOnlyList<InvariantViolation> Check(InvariantSnapshot snapshot)
    {
        var violations = new List<InvariantViolation>();
        var allCards = new List<CardInstance>();

        foreach (var player in snapshot.Players)
        {
            var cards = CardsOf(player).ToList();
            var inResolution = snapshot.CardsInResolution.Where(card => card.OwnerId == player.PlayerId).ToList();
            allCards.AddRange(cards);
            allCards.AddRange(inResolution);

            // Cards never appear or disappear: Leader + deck size in all areas together.
            if (snapshot.ExpectedCardCounts.TryGetValue(player.PlayerId, out var expected) && cards.Count + inResolution.Count != expected)
            {
                violations.Add(new(InvariantNames.CardConservation,
                    $"{player.DisplayName} has {cards.Count + inResolution.Count} cards, expected {expected} " +
                    $"(deck {player.DrawDeck.Count}, hand {player.Hand.Count}, characters {player.Characters.Count}, stage {(player.Stage is null ? 0 : 1)}, " +
                    $"life {player.LifeCards.Count}, trash {player.TrashCards.Count}, in resolution {inResolution.Count})."));
            }

            // Rule 5-1 / 6-4: 10 DON!! per player in the DON!! deck, the cost area or given to cards.
            var given = player.FieldCards().Sum(card => card.AttachedDon);
            var donTotal = player.DonDeckCount + player.ActiveDon + player.RestedDon + given;
            if (donTotal != PlayerState.DonDeckSize)
            {
                violations.Add(new(InvariantNames.DonCount,
                    $"{player.DisplayName} has {donTotal} DON!! (deck {player.DonDeckCount}, active {player.ActiveDon}, rested {player.RestedDon}, given {given}), expected {PlayerState.DonDeckSize}."));
            }

            if (player.DonDeckCount < 0 || player.ActiveDon < 0 || player.RestedDon < 0 || player.FieldCards().Any(card => card.AttachedDon < 0))
            {
                violations.Add(new(InvariantNames.NegativeValue, $"{player.DisplayName} has a negative DON!! count."));
            }

            // Rule 3-7-6: at most 5 Characters (the Stage area holds one card by design).
            if (player.Characters.Count > PlayerState.MaxCharacters)
            {
                violations.Add(new(InvariantNames.CharacterLimit, $"{player.DisplayName} has {player.Characters.Count} Characters."));
            }

            // Rule 1-3-6-2: a cost is never below 0. Power may be negative (rule 1-3-6-1).
            foreach (var card in player.FieldCards().Concat(player.Hand).Where(card => card.CurrentCost < 0))
            {
                violations.Add(new(InvariantNames.NegativeValue, $"{card.Name} ({card.InstanceId}) has the cost {card.CurrentCost}."));
            }

            // The Leader never leaves the Leader area.
            if (cards.Any(card => card.IsLeader && card != player.Leader))
            {
                violations.Add(new(InvariantNames.UniqueCards, $"{player.DisplayName} has a Leader card outside the Leader area."));
            }
        }

        // The same card in two places (or twice in one place).
        foreach (var duplicate in allCards.GroupBy(card => card.InstanceId).Where(group => group.Count() > 1))
        {
            violations.Add(new(InvariantNames.UniqueCards, $"{duplicate.Key} exists {duplicate.Count()} times."));
        }

        var playerIds = snapshot.Players.Select(player => player.PlayerId).ToHashSet(StringComparer.Ordinal);
        if (RunningPhases.Contains(snapshot.Phase) && !playerIds.Contains(snapshot.ActivePlayerId))
        {
            violations.Add(new(InvariantNames.Phase, $"Phase {snapshot.Phase} without a valid turn player ('{snapshot.ActivePlayerId}')."));
        }

        if (snapshot.Phase == MatchPhase.GameOver && snapshot.WinnerPlayerId.Length > 0 && !playerIds.Contains(snapshot.WinnerPlayerId))
        {
            violations.Add(new(InvariantNames.Phase, $"The winner '{snapshot.WinnerPlayerId}' is not a player."));
        }

        foreach (var prompt in snapshot.OpenPrompts)
        {
            if (!playerIds.Contains(prompt.PlayerId) || prompt.Options.Count == 0)
            {
                violations.Add(new(InvariantNames.Prompts, $"Prompt {prompt.ChoiceId} ({prompt.Kind}) for '{prompt.PlayerId}' with {prompt.Options.Count} options."));
            }
        }

        if (snapshot.Phase == MatchPhase.GameOver && snapshot.OpenPrompts.Count > 0)
        {
            violations.Add(new(InvariantNames.Prompts, $"{snapshot.OpenPrompts.Count} prompt(s) are still open after the game ended."));
        }

        // A running match always waits for somebody - otherwise it is stuck.
        if ((RunningPhases.Contains(snapshot.Phase) || snapshot.Phase is MatchPhase.TurnOrder or MatchPhase.Mulligan) && snapshot.OpenPrompts.Count == 0)
        {
            violations.Add(new(InvariantNames.NoOpenPrompt, $"Phase {snapshot.Phase}: nobody has a prompt, the match cannot go on."));
        }

        return violations;
    }

    private static IEnumerable<CardInstance> CardsOf(PlayerState player)
    {
        if (player.Leader is not null)
        {
            yield return player.Leader;
        }

        if (player.Stage is not null)
        {
            yield return player.Stage;
        }

        foreach (var card in player.Characters.Concat(player.Hand).Concat(player.DrawDeck).Concat(player.LifeCards).Concat(player.TrashCards))
        {
            yield return card;
        }
    }
}
