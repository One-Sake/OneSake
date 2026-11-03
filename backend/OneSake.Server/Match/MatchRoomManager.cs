using OneSake.Domain;

namespace OneSake.Server.Match;

public sealed class MatchRoomManager
{
    private readonly object _sync = new();
    private readonly Dictionary<string, MatchRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public MatchRoom Create(string roomCode)
    {
        lock (_sync)
        {
            if (_rooms.ContainsKey(roomCode))
            {
                throw new InvalidOperationException($"Room {roomCode} already exists.");
            }

            var createdRoom = new MatchRoom(roomCode);
            _rooms[roomCode] = createdRoom;
            return createdRoom;
        }
    }

    public MatchRoom? Get(string roomCode)
    {
        lock (_sync)
        {
            _rooms.TryGetValue(roomCode, out var room);
            return room;
        }
    }

    // Returns every room the given connection is currently a player in.
    public IReadOnlyList<MatchRoom> GetRoomsForConnection(string connectionId)
    {
        lock (_sync)
        {
            return _rooms.Values.Where(room => room.HasPlayer(connectionId)).ToArray();
        }
    }

    public IReadOnlyList<RoomSummaryDto> GetRoomSummaries()
    {
        lock (_sync)
        {
            return _rooms.Values
                .Select(room => room.CreateSummary())
                .OrderByDescending(summary => summary.CreatedAtUnixMs)
                .ToArray();
        }
    }

    // Deletes all rooms that have been empty for at least the given timeout.
    // Returns the codes of the deleted rooms.
    public IReadOnlyList<string> RemoveInactiveRooms(DateTimeOffset now, TimeSpan timeout)
    {
        lock (_sync)
        {
            var inactiveRoomCodes = _rooms
                .Where(entry => entry.Value.IsInactive(now, timeout))
                .Select(entry => entry.Key)
                .ToArray();

            foreach (var roomCode in inactiveRoomCodes)
            {
                _rooms.Remove(roomCode);
            }

            return inactiveRoomCodes;
        }
    }
}
