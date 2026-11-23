using Microsoft.AspNetCore.SignalR;
using OneSake.Domain;
using OneSake.Server.Auth;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Server.Hubs;

public class MatchHub : Hub
{
    private readonly MatchRoomManager _roomManager;
    private readonly MatchmakingQueue _matchmakingQueue;
    private readonly MatchUpdatePublisher _publisher;
    private readonly IAccountStore _accounts;

    public MatchHub(
        MatchRoomManager roomManager,
        MatchmakingQueue matchmakingQueue,
        MatchUpdatePublisher publisher,
        IAccountStore accounts)
    {
        _roomManager = roomManager;
        _matchmakingQueue = matchmakingQueue;
        _publisher = publisher;
        _accounts = accounts;
    }

    private const int MaxRoomCodeLength = 12;

    // Returns all rooms that currently exist on the server.
    public IReadOnlyList<RoomSummaryDto> GetRooms()
    {
        return _roomManager.GetRoomSummaries();
    }

    public async Task CreateRoom(string roomCode, string displayName)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = RunRoomAction(() => _roomManager.Create(normalizedRoomCode));
        await JoinExistingRoom(room, normalizedRoomCode, displayName, await ResolveConnectionPlayerIdAsync());
    }

    // Private table with a chosen timer: "STANDARD", "RELAXED" or "OFF". CreateRoom stays for older
    // clients (SignalR has no optional parameters).
    public async Task CreateRoomWithTimer(string roomCode, string displayName, string timerMode)
    {
        var mode = (timerMode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "RELAXED" => TimerMode.Relaxed,
            "OFF" => TimerMode.Off,
            _ => TimerMode.Standard
        };
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = RunRoomAction(() => _roomManager.Create(normalizedRoomCode, mode));
        await JoinExistingRoom(room, normalizedRoomCode, displayName, await ResolveConnectionPlayerIdAsync());
    }

    public async Task JoinRoom(string roomCode, string displayName)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        await JoinExistingRoom(room, normalizedRoomCode, displayName, await ResolveConnectionPlayerIdAsync());
    }

    // Logged in: the player ID always comes from the login cookie (Context.User), never from the client.
    // Guest: the guest ID from the hub URL (/matchHub?guestId=...), unless an account has taken it over.
    // SignalR does not support optional hub method parameters, so a third argument on
    // CreateRoom/JoinRoom would break older clients. Without the query value the ID is null.
    private Task<string?> ResolveConnectionPlayerIdAsync()
    {
        var guestId = Context.GetHttpContext()?.Request.Query["guestId"].ToString();
        return PlayerIdentity.ResolveAsync(Context.User, guestId, IsTakenByAccountAsync);
    }

    // Without the database the guest plays without statistics (safe side), the match still works.
    private async Task<bool> IsTakenByAccountAsync(string playerId)
    {
        try
        {
            return await _accounts.IsPlayerIdTakenAsync(playerId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return true;
        }
    }

    // Quick Match: waits in the queue or, if somebody is already waiting, creates a private
    // room for both players and sends them "MatchFound". Each client then sets its deck as usual.
    // The guestId parameter stays for older clients but is ignored: ranked games (rating/bounty)
    // only count for logged-in players, otherwise anybody could play on somebody else's rating.
    public async Task JoinQueue(string displayName, string? guestId)
    {
        // Searching for a match means leaving the current room.
        var currentRooms = _roomManager.GetRoomsForConnection(Context.ConnectionId);
        foreach (var room in currentRooms)
        {
            await RemoveFromRoom(room);
        }

        var entry = new QueueEntry(
            Context.ConnectionId,
            displayName ?? string.Empty,
            AuthClaims.GetPlayerId(Context.User));
        var pair = _matchmakingQueue.Enqueue(entry);

        if (pair is not null)
        {
            var (matchRoom, update, firstSeat, secondSeat) = _roomManager.CreateQuickMatchRoom(pair);
            await Groups.AddToGroupAsync(pair.First.ConnectionId, matchRoom.RoomCode);
            await Groups.AddToGroupAsync(pair.Second.ConnectionId, matchRoom.RoomCode);
            await Clients.Client(pair.First.ConnectionId).SendAsync("SeatAssigned", firstSeat);
            await Clients.Client(pair.Second.ConnectionId).SendAsync("SeatAssigned", secondSeat);
            await PublishMatchUpdate(matchRoom.RoomCode, update);

            var matchFound = new MatchFoundDto { RoomCode = matchRoom.RoomCode };
            await Clients.Clients(pair.First.ConnectionId, pair.Second.ConnectionId).SendAsync("MatchFound", matchFound);
        }

        if (currentRooms.Count > 0)
        {
            await BroadcastRoomList();
        }
    }

    public Task LeaveQueue()
    {
        _matchmakingQueue.Remove(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public async Task LeaveRoom(string roomCode)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        // Leaving on purpose during a running match counts as giving up.
        await RemoveFromRoom(room, MatchEndReasons.Concede);
        await BroadcastRoomList();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _matchmakingQueue.Remove(Context.ConnectionId);

        // The seat is kept for a grace period (MatchRoom.Disconnect), the player can come back with
        // RejoinRoom. Leaving on purpose (LeaveRoom, Concede) is still immediate.
        foreach (var room in _roomManager.GetRoomsForConnection(Context.ConnectionId))
        {
            var update = room.Disconnect(Context.ConnectionId);
            if (update is not null)
            {
                await PublishMatchUpdate(room.RoomCode, update);
            }
        }

        await BroadcastRoomList();
        await base.OnDisconnectedAsync(exception);
    }

    // After a page reload: is the stored seat still there? Does not take the seat.
    public RejoinResultDto CheckRejoin(string roomCode, string playerId, string rejoinToken)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var status = _roomManager.Get(normalizedRoomCode)?.CheckRejoin(playerId, rejoinToken) ?? RejoinStatus.Invalid;
        return new RejoinResultDto
        {
            Status = status switch
            {
                RejoinStatus.Ok => "OK",
                RejoinStatus.Expired => "EXPIRED",
                _ => "INVALID"
            },
            RoomCode = normalizedRoomCode,
            PlayerId = status == RejoinStatus.Invalid ? string.Empty : playerId
        };
    }

    // Back to the own seat after a lost connection or a page reload (token from SeatAssigned).
    public async Task<RejoinResultDto> RejoinRoom(string roomCode, string playerId, string rejoinToken)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = _roomManager.Get(normalizedRoomCode);
        var invalid = new RejoinResultDto { Status = "INVALID", RoomCode = normalizedRoomCode };
        if (room is null)
        {
            return invalid;
        }

        var outcome = room.Rejoin(playerId, rejoinToken, Context.ConnectionId);
        switch (outcome.Status)
        {
            case RejoinStatus.Invalid:
                return invalid;

            case RejoinStatus.Expired:
                if (outcome.Update is not null)
                {
                    await PublishMatchUpdate(normalizedRoomCode, outcome.Update);
                }

                // Only the final result: the seat is lost, no actions are possible.
                await Clients.Caller.SendAsync("StateSnapshot", outcome.FinalState);
                await Clients.Caller.SendAsync("Resync", outcome.Resync);
                return new RejoinResultDto { Status = "EXPIRED", RoomCode = normalizedRoomCode, PlayerId = playerId };
        }

        // A connection can only be in one room; a running Quick Match search ends.
        _matchmakingQueue.Remove(Context.ConnectionId);
        if (outcome.PreviousConnectionId is not null)
        {
            await Groups.RemoveFromGroupAsync(outcome.PreviousConnectionId, normalizedRoomCode);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedRoomCode);
        await PublishMatchUpdate(normalizedRoomCode, outcome.Update!);
        await Clients.Caller.SendAsync("Resync", outcome.Resync);
        await BroadcastRoomList();
        return new RejoinResultDto { Status = "OK", RoomCode = normalizedRoomCode, PlayerId = playerId };
    }

    public async Task StartMatch(string roomCode)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        var update = RunRoomAction(room.StartMatch);
        await PublishMatchUpdate(normalizedRoomCode, update);
        await BroadcastRoomList();
    }

    public async Task SetPlayerDeck(string roomCode, PlayerDeckSubmissionDto deck)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        var update = RunRoomAction(() => room.SetPlayerDeck(ResolvePlayerId(room), deck));
        await PublishMatchUpdate(normalizedRoomCode, update);
    }

    public async Task SubmitChoice(string roomCode, ChoiceSubmissionDto submission)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);

        // The player always comes from the connection. A client can only answer for its own seat.
        var playerId = ResolvePlayerId(room);
        if (!string.Equals(submission.PlayerId, playerId, StringComparison.Ordinal))
        {
            throw new HubException("Invalid player id for this connection.");
        }

        var normalizedSubmission = submission with
        {
            ChoiceId = submission.ChoiceId.Trim(),
            PlayerId = playerId,
            SelectedOption = submission.SelectedOption.Trim(),
            SelectedCardInstanceId = string.IsNullOrWhiteSpace(submission.SelectedCardInstanceId)
                ? null
                : submission.SelectedCardInstanceId.Trim(),
            TargetCardInstanceId = string.IsNullOrWhiteSpace(submission.TargetCardInstanceId)
                ? null
                : submission.TargetCardInstanceId.Trim()
        };

        var update = RunRoomAction(() => room.SubmitChoice(normalizedSubmission));
        await PublishMatchUpdate(normalizedRoomCode, update);
    }

    // Manual mode: move cards, K.O., +power... to resolve card effects by hand.
    public async Task ManualAction(string roomCode, ManualActionDto action)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        if (action is null)
        {
            throw new HubException("A manual action is required.");
        }

        var update = RunRoomAction(() => room.ManualAction(ResolvePlayerId(room), action));
        await PublishMatchUpdate(normalizedRoomCode, update);
    }

    public async Task SendChat(string roomCode, string text)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        var normalizedText = text.Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        var senderId = ResolvePlayerId(room);
        var chatMessage = new ChatMessageDto
        {
            RoomCode = normalizedRoomCode,
            SenderId = senderId,
            Text = normalizedText,
            TsUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        room.AddChatMessage(chatMessage);
        await Clients.Group(normalizedRoomCode).SendAsync("ChatMessage", chatMessage);

        var logEvent = room.CreateLogEvent("CHAT", $"Player {senderId}: {normalizedText}");
        await Clients.Group(normalizedRoomCode).SendAsync("LogEvent", logEvent);
    }

    private async Task JoinExistingRoom(MatchRoom room, string roomCode, string displayName, string? guestId)
    {
        // Entering a room ends a running Quick Match search.
        _matchmakingQueue.Remove(Context.ConnectionId);

        // A connection can only be in one room at a time.
        foreach (var otherRoom in _roomManager.GetRoomsForConnection(Context.ConnectionId))
        {
            if (otherRoom != room)
            {
                await RemoveFromRoom(otherRoom);
            }
        }

        var (update, seat) = RunRoomAction(() => room.JoinConnection(Context.ConnectionId, displayName, guestId));
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Caller.SendAsync("SeatAssigned", seat);
        await PublishMatchUpdate(roomCode, update);
        await BroadcastRoomList();
    }

    private async Task RemoveFromRoom(MatchRoom room, string endReason = MatchEndReasons.Disconnect)
    {
        var playerId = room.ResolvePlayerId(Context.ConnectionId);
        var update = playerId is null ? null : room.RemovePlayer(playerId, endReason);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.RoomCode);

        if (update is not null)
        {
            await PublishMatchUpdate(room.RoomCode, update);
        }
    }

    private Task BroadcastRoomList()
    {
        return Clients.All.SendAsync("RoomList", _roomManager.GetRoomSummaries());
    }

    // Room rules throw InvalidOperationException. HubException sends the message to the client.
    private static T RunRoomAction<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    // Every action is done for the player whose seat this connection holds, never for the
    // connection ID itself (it changes after a reconnect).
    private string ResolvePlayerId(MatchRoom room)
    {
        return room.ResolvePlayerId(Context.ConnectionId)
            ?? throw new HubException("You are not a player in this room.");
    }

    private MatchRoom GetExistingRoom(string roomCode)
    {
        return _roomManager.Get(roomCode) ?? throw new HubException($"Room {roomCode} does not exist.");
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        var normalizedRoomCode = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRoomCode))
        {
            throw new HubException("roomCode is required.");
        }

        if (normalizedRoomCode.Length > MaxRoomCodeLength || !normalizedRoomCode.All(char.IsLetterOrDigit))
        {
            throw new HubException($"Room code must be 1-{MaxRoomCodeLength} letters or digits.");
        }

        return normalizedRoomCode;
    }

    private Task PublishMatchUpdate(string roomCode, MatchUpdate update)
    {
        return _publisher.PublishAsync(roomCode, update);
    }
}
