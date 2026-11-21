using OneSake.Server.Match;

namespace OneSake.Tests.Session;

// A player is identified by a stable seat (player ID + rejoin token), not by the SignalR connection.
public class SeatIdentityTests
{
    [Fact]
    public void JoinConnection_GivesEachSeatAStablePlayerIdAndASecretToken()
    {
        var room = new MatchRoom("ABCD");

        var (_, alice) = room.JoinConnection("conn-a", "Alice");
        var (update, bob) = room.JoinConnection("conn-b", "Bob");

        Assert.StartsWith("p1-", alice.PlayerId);
        Assert.StartsWith("p2-", bob.PlayerId);
        Assert.Equal("ABCD", alice.RoomCode);
        // 32 random bytes as Base64Url without padding = 43 characters.
        Assert.Equal(43, alice.RejoinToken.Length);
        Assert.NotEqual(alice.RejoinToken, bob.RejoinToken);

        Assert.Equal(alice.PlayerId, room.ResolvePlayerId("conn-a"));
        Assert.Equal(bob.PlayerId, room.ResolvePlayerId("conn-b"));
        Assert.Null(room.ResolvePlayerId("someone-else"));

        // Snapshots and prompts go to the connection of the seat.
        Assert.Equal("conn-a", update.Connections[alice.PlayerId]);
        Assert.Equal("conn-b", update.Connections[bob.PlayerId]);
        Assert.Contains(update.StateSnapshot.Players, player => player.PlayerId == alice.PlayerId);
        Assert.DoesNotContain(update.StateSnapshot.Players, player => player.PlayerId == "conn-a");
    }

    [Fact]
    public void JoinConnection_SameConnectionAgain_KeepsTheSeat()
    {
        var room = new MatchRoom("ABCD");
        var (_, first) = room.JoinConnection("conn-a", "Alice");

        var (update, second) = room.JoinConnection("conn-a", "Alice the Second");

        Assert.Equal(first.PlayerId, second.PlayerId);
        Assert.Equal(first.RejoinToken, second.RejoinToken);
        Assert.Single(update.StateSnapshot.Players);
    }

    [Fact]
    public void RemovePlayer_FreesTheSeatOfTheConnection()
    {
        var room = new MatchRoom("ABCD");
        var (_, alice) = room.JoinConnection("conn-a", "Alice");

        room.RemovePlayer(alice.PlayerId);

        Assert.False(room.HasPlayer("conn-a"));
        Assert.Null(room.ResolvePlayerId("conn-a"));
    }
}
