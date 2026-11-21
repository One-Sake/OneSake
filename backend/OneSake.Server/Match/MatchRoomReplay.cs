using System.Globalization;
using System.Text;
using OneSake.Domain;
using OneSake.Game.Replay;

namespace OneSake.Server.Match;

// Plays a recording in a new MatchRoom (same seed, same dice). Seat 1 is "seat1", seat 2 "seat2".
public sealed class MatchRoomReplay : IReplayTarget
{
    private readonly List<LogEventDto> _log = [];

    private MatchRoomReplay(MatchRoom room)
    {
        Room = room;
    }

    public MatchRoom Room { get; }
    public IReadOnlyList<LogEventDto> Log => _log;
    public MatchUpdate? LastUpdate { get; private set; }

    public static string SeatId(int seat) => $"seat{seat}";

    public static MatchRoomReplay Start(MatchRecording recording)
    {
        var seed = ulong.Parse(recording.Seed, CultureInfo.InvariantCulture);
        var room = new MatchRoom(recording.RoomCode, recording.IsQuickMatch, new FixedDiceRoller(recording.DiceRolls), seed);
        var replay = new MatchRoomReplay(room);

        foreach (var player in recording.Players.OrderBy(entry => entry.Seat))
        {
            replay.Remember(room.JoinPlayer(SeatId(player.Seat), player.DisplayName, player.GuestId));
            replay.Remember(room.SetPlayerDeck(SeatId(player.Seat), player.Deck));
        }

        replay.Remember(room.StartMatch());
        return replay;
    }

    public void Apply(RecordedInput input)
    {
        var playerId = SeatId(input.PlayerSlot);
        var update = input.Kind switch
        {
            RecordedInputKinds.Choice => Room.SubmitChoice(
                MatchRecordingJson.FromPayload<ChoiceSubmissionDto>(input.Payload) with { PlayerId = playerId }),
            RecordedInputKinds.Manual => Room.ManualAction(playerId, MatchRecordingJson.FromPayload<ManualActionDto>(input.Payload)),
            RecordedInputKinds.Leave => Room.RemovePlayer(
                playerId,
                input.Payload.TryGetProperty("endReason", out var reason) ? reason.GetString() ?? MatchEndReasons.Disconnect : MatchEndReasons.Disconnect),
            // Connection events change nothing in the game.
            RecordedInputKinds.Disconnect or RecordedInputKinds.Reconnect => null,
            RecordedInputKinds.DisconnectTimeout => Room.ApplyDisconnectTimeout(playerId),
            _ => throw new InvalidOperationException($"Unknown input kind '{input.Kind}'.")
        };

        Remember(update);
    }

    public string StateHash() => Room.ComputeStateHash();

    // Full view of one seat (own hand, open prompts are in LastUpdate).
    public PlayerStateDto Player(int seat)
    {
        return Room.CreateStateSnapshot(SeatId(seat)).Players.Single(player => player.PlayerId == SeatId(seat));
    }

    // Short text overview of the current state (ReplayTool --print, test failures).
    public string Describe()
    {
        var state = Room.CreateStateSnapshot();
        var text = new StringBuilder();
        text.AppendLine($"Turn {state.TurnNumber}, phase {state.Phase}, active {state.ActivePlayerId}, winner {(state.WinnerPlayerId.Length == 0 ? "-" : state.WinnerPlayerId)}");
        foreach (var seat in new[] { 1, 2 })
        {
            var player = Player(seat);
            text.AppendLine($"  {player.PlayerId} ({player.DisplayName}): life {player.LifeCount}, hand {player.HandCount}, deck {player.DeckCount}, trash {player.TrashCount}, DON!! {player.ActiveDonCount} active / {player.RestedDonCount} rested");
            text.AppendLine($"    leader {Card(player.Leader)}");
            if (player.Stage is not null)
            {
                text.AppendLine($"    stage {Card(player.Stage)}");
            }

            foreach (var card in player.BoardCards)
            {
                text.AppendLine($"    char  {Card(card)}");
            }

            text.AppendLine($"    hand  {string.Join(", ", player.HandCards.Select(card => $"{card.Name} [{card.InstanceId}]"))}");
        }

        return text.ToString();
    }

    private static string Card(CardInstanceDto? card)
    {
        return card is null
            ? "-"
            : $"{card.Name} [{card.InstanceId}] {card.Power} power{(card.IsRested ? ", rested" : string.Empty)}{(card.AttachedDon > 0 ? $", {card.AttachedDon} DON!!" : string.Empty)}";
    }

    private void Remember(MatchUpdate? update)
    {
        if (update is null)
        {
            return;
        }

        LastUpdate = update;
        _log.AddRange(update.LogEvents);
    }
}
