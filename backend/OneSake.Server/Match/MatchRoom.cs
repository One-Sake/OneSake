using OneSake.Domain;
using OneSake.Game.Actions;
using OneSake.Game.State;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OneSake.Server.Match;

public sealed class MatchRoom
{
    private readonly object _sync = new();
    private readonly string _roomCode;
    private readonly List<PlayerState> _players = [];
    private readonly Dictionary<string, ChoicePromptDto> _openChoicePrompts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mulliganPlayerDecisions = new(StringComparer.Ordinal);
    private readonly List<LogEventDto> _pendingLogEvents = [];
    private readonly List<ChoicePromptDto> _pendingChoicePrompts = [];
    private readonly AttackAction _attackAction = new();
    private readonly EndTurnAction _endTurnAction = new();

    private int _logSeq;
    private int _turnNumber;
    private string _activePlayerId = string.Empty;
    private MatchPhase _phase = MatchPhase.Lobby;
    private readonly PlayCardAction _playCardAction = new();

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

            _turnNumber = 1;
            _phase = MatchPhase.Mulligan;
            _activePlayerId = _players[0].PlayerId;
            _logSeq = 0;
            _openChoicePrompts.Clear();
            _mulliganPlayerDecisions.Clear();

            foreach (var player in _players)
            {
                player.DrawDeck = BuildMatchDeck(player);
                Shuffle(player.DrawDeck);
                player.Hand.Clear();
                player.PlayedCards.Clear();
                player.TrashCards.Clear();
                DrawCards(player, 5);
                player.LifeCount = 5;
                player.Connected = true;
            }

            CreateLogEvent("START_MATCH", "Match started.");
            CreateLogEvent("SHUFFLE_DECKS", "Both players shuffled their decks.");
            CreateLogEvent("DRAW_OPENING_HAND", "Both players drew their opening hand.");

            foreach (var player in _players)
            {
                CreateChoicePrompt(
                    player.PlayerId,
                    "MULLIGAN_DECISION",
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
                    return new PlayerDeckCardDto
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
            player.DeckCards = normalizedCards;
            player.MainDeckCount = normalizedCards.Sum(card => card.Quantity);
            player.HasDeck = !string.IsNullOrWhiteSpace(player.LeaderCardId) && player.MainDeckCount > 0;

            CreateLogEvent("DECK_SELECTED", $"{player.DisplayName} selected deck {player.DeckName}.");
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

    private void HandleMulliganDecision(string action, PlayerState player, ChoiceSubmissionDto submission)
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
            default: throw new InvalidOperationException("Unsupported option for MULLIGAN_DECISION.");
        }

        _mulliganPlayerDecisions.Add(submission.PlayerId);

        if (_mulliganPlayerDecisions.Count == _players.Count)
        {
            CreateLogEvent("MULLIGAN_RESOLVED", "Both players finalized mulligan.");
            BeginTurn(GetPlayerSlot(_activePlayerId));
        }
    }

    private void HandleMainAction(string action, PlayerState player, ChoiceSubmissionDto submission, PlayerState opponent)
    {
        switch (action)
        {
            case "PLAY_CARD":
                _playCardAction.Execute(
                player,
                _phase,
                _activePlayerId,
                submission.SelectedCardInstanceId);

                CreateLogEvent(
                    "PLAY_CARD",
                    $"{player.DisplayName} played a card.");
                CreateMainActionPrompt(player);
                break;
            case "ATTACK":
                var gameOver = _attackAction.Execute(player, opponent, _phase, _activePlayerId, submission.SelectedCardInstanceId);

                CreateLogEvent(
                        "ATTACK",
                        $"{player.DisplayName} attacked {opponent.DisplayName}");
                CreateLogEvent(
                    "LIFE_LOST",
                    $"{opponent.DisplayName} lost 1 life");
                if (gameOver)
                {
                    _phase = MatchPhase.GameOver;

                    CreateLogEvent(
                    "GAME_OVER",
                    $"{player.DisplayName} wins!");
                    break;
                }
                else
                {
                    CreateMainActionPrompt(player);
                    break;
                }
            case "END_TURN":

                var previousActivePlayerId = player.PlayerId;

                _endTurnAction.Execute(player, _phase, _activePlayerId);

                EnterEndPhase(player);

                CreateLogEvent("TURN_END", $"Player {previousActivePlayerId} ended turn {_turnNumber}.");

                _turnNumber += 1;

                opponent = GetPlayerSlot(GetOtherPlayerId(previousActivePlayerId));

                _activePlayerId = opponent.PlayerId;


                BeginTurn(opponent);
                break;
            default: throw new InvalidOperationException("Unsupported option for MAIN_ACTION.");
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

            switch (prompt.Kind)
            {
                
                case "MULLIGAN_DECISION":
                    HandleMulliganDecision(normalizedOption, player, submission);
                    break;

                case "MAIN_ACTION":
                    HandleMainAction(normalizedOption, player, submission, opponent);
                    break;
                default: throw new InvalidOperationException($"Unsupported choice kind '{prompt.Kind}'.");
            }
            _openChoicePrompts.Remove(prompt.ChoiceId);
            return CreateUpdate();
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

        if (player.Hand.Count > 0)
        {
            options.Add("PLAY_CARD");
        }

        if (player.PlayedCards.Count > 0)
        {
            options.Add("ATTACK");
        }

        options.Add("END_TURN");

        CreateChoicePrompt(
            player.PlayerId,
            "MAIN_ACTION",
            "Choose your main phase action.",
            options);
    }
    #endregion


    #region MATCH FLOW

    private void BeginTurn(PlayerState player)
    {
        _activePlayerId = player.PlayerId;
        EnterRefreshPhase(player);
        EnterDrawPhase(player);
        EnterMainPhase(player);
    }

    private void EnterRefreshPhase(PlayerState player)
    {
        _phase = MatchPhase.Refresh;
        CreateLogEvent("REFRESH_PHASE", $"Refresh phase for {player.DisplayName}.");
    }

    private void EnterDrawPhase(PlayerState player)
    {
        _phase = MatchPhase.Draw;

        if (player.DrawDeck.Count > 0)
        {
            DrawCards(player, 1);
            CreateLogEvent("DRAW_CARD", $"{player.DisplayName} drew 1 card.");
            return;
        }

        CreateLogEvent("DECK_EMPTY", $"{player.DisplayName} cannot draw because their deck is empty.");
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
                Phase = _phase,
                Players = _players
                    .Select(player => CreatePlayerStateSnapshot(player, viewerPlayerId))
                    .ToArray()
            };
        }
    }
    private static PlayerStateDto CreatePlayerStateSnapshot(PlayerState player, string? viewerPlayerId)
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
            BoardCount = player.PlayedCards.Count,
            DeckName = player.DeckName,
            LeaderCardId = player.LeaderCardId,
            MainDeckCount = player.MainDeckCount,
            HasDeck = player.HasDeck,
            TrashCount = player.TrashCards.Count,
            HandCards = isViewer ? player.Hand.Select(ToCardInstanceDto).ToArray() : [],
            BoardCards = player.PlayedCards.Select(ToCardInstanceDto).ToArray(),
            TrashCards = player.TrashCards.Select(ToCardInstanceDto).ToArray()
        };
    }

    private static CardInstanceDto ToCardInstanceDto(CardInstance card)
    {
        return new CardInstanceDto
        {
            InstanceId = card.InstanceId,
            CardId = card.CardId,
            Name = card.Name
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
        DrawCards(player, 5);
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
                    Name = string.IsNullOrWhiteSpace(card.Name) ? card.CardId : card.Name
                }))
            .ToList();
    }

    private static void Shuffle(IList<CardInstance> cards)
    {
        for (var index = cards.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
        }
    }

    private static void DrawCards(PlayerState player, int amount)
    {
        var cardsToDraw = Math.Min(Math.Max(0, amount), player.DrawDeck.Count);

        for (var index = 0; index < cardsToDraw; index += 1)
        {
            var drawnCard = player.DrawDeck[0];
            player.DrawDeck.RemoveAt(0);
            player.Hand.Add(drawnCard);
        }

        player.DeckCount = player.DrawDeck.Count;
        player.HandCount = player.Hand.Count;
    }
}

#endregion

public sealed record MatchUpdate
{
    public GameStateDto StateSnapshot { get; init; } = new();
    public IReadOnlyDictionary<string, GameStateDto> StateSnapshots { get; init; } =
        new Dictionary<string, GameStateDto>(StringComparer.Ordinal);
    public IReadOnlyList<LogEventDto> LogEvents { get; init; } = [];
    public IReadOnlyList<ChoicePromptDto> ChoicePrompts { get; init; } = [];
}

