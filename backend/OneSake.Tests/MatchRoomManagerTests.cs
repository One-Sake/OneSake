using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests;

public class MatchRoomManagerTests
{
    [Fact]
    public void Create_ThrowsWhenRoomAlreadyExists()
    {
        var manager = new MatchRoomManager();
        manager.Create("ABCD");

        Assert.Throws<InvalidOperationException>(() => manager.Create("abcd"));
    }

    [Fact]
    public void GetRoomSummaries_ListsRoomsWithPlayerCount()
    {
        var manager = new MatchRoomManager();
        var room = manager.Create("ABCD");
        room.JoinPlayer("p1", "Alice");

        var summary = Assert.Single(manager.GetRoomSummaries());

        Assert.Equal("ABCD", summary.RoomCode);
        Assert.Equal(1, summary.PlayerCount);
        Assert.Equal(2, summary.MaxPlayers);
        Assert.Equal(["Alice"], summary.PlayerNames);
    }

    [Fact]
    public void RemovePlayer_FreesSlotInRoom()
    {
        var room = new MatchRoom("ABCD");
        room.JoinPlayer("p1", "Alice");
        room.JoinPlayer("p2", "Bob");

        var update = room.RemovePlayer("p1");

        Assert.NotNull(update);
        Assert.False(room.HasPlayer("p1"));
        Assert.Equal(1, room.CreateSummary().PlayerCount);
        Assert.Contains(update.LogEvents, logEvent => logEvent.Type == "LEAVE");
        Assert.Null(room.RemovePlayer("p1"));
    }

    [Fact]
    public void RemoveInactiveRooms_OnlyDeletesEmptyRoomsAfterTimeout()
    {
        var manager = new MatchRoomManager();
        var emptyRoom = manager.Create("EMPTY");
        var activeRoom = manager.Create("ACTIVE");
        activeRoom.JoinPlayer("p1", "Alice");
        emptyRoom.JoinPlayer("p2", "Bob");
        emptyRoom.RemovePlayer("p2");

        var timeout = TimeSpan.FromMinutes(5);

        Assert.Empty(manager.RemoveInactiveRooms(DateTimeOffset.UtcNow, timeout));

        var removed = manager.RemoveInactiveRooms(DateTimeOffset.UtcNow.AddMinutes(6), timeout);

        Assert.Equal(["EMPTY"], removed);
        Assert.Null(manager.Get("EMPTY"));
        Assert.NotNull(manager.Get("ACTIVE"));
    }

    [Fact]
    public void GetRoomsForConnection_FindsRoomOfPlayer()
    {
        var manager = new MatchRoomManager();
        manager.Create("ROOM1").JoinPlayer("p1", "Alice");
        manager.Create("ROOM2");

        var room = Assert.Single(manager.GetRoomsForConnection("p1"));

        Assert.Equal("ROOM1", room.RoomCode);
        Assert.Equal(MatchPhase.Lobby, room.CreateSummary().Phase);
    }
}
