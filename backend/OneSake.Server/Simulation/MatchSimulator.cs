using OneSake.Domain;
using OneSake.Game.Effects;
using OneSake.Game.Randomness;
using OneSake.Game.Replay;
using OneSake.Game.Simulation;
using OneSake.Server.Match;

namespace OneSake.Server.Simulation;

public sealed record SimulationOptions
{
    // A match of two passive players ends by deck-out after about 85 turns (40 draws each).
    public int MaxTurns { get; init; } = 100;
    public int MaxSteps { get; init; } = 5000;
    // Rejected decisions in a row for one prompt before it counts as "no valid decision".
    public int MaxRejections { get; init; } = 40;
    // Plays the match a second time with the same seed and compares the end state.
    public bool CheckDeterminism { get; init; }
}

public sealed record SimulationResult
{
    public ulong Seed { get; init; }
    public string Bots { get; init; } = string.Empty;
    public string Decks { get; init; } = string.Empty;
    public int Steps { get; init; }
    public int Turns { get; init; }
    public bool Finished { get; init; }
    public string WinnerSeat { get; init; } = string.Empty;
    public int Rejections { get; init; }
    public IReadOnlyList<InvariantViolation> Violations { get; init; } = [];
    // Number of recorded decisions when the first violation was found.
    public int? FailedAtSeq { get; init; }
    public MatchRecording? Recording { get; init; }
    public IReadOnlyList<string> LastLog { get; init; } = [];
    public IReadOnlyList<string> CardIds { get; init; } = [];
    public IReadOnlyList<string> ManualCardIds { get; init; } = [];
    public string FinalStateHash { get; init; } = string.Empty;

    public bool Failed => Violations.Count > 0;
}

// Plays one match between two bots on a real MatchRoom (same actions and prompts as players)
// and checks the invariants after every step.
public static class MatchSimulator
{
    private const string Seat1 = "sim1";
    private const string Seat2 = "sim2";

    public static SimulationResult Play(
        ulong seed,
        PlayerDeckSubmissionDto deck1,
        PlayerDeckSubmissionDto deck2,
        Func<IRandom, IBotPlayer> bot1,
        Func<IRandom, IBotPlayer> bot2,
        SimulationOptions? options = null)
    {
        options ??= new SimulationOptions();
        var result = PlayOnce(seed, deck1, deck2, bot1, bot2, options);

        if (options.CheckDeterminism && !result.Failed)
        {
            var again = PlayOnce(seed, deck1, deck2, bot1, bot2, options);
            if (again.FinalStateHash != result.FinalStateHash || again.Steps != result.Steps)
            {
                result = result with
                {
                    Violations = [new InvariantViolation(InvariantNames.Determinism, $"The same seed gave another match ({result.Steps} vs {again.Steps} steps).")]
                };
            }
        }

        return result;
    }

    private static SimulationResult PlayOnce(
        ulong seed,
        PlayerDeckSubmissionDto deck1,
        PlayerDeckSubmissionDto deck2,
        Func<IRandom, IBotPlayer> bot1Factory,
        Func<IRandom, IBotPlayer> bot2Factory,
        SimulationOptions options)
    {
        var bots = new Dictionary<string, IBotPlayer>(StringComparer.Ordinal)
        {
            [Seat1] = bot1Factory(Pcg32.FromSeed(seed, "bot1")),
            [Seat2] = bot2Factory(Pcg32.FromSeed(seed, "bot2"))
        };
        var room = new MatchRoom("SIM", seed: seed);
        var match = new MatchState();
        var violations = new List<InvariantViolation>();
        var steps = 0;
        var rejections = 0;

        try
        {
            room.JoinPlayer(Seat1, "Bot 1");
            room.JoinPlayer(Seat2, "Bot 2");
            room.SetPlayerDeck(Seat1, deck1);
            room.SetPlayerDeck(Seat2, deck2);
            match.Apply(room.StartMatch());
            violations.AddRange(InvariantChecker.Check(room.CreateInvariantSnapshot()));

            while (violations.Count == 0 && !match.IsGameOver && steps < options.MaxSteps)
            {
                var playerId = new[] { Seat1, Seat2 }.FirstOrDefault(match.Prompts.ContainsKey);
                if (playerId is null)
                {
                    violations.Add(new(InvariantNames.NoOpenPrompt, "The client has no open prompt, but the match is not over."));
                    break;
                }

                var prompt = match.Prompts[playerId];
                var view = new BotView(match.Views[playerId], prompt, playerId);
                var rejected = new List<string>();
                MatchUpdate? update = null;
                BotDecision? applied = null;

                while (update is null)
                {
                    BotDecision decision;
                    try
                    {
                        decision = bots[playerId].Decide(view, rejected);
                    }
                    catch (InvalidOperationException exception)
                    {
                        violations.Add(new(InvariantNames.NoValidDecision, $"{prompt.Kind} \"{prompt.Title}\": {exception.Message} Rejected: {string.Join("; ", rejected)}"));
                        break;
                    }

                    try
                    {
                        update = decision.Manual is { } manual
                            ? room.ManualAction(playerId, manual)
                            : room.SubmitChoice(decision.Choice!);
                        applied = decision;
                    }
                    catch (InvalidOperationException)
                    {
                        // The server does not accept it (rules a client cannot see) - choose again.
                        rejections += 1;
                        rejected.Add(decision.Key);
                        if (rejected.Count >= options.MaxRejections)
                        {
                            violations.Add(new(InvariantNames.NoValidDecision, $"{prompt.Kind}: {rejected.Count} decisions were rejected in a row."));
                            break;
                        }
                    }
                }

                if (update is null)
                {
                    break;
                }

                // An answer uses up the prompt. A manual action keeps it open (if it changed, the
                // update contains the new one).
                if (applied?.Choice is not null)
                {
                    match.Prompts.Remove(playerId);
                }

                match.Apply(update);
                steps += 1;

                violations.AddRange(InvariantChecker.Check(room.CreateInvariantSnapshot()));
                foreach (var entry in update.LogEvents.Where(entry => entry.Type == "EFFECT_ERROR"))
                {
                    violations.Add(new(InvariantNames.EffectLoop, entry.Text));
                }

                if (match.Turn > options.MaxTurns)
                {
                    violations.Add(new(InvariantNames.Hang, $"Still running after {options.MaxTurns} turns."));
                }
            }

            if (violations.Count == 0 && !match.IsGameOver)
            {
                violations.Add(new(InvariantNames.Hang, $"Still running after {steps} steps."));
            }

            if (violations.Count == 0)
            {
                violations.AddRange(CheckNothingAfterGameOver(room, match));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            violations.Add(new(InvariantNames.Exception, $"{exception.GetType().Name}: {exception.Message} | {exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"));
        }

        var cardIds = deck1.Cards.Concat(deck2.Cards).Select(card => card.CardId).Append(deck1.LeaderCardId).Append(deck2.LeaderCardId).Distinct().ToArray();
        var manualCardIds = deck1.Cards.Concat(deck2.Cards)
            .Where(card => CardScriptRegistry.Resolve(card.CardId, card.Text, card.Category).Status == CardScriptStatus.Manual)
            .Select(card => card.CardId)
            .Distinct()
            .ToArray();

        return new SimulationResult
        {
            Seed = seed,
            Bots = $"{bots[Seat1].Name}-{bots[Seat2].Name}",
            Decks = $"{deck1.DeckName} vs {deck2.DeckName}",
            Steps = steps,
            Turns = match.Turn,
            Finished = match.IsGameOver,
            WinnerSeat = match.Winner switch { Seat1 => "1", Seat2 => "2", _ => string.Empty },
            Rejections = rejections,
            Violations = violations,
            FailedAtSeq = violations.Count > 0 ? room.RecordedInputCount : null,
            Recording = violations.Count > 0 ? room.CreateRecordingSnapshot() : null,
            LastLog = match.Log.TakeLast(20).Select(entry => $"T{entry.TurnNumber} {entry.Phase} {entry.Type}: {entry.Text}").ToArray(),
            CardIds = cardIds,
            ManualCardIds = manualCardIds,
            FinalStateHash = room.ComputeStateHash()
        };
    }

    // Rule: after the end of the game nothing can be done any more.
    private static IEnumerable<InvariantViolation> CheckNothingAfterGameOver(MatchRoom room, MatchState match)
    {
        foreach (var (playerId, prompt) in match.LastPrompts)
        {
            if (Accepts(() => room.SubmitChoice(new ChoiceSubmissionDto { ChoiceId = prompt.ChoiceId, PlayerId = playerId, SelectedOption = prompt.Options.FirstOrDefault() ?? "END_TURN" })))
            {
                yield return new InvariantViolation(InvariantNames.ActionAfterGameOver, $"A {prompt.Kind} answer was accepted after the game ended.");
            }
        }

        if (Accepts(() => room.ManualAction(Seat1, new ManualActionDto { Kind = "DRAW", Amount = 1 })))
        {
            yield return new InvariantViolation(InvariantNames.ActionAfterGameOver, "A manual action was accepted after the game ended.");
        }
    }

    private static bool Accepts(Func<MatchUpdate> action)
    {
        try
        {
            action();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // What the two clients know: their own view, their open prompt, the log.
    private sealed class MatchState
    {
        public Dictionary<string, GameStateDto> Views { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ChoicePromptDto> Prompts { get; } = new(StringComparer.Ordinal);
        // The last prompt of every player (to check that nothing is possible after the end).
        public Dictionary<string, ChoicePromptDto> LastPrompts { get; } = new(StringComparer.Ordinal);
        public List<LogEventDto> Log { get; } = [];
        public bool IsGameOver => Views.Values.FirstOrDefault()?.Phase == MatchPhase.GameOver;
        public int Turn => Views.Values.FirstOrDefault()?.TurnNumber ?? 0;
        public string Winner => Views.Values.FirstOrDefault()?.WinnerPlayerId ?? string.Empty;

        public void Apply(MatchUpdate update)
        {
            foreach (var (playerId, view) in update.StateSnapshots)
            {
                Views[playerId] = view;
            }

            foreach (var prompt in update.ChoicePrompts)
            {
                Prompts[prompt.PlayerId] = prompt;
                LastPrompts[prompt.PlayerId] = prompt;
            }

            Log.AddRange(update.LogEvents);
            if (IsGameOver)
            {
                Prompts.Clear();
            }
        }
    }
}
