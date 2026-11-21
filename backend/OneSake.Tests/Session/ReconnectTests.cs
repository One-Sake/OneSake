using OneSake.Domain;
using OneSake.Game.Replay;
using OneSake.Server.Match;

namespace OneSake.Tests.Session;

// A lost connection does not end the match: the seat is kept for a grace period.
public class ReconnectTests
{
    [Fact]
    public void Disconnect_KeepsTheSeat_AndRejoinRestoresStateAndPrompts()
    {
        var match = SessionMatch.StartInMain();
        var alice = match.Alice;
        var handBefore = match.Player(alice.PlayerId).HandCards.Select(card => card.InstanceId).ToArray();
        var promptBefore = match.Prompt("MAIN_ACTION", alice.PlayerId);

        var disconnect = match.Remember(match.Room.Disconnect("conn-a"));

        // Bob sees that Alice is gone and until when her seat is kept.
        var bobView = disconnect.StateSnapshots[match.Bob.PlayerId].Players.Single(player => player.PlayerId == alice.PlayerId);
        Assert.False(bobView.Connected);
        Assert.Equal(match.Clock.UtcNow.AddSeconds(90).ToUnixTimeMilliseconds(), bobView.DisconnectedUntilUnixMs);
        Assert.Contains(disconnect.LogEvents, log => log.Type == "PLAYER_DISCONNECTED");
        // Alice has no connection right now: nothing is sent to her.
        Assert.False(disconnect.Connections.ContainsKey(alice.PlayerId));
        Assert.Equal(MatchPhase.Main, disconnect.StateSnapshot.Phase);

        match.Clock.Advance(30);
        Assert.Null(match.Room.Tick());
        var outcome = match.Room.Rejoin(alice.PlayerId, alice.RejoinToken, "conn-a2");

        Assert.Equal(RejoinStatus.Ok, outcome.Status);
        var update = match.Remember(outcome.Update);
        Assert.Equal("conn-a2", update.Connections[alice.PlayerId]);
        Assert.Contains(update.LogEvents, log => log.Type == "PLAYER_RECONNECTED");
        var aliceView = update.StateSnapshots[alice.PlayerId].Players.Single(player => player.PlayerId == alice.PlayerId);
        Assert.True(aliceView.Connected);
        Assert.Null(aliceView.DisconnectedUntilUnixMs);
        Assert.Equal(handBefore, aliceView.HandCards.Select(card => card.InstanceId));

        // The open decision comes again, unchanged, plus the log so far.
        var resync = Assert.IsType<ResyncDto>(outcome.Resync);
        var prompt = Assert.Single(resync.ChoicePrompts);
        Assert.Equal(promptBefore, prompt);
        Assert.Contains(resync.LogEvents, log => log.Type == "PLAYER_DISCONNECTED");

        // The match goes on.
        match.EndTurn(alice.PlayerId);
        Assert.Equal(match.Bob.PlayerId, match.View(alice.PlayerId).ActivePlayerId);
    }

    [Fact]
    public void AfterRejoin_OnlyTheNewConnectionActsForTheSeat()
    {
        var match = SessionMatch.StartInMain();
        match.Room.Disconnect("conn-a");

        match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2");

        // The hub resolves the player through the connection: the old one has no seat any more.
        Assert.Null(match.Room.ResolvePlayerId("conn-a"));
        Assert.False(match.Room.HasPlayer("conn-a"));
        Assert.Equal(match.Alice.PlayerId, match.Room.ResolvePlayerId("conn-a2"));
        Assert.True(match.Room.HasPlayer("conn-a2"));
        // The old connection dropping later changes nothing.
        Assert.Null(match.Room.Disconnect("conn-a"));
    }

    [Fact]
    public void Rejoin_TakesOverASeatWhoseOldConnectionIsNotNoticedAsGoneYet()
    {
        var match = SessionMatch.StartInMain();

        // Page reload: the new connection arrives before the server noticed that the old one is gone.
        var outcome = match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2");

        Assert.Equal(RejoinStatus.Ok, outcome.Status);
        Assert.Equal("conn-a", outcome.PreviousConnectionId);
        Assert.Null(match.Room.ResolvePlayerId("conn-a"));
    }

    [Fact]
    public void Rejoin_WithAWrongTokenOrAnotherPlayersSeat_IsRejected()
    {
        var match = SessionMatch.StartInMain();
        match.Room.Disconnect("conn-a");

        Assert.Equal(RejoinStatus.Invalid, match.Room.Rejoin(match.Alice.PlayerId, "wrong-token", "x").Status);
        Assert.Equal(RejoinStatus.Invalid, match.Room.Rejoin(match.Alice.PlayerId, match.Bob.RejoinToken, "x").Status);
        Assert.Equal(RejoinStatus.Invalid, match.Room.Rejoin("p9-unknown", match.Alice.RejoinToken, "x").Status);
        Assert.Equal(RejoinStatus.Invalid, match.Room.Rejoin(match.Alice.PlayerId, string.Empty, "x").Status);
        Assert.Null(match.Room.ResolvePlayerId("x"));
    }

    [Fact]
    public void GracePeriodRunsOut_ThePlayerLosesByDisconnect()
    {
        var match = SessionMatch.StartInMain(isQuickMatch: true);
        match.Room.Disconnect("conn-a");

        match.Clock.Advance(89);
        Assert.Null(match.Room.Tick());
        match.Clock.Advance(1);
        var update = match.Remember(match.Room.Tick());

        Assert.Equal(MatchPhase.GameOver, update.StateSnapshot.Phase);
        Assert.Equal(match.Bob.PlayerId, update.StateSnapshot.WinnerPlayerId);
        var result = Assert.IsType<MatchResultData>(update.MatchResult);
        Assert.Equal(MatchEndReasons.Disconnect, result.EndReason);
        Assert.Equal("Bob", result.Winner.DisplayName);
        Assert.Equal("Alice", result.Loser.DisplayName);
        Assert.True(result.IsRanked);
        Assert.Contains(update.LogEvents, log => log.Type == "DISCONNECT_TIMEOUT");
        Assert.Null(match.Room.Tick());

        // Too late: Alice may only look at the result.
        var outcome = match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2");
        Assert.Equal(RejoinStatus.Expired, outcome.Status);
        Assert.Equal(match.Bob.PlayerId, outcome.FinalState!.WinnerPlayerId);
        Assert.Null(match.Room.ResolvePlayerId("conn-a2"));
    }

    [Fact]
    public void RejoinAfterTheDeadline_BeforeTheBackgroundServiceRan_IsTooLate()
    {
        var match = SessionMatch.StartInMain();
        match.Room.Disconnect("conn-a");
        match.Clock.Advance(91);

        var outcome = match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2");

        Assert.Equal(RejoinStatus.Expired, outcome.Status);
        Assert.Equal(MatchEndReasons.Disconnect, outcome.Update!.MatchResult!.EndReason);
    }

    [Fact]
    public void ReconnectBudget_DisconnectsAddUp_ThenTheSeatIsLostRightAway()
    {
        var match = SessionMatch.StartInMain(new MatchRoomOptions
        {
            ReconnectGrace = TimeSpan.FromSeconds(90),
            ReconnectBudget = TimeSpan.FromSeconds(100)
        });
        var alice = match.Alice;

        match.Room.Disconnect("conn-a");
        match.Clock.Advance(60);
        match.Room.Rejoin(alice.PlayerId, alice.RejoinToken, "c2");

        // Only 40 seconds of the budget are left: the grace period is shorter.
        var second = match.Room.Disconnect("c2")!;
        Assert.Equal(
            match.Clock.UtcNow.AddSeconds(40).ToUnixTimeMilliseconds(),
            second.StateSnapshot.Players.Single(player => player.PlayerId == alice.PlayerId).DisconnectedUntilUnixMs);
        match.Clock.Advance(40);
        Assert.Equal(RejoinStatus.Expired, match.Room.Rejoin(alice.PlayerId, alice.RejoinToken, "c3").Status);
    }

    [Fact]
    public void ReconnectBudget_UsedUp_NextDisconnectLosesImmediately()
    {
        // Grace period longer than the budget: the budget decides.
        var options = new MatchRoomOptions { ReconnectGrace = TimeSpan.FromSeconds(90), ReconnectBudget = TimeSpan.FromSeconds(50) };

        // Reaching the deadline exactly is already too late.
        var match = SessionMatch.StartInMain(options);
        match.Room.Disconnect("conn-a");
        match.Clock.Advance(50);
        Assert.Equal(RejoinStatus.Expired, match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "c2").Status);

        // Two disconnects use up the budget (less than 1 s left): the third one loses at once.
        var other = SessionMatch.StartInMain(options);
        other.Room.Disconnect("conn-b");
        other.Clock.Advance(25);
        Assert.Equal(RejoinStatus.Ok, other.Room.Rejoin(other.Bob.PlayerId, other.Bob.RejoinToken, "c2").Status);
        other.Room.Disconnect("c2");
        other.Clock.Advance(24.5);
        Assert.Equal(RejoinStatus.Ok, other.Room.Rejoin(other.Bob.PlayerId, other.Bob.RejoinToken, "c3").Status);

        var update = other.Room.Disconnect("c3")!;

        Assert.Equal(MatchPhase.GameOver, update.StateSnapshot.Phase);
        Assert.Equal(MatchEndReasons.Disconnect, update.MatchResult!.EndReason);
        Assert.Equal(other.Alice.PlayerId, update.StateSnapshot.WinnerPlayerId);
    }

    [Fact]
    public void Lobby_DisconnectedPlayerKeepsTheSeatFor30Seconds()
    {
        var match = SessionMatch.CreateLobby();
        match.Room.Disconnect("conn-b");

        // The match cannot start without Bob.
        Assert.Throws<InvalidOperationException>(() => match.Room.StartMatch());
        match.Clock.Advance(29);
        Assert.Null(match.Room.Tick());
        Assert.Equal(2, match.Room.CreateSummary().PlayerCount);

        match.Clock.Advance(1);
        var update = match.Remember(match.Room.Tick());

        Assert.Single(update.StateSnapshot.Players);
        Assert.Null(update.MatchResult);
        Assert.Equal(RejoinStatus.Invalid, match.Room.Rejoin(match.Bob.PlayerId, match.Bob.RejoinToken, "c2").Status);
    }

    [Fact]
    public void Lobby_RejoinInTime_KeepsTheSeat()
    {
        var match = SessionMatch.CreateLobby();
        match.Room.Disconnect("conn-b");
        match.Clock.Advance(10);

        Assert.Equal(RejoinStatus.Ok, match.Room.Rejoin(match.Bob.PlayerId, match.Bob.RejoinToken, "c2").Status);
        match.Clock.Advance(60);

        Assert.Null(match.Room.Tick());
        Assert.NotNull(match.Room.StartMatch());
    }

    [Fact]
    public void AfterTheGame_ADisconnectFreesTheSeatAsBefore()
    {
        var match = SessionMatch.StartInMain();
        match.Room.Disconnect("conn-a");
        match.Clock.Advance(90);
        match.Room.Tick();

        var update = match.Room.Disconnect("conn-b");

        Assert.NotNull(update);
        Assert.False(match.Room.HasPlayer("conn-b"));
    }

    [Fact]
    public void Recording_KeepsConnectionEvents_AndTheReplayEndsTheSame()
    {
        var match = SessionMatch.StartInMain();
        match.Room.Disconnect("conn-a");
        match.Clock.Advance(20);
        match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2");
        match.EndTurn(match.Alice.PlayerId);
        match.Room.Disconnect("conn-b");
        match.Clock.Advance(90);
        var end = match.Remember(match.Room.Tick());

        var recording = Assert.IsType<MatchRecording>(end.Recording);
        Assert.Equal(
            [RecordedInputKinds.Disconnect, RecordedInputKinds.Reconnect, RecordedInputKinds.Choice, RecordedInputKinds.Disconnect, RecordedInputKinds.DisconnectTimeout],
            recording.Inputs.Skip(3).Select(input => input.Kind));
        Assert.Equal(2, recording.Inputs[^1].PlayerSlot);

        var replay = MatchRoomReplay.Start(recording);
        ReplayRunner.Run(recording, replay);

        Assert.Equal(MatchPhase.GameOver, replay.Room.CreateStateSnapshot().Phase);
        Assert.Equal(MatchRoomReplay.SeatId(1), replay.Room.CreateStateSnapshot().WinnerPlayerId);
        Assert.Equal(recording.FinalStateHash, replay.StateHash());
    }
}
