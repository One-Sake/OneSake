using Microsoft.AspNetCore.SignalR;
using OneSake.Server.Hubs;

namespace OneSake.Server.Match;

// Checks every second whether a deadline ran out: the reconnect grace period of a disconnected
// player (and, later, turn and answer timers). The rooms decide themselves (MatchRoom.Tick, thread-safe).
public sealed class MatchDeadlineService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly MatchRoomManager _roomManager;
    private readonly MatchUpdatePublisher _publisher;
    private readonly IHubContext<MatchHub> _hubContext;
    private readonly ILogger<MatchDeadlineService> _logger;

    public MatchDeadlineService(
        MatchRoomManager roomManager,
        MatchUpdatePublisher publisher,
        IHubContext<MatchHub> hubContext,
        ILogger<MatchDeadlineService> logger)
    {
        _roomManager = roomManager;
        _publisher = publisher;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var anyChange = false;
            foreach (var room in _roomManager.GetAllRooms())
            {
                try
                {
                    var update = room.Tick();
                    if (update is null)
                    {
                        continue;
                    }

                    anyChange = true;
                    await _publisher.PublishAsync(room.RoomCode, update, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // One broken room must not stop the deadlines of all other rooms.
                    _logger.LogError(exception, "Deadline check failed for room {RoomCode}.", room.RoomCode);
                }
            }

            if (anyChange)
            {
                await _hubContext.Clients.All.SendAsync("RoomList", _roomManager.GetRoomSummaries(), stoppingToken);
            }
        }
    }
}
