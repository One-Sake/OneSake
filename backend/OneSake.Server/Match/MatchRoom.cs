using OneSake.Domain;
using OneSake.Game.Actions;
using OneSake.Game.State;

namespace OneSake.Server.Match;

public sealed class MatchRoom
{
    // Choice kinds sent to the clients.
    public const string MulliganDecisionKind = "MULLIGAN_DECISION";
    public const string MainActionKind = "MAIN_ACTION";
    public const string BlockStepKind = "BLOCK_STEP";
    public const string CounterStepKind = "COUNTER_STEP";

    private const int OpeningHandSize = 5;
    private const int DefaultLeaderLife = 5;
    private const int DefaultLeaderPower = 5000;

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

    public const int MaxPlayers = 2;

    private readonly DateTimeOffset _createdAtUtc;
    private DateTimeOffset _lastActivityUtc;

    public MatchRoom(string roomCode)
    {
        _roomCode = roomCode;
        _createdAtUtc = DateTimeOffset.UtcNow;
        _lastActivityUtc = _createdAtUtc;
    }

    public string RoomCode => _roomCode;

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
    public MatchUpdate? RemovePlayer(string connectionId)
    {
        lock (_sync)
        {
            var player = _players.FirstOrDefault(entry => entry.PlayerId == connectionId);
            if (player is null)
            {
                return null;
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
    public MatchUpdate JoinPlayer(string connectionId, string displayName)
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
                    Connected = true
                });
            }
            else
            {
                existingPlayer.DisplayName = normalizedDisplayName;
                existingPlayer.Connected = true;
            }

            CreateLogEvent("JOIN", $"{normalizedDisplayName} joined room {_roomCode}.");
            return CreateUpdate();
        }
    }

    // Rule 5-2: game preparation. Life cards are placed after the mulligan (5-2-1-7).
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
            _phase = MatchPhase.Mulligan;
            _activePlayerId = _players[0].PlayerId;
            _winnerPlayerId = string.Empty;
            _battle = null;
            _logSeq = 0;
            _openChoicePrompts.Clear();
            _mulliganPlayerDecisions.Clear();

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
                player.DrawCards(OpeningHandSize);
                player.Connected = true;
            }

            CreateLogEvent("START_MATCH", "Match started.");
            CreateLogEvent("SHUFFLE_DECKS", "Both players shuffled their decks.");
            CreateLogEvent("DRAW_OPENING_HAND", "Both players drew their opening hand.");
            CreateLogEvent("FIRST_PLAYER", $"{_players[0].DisplayName} goes first.");

            foreach (var player in _players)
            {
                CreateChoicePrompt(
                    player.PlayerId,
                    MulliganDecisionKind,
                    "Do you want to keep your opening hand?",
                    ["KEEP", "MULLIGAN"]);
            }

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

                default:
                    throw new InvalidOperationException($"Unsupported choice kind '{prompt.Kind}'.");
            }

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
                var playedCard = _playCardAction.Execute(
                    player,
                    _phase,
                    _activePlayerId,
                    submission.SelectedCardInstanceId,
                    submission.TargetCardInstanceId,
                    _turnNumber);

                _openChoicePrompts.Remove(prompt.ChoiceId);
                var effectNote = playedCard.IsEvent ? " (event effect must be resolved manually)" : string.Empty;
                CreateLogEvent(
                    "PLAY_CARD",
                    $"{player.DisplayName} played {playedCard.Name} for {playedCard.Cost} DON!!{effectNote}.");
                CreateMainActionPrompt(player);
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
                ContinueBattle();
                break;
            }

            case "END_TURN":
            {
                _endTurnAction.Execute(player, _phase, _activePlayerId);
                _openChoicePrompts.Remove(prompt.ChoiceId);

                EnterEndPhase(player);
                CreateLogEvent("TURN_END", $"{player.DisplayName} ended turn {_turnNumber}.");

                _turnNumber += 1;
                BeginTurn(opponent);
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
                break;
            case "NO_BLOCK":
                CreateLogEvent("NO_BLOCK", $"{defender.DisplayName} does not block.");
                break;
            default:
                throw new InvalidOperationException("Unsupported option for BLOCK_STEP.");
        }

        _openChoicePrompts.Remove(prompt.ChoiceId);
        battle.Step = BattleStep.Counter;
        ContinueBattle();
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

    private void CreateChoicePrompt(string playerId, string kind, string title, IReadOnlyList<string> options)
    {
        var prompt = new ChoicePromptDto
        {
            ChoiceId = Guid.NewGuid().ToString("N"),
            PlayerId = playerId,
            Kind = kind,
            Title = title,
            Options = options.ToArray()
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

        if (player.Hand.Any(card => card.Cost <= player.ActiveDon))
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
        if (battle is null)
        {
            return;
        }

        var defender = GetPlayerSlot(battle.DefenderPlayerId);

        if (battle.Step == BattleStep.Block)
        {
            if (BlockAction.HasPossibleBlocker(defender, battle))
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

        if (CounterAction.HasCounterCard(defender))
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

    private void ResolveDamageStep(BattleState battle)
    {
        var attacker = GetPlayerSlot(battle.AttackerPlayerId);
        var defender = GetPlayerSlot(battle.DefenderPlayerId);
        var result = _damageStepAction.Execute(battle, attacker, defender);
        _battle = null;

        var powerText = $"{result.AttackerPower} vs {result.TargetPower}";

        if (!result.AttackerWon)
        {
            CreateLogEvent("ATTACK_FAILED", $"{battle.Attacker.Name} lost the battle ({powerText}).");
        }
        else if (result.CharacterKnockedOut)
        {
            CreateLogEvent("KO", $"{defender.DisplayName}'s {battle.Target.Name} was K.O.'d ({powerText}).");
        }
        else if (result.DefenderLostGame)
        {
            CreateLogEvent("LEADER_DAMAGE", $"{defender.DisplayName}'s Leader took damage with 0 Life ({powerText}).");
            EndGame(attacker, $"{attacker.DisplayName} wins!");
            return;
        }
        else
        {
            CreateLogEvent(
                "LIFE_LOST",
                $"{defender.DisplayName} lost {result.LifeCardsLost} Life ({powerText}). {defender.LifeCount} Life left.");
        }

        CreateMainActionPrompt(attacker);
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
            card.IsRested = false;
        }

        if (player.Stage is not null)
        {
            player.Stage.IsRested = false;
        }

        player.ActiveDon += player.RestedDon;
        player.RestedDon = 0;

        CreateLogEvent("REFRESH_PHASE", $"Refresh phase for {player.DisplayName}.");
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
        EndGame(winner, $"{winner.DisplayName} wins!");
        return true;
    }

    private void EndGame(PlayerState winner, string message)
    {
        _phase = MatchPhase.GameOver;
        _winnerPlayerId = winner.PlayerId;
        _battle = null;
        _openChoicePrompts.Clear();
        CreateLogEvent("GAME_OVER", message);
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
            ChoicePrompts = _pendingChoicePrompts.ToArray()
        };

        _pendingLogEvents.Clear();
        _pendingChoicePrompts.Clear();

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
                    .ToArray()
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
            TrashCards = player.TrashCards.Select(card => ToCardInstanceDto(card, player)).ToArray()
        };
    }

    private CardInstanceDto ToCardInstanceDto(CardInstance card, PlayerState owner)
    {
        var isOwnersTurn = owner.PlayerId == _activePlayerId;

        return new CardInstanceDto
        {
            InstanceId = card.InstanceId,
            CardId = card.CardId,
            Name = card.Name,
            Category = card.Category,
            Color = card.Color,
            ImageUrl = card.ImageUrl,
            Text = card.Text,
            Cost = card.Cost,
            BasePower = card.BasePower,
            Power = card.GetPower(isOwnersTurn),
            Counter = card.Counter,
            IsRested = card.IsRested,
            AttachedDon = card.AttachedDon,
            CanAttack = _phase == MatchPhase.Main
                && _battle is null
                && isOwnersTurn
                && AttackAction.CanAttack(card, owner, _turnNumber),
            Keywords = card.Keywords
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
            BasePower = deck?.LeaderPower ?? DefaultLeaderPower,
            Life = deck?.LeaderLife ?? DefaultLeaderLife
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
}
