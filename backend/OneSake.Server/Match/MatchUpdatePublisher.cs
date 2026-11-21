using Microsoft.AspNetCore.SignalR;
using OneSake.Server.Hubs;
using OneSake.Server.Services;

namespace OneSake.Server.Match;

// Sends a MatchUpdate to the players and saves a finished match. Used by the hub and by the
// background service that checks reconnect deadlines and turn timers.
public sealed class MatchUpdatePublisher
{
    private readonly IHubContext<MatchHub> _hubContext;
    private readonly MatchResultService _matchResultService;
    private readonly MatchRecordingService _matchRecordingService;

    public MatchUpdatePublisher(
        IHubContext<MatchHub> hubContext,
        MatchResultService matchResultService,
        MatchRecordingService matchRecordingService)
    {
        _hubContext = hubContext;
        _matchResultService = matchResultService;
        _matchRecordingService = matchRecordingService;
    }

    public async Task PublishAsync(string roomCode, MatchUpdate update, CancellationToken cancellationToken = default)
    {
        // Every update passes through here, so a finished match is saved exactly once.
        if (update.MatchResult is not null)
        {
            _matchResultService.SaveInBackground(update.MatchResult, update.Recording);
        }
        else if (update.Recording is not null)
        {
            // A match without a result (both players lost): only the recording is stored.
            _matchRecordingService.SaveInBackground(update.Recording);
        }

        var clients = _hubContext.Clients;

        // Snapshots and prompts are private: they go to the connection of the player's seat.
        // A disconnected player has no connection and gets everything again on RejoinRoom.
        if (update.StateSnapshots.Count > 0)
        {
            foreach (var playerSnapshot in update.StateSnapshots)
            {
                if (update.Connections.TryGetValue(playerSnapshot.Key, out var connectionId))
                {
                    await clients.Client(connectionId).SendAsync("StateSnapshot", playerSnapshot.Value, cancellationToken);
                }
            }
        }
        else
        {
            await clients.Group(roomCode).SendAsync("StateSnapshot", update.StateSnapshot, cancellationToken);
        }

        foreach (var logEvent in update.LogEvents)
        {
            await clients.Group(roomCode).SendAsync("LogEvent", logEvent, cancellationToken);
        }

        foreach (var prompt in update.ChoicePrompts)
        {
            if (update.Connections.TryGetValue(prompt.PlayerId, out var connectionId))
            {
                await clients.Client(connectionId).SendAsync("ChoicePrompt", prompt, cancellationToken);
            }
        }
    }
}
