using OneSake.Domain;

namespace OneSake.Server.Match;

public sealed class MatchRoomManager
{
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int RoomCodeLength = 6;

    private readonly object _sync = new();
    private readonly Dictionary<string, MatchRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    // Creates the dice for each new room (fixed values only in Development, see Program.cs).
    private readonly Func<IDiceRoller> _diceRollerFactory;

    public MatchRoomManager(Func<IDiceRoller>? diceRollerFactory = null)
    {
        _diceRollerFactory = diceRollerFactory ?? (() => CryptoDiceRoller.Instance);
    }

    public MatchRoom Create(string roomCode)
    {
        lock (_sync)
        {
            if (_rooms.ContainsKey(roomCode))
            {
                throw new InvalidOperationException($"Room {roomCode} already exists.");
            }

            var createdRoom = new MatchRoom(roomCode, diceRoller: _diceRollerFactory());
            _rooms[roomCode] = createdRoom;
            return createdRoom;
        }
    }

    // Creates a private room with a random code and puts both Quick Match players into it.
    public (MatchRoom Room, MatchUpdate Update) CreateQuickMatchRoom(QueuePair pair)
    {
        MatchRoom room;
        lock (_sync)
        {
            string roomCode;
            do
            {
                roomCode = CreateRandomRoomCode();
            }
            while (_rooms.ContainsKey(roomCode));

            room = new MatchRoom(roomCode, isQuickMatch: true, diceRoller: _diceRollerFactory());
            _rooms[roomCode] = room;
        }

        room.JoinPlayer(pair.First.ConnectionId, pair.First.DisplayName, pair.First.GuestId);
        var update = room.JoinPlayer(pair.Second.ConnectionId, pair.Second.DisplayName, pair.Second.GuestId);
        return (room, update);
    }

    private static string CreateRandomRoomCode()
    {
        var chars = new char[RoomCodeLength];
        for (var index = 0; index < chars.Length; index += 1)
        {
            chars[index] = RoomCodeAlphabet[Random.Shared.Next(RoomCodeAlphabet.Length)];
        }

        return new string(chars);
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
                .Where(room => !room.IsQuickMatch)
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
