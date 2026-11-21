using Microsoft.AspNetCore.SignalR;
using OneSake.Domain;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Server.Hubs;

public class MatchHub : Hub
{
    private readonly MatchRoomManager _roomManager;
    private readonly MatchmakingQueue _matchmakingQueue;
    private readonly MatchUpdatePublisher _publisher;

    public MatchHub(
        MatchRoomManager roomManager,
        MatchmakingQueue matchmakingQueue,
        MatchUpdatePublisher publisher)
    {
        _roomManager = roomManager;
        _matchmakingQueue = matchmakingQueue;
        _publisher = publisher;
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
        await JoinExistingRoom(room, normalizedRoomCode, displayName, GetConnectionGuestId());
    }

    public async Task JoinRoom(string roomCode, string displayName)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        await JoinExistingRoom(room, normalizedRoomCode, displayName, GetConnectionGuestId());
    }

    // The client sends its guest ID in the hub URL (/matchHub?guestId=...).
    // SignalR does not support optional hub method parameters, so a third argument on
    // CreateRoom/JoinRoom would break older clients. Without the query value the ID is null.
    // NOT secure authentication - see GuestIdValidator.
    private string? GetConnectionGuestId()
    {
        var guestId = Context.GetHttpContext()?.Request.Query["guestId"].ToString();
        return GuestIdValidator.Normalize(guestId);
    }

    // Quick Match: waits in the queue or, if somebody is already waiting, creates a private
    // room for both players and sends them "MatchFound". Each client then sets its deck as usual.
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
            GuestIdValidator.Normalize(guestId) ?? GetConnectionGuestId());
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

        foreach (var room in _roomManager.GetRoomsForConnection(Context.ConnectionId))
        {
            await RemoveFromRoom(room);
        }

        await BroadcastRoomList();
        await base.OnDisconnectedAsync(exception);
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
