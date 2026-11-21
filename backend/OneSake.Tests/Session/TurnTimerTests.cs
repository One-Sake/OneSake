using OneSake.Domain;
using OneSake.Game.Replay;
using OneSake.Server.Match;

namespace OneSake.Tests.Session;

// Turn timer: 90 s per turn, then the time bank (180 s, +10 s per own turn, max 240 s),
// 25 s for answers outside the own turn, 2 s tolerance. The server decides when time runs out.
public class TurnTimerTests
{
    private static SessionMatch Start(TimerMode timer = TimerMode.Standard) => SessionMatch.StartInMain(timer: timer);

    private static TimerDto Timer(SessionMatch match) => Assert.IsType<TimerDto>(match.Room.CreateStateSnapshot().Timer);

    private static long Bank(SessionMatch match, string playerId) => Timer(match).TimeBankMs[playerId];

    [Fact]
    public void TurnTime_ThenTimeBank_ThenTheTurnEndsAutomatically()
    {
        var match = Start();
        var alice = match.Alice.PlayerId;
        var start = match.Clock.UtcNow;

        var timer = Timer(match);
        Assert.Equal("TURN", timer.Kind);
        Assert.Equal(alice, timer.ActivePlayerId);
        Assert.Equal(start.AddSeconds(90).ToUnixTimeMilliseconds(), timer.DeadlineUnixMs);
        Assert.True(timer.UsesTimeBank);
        Assert.False(timer.UsingTimeBank);
        Assert.Equal(180_000, timer.TimeBankMs[alice]);
        Assert.Equal("STANDARD", timer.Mode);

        // Normal time over: the time bank runs.
        match.Clock.Advance(100);
        Assert.Null(match.Room.Tick());
        Assert.True(Timer(match).UsingTimeBank);

        // 90 s + 180 s bank + 2 s tolerance.
        match.Clock.Advance(90 + 180 + 2 - 100 - 0.5);
        Assert.Null(match.Room.Tick());
        match.Clock.Advance(0.5);
        var update = match.Remember(match.Room.Tick());

        Assert.Equal(match.Bob.PlayerId, update.StateSnapshot.ActivePlayerId);
        Assert.Contains(update.LogEvents, log => log.Type == "TIMEOUT" && log.Text.Contains("Alice: automatisch Zug beendet"));
        // Bank used up, then +10 s for the finished turn.
        Assert.Equal(10_000, Bank(match, alice));
        Assert.Equal(match.Bob.PlayerId, Timer(match).ActivePlayerId);
        Assert.Equal(MatchPhase.Main, update.StateSnapshot.Phase);
    }

    [Fact]
    public void TwoOwnTurnsInARowWithoutTime_LoseByTimeout()
    {
        var match = SessionMatch.StartInMain(isQuickMatch: true, timer: TimerMode.Standard);
        match.Clock.Advance(90 + 180 + 2);
        match.Remember(match.Room.Tick());
        match.EndTurn(match.Bob.PlayerId);

        // Alice's next turn: 90 s + 10 s bank.
        match.Clock.Advance(90 + 10 + 2);
        var update = match.Remember(match.Room.Tick());

        Assert.Equal(MatchPhase.GameOver, update.StateSnapshot.Phase);
        Assert.Equal(match.Bob.PlayerId, update.StateSnapshot.WinnerPlayerId);
        Assert.Equal(MatchEndReasons.Timeout, update.StateSnapshot.EndReason);
        var result = Assert.IsType<MatchResultData>(update.MatchResult);
        Assert.Equal(MatchEndReasons.Timeout, result.EndReason);
        Assert.Equal("Alice", result.Loser.DisplayName);
        Assert.True(result.IsRanked);
        Assert.Null(update.StateSnapshot.Timer);
    }

    [Fact]
    public void EndingATurnInTime_StartsTheTimeoutStreakAgain()
    {
        var match = Start();
        match.Clock.Advance(90 + 180 + 2);
        match.Room.Tick();
        match.EndTurn(match.Bob.PlayerId);
        // Alice ends this turn herself.
        match.Clock.Advance(50);
        match.EndTurn(match.Alice.PlayerId);
        match.EndTurn(match.Bob.PlayerId);

        match.Clock.Advance(90 + 20 + 2);
        var update = match.Remember(match.Room.Tick());

        Assert.Equal(MatchPhase.Main, update.StateSnapshot.Phase);
        Assert.Equal(match.Bob.PlayerId, update.StateSnapshot.ActivePlayerId);
    }

    [Fact]
    public void CounterAnswerRunsOut_NoCounter_TheBattleGoesOn()
    {
        var match = Start();
        var bob = match.Bob.PlayerId;
        var alice = match.Alice.PlayerId;
        // Nobody attacks in the own first turn: Alice attacks in turn 3.
        match.EndTurn(alice);
        match.EndTurn(bob);
        var lifeBefore = match.Player(bob).LifeCount;

        match.Clock.Advance(5);
        match.Submit(match.Prompt("MAIN_ACTION", alice), "ATTACK", "s1:leader", "s2:leader");
        match.Prompt("COUNTER_STEP", bob);
        var timer = Timer(match);
        Assert.Equal("RESPONSE", timer.Kind);
        Assert.Equal(bob, timer.ActivePlayerId);
        Assert.Equal(match.Clock.UtcNow.AddSeconds(25).ToUnixTimeMilliseconds(), timer.DeadlineUnixMs);

        // 25 s + the whole bank (190 s after one own turn) + tolerance.
        match.Clock.Advance(25 + 190 + 2);
        var update = match.Remember(match.Room.Tick());

        Assert.Contains(update.LogEvents, log => log.Text.Contains("Bob: automatisch keine Reaktion"));
        Assert.DoesNotContain(update.LogEvents, log => log.Type == "COUNTER");
        Assert.Equal(lifeBefore - 1, match.Player(bob).LifeCount);
        Assert.Equal(0, Bank(match, bob));
        // Alice's turn time did not run while Bob had to decide: 85 s left.
        timer = Timer(match);
        Assert.Equal("TURN", timer.Kind);
        Assert.Equal(alice, timer.ActivePlayerId);
        Assert.Equal(match.Clock.UtcNow.AddSeconds(85).ToUnixTimeMilliseconds(), timer.DeadlineUnixMs);
        match.Prompt("MAIN_ACTION", alice);
    }

    [Fact]
    public void TurnOrderAndMulligan_RunOut_GoFirstAndKeep()
    {
        var match = SessionMatch.CreateLobby(timer: TimerMode.Standard);
        match.Remember(match.Room.StartMatch());
        var order = match.Prompt("TURN_ORDER_DECISION");
        Assert.Equal("TURN_ORDER", Timer(match).Kind);
        Assert.Equal(order.PlayerId, Timer(match).ActivePlayerId);
        Assert.False(Timer(match).UsesTimeBank);

        match.Clock.Advance(20 + 2);
        var afterOrder = match.Remember(match.Room.Tick());
        Assert.Equal(MatchPhase.Mulligan, afterOrder.StateSnapshot.Phase);
        Assert.Contains(afterOrder.LogEvents, log => log.Type == "FIRST_PLAYER" && log.Text.Contains("als Erster"));
        Assert.Equal(string.Empty, Timer(match).ActivePlayerId);
        Assert.Equal("MULLIGAN", Timer(match).Kind);

        // One player keeps in time, the other one does not decide.
        match.Submit(match.Prompt("MULLIGAN_DECISION", match.Alice.PlayerId), "KEEP");
        match.Clock.Advance(30 + 2);
        var afterMulligan = match.Remember(match.Room.Tick());

        Assert.Equal(MatchPhase.Main, afterMulligan.StateSnapshot.Phase);
        Assert.Equal(order.PlayerId, afterMulligan.StateSnapshot.ActivePlayerId);
        Assert.Contains(afterMulligan.LogEvents, log => log.Type == "MULLIGAN_KEEP" && log.Text.StartsWith("Bob"));
        Assert.Single(afterMulligan.LogEvents, log => log.Type == "TIMEOUT");
    }

    [Fact]
    public void TimeBank_GrowsBy10SecondsPerOwnTurn_UpToTheMaximum()
    {
        var match = Start();
        var alice = match.Alice.PlayerId;

        match.EndTurn(alice);
        Assert.Equal(190_000, Bank(match, alice));
        for (var turn = 0; turn < 7; turn += 1)
        {
            match.EndTurn(match.Bob.PlayerId);
            match.EndTurn(alice);
        }

        Assert.Equal(240_000, Bank(match, alice));
        Assert.Equal(240_000, Bank(match, match.Bob.PlayerId));
    }

    [Fact]
    public void Timer_IsPausedWhileAPlayerIsDisconnected_AndGoesOnAfterTheRejoin()
    {
        var match = Start();
        match.Clock.Advance(30);

        var disconnect = match.Remember(match.Room.Disconnect("conn-a"));
        Assert.Null(disconnect.StateSnapshot.Timer);
        match.Clock.Advance(80);
        Assert.Null(match.Room.Tick());

        match.Remember(match.Room.Rejoin(match.Alice.PlayerId, match.Alice.RejoinToken, "conn-a2").Update);
        var timer = Timer(match);
        Assert.Equal(match.Alice.PlayerId, timer.ActivePlayerId);
        Assert.Equal(match.Clock.UtcNow.AddSeconds(60).ToUnixTimeMilliseconds(), timer.DeadlineUnixMs);
        Assert.Equal(180_000, timer.TimeBankMs[match.Alice.PlayerId]);

        // A disconnect of the other player pauses the clock as well.
        match.Clock.Advance(10);
        match.Room.Disconnect("conn-b");
        match.Clock.Advance(85);
        Assert.Null(match.Room.Tick());
        match.Room.Rejoin(match.Bob.PlayerId, match.Bob.RejoinToken, "conn-b2");
        Assert.Equal(match.Clock.UtcNow.AddSeconds(50).ToUnixTimeMilliseconds(), Timer(match).DeadlineUnixMs);
    }

    [Fact]
    public void PrivateTableWithTimerOff_NeverTimesOut()
    {
        var match = Start(TimerMode.Off);

        match.Clock.Advance(TimeSpan.FromHours(3));

        Assert.Null(match.Room.Tick());
        Assert.Null(match.Room.CreateStateSnapshot().Timer);
        Assert.Equal("OFF", match.Room.CreateStateSnapshot().TimerMode);
        Assert.Equal("OFF", match.Room.CreateSummary().TimerMode);
        match.Prompt("MAIN_ACTION", match.Alice.PlayerId);
    }

    [Fact]
    public void RelaxedTimer_DoublesAllTimes()
    {
        var match = Start(TimerMode.Relaxed);

        var timer = Timer(match);

        Assert.Equal("RELAXED", timer.Mode);
        Assert.Equal(match.Clock.UtcNow.AddSeconds(180).ToUnixTimeMilliseconds(), timer.DeadlineUnixMs);
        Assert.Equal(360_000, timer.TimeBankMs[match.Alice.PlayerId]);
    }

    [Fact]
    public void QuickMatchRooms_AlwaysHaveTheStandardTimer()
    {
        var manager = new MatchRoomManager();
        var (room, _, _, _) = manager.CreateQuickMatchRoom(new QueuePair(new QueueEntry("c1", "A", null), new QueueEntry("c2", "B", null)));

        Assert.Equal(TimerMode.Standard, room.TimerMode);
        Assert.Equal(TimerMode.Relaxed, manager.Create("RELAX", TimerMode.Relaxed).TimerMode);
        Assert.Equal("STANDARD", manager.Create("STD").CreateSummary().TimerMode);
    }

    [Fact]
    public void Replay_WithTimeouts_EndsInTheSameState()
    {
        // Alice runs out of time (turn 1), Bob ends turn 2, Alice attacks in turn 3 and Bob does not
        // answer the counter step in time, then Alice runs out of time again: defeat TIMEOUT.
        var match = SessionMatch.StartInMain(isQuickMatch: true, timer: TimerMode.Standard);
        match.Clock.Advance(90 + 180 + 2);
        match.Room.Tick();
        match.EndTurn(match.Bob.PlayerId);
        match.Clock.Advance(3);
        match.Submit(match.Prompt("MAIN_ACTION", match.Alice.PlayerId), "ATTACK", "s1:leader", "s2:leader");
        match.Clock.Advance(25 + 190 + 2);
        match.Room.Tick();
        match.Clock.Advance(87 + 10 + 2);
        var end = match.Remember(match.Room.Tick());

        var recording = Assert.IsType<MatchRecording>(end.Recording);
        Assert.Equal(3, recording.Inputs.Count(input => input.Kind == RecordedInputKinds.Timeout));

        var replay = MatchRoomReplay.Start(recording);
        ReplayRunner.Run(recording, replay);

        var state = replay.Room.CreateStateSnapshot();
        Assert.Equal(MatchPhase.GameOver, state.Phase);
        Assert.Equal(MatchEndReasons.Timeout, state.EndReason);
        Assert.Equal(MatchRoomReplay.SeatId(2), state.WinnerPlayerId);
        Assert.Equal(recording.FinalStateHash, replay.StateHash());
    }
}
