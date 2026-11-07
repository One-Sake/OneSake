using Microsoft.AspNetCore.SignalR;
using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Server.Hubs;

public class MatchHub : Hub
{
    private readonly MatchRoomManager _roomManager;

    public MatchHub(MatchRoomManager roomManager)
    {
        _roomManager = roomManager;
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
        await JoinExistingRoom(room, normalizedRoomCode, displayName);
    }

    public async Task JoinRoom(string roomCode, string displayName)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        await JoinExistingRoom(room, normalizedRoomCode, displayName);
    }

    public async Task LeaveRoom(string roomCode)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        await RemoveFromRoom(room);
        await BroadcastRoomList();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
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
        var update = RunRoomAction(() => room.SetPlayerDeck(Context.ConnectionId, deck));
        await PublishMatchUpdate(normalizedRoomCode, update);
    }

    public async Task SubmitChoice(string roomCode, ChoiceSubmissionDto submission)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);

        if (!string.Equals(submission.PlayerId, Context.ConnectionId, StringComparison.Ordinal))
        {
            throw new HubException("Invalid player id for this connection.");
        }

        var normalizedSubmission = submission with
        {
            ChoiceId = submission.ChoiceId.Trim(),
            PlayerId = Context.ConnectionId,
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

    public async Task SendChat(string roomCode, string text)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var room = GetExistingRoom(normalizedRoomCode);
        var normalizedText = text.Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        var senderId = room.ResolvePlayerId(Context.ConnectionId);
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

    private async Task JoinExistingRoom(MatchRoom room, string roomCode, string displayName)
    {
        // A connection can only be in one room at a time.
        foreach (var otherRoom in _roomManager.GetRoomsForConnection(Context.ConnectionId))
        {
            if (otherRoom != room)
            {
                await RemoveFromRoom(otherRoom);
            }
        }

        var update = RunRoomAction(() => room.JoinPlayer(Context.ConnectionId, displayName));
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await PublishMatchUpdate(roomCode, update);
        await BroadcastRoomList();
    }

    private async Task RemoveFromRoom(MatchRoom room)
    {
        var update = room.RemovePlayer(Context.ConnectionId);
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

    private async Task PublishMatchUpdate(string roomCode, MatchUpdate update)
    {
        if (update.StateSnapshots.Count > 0)
        {
            foreach (var playerSnapshot in update.StateSnapshots)
            {
                await Clients.Client(playerSnapshot.Key).SendAsync("StateSnapshot", playerSnapshot.Value);
            }
        }
        else
        {
            await Clients.Group(roomCode).SendAsync("StateSnapshot", update.StateSnapshot);
        }

        foreach (var logEvent in update.LogEvents)
        {
            await Clients.Group(roomCode).SendAsync("LogEvent", logEvent);
        }

        foreach (var prompt in update.ChoicePrompts)
        {
            await Clients.Client(prompt.PlayerId).SendAsync("ChoicePrompt", prompt);
        }
    }
}
