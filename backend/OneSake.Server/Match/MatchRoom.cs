using System.Security.Cryptography;
using OneSake.Domain;
using OneSake.Game.Actions;
using OneSake.Game.Effects;
using OneSake.Game.State;

namespace OneSake.Server.Match;

public sealed class MatchRoom
{
    // Choice kinds sent to the clients.
    public const string TurnOrderDecisionKind = "TURN_ORDER_DECISION";
    public const string GoFirstOption = "GO_FIRST";
    public const string GoSecondOption = "GO_SECOND";
    public const string MulliganDecisionKind = "MULLIGAN_DECISION";
    public const string MainActionKind = "MAIN_ACTION";
    public const string BlockStepKind = "BLOCK_STEP";
    public const string CounterStepKind = "COUNTER_STEP";
    // A card effect asks the player something (choose a target, pay a cost...).
    public const string EffectChoiceKind = "EFFECT_CHOICE";
    // A Life card with [Trigger] was added by damage: activate it or add it to the hand.
    public const string TriggerDecisionKind = "TRIGGER_DECISION";
    public const string ActivateTriggerOption = "ACTIVATE";
    public const string DeclineTriggerOption = "DECLINE";
    // An effect the engine cannot automate: the player resolves it with manual actions, then presses "Done".
    public const string ManualEffectKind = "MANUAL_EFFECT";
    public const string ManualDoneOption = "DONE";

    // Players with one of these prompts open may use manual actions even when it is not their turn.
    private static readonly HashSet<string> ManualDecisionKinds = new(StringComparer.Ordinal)
    {
        EffectChoiceKind,
        CounterStepKind,
        TriggerDecisionKind,
        ManualEffectKind
    };

    // Card effects. The engine resolves abilities, the continuation runs when it is finished.
    private readonly EffectEngine _effects = new();
    private readonly RoomContext _context;
    private readonly ManualActions _manualActions;
    // Effect texts waiting for a "resolve manually" prompt, in order.
    private readonly List<ManualEffectRequest> _manualEffects = [];
    private Action? _afterEffects;
    private int _damageLeft;
    private CardInstance? _pendingTriggerCard;

    private const int OpeningHandSize = 5;
    private const int DefaultLeaderLife = 5;
    private const int DefaultLeaderPower = 5000;
    // Safety limit for tie rounds (a real tie streak this long practically never happens).
    private const int MaxTurnOrderRounds = 20;

    private readonly object _sync = new();
    private readonly string _roomCode;
    private readonly List<PlayerState> _players = [];
    private readonly Dictionary<string, ChoicePromptDto> _openChoicePrompts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mulliganPlayerDecisions = new(StringComparer.Ordinal);
    private readonly List<LogEventDto> _pendingLogEvents = [];
    private readonly List<ChoicePromptDto> _pendingChoicePrompts = [];

    private readonly PlayCardAction _playCardAction = new();
    private readonly AttachDonAction _attachDonAction = new();
    private readonly AttackAction _attackAction = new();
    private readonly BlockAction _blockAction = new();
    private readonly CounterAction _counterAction = new();
    private readonly DamageStepAction _damageStepAction = new();
    private readonly EndTurnAction _endTurnAction = new();

    private int _logSeq;
    private int _turnNumber;
    private string _activePlayerId = string.Empty;
    private string _winnerPlayerId = string.Empty;
    private MatchPhase _phase = MatchPhase.Lobby;
    private BattleState? _battle;

    private readonly IDiceRoller _diceRoller;
    private readonly List<TurnOrderRoundDto> _turnOrderRounds = [];
    private string _turnOrderWinnerId = string.Empty;
    private string? _turnOrderDecision;
    private string? _firstPlayerId;

    // Result of the match that just ended. Reported once with the next update.
    private DateTimeOffset _matchStartedAtUtc;
    private bool _resultReported;
    private MatchResultData? _pendingResult;

    public const int MaxPlayers = 2;

    private readonly DateTimeOffset _createdAtUtc;
    private DateTimeOffset _lastActivityUtc;

    public MatchRoom(string roomCode, bool isQuickMatch = false, IDiceRoller? diceRoller = null)
    {
        _roomCode = roomCode;
        IsQuickMatch = isQuickMatch;
        _diceRoller = diceRoller ?? CryptoDiceRoller.Instance;
        _createdAtUtc = DateTimeOffset.UtcNow;
        _lastActivityUtc = _createdAtUtc;
        _context = new RoomContext(this);
        _manualActions = new ManualActions(_effects);
    }

    private sealed record ManualEffectRequest(string PlayerId, CardInstance Card, string Text);

    // Gives the effect engine access to the running match.
    private sealed class RoomContext : IGameContext
    {
        private readonly MatchRoom _room;

        public RoomContext(MatchRoom room)
        {
            _room = room;
        }

        public IReadOnlyList<PlayerState> Players => _room._players;
        public int TurnNumber => _room._turnNumber;
        public string ActivePlayerId => _room._activePlayerId;
        public BattleState? Battle => _room._battle;

        public void Log(string type, string text)
        {
            _room.CreateLogEvent(type, text);
        }

        public void RequestManualEffect(CardInstance card, string controllerId, EffectTiming timing)
        {
            _room.QueueManualEffects(card, controllerId, timing);
        }
    }

    public string RoomCode => _roomCode;

    // Quick Match rooms are private and do not appear in the public room list.
    public bool IsQuickMatch { get; }

    #region ROOM MEMBERSHIP

    public bool HasPlayer(string connectionId)
    {
        lock (_sync)
        {
            return _players.Any(player => player.PlayerId == connectionId);
        }
    }

    // Removes a player from the room. Returns null when the player was not in this room.
    // If a match was running, it is aborted and the room goes back to the lobby.
    // endReason is stored when this ends a running match (the remaining player wins).
    public MatchUpdate? RemovePlayer(string connectionId, string endReason = MatchEndReasons.Disconnect)
    {
        lock (_sync)
        {
            var player = _players.FirstOrDefault(entry => entry.PlayerId == connectionId);
            if (player is null)
            {
                return null;
            }

            var wasMatchRunning = _phase != MatchPhase.Lobby && _phase != MatchPhase.GameOver;
            var remainingPlayer = _players.FirstOrDefault(entry => entry.PlayerId != connectionId);
            if (wasMatchRunning && remainingPlayer is not null)
            {
                RecordResult(remainingPlayer, player, endReason);
            }

            _players.Remove(player);
            CreateLogEvent("LEAVE", $"{player.DisplayName} left room {_roomCode}.");

            if (_phase != MatchPhase.Lobby)
            {
                _phase = MatchPhase.Lobby;
                _turnNumber = 0;
                _activePlayerId = string.Empty;
                _winnerPlayerId = string.Empty;
                _battle = null;
                _openChoicePrompts.Clear();
                _mulliganPlayerDecisions.Clear();
                ResetEffectState();
                ResetTurnOrder();
                CreateLogEvent("MATCH_ABORTED", "Match ended because a player left.");
            }

            return CreateUpdate();
        }
    }

    // A room is inactive when nobody is in it and nothing happened for the given time.
    public bool IsInactive(DateTimeOffset now, TimeSpan timeout)
    {
        lock (_sync)
        {
            return _players.Count == 0 && now - _lastActivityUtc >= timeout;
        }
    }

    public RoomSummaryDto CreateSummary()
    {
        lock (_sync)
        {
            return new RoomSummaryDto
            {
                RoomCode = _roomCode,
                PlayerCount = _players.Count,
                MaxPlayers = MaxPlayers,
                Phase = _phase,
                PlayerNames = _players.Select(player => player.DisplayName).ToArray(),
                CreatedAtUnixMs = _createdAtUtc.ToUnixTimeMilliseconds()
            };
        }
    }

    #endregion

    #region PUBLIC API
    public MatchUpdate JoinPlayer(string connectionId, string displayName, string? guestId = null)
    {
        lock (_sync)
        {
            var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? $"Player-{_players.Count + 1}"
                : displayName.Trim();

            var existingPlayer = _players.FirstOrDefault(player => player.PlayerId == connectionId);
            if (existingPlayer is null)
            {
                if (_players.Count >= MaxPlayers)
                {
                    throw new InvalidOperationException("Room is full.");
                }

                _players.Add(new PlayerState
                {
                    PlayerId = connectionId,
                    DisplayName = normalizedDisplayName,
                    GuestId = guestId,
                    Connected = true
                });
            }
            else
            {
                existingPlayer.DisplayName = normalizedDisplayName;
                existingPlayer.GuestId = guestId ?? existingPlayer.GuestId;
                existingPlayer.Connected = true;
            }

            CreateLogEvent("JOIN", $"{normalizedDisplayName} joined room {_roomCode}.");
            return CreateUpdate();
        }
    }

    // Rule 5-2: game preparation. First the dice decide who chooses the turn order,
    // the opening hands are drawn after that decision. Life cards follow after the mulligan (5-2-1-7).
    public MatchUpdate StartMatch()
    {
        lock (_sync)
        {
            if (_players.Count != 2)
            {
                throw new InvalidOperationException("StartMatch requires exactly 2 players.");
            }

            if (_players.Any(player => !player.HasDeck))
            {
                throw new InvalidOperationException("Both players must select a deck before starting.");
            }

            if (_phase != MatchPhase.Lobby && _phase != MatchPhase.GameOver)
            {
                throw new InvalidOperationException("The match is already running.");
            }

            _turnNumber = 1;
            _phase = MatchPhase.TurnOrder;
            _matchStartedAtUtc = DateTimeOffset.UtcNow;
            _resultReported = false;
            _pendingResult = null;
            _activePlayerId = string.Empty;
            _winnerPlayerId = string.Empty;
            _battle = null;
            _logSeq = 0;
            _openChoicePrompts.Clear();
            _mulliganPlayerDecisions.Clear();
            ResetEffectState();

            foreach (var player in _players)
            {
                player.Leader = BuildLeader(player);
                player.Stage = null;
                player.DrawDeck = BuildMatchDeck(player);
                Shuffle(player.DrawDeck);
                player.Hand.Clear();
                player.Characters.Clear();
                player.LifeCards.Clear();
                player.TrashCards.Clear();
                player.DonDeckCount = PlayerState.DonDeckSize;
                player.ActiveDon = 0;
                player.RestedDon = 0;
                player.TurnsStarted = 0;
                player.TurnFlags.Clear();
                player.DonToActivateAtEndOfTurn = 0;
                player.CloseManualView();
                player.Connected = true;
            }

            CreateLogEvent("START_MATCH", "Match started.");
            CreateLogEvent("SHUFFLE_DECKS", "Both players shuffled their decks.");
            RollForTurnOrder();

            return CreateUpdate();
        }
    }

    public MatchUpdate SetPlayerDeck(string connectionId, PlayerDeckSubmissionDto deck)
    {
        lock (_sync)
        {
            var player = GetPlayerSlot(connectionId);

            var normalizedCards = deck.Cards
                .Where(card => !string.IsNullOrWhiteSpace(card.CardId) && card.Quantity > 0)
                .GroupBy(card => card.CardId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var firstCard = group.First();
                    return firstCard with
                    {
                        CardId = group.Key,
                        Name = string.IsNullOrWhiteSpace(firstCard.Name)
                            ? group.Key
                            : firstCard.Name.Trim(),
                        Quantity = group.Sum(card => card.Quantity)
                    };
                })
                .OrderBy(card => card.CardId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            player.DeckId = deck.DeckId.Trim();
            player.DeckName = string.IsNullOrWhiteSpace(deck.DeckName)
                ? "Unnamed Deck"
                : deck.DeckName.Trim();
            player.LeaderCardId = deck.LeaderCardId.Trim();
            player.DeckSubmission = deck;
            player.DeckCards = normalizedCards;
            player.MainDeckCount = normalizedCards.Sum(card => card.Quantity);
            player.HasDeck = !string.IsNullOrWhiteSpace(player.LeaderCardId) && player.MainDeckCount > 0;

            CreateLogEvent("DECK_SELECTED", $"{player.DisplayName} selected deck {player.DeckName}.");
            return CreateUpdate();
        }
    }

    public MatchUpdate SubmitChoice(ChoiceSubmissionDto submission)
    {
        lock (_sync)
        {
            if (!_openChoicePrompts.TryGetValue(submission.ChoiceId, out var prompt))
            {
                throw new InvalidOperationException("Choice prompt was not found.");
            }

            if (!string.Equals(prompt.PlayerId, submission.PlayerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Choice prompt does not belong to this player.");
            }

            var normalizedOption = prompt.Options.FirstOrDefault(
                option => string.Equals(option, submission.SelectedOption, StringComparison.OrdinalIgnoreCase));

            if (normalizedOption is null)
            {
                throw new InvalidOperationException("Selected option is invalid.");
            }

            var player = GetPlayerSlot(submission.PlayerId);
            var opponent = GetPlayerSlot(GetOtherPlayerId(player.PlayerId));

            // Actions throw when they are not allowed. The prompt then stays open so the player can retry.
            switch (prompt.Kind)
            {
                case TurnOrderDecisionKind:
                    HandleTurnOrderDecision(normalizedOption, player, opponent);
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    break;

                case MulliganDecisionKind:
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    HandleMulliganDecision(normalizedOption, player);
                    break;

                case MainActionKind:
                    HandleMainAction(normalizedOption, player, opponent, submission, prompt);
                    break;

                case BlockStepKind:
                    HandleBlockStep(normalizedOption, player, submission, prompt);
                    break;

                case CounterStepKind:
                    HandleCounterStep(normalizedOption, player, submission, prompt);
                    break;

                case EffectChoiceKind:
                {
                    // Answer() throws for invalid answers - then the prompt stays open.
                    var request = _effects.Answer(_context, normalizedOption, submission.SelectedCardInstanceId);
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    ContinueEffects(request);
                    break;
                }

                case TriggerDecisionKind:
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    HandleTriggerDecision(normalizedOption, player);
                    break;

                case ManualEffectKind:
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    CreateLogEvent("MANUAL", $"✋ {player.DisplayName}: Effekt von {prompt.SourceName} erledigt.");
                    ContinueEffects(_effects.Run(_context));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported choice kind '{prompt.Kind}'.");
            }

            return CreateUpdate();
        }
    }

    // Manual mode: the player resolves a card effect by hand. Every action is logged for both players.
    public MatchUpdate ManualAction(string playerId, ManualActionDto action)
    {
        lock (_sync)
        {
            var player = GetPlayerSlot(playerId);
            EnsureCanUseManualActions(player, action);
            var opponent = GetPlayerSlot(GetOtherPlayerId(player.PlayerId));

            var text = _manualActions.Execute(action, player, opponent, _context);
            CreateLogEvent("MANUAL", $"✋ {player.DisplayName} (manuell): {text}");
            AfterManualAction();

            return CreateUpdate();
        }
    }

    #endregion

    #region LOGGING

    public LogEventDto CreateLogEvent(string type, string text)
    {
        lock (_sync)
        {
            _logSeq += 1;
            var logEvent = new LogEventDto
            {
                RoomCode = _roomCode,
                Seq = _logSeq,
                Type = type,
                Text = text,
                TurnNumber = _turnNumber,
                Phase = _phase,
                TsUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _pendingLogEvents.Add(logEvent);
            return logEvent;
        }
    }

    #endregion

    #region TURN ORDER

    // Both players roll until the values differ. Every round is kept for the animation.
    private void RollForTurnOrder()
    {
        ResetTurnOrder();
        var first = _players[0];
        var second = _players[1];

        while (true)
        {
            var firstValue = _diceRoller.Roll();
            var secondValue = _diceRoller.Roll();

            _turnOrderRounds.Add(new TurnOrderRoundDto
            {
                Rolls =
                [
                    new DiceRollDto { PlayerId = first.PlayerId, Value = firstValue },
                    new DiceRollDto { PlayerId = second.PlayerId, Value = secondValue }
                ],
                AnimationSeed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))
            });
            CreateLogEvent(
                "DICE_ROLL",
                $"{first.DisplayName} würfelt {firstValue}, {second.DisplayName} würfelt {secondValue}.");

            if (firstValue != secondValue)
            {
                _turnOrderWinnerId = firstValue > secondValue ? first.PlayerId : second.PlayerId;
                break;
            }

            CreateLogEvent("DICE_TIE", "Gleichstand – es wird nochmal gewürfelt.");

            if (_turnOrderRounds.Count >= MaxTurnOrderRounds)
            {
                _turnOrderWinnerId = first.PlayerId;
                break;
            }
        }

        CreateChoicePrompt(
            _turnOrderWinnerId,
            TurnOrderDecisionKind,
            "Du hast gewonnen! Willst du als Erster oder Zweiter spielen?",
            [GoFirstOption, GoSecondOption]);
    }

    private void HandleTurnOrderDecision(string option, PlayerState player, PlayerState opponent)
    {
        if (_phase != MatchPhase.TurnOrder || player.PlayerId != _turnOrderWinnerId)
        {
            throw new InvalidOperationException("Only the winner of the dice roll can decide.");
        }

        var firstPlayer = option == GoFirstOption ? player : opponent;
        _turnOrderDecision = option;
        _firstPlayerId = firstPlayer.PlayerId;
        _activePlayerId = firstPlayer.PlayerId;

        CreateLogEvent(
            "FIRST_PLAYER",
            option == GoFirstOption
                ? $"{player.DisplayName} entscheidet sich, als Erster zu spielen."
                : $"{player.DisplayName} lässt {opponent.DisplayName} beginnen.");

        // Opening hands and mulligan as before - only moved behind the turn order decision.
        foreach (var entry in _players)
        {
            entry.DrawCards(OpeningHandSize);
        }

        _phase = MatchPhase.Mulligan;
        CreateLogEvent("DRAW_OPENING_HAND", "Both players drew their opening hand.");

        foreach (var entry in _players)
        {
            CreateChoicePrompt(
                entry.PlayerId,
                MulliganDecisionKind,
                "Do you want to keep your opening hand?",
                ["KEEP", "MULLIGAN"]);
        }
    }

    private void ResetTurnOrder()
    {
        _turnOrderRounds.Clear();
        _turnOrderWinnerId = string.Empty;
        _turnOrderDecision = null;
        _firstPlayerId = null;
    }

    private TurnOrderRollDto? CreateTurnOrderSnapshot()
    {
        if (_turnOrderRounds.Count == 0)
        {
            return null;
        }

        return new TurnOrderRollDto
        {
            Rounds = _turnOrderRounds.ToArray(),
            WinnerPlayerId = _turnOrderWinnerId,
            Decision = _turnOrderDecision,
            FirstPlayerId = _firstPlayerId
        };
    }

    #endregion

    #region CHOICE HANDLING

    private void HandleMulliganDecision(string action, PlayerState player)
    {
        switch (action)
        {
            case "KEEP":
                CreateLogEvent("MULLIGAN_KEEP", $"{player.DisplayName} kept their opening hand.");
                break;
            case "MULLIGAN":
                ResolveMulligan(player);
                CreateLogEvent("MULLIGAN_TAKEN", $"{player.DisplayName} took a mulligan.");
                break;
            default:
                throw new InvalidOperationException("Unsupported option for MULLIGAN_DECISION.");
        }

        _mulliganPlayerDecisions.Add(player.PlayerId);

        if (_mulliganPlayerDecisions.Count < _players.Count)
        {
            return;
        }

        CreateLogEvent("MULLIGAN_RESOLVED", "Both players finalized mulligan.");

        foreach (var entry in _players)
        {
            PlaceLifeCards(entry);
        }

        if (CheckDeckOut())
        {
            return;
        }

        BeginTurn(GetPlayerSlot(_activePlayerId));
    }

    private void HandleMainAction(
        string action,
        PlayerState player,
        PlayerState opponent,
        ChoiceSubmissionDto submission,
        ChoicePromptDto prompt)
    {
        switch (action)
        {
            case "PLAY_CARD":
            {
                var handCard = player.FindHandCard(submission.SelectedCardInstanceId);
                if (handCard is not null && handCard.IsEvent
                    && !handCard.Text.Contains("[Main]", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"{handCard.Name} has no [Main] effect and cannot be played in the main phase.");
                }

                var playedCard = _playCardAction.Execute(
                    player,
                    _phase,
                    _activePlayerId,
                    submission.SelectedCardInstanceId,
                    submission.TargetCardInstanceId,
                    _turnNumber);

                _openChoicePrompts.Remove(prompt.ChoiceId);
                playedCard.OwnerId = player.PlayerId;
                CreateLogEvent(
                    "PLAY_CARD",
                    $"{player.DisplayName} played {playedCard.Name} for {playedCard.Cost} DON!!.");

                var timing = playedCard.IsEvent ? EffectTiming.EventMain : EffectTiming.OnPlay;
                QueueAbilities(playedCard, player, timing, alreadyChosen: playedCard.IsEvent);
                RunEffects(() => CreateMainActionPrompt(player));
                break;
            }

            case "ACTIVATE":
            {
                if (_phase != MatchPhase.Main || player.PlayerId != _activePlayerId)
                {
                    throw new InvalidOperationException("Abilities can only be activated in your main phase.");
                }

                var card = player.FieldCards().FirstOrDefault(entry => entry.InstanceId == submission.SelectedCardInstanceId)
                    ?? throw new InvalidOperationException("Choose your Leader, Character or Stage with an [Activate: Main] ability.");

                var ability = card.Script.WithTiming(EffectTiming.ActivateMain)
                    .FirstOrDefault(entry => EffectEngine.CanActivate(entry, card, player.PlayerId, _context))
                    ?? throw new InvalidOperationException($"{card.Name} has no ability that can be activated right now.");

                _openChoicePrompts.Remove(prompt.ChoiceId);
                CreateLogEvent("ACTIVATE", $"{player.DisplayName} activates {card.Name}.");
                _effects.Enqueue(ability, card, player.PlayerId, alreadyChosen: true);
                RunEffects(() => CreateMainActionPrompt(player));
                break;
            }

            case "ATTACH_DON":
            {
                var target = _attachDonAction.Execute(
                    player,
                    _phase,
                    _activePlayerId,
                    submission.SelectedCardInstanceId ?? submission.TargetCardInstanceId);

                _openChoicePrompts.Remove(prompt.ChoiceId);
                CreateLogEvent(
                    "ATTACH_DON",
                    $"{player.DisplayName} gave 1 DON!! to {target.Name} ({target.GetPower(true)} power).");
                CreateMainActionPrompt(player);
                break;
            }

            case "ATTACK":
            {
                var battle = _attackAction.Execute(
                    player,
                    opponent,
                    _phase,
                    _activePlayerId,
                    submission.SelectedCardInstanceId,
                    submission.TargetCardInstanceId,
                    _turnNumber);

                _openChoicePrompts.Remove(prompt.ChoiceId);
                _battle = battle;
                CreateLogEvent(
                    "ATTACK",
                    $"{player.DisplayName}'s {battle.Attacker.Name} ({battle.Attacker.GetPower(true)}) attacks " +
                    $"{opponent.DisplayName}'s {battle.Target.Name} ({battle.Target.GetPower(false)}).");

                // [When Attacking] of the attacker first, then [On Your Opponent's Attack] of the defender.
                QueueAbilities(battle.Attacker, player, EffectTiming.WhenAttacking);
                foreach (var card in opponent.FieldCards().ToList())
                {
                    QueueAbilities(card, opponent, EffectTiming.OnOpponentsAttack);
                }

                RunEffects(ContinueBattle);
                break;
            }

            case "END_TURN":
            {
                _endTurnAction.Execute(player, _phase, _activePlayerId);
                _openChoicePrompts.Remove(prompt.ChoiceId);

                EnterEndPhase(player);

                foreach (var card in player.FieldCards().ToList())
                {
                    QueueAbilities(card, player, EffectTiming.EndOfYourTurn);
                }

                foreach (var card in opponent.FieldCards().ToList())
                {
                    QueueAbilities(card, opponent, EffectTiming.EndOfOpponentsTurn);
                }

                RunEffects(() => FinishTurn(player, opponent));
                break;
            }

            default:
                throw new InvalidOperationException("Unsupported option for MAIN_ACTION.");
        }
    }

    private void HandleBlockStep(
        string action,
        PlayerState defender,
        ChoiceSubmissionDto submission,
        ChoicePromptDto prompt)
    {
        var battle = _battle ?? throw new InvalidOperationException("There is no battle running.");

        switch (action)
        {
            case "BLOCK":
                var blocker = _blockAction.Execute(battle, defender, submission.SelectedCardInstanceId);
                CreateLogEvent("BLOCK", $"{defender.DisplayName} blocks with {blocker.Name}.");
                QueueAbilities(blocker, defender, EffectTiming.OnBlock);
                break;
            case "NO_BLOCK":
                CreateLogEvent("NO_BLOCK", $"{defender.DisplayName} does not block.");
                break;
            default:
                throw new InvalidOperationException("Unsupported option for BLOCK_STEP.");
        }

        _openChoicePrompts.Remove(prompt.ChoiceId);
        battle.Step = BattleStep.Counter;
        RunEffects(ContinueBattle);
    }

    private void HandleCounterStep(
        string action,
        PlayerState defender,
        ChoiceSubmissionDto submission,
        ChoicePromptDto prompt)
    {
        var battle = _battle ?? throw new InvalidOperationException("There is no battle running.");

        switch (action)
        {
            case "COUNTER":
                var handCard = defender.FindHandCard(submission.SelectedCardInstanceId);
                if (handCard is not null && IsCounterEvent(handCard))
                {
                    // Event with [Counter]: pay its cost, trash it and resolve the effect.
                    if (handCard.Cost > defender.ActiveDon)
                    {
                        throw new InvalidOperationException(
                            $"Not enough DON!!: {handCard.Name} costs {handCard.Cost}, you have {defender.ActiveDon} active DON!!.");
                    }

                    defender.ActiveDon -= handCard.Cost;
                    defender.RestedDon += handCard.Cost;
                    defender.Hand.Remove(handCard);
                    defender.TrashCards.Add(handCard);
                    _openChoicePrompts.Remove(prompt.ChoiceId);
                    CreateLogEvent("COUNTER", $"{defender.DisplayName} used the Event {handCard.Name} ({handCard.Cost} DON!!).");
                    QueueAbilities(handCard, defender, EffectTiming.EventCounter, alreadyChosen: true);
                    RunEffects(ContinueBattle);
                    break;
                }

                var counterCard = _counterAction.Execute(battle, defender, submission.SelectedCardInstanceId);
                CreateLogEvent(
                    "COUNTER",
                    $"{defender.DisplayName} used {counterCard.Name} as counter (+{counterCard.Counter}).");
                _openChoicePrompts.Remove(prompt.ChoiceId);
                ContinueBattle();
                break;
            case "PASS":
                _openChoicePrompts.Remove(prompt.ChoiceId);
                ResolveDamageStep(battle);
                break;
            default:
                throw new InvalidOperationException("Unsupported option for COUNTER_STEP.");
        }
    }

    private void CreateChoicePrompt(
        string playerId,
        string kind,
        string title,
        IReadOnlyList<string> options,
        IReadOnlyList<CardInstanceDto>? cards = null,
        string sourceName = "",
        string effectText = "")
    {
        var prompt = new ChoicePromptDto
        {
            ChoiceId = Guid.NewGuid().ToString("N"),
            PlayerId = playerId,
            Kind = kind,
            Title = title,
            Options = options.ToArray(),
            Cards = cards?.ToArray() ?? [],
            SourceName = sourceName,
            EffectText = effectText
        };

        _openChoicePrompts[prompt.ChoiceId] = prompt;
        _pendingChoicePrompts.Add(prompt);
    }

    private void CreateMainActionPrompt(PlayerState player)
    {
        if (_phase == MatchPhase.GameOver)
        {
            return;
        }

        var options = new List<string>();

        if (player.Hand.Any(card => card.Cost <= player.ActiveDon && CanPlayInMainPhase(card)))
        {
            options.Add("PLAY_CARD");
        }

        if (player.ActiveDon > 0)
        {
            options.Add("ATTACH_DON");
        }

        if (GetAttackers(player).Any())
        {
            options.Add("ATTACK");
        }

        if (player.FieldCards().Any(card => CanActivateAny(card, player)))
        {
            options.Add("ACTIVATE");
        }

        options.Add("END_TURN");

        CreateChoicePrompt(
            player.PlayerId,
            MainActionKind,
            "Choose your main phase action.",
            options);
    }

    private IEnumerable<CardInstance> GetAttackers(PlayerState player)
    {
        var fieldCards = player.Leader is null
            ? player.Characters
            : player.Characters.Prepend(player.Leader);

        return fieldCards.Where(card => AttackAction.CanAttack(card, player, _turnNumber));
    }

    #endregion

    #region BATTLE

    // Moves the battle to the next step that needs a decision from the defender.
    private void ContinueBattle()
    {
        var battle = _battle;
        if (battle is null || _phase == MatchPhase.GameOver)
        {
            return;
        }

        var attacker = GetPlayerSlot(battle.AttackerPlayerId);
        var defender = GetPlayerSlot(battle.DefenderPlayerId);

        // An effect (e.g. [When Attacking] K.O.) removed the attacker or the target: the battle ends.
        if (!attacker.FieldCards().Contains(battle.Attacker) || !defender.FieldCards().Contains(battle.Target))
        {
            CreateLogEvent("BATTLE_END", "The attacker or the target left the field. The battle ends.");
            EndBattle(attacker);
            return;
        }

        if (battle.Step == BattleStep.Block)
        {
            var blockersAllowed = !battle.Attacker.HasFlag(EffectFlags.CannotBeBlocked)
                && !attacker.TurnFlags.Contains(EffectFlags.CannotBeBlocked);

            if (blockersAllowed && BlockAction.HasPossibleBlocker(defender, battle))
            {
                CreateChoicePrompt(
                    defender.PlayerId,
                    BlockStepKind,
                    $"{battle.Attacker.Name} attacks {battle.Target.Name}. Block with a [Blocker]?",
                    ["BLOCK", "NO_BLOCK"]);
                return;
            }

            battle.Step = BattleStep.Counter;
        }

        if (CounterAction.HasCounterCard(defender) || defender.Hand.Any(card => IsCounterEvent(card) && card.Cost <= defender.ActiveDon))
        {
            var attackerPower = battle.Attacker.GetPower(isOwnersTurn: true);
            var targetPower = battle.Target.GetPower(isOwnersTurn: false) + battle.CounterBonus;
            CreateChoicePrompt(
                defender.PlayerId,
                CounterStepKind,
                $"{battle.Attacker.Name} ({attackerPower}) vs {battle.Target.Name} ({targetPower}). Use a counter?",
                ["COUNTER", "PASS"]);
            return;
        }

        ResolveDamageStep(battle);
    }

    // Rule 7-1-4 Damage Step: compare power, then K.O. the Character or deal damage to the Leader.
    private void ResolveDamageStep(BattleState battle)
    {
        var attacker = GetPlayerSlot(battle.AttackerPlayerId);
        var defender = GetPlayerSlot(battle.DefenderPlayerId);
        var attackerPower = battle.Attacker.GetPower(isOwnersTurn: true);
        var targetPower = battle.Target.GetPower(isOwnersTurn: false) + battle.CounterBonus;
        var powerText = $"{attackerPower} vs {targetPower}";

        if (attackerPower < targetPower)
        {
            CreateLogEvent("ATTACK_FAILED", $"{battle.Attacker.Name} lost the battle ({powerText}).");
            EndBattle(attacker);
            return;
        }

        if (!battle.Target.IsLeader)
        {
            if (_effects.KnockOut(battle.Target, _context, byEffect: false))
            {
                CreateLogEvent("KO", $"{defender.DisplayName}'s {battle.Target.Name} was K.O.'d ({powerText}).");
            }
            else
            {
                CreateLogEvent("KO_PREVENTED", $"{battle.Target.Name} cannot be K.O.'d in battle ({powerText}).");
            }

            EndBattle(attacker);
            return;
        }

        // Rule 7-1-4-1-1-1: 0 Life when damage is dealt -> the defender loses.
        if (defender.LifeCards.Count == 0)
        {
            CreateLogEvent("LEADER_DAMAGE", $"{defender.DisplayName}'s Leader took damage with 0 Life ({powerText}).");
            EndGame(attacker, $"{attacker.DisplayName} wins!", MatchEndReasons.Life);
            return;
        }

        CreateLogEvent("LEADER_HIT", $"{battle.Attacker.Name} hits {defender.DisplayName}'s Leader ({powerText}).");
        _damageLeft = battle.Attacker.HasDoubleAttack ? 2 : 1;
        DealNextDamage();
    }

    // Rule 7-1-4-1-1-2/3: one Life card per damage. [Banish] trashes it, a [Trigger] can be activated.
    private void DealNextDamage()
    {
        var battle = _battle;
        if (battle is null || _phase == MatchPhase.GameOver)
        {
            return;
        }

        var attacker = GetPlayerSlot(battle.AttackerPlayerId);
        var defender = GetPlayerSlot(battle.DefenderPlayerId);

        if (_damageLeft <= 0 || defender.LifeCards.Count == 0)
        {
            _damageLeft = 0;
            EndBattle(attacker);
            return;
        }

        _damageLeft -= 1;
        var lifeCard = defender.LifeCards[^1];
        defender.LifeCards.RemoveAt(defender.LifeCards.Count - 1);

        if (battle.Attacker.HasBanish)
        {
            defender.TrashCards.Add(lifeCard);
            CreateLogEvent("LIFE_LOST", $"{defender.DisplayName} lost 1 Life ([Banish]: {lifeCard.Name} was trashed). {defender.LifeCount} Life left.");
            DealNextDamage();
            return;
        }

        var trigger = lifeCard.Script.WithTiming(EffectTiming.Trigger).FirstOrDefault();
        // A [Trigger] the engine cannot automate can still be activated - it is then resolved by hand.
        var triggerText = trigger?.Text
            ?? (lifeCard.HasTrigger ? ManualTexts(lifeCard, EffectTiming.Trigger).FirstOrDefault() ?? lifeCard.Text : null);
        if (triggerText is not null)
        {
            _pendingTriggerCard = lifeCard;
            lifeCard.OwnerId = defender.PlayerId;
            CreateChoicePrompt(
                defender.PlayerId,
                TriggerDecisionKind,
                $"{lifeCard.Name} has a [Trigger]. Activate it instead of adding the card to your hand?",
                [ActivateTriggerOption, DeclineTriggerOption],
                [ToCardInstanceDto(lifeCard, defender)],
                lifeCard.Name,
                triggerText);
            return;
        }

        defender.Hand.Add(lifeCard);
        CreateLogEvent("LIFE_LOST", $"{defender.DisplayName} lost 1 Life. {defender.LifeCount} Life left.");
        DealNextDamage();
    }

    private void HandleTriggerDecision(string option, PlayerState defender)
    {
        var lifeCard = _pendingTriggerCard ?? throw new InvalidOperationException("There is no [Trigger] to decide.");
        _pendingTriggerCard = null;

        if (option != ActivateTriggerOption)
        {
            defender.Hand.Add(lifeCard);
            CreateLogEvent("LIFE_LOST", $"{defender.DisplayName} lost 1 Life and added {lifeCard.Name} to their hand. {defender.LifeCount} Life left.");
            DealNextDamage();
            return;
        }

        CreateLogEvent("TRIGGER", $"{defender.DisplayName} reveals {lifeCard.Name} and activates its [Trigger].");
        QueueAbilities(lifeCard, defender, EffectTiming.Trigger, alreadyChosen: true);
        RunEffects(() =>
        {
            // The card is trashed after its [Trigger], unless the effect played it or moved it.
            if (!IsInAnyZone(lifeCard))
            {
                defender.TrashCards.Add(lifeCard);
            }

            DealNextDamage();
        });
    }

    // Removes "during this battle" effects and goes back to the attacker's main phase.
    private void EndBattle(PlayerState attacker)
    {
        foreach (var card in _players.SelectMany(player => player.FieldCards()))
        {
            card.Modifiers.RemoveAll(modifier => modifier.Duration == Duration.ThisBattle);
        }

        _battle = null;
        _damageLeft = 0;
        RunEffects(() => CreateMainActionPrompt(attacker));
    }

    #endregion

    #region EFFECTS

    // Queues all abilities of one card for the given timing.
    private void QueueAbilities(CardInstance card, PlayerState owner, EffectTiming timing, bool alreadyChosen = false)
    {
        var isEvent = timing is EffectTiming.EventMain or EffectTiming.EventCounter or EffectTiming.Trigger;

        foreach (var ability in card.Script.WithTiming(timing))
        {
            if (!isEvent && (!EffectQueries.AbilityIsActive(ability, card, owner.PlayerId, _context)
                || EffectEngine.IsUsedThisTurn(ability, card, _context)))
            {
                continue;
            }

            _effects.Enqueue(ability, card, owner.PlayerId, alreadyChosen);
        }

        QueueManualEffects(card, owner.PlayerId, timing);
    }

    // Effect texts of this timing the engine cannot automate. Each one becomes a
    // "resolve manually" prompt after the automated effects are finished.
    private void QueueManualEffects(CardInstance card, string controllerId, EffectTiming timing)
    {
        foreach (var manualText in ManualTexts(card, timing))
        {
            CreateLogEvent("EFFECT_MANUAL", $"{card.Name}: \"{manualText}\" is not automated - please resolve it manually.");
            _manualEffects.Add(new ManualEffectRequest(controllerId, card, manualText));
        }
    }

    // Unsupported texts of a card that belong to the timing (e.g. all "[On Play] ..." parts).
    private static IEnumerable<string> ManualTexts(CardInstance card, EffectTiming timing)
    {
        var tag = timing switch
        {
            EffectTiming.OnPlay => "[On Play]",
            EffectTiming.WhenAttacking => "[When Attacking]",
            EffectTiming.OnBlock => "[On Block]",
            EffectTiming.OnKO => "[On K.O.]",
            EffectTiming.EventMain => "[Main]",
            EffectTiming.EventCounter => "[Counter]",
            EffectTiming.Trigger => "[Trigger]",
            EffectTiming.EndOfYourTurn => "[End of Your Turn]",
            EffectTiming.OnOpponentsAttack => "[On Your Opponent's Attack]",
            _ => null
        };

        return tag is null
            ? []
            : card.Script.UnsupportedTexts.Where(text => text.Contains(tag, StringComparison.OrdinalIgnoreCase));
    }

    // Runs all queued effects. "continuation" runs when everything is resolved.
    private void RunEffects(Action continuation)
    {
        _afterEffects = continuation;
        ContinueEffects(_effects.Run(_context));
    }

    private void ContinueEffects(EffectChoiceRequest? request)
    {
        StaticEffects.Recalculate(_context);

        if (_phase == MatchPhase.GameOver)
        {
            return;
        }

        if (request is not null)
        {
            var player = GetPlayerSlot(request.PlayerId);
            CreateChoicePrompt(
                request.PlayerId,
                EffectChoiceKind,
                request.Title,
                request.Options,
                request.Cards.Select(card => ToCardInstanceDto(card, _context.FindOwner(card) ?? player)).ToArray(),
                request.SourceName,
                request.AbilityText);
            return;
        }

        // Rule 1-2-1-1-2: a player whose deck became empty by an effect loses.
        if (_phase == MatchPhase.Main && CheckDeckOut())
        {
            return;
        }

        // Effects the engine cannot automate: the player resolves them by hand, then the game goes on.
        if (_manualEffects.Count > 0)
        {
            var manual = _manualEffects[0];
            _manualEffects.RemoveAt(0);
            var owner = GetPlayerSlot(manual.PlayerId);
            CreateChoicePrompt(
                manual.PlayerId,
                ManualEffectKind,
                $"Effekt bitte manuell ausführen: {manual.Text}",
                [ManualDoneOption],
                [ToCardInstanceDto(manual.Card, _context.FindOwner(manual.Card) ?? owner)],
                manual.Card.Name,
                manual.Text);
            return;
        }

        var next = _afterEffects;
        _afterEffects = null;
        next?.Invoke();
    }

    private bool IsInAnyZone(CardInstance card)
    {
        return _players.Any(player =>
            player.Hand.Contains(card)
            || player.TrashCards.Contains(card)
            || player.Characters.Contains(card)
            || player.Stage == card
            || player.LifeCards.Contains(card)
            || player.DrawDeck.Contains(card));
    }

    private bool CanActivateAny(CardInstance card, PlayerState owner)
    {
        return _phase == MatchPhase.Main
            && _battle is null
            && owner.PlayerId == _activePlayerId
            && !_effects.IsBusy
            && card.Script.WithTiming(EffectTiming.ActivateMain).Any(ability => EffectEngine.CanActivate(ability, card, owner.PlayerId, _context));
    }

    private static bool IsCounterEvent(CardInstance card)
    {
        return card.IsEvent && card.Text.Contains("[Counter]", StringComparison.OrdinalIgnoreCase);
    }

    // Events without a [Main] effect (e.g. only [Counter]) cannot be played in the main phase.
    private static bool CanPlayInMainPhase(CardInstance card)
    {
        return !card.IsEvent || card.Text.Contains("[Main]", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetEffectState()
    {
        _effects.Clear();
        _manualEffects.Clear();
        _afterEffects = null;
        _damageLeft = 0;
        _pendingTriggerCard = null;
    }

    // Removes effects that end with this turn. "Until the end of your opponent's next turn"
    // effects end at the end of the turn after the one in which they were created.
    private void ExpireModifiers(int endingTurn)
    {
        foreach (var card in _players.SelectMany(player => player.FieldCards()))
        {
            card.Modifiers.RemoveAll(modifier =>
                modifier.Duration is Duration.ThisTurn or Duration.ThisBattle
                || (modifier.Duration == Duration.UntilEndOfOpponentsNextTurn && modifier.CreatedOnTurn < endingTurn));
        }
    }

    #endregion

    #region MANUAL MODE

    // Only the turn player, or a player who has to decide something right now (effect, counter,
    // [Trigger], manual effect), may use manual actions. Closing an own deck view is always allowed.
    private void EnsureCanUseManualActions(PlayerState player, ManualActionDto action)
    {
        var isRunning = _phase is MatchPhase.Refresh or MatchPhase.Draw or MatchPhase.Don or MatchPhase.Main or MatchPhase.End;
        if (!isRunning)
        {
            throw new InvalidOperationException("Manual actions are only possible during a running match.");
        }

        if (string.Equals(action.Kind?.Trim(), ManualActionKinds.CloseView, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hasDecision = _openChoicePrompts.Values.Any(prompt =>
            prompt.PlayerId == player.PlayerId && ManualDecisionKinds.Contains(prompt.Kind));
        if (player.PlayerId != _activePlayerId && !hasDecision)
        {
            throw new InvalidOperationException("Manual actions are only possible on your turn or while you have to decide something.");
        }
    }

    // Same follow-up as after an automated effect: static effects, deck-out, triggered abilities.
    private void AfterManualAction()
    {
        StaticEffects.Recalculate(_context);

        if (CheckDeckOut())
        {
            return;
        }

        // A running effect or an open "resolve manually" prompt continues later on its own.
        if (_effects.IsBusy || _afterEffects is not null)
        {
            return;
        }

        RunEffects(ResendOpenPrompts);
    }

    // Prompts can be outdated after a manual action (e.g. a new card can be played now).
    // The main action prompt is created again, all other open prompts are sent again unchanged.
    private void ResendOpenPrompts()
    {
        foreach (var prompt in _openChoicePrompts.Values.ToList())
        {
            if (prompt.Kind == MainActionKind)
            {
                _openChoicePrompts.Remove(prompt.ChoiceId);
                CreateMainActionPrompt(GetPlayerSlot(prompt.PlayerId));
            }
            else if (!_pendingChoicePrompts.Contains(prompt))
            {
                _pendingChoicePrompts.Add(prompt);
            }
        }
    }

    #endregion

    #region MATCH FLOW

    private void BeginTurn(PlayerState player)
    {
        _activePlayerId = player.PlayerId;
        player.TurnsStarted += 1;
        EnterRefreshPhase(player);
        EnterDrawPhase(player);

        if (_phase == MatchPhase.GameOver)
        {
            return;
        }

        EnterDonPhase(player);
        EnterMainPhase(player);
    }

    // Rule 6-2: given DON!! go back to the cost area, then all rested cards become active.
    private void EnterRefreshPhase(PlayerState player)
    {
        _phase = MatchPhase.Refresh;

        var fieldCards = player.Characters.ToList();
        if (player.Leader is not null)
        {
            fieldCards.Add(player.Leader);
        }

        foreach (var card in fieldCards)
        {
            player.RestedDon += card.AttachedDon;
            card.AttachedDon = 0;
            RefreshCard(card);
        }

        if (player.Stage is not null)
        {
            RefreshCard(player.Stage);
        }

        player.ActiveDon += player.RestedDon;
        player.RestedDon = 0;

        StaticEffects.Recalculate(_context);
        CreateLogEvent("REFRESH_PHASE", $"Refresh phase for {player.DisplayName}.");
    }

    // A card with "will not become active in the next Refresh Phase" stays rested once.
    private void RefreshCard(CardInstance card)
    {
        if (card.Modifiers.RemoveAll(modifier => modifier.Flag == EffectFlags.SkipNextRefresh) > 0)
        {
            CreateLogEvent("REFRESH_SKIPPED", $"{card.Name} does not become active this Refresh Phase.");
            return;
        }

        card.IsRested = false;
    }

    // Rule 6-6: end of the turn. Effects that last "during this turn" end, then the next turn starts.
    private void FinishTurn(PlayerState player, PlayerState opponent)
    {
        if (player.DonToActivateAtEndOfTurn > 0)
        {
            var amount = Math.Min(player.DonToActivateAtEndOfTurn, player.RestedDon);
            player.RestedDon -= amount;
            player.ActiveDon += amount;
            player.DonToActivateAtEndOfTurn = 0;
            CreateLogEvent("EFFECT", $"{player.DisplayName} sets {amount} DON!! as active.");
        }

        ExpireModifiers(_turnNumber);
        foreach (var entry in _players)
        {
            entry.TurnFlags.Clear();
        }

        StaticEffects.Recalculate(_context);
        CreateLogEvent("TURN_END", $"{player.DisplayName} ended turn {_turnNumber}.");

        _turnNumber += 1;
        BeginTurn(opponent);
    }

    // Rule 6-3: draw 1 card. The player going first does not draw on their first turn.
    private void EnterDrawPhase(PlayerState player)
    {
        _phase = MatchPhase.Draw;

        if (_turnNumber == 1)
        {
            CreateLogEvent("SKIP_DRAW", $"{player.DisplayName} goes first and does not draw.");
            return;
        }

        if (player.DrawDeck.Count > 0)
        {
            player.DrawCards(1);
            CreateLogEvent("DRAW_CARD", $"{player.DisplayName} drew 1 card.");
        }
        else
        {
            CreateLogEvent("DECK_EMPTY", $"{player.DisplayName} cannot draw because their deck is empty.");
        }

        CheckDeckOut();
    }

    // Rule 6-4: place 2 DON!! (only 1 for the first player's first turn).
    private void EnterDonPhase(PlayerState player)
    {
        _phase = MatchPhase.Don;

        var amount = Math.Min(_turnNumber == 1 ? 1 : 2, player.DonDeckCount);
        player.DonDeckCount -= amount;
        player.ActiveDon += amount;

        CreateLogEvent("DON_PHASE", $"{player.DisplayName} placed {amount} DON!! ({player.ActiveDon} active).");
    }

    private void EnterMainPhase(PlayerState player)
    {
        _phase = MatchPhase.Main;
        CreateLogEvent("MAIN_PHASE", $"Main phase for {player.DisplayName}.");
        CreateMainActionPrompt(player);
    }

    private void EnterEndPhase(PlayerState player)
    {
        _phase = MatchPhase.End;
        CreateLogEvent("END_PHASE", $"End phase for {player.DisplayName}.");
    }

    // Rule 1-2-1-1-2: a player with 0 cards in their deck loses.
    private bool CheckDeckOut()
    {
        var emptyDeckPlayer = _players.FirstOrDefault(player => player.DrawDeck.Count == 0);
        if (emptyDeckPlayer is null)
        {
            return false;
        }

        var winner = GetPlayerSlot(GetOtherPlayerId(emptyDeckPlayer.PlayerId));
        CreateLogEvent("DECK_OUT", $"{emptyDeckPlayer.DisplayName} has no cards left in their deck.");
        EndGame(winner, $"{winner.DisplayName} wins!", MatchEndReasons.DeckOut);
        return true;
    }

    private void EndGame(PlayerState winner, string message, string endReason)
    {
        _phase = MatchPhase.GameOver;
        _winnerPlayerId = winner.PlayerId;
        _battle = null;
        _openChoicePrompts.Clear();
        ResetEffectState();
        CreateLogEvent("GAME_OVER", message);
        RecordResult(winner, GetPlayerSlot(GetOtherPlayerId(winner.PlayerId)), endReason);
    }

    // Prepares the result for saving. The flag makes sure a match is only reported once.
    private void RecordResult(PlayerState winner, PlayerState loser, string endReason)
    {
        if (_resultReported)
        {
            return;
        }

        _resultReported = true;
        _pendingResult = new MatchResultData(
            _roomCode,
            _matchStartedAtUtc,
            DateTimeOffset.UtcNow,
            _turnNumber,
            endReason,
            new MatchPlayerResult(winner.GuestId, winner.DisplayName, winner.LeaderCardId),
            new MatchPlayerResult(loser.GuestId, loser.DisplayName, loser.LeaderCardId),
            IsRanked: IsQuickMatch,
            FirstPlayerGuestId: _firstPlayerId is null
                ? null
                : _players.FirstOrDefault(player => player.PlayerId == _firstPlayerId)?.GuestId,
            WinnerWentFirst: _firstPlayerId is null ? null : winner.PlayerId == _firstPlayerId);
    }

    #endregion

    #region SNAPSHOTS
    private MatchUpdate CreateUpdate()
    {
        _lastActivityUtc = DateTimeOffset.UtcNow;

        var update = new MatchUpdate
        {
            StateSnapshot = CreateStateSnapshot(),
            StateSnapshots = _players
                .ToDictionary(player => player.PlayerId, player => CreateStateSnapshot(player.PlayerId), StringComparer.Ordinal),
            LogEvents = _pendingLogEvents.ToArray(),
            ChoicePrompts = _pendingChoicePrompts.ToArray(),
            MatchResult = _pendingResult
        };

        _pendingLogEvents.Clear();
        _pendingChoicePrompts.Clear();
        _pendingResult = null;

        return update;
    }

    public GameStateDto CreateStateSnapshot(string? viewerPlayerId = null)
    {
        lock (_sync)
        {
            return new GameStateDto
            {
                RoomCode = _roomCode,
                TurnNumber = _turnNumber,
                ActivePlayerId = _activePlayerId,
                ViewerPlayerId = viewerPlayerId ?? string.Empty,
                WinnerPlayerId = _winnerPlayerId,
                Phase = _phase,
                Battle = CreateBattleSnapshot(),
                Players = _players
                    .Select(player => CreatePlayerStateSnapshot(player, viewerPlayerId))
                    .ToArray(),
                TurnOrderRoll = CreateTurnOrderSnapshot(),
                IsRanked = IsQuickMatch
            };
        }
    }

    private BattleDto? CreateBattleSnapshot()
    {
        if (_battle is null)
        {
            return null;
        }

        return new BattleDto
        {
            AttackerPlayerId = _battle.AttackerPlayerId,
            AttackerInstanceId = _battle.Attacker.InstanceId,
            TargetInstanceId = _battle.Target.InstanceId,
            AttackerPower = _battle.Attacker.GetPower(isOwnersTurn: true),
            TargetPower = _battle.Target.GetPower(isOwnersTurn: false) + _battle.CounterBonus,
            CounterBonus = _battle.CounterBonus,
            Step = _battle.Step.ToString()
        };
    }

    private PlayerStateDto CreatePlayerStateSnapshot(PlayerState player, string? viewerPlayerId)
    {
        var isViewer = string.Equals(player.PlayerId, viewerPlayerId, StringComparison.Ordinal);

        return new PlayerStateDto
        {
            PlayerId = player.PlayerId,
            DisplayName = player.DisplayName,
            Connected = player.Connected,
            DeckCount = player.DeckCount,
            HandCount = player.HandCount,
            LifeCount = player.LifeCount,
            BoardCount = player.Characters.Count,
            DeckName = player.DeckName,
            LeaderCardId = player.LeaderCardId,
            MainDeckCount = player.MainDeckCount,
            HasDeck = player.HasDeck,
            TrashCount = player.TrashCards.Count,
            DonDeckCount = player.DonDeckCount,
            ActiveDonCount = player.ActiveDon,
            RestedDonCount = player.RestedDon,
            Leader = player.Leader is null ? null : ToCardInstanceDto(player.Leader, player),
            Stage = player.Stage is null ? null : ToCardInstanceDto(player.Stage, player),
            HandCards = isViewer ? player.Hand.Select(card => ToCardInstanceDto(card, player)).ToArray() : [],
            BoardCards = player.Characters.Select(card => ToCardInstanceDto(card, player)).ToArray(),
            TrashCards = player.TrashCards.Select(card => ToCardInstanceDto(card, player)).ToArray(),
            ManualView = isViewer && player.ManualViewKind.Length > 0
                ? new ManualViewDto
                {
                    Kind = player.ManualViewKind,
                    Cards = player.ManualViewCards.Select(card => ToCardInstanceDto(card, player)).ToArray()
                }
                : null
        };
    }

    private CardInstanceDto ToCardInstanceDto(CardInstance card, PlayerState owner)
    {
        var isOwnersTurn = owner.PlayerId == _activePlayerId;
        var script = card.Script;
        var effectStatus = EffectParser.Clean(card.Text).Length == 0
            ? string.Empty
            : script.IsFullySupported ? "auto" : script.Abilities.Count > 0 ? "partial" : "manual";

        return new CardInstanceDto
        {
            InstanceId = card.InstanceId,
            CardId = card.CardId,
            Name = card.Name,
            Category = card.Category,
            Color = card.Color,
            ImageUrl = card.ImageUrl,
            Text = card.Text,
            Cost = card.CurrentCost,
            BasePower = card.BasePower,
            Power = card.GetPower(isOwnersTurn),
            Counter = card.Counter,
            IsRested = card.IsRested,
            AttachedDon = card.AttachedDon,
            CanAttack = _phase == MatchPhase.Main
                && _battle is null
                && !_effects.IsBusy
                && isOwnersTurn
                && owner.FieldCards().Contains(card)
                && AttackAction.CanAttack(card, owner, _turnNumber),
            Keywords = card.Keywords,
            CanActivate = owner.FieldCards().Contains(card) && CanActivateAny(card, owner),
            IsCounterEvent = IsCounterEvent(card),
            EffectStatus = effectStatus,
            ManualEffects = script.UnsupportedTexts.ToArray()
        };
    }
    #endregion

    #region UTILITIES

    public string ResolvePlayerId(string connectionId)
    {
        lock (_sync)
        {
            var player = _players.FirstOrDefault(entry => entry.PlayerId == connectionId);
            return player?.PlayerId ?? connectionId;
        }
    }

    private string GetOtherPlayerId(string currentPlayerId)
    {
        var otherPlayer = _players.FirstOrDefault(player => player.PlayerId != currentPlayerId);
        if (otherPlayer is null)
        {
            throw new InvalidOperationException("Unable to resolve next active player.");
        }

        return otherPlayer.PlayerId;
    }

    private PlayerState GetPlayerSlot(string playerId)
    {
        return _players.FirstOrDefault(player => player.PlayerId == playerId)
            ?? throw new InvalidOperationException("Player is not in this room.");
    }

    private static void ResolveMulligan(PlayerState player)
    {
        player.DrawDeck.AddRange(player.Hand);
        player.Hand.Clear();
        Shuffle(player.DrawDeck);
        player.DrawCards(OpeningHandSize);
    }

    // Rule 5-2-1-7: the top card of the deck ends up at the bottom of the Life area.
    private static void PlaceLifeCards(PlayerState player)
    {
        var life = player.Leader?.Life > 0 ? player.Leader.Life : DefaultLeaderLife;
        var amount = Math.Min(life, player.DrawDeck.Count);

        for (var index = 0; index < amount; index += 1)
        {
            player.LifeCards.Add(player.DrawDeck[0]);
            player.DrawDeck.RemoveAt(0);
        }
    }

    private static CardInstance BuildLeader(PlayerState player)
    {
        var deck = player.DeckSubmission;

        return new CardInstance
        {
            InstanceId = $"{player.PlayerId}:leader:{Guid.NewGuid():N}",
            CardId = player.LeaderCardId,
            Name = string.IsNullOrWhiteSpace(deck?.LeaderName) ? player.LeaderCardId : deck.LeaderName,
            Category = CardInstance.LeaderCategory,
            Color = deck?.LeaderColor ?? string.Empty,
            Text = deck?.LeaderText ?? string.Empty,
            ImageUrl = deck?.LeaderImageUrl ?? string.Empty,
            Types = deck?.LeaderTypes ?? string.Empty,
            Attribute = deck?.LeaderAttribute ?? string.Empty,
            BasePower = deck?.LeaderPower ?? DefaultLeaderPower,
            Life = deck?.LeaderLife ?? DefaultLeaderLife,
            OwnerId = player.PlayerId
        };
    }

    private static List<CardInstance> BuildMatchDeck(PlayerState player)
    {
        return player.DeckCards
            .Where(card => card.Quantity > 0 && !string.IsNullOrWhiteSpace(card.CardId))
            .SelectMany(card =>
                Enumerable.Range(0, card.Quantity).Select(index => new CardInstance
                {
                    InstanceId = $"{player.PlayerId}:{card.CardId}:{index + 1}:{Guid.NewGuid():N}",
                    CardId = card.CardId,
                    OwnerId = player.PlayerId,
                    Types = card.Types,
                    Attribute = card.Attribute,
                    Name = string.IsNullOrWhiteSpace(card.Name) ? card.CardId : card.Name,
                    Category = NormalizeCategory(card.Category),
                    Color = card.Color,
                    Text = card.Text,
                    ImageUrl = card.ImageUrl,
                    Cost = Math.Max(0, card.Cost ?? 0),
                    BasePower = Math.Max(0, card.Power ?? 0),
                    Counter = Math.Max(0, card.Counter ?? 0)
                }))
            .ToList();
    }

    private static string NormalizeCategory(string? category)
    {
        var value = (category ?? string.Empty).Trim();

        if (value.Equals(CardInstance.EventCategory, StringComparison.OrdinalIgnoreCase))
        {
            return CardInstance.EventCategory;
        }

        if (value.Equals(CardInstance.StageCategory, StringComparison.OrdinalIgnoreCase))
        {
            return CardInstance.StageCategory;
        }

        // Unknown categories are treated as Characters so older decks stay playable.
        return CardInstance.CharacterCategory;
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
}

public sealed record MatchUpdate
{
    public GameStateDto StateSnapshot { get; init; } = new();
    public IReadOnlyDictionary<string, GameStateDto> StateSnapshots { get; init; } =
        new Dictionary<string, GameStateDto>(StringComparer.Ordinal);
    public IReadOnlyList<LogEventDto> LogEvents { get; init; } = [];
    public IReadOnlyList<ChoicePromptDto> ChoicePrompts { get; init; } = [];

    // Set exactly once, in the update where the match ended.
    public MatchResultData? MatchResult { get; init; }
}
