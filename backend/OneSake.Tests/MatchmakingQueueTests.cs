using OneSake.Server.Match;

namespace OneSake.Tests;

public class MatchmakingQueueTests
{
    [Fact]
    public void Enqueue_FirstPlayerWaits_SecondPlayerCreatesPair()
    {
        var queue = new MatchmakingQueue();

        Assert.Null(queue.Enqueue(new QueueEntry("c1", "Alice", null)));
        var pair = queue.Enqueue(new QueueEntry("c2", "Bob", null));

        Assert.NotNull(pair);
        Assert.Equal("c1", pair.First.ConnectionId);
        Assert.Equal("c2", pair.Second.ConnectionId);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_SameConnectionTwice_HasNoDuplicateEntry()
    {
        var queue = new MatchmakingQueue();

        Assert.Null(queue.Enqueue(new QueueEntry("c1", "Alice", null)));
        Assert.Null(queue.Enqueue(new QueueEntry("c1", "Alice again", null)));

        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Enqueue_SameGuestInTwoTabs_IsNotMatchedAgainstItself()
    {
        var queue = new MatchmakingQueue();
        const string guestId = "11111111-1111-4111-8111-111111111111";

        Assert.Null(queue.Enqueue(new QueueEntry("tab1", "Alice", guestId)));
        Assert.Null(queue.Enqueue(new QueueEntry("tab2", "Alice", guestId)));
        Assert.Equal(2, queue.Count);

        var pair = queue.Enqueue(new QueueEntry("c3", "Bob", null));
        Assert.NotNull(pair);
        Assert.Equal("tab1", pair.First.ConnectionId);
    }

    [Fact]
    public void Remove_TakesPlayerOutOfTheQueue()
    {
        var queue = new MatchmakingQueue();
        queue.Enqueue(new QueueEntry("c1", "Alice", null));

        // LeaveQueue and OnDisconnectedAsync both call Remove.
        Assert.True(queue.Remove("c1"));
        Assert.False(queue.Contains("c1"));
        Assert.False(queue.Remove("c1"));

        // Nobody is waiting anymore, so the next player waits instead of being paired.
        Assert.Null(queue.Enqueue(new QueueEntry("c2", "Bob", null)));
    }

    [Fact]
    public void CreateQuickMatchRoom_PutsBothPlayersIntoAHiddenRoom()
    {
        var manager = new MatchRoomManager();
        manager.Create("PUBLIC");
        var pair = new QueuePair(new QueueEntry("c1", "Alice", null), new QueueEntry("c2", "Bob", null));

        var (room, update) = manager.CreateQuickMatchRoom(pair);

        Assert.True(room.IsQuickMatch);
        Assert.Equal(6, room.RoomCode.Length);
        Assert.True(room.HasPlayer("c1"));
        Assert.True(room.HasPlayer("c2"));
        Assert.Equal(2, update.StateSnapshot.Players.Count);
        Assert.Same(room, manager.Get(room.RoomCode));

        // Only the normal room is listed.
        var summary = Assert.Single(manager.GetRoomSummaries());
        Assert.Equal("PUBLIC", summary.RoomCode);
    }
}
