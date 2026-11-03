using Microsoft.AspNetCore.SignalR;
using OneSake.Server.Hubs;

namespace OneSake.Server.Match;

// Runs in the background and deletes rooms that have been empty for 5 minutes.
public sealed class RoomCleanupService : BackgroundService
{
    public static readonly TimeSpan InactiveRoomTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly MatchRoomManager _roomManager;
    private readonly IHubContext<MatchHub> _hubContext;
    private readonly ILogger<RoomCleanupService> _logger;

    public RoomCleanupService(
        MatchRoomManager roomManager,
        IHubContext<MatchHub> hubContext,
        ILogger<RoomCleanupService> logger)
    {
        _roomManager = roomManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removedRoomCodes = _roomManager.RemoveInactiveRooms(DateTimeOffset.UtcNow, InactiveRoomTimeout);
            if (removedRoomCodes.Count == 0)
            {
                continue;
            }

            _logger.LogInformation("Deleted inactive rooms: {RoomCodes}", string.Join(", ", removedRoomCodes));
            await _hubContext.Clients.All.SendAsync("RoomList", _roomManager.GetRoomSummaries(), stoppingToken);
        }
    }
}
