using System.Text.Json;
using OneSake.Domain;
using OneSake.Game.Replay;
using OneSake.Server.Match;
using Xunit.Abstractions;

namespace OneSake.Tests.Replays;

// Plays every bug report in OneSake.Tests/Replays (bug-123.json, exported with the ReplayTool).
// The match must still be playable to the end. An optional bug-123.expect.json (written after the
// fix) checks values at one seq. See docs/bug-reports.md.
public class ReplayRegressionTests
{
    private readonly ITestOutputHelper _output;

    public ReplayRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Replays");

    [Fact]
    public void EveryReplayFile_PlaysAndMeetsItsExpectations()
    {
        var files = Directory.Exists(Folder)
            ? Directory.GetFiles(Folder, "*.json").Where(path => !path.EndsWith(".expect.json", StringComparison.Ordinal)).Order().ToList()
            : [];
        _output.WriteLine($"{files.Count} replay file(s) in {Folder}");

        var failures = new List<string>();
        foreach (var path in files)
        {
            try
            {
                Check(path);
                _output.WriteLine($"ok   {Path.GetFileName(path)}");
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static void Check(string path)
    {
        var file = ReplayFile.Load(path);
        var expectPath = Path.ChangeExtension(path, ".expect.json");
        var expectation = File.Exists(expectPath) ? ReplayExpectation.Load(expectPath) : null;

        var replay = MatchRoomReplay.Start(file.Recording);
        if (expectation is not null)
        {
            ReplayRunner.Run(file.Recording, replay, expectation.Seq);
            expectation.Verify(replay);
        }

        // The rest of the match must still be playable (no divergence after the reported point).
        ReplayRunner.Run(file.Recording, MatchRoomReplay.Start(file.Recording));
    }
}

// bug-123.expect.json:
// { "seq": 12, "checks": [ { "seat": 1, "field": "life", "expected": 4 },
//                          { "instanceId": "s1:OP01-016:2", "field": "zone", "expected": "character" },
//                          { "instanceId": "s1:leader", "field": "power", "expected": 6000 } ] }
public sealed record ReplayExpectation
{
    public int Seq { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ReplayCheck> Checks { get; init; } = [];

    public static ReplayExpectation Load(string path)
    {
        return JsonSerializer.Deserialize<ReplayExpectation>(File.ReadAllText(path), MatchRecordingJson.Options)
            ?? throw new JsonException($"{path} is empty.");
    }

    public void Verify(MatchRoomReplay replay)
    {
        var errors = Checks
            .Select(check => (Check: check, Actual: check.Read(replay)))
            .Where(entry => !string.Equals(entry.Actual, entry.Check.Expected.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.Check}: expected {entry.Check.Expected}, actual {entry.Actual}")
            .ToList();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"at seq {Seq}: {string.Join("; ", errors)}{Environment.NewLine}{replay.Describe()}");
        }
    }
}

public sealed record ReplayCheck
{
    public int? Seat { get; init; }
    public string? InstanceId { get; init; }
    // Seat: life, hand, deck, trash, activeDon, restedDon. Card: zone, power, rested, attachedDon.
    // Match: phase, turn, winnerSeat.
    public string Field { get; init; } = string.Empty;
    public JsonElement Expected { get; init; }

    public string Read(MatchRoomReplay replay)
    {
        var state = replay.Room.CreateStateSnapshot();
        switch (Field.ToLowerInvariant())
        {
            case "phase":
                return state.Phase.ToString();
            case "turn":
                return state.TurnNumber.ToString();
            case "winnerseat":
                return state.WinnerPlayerId.Length == 0 ? "0" : state.WinnerPlayerId.Replace("seat", string.Empty);
        }

        if (InstanceId is null)
        {
            var player = replay.Player(Seat ?? 1);
            return Field.ToLowerInvariant() switch
            {
                "life" => player.LifeCount.ToString(),
                "hand" => player.HandCount.ToString(),
                "deck" => player.DeckCount.ToString(),
                "trash" => player.TrashCount.ToString(),
                "activedon" => player.ActiveDonCount.ToString(),
                "resteddon" => player.RestedDonCount.ToString(),
                _ => throw new InvalidOperationException($"Unknown field '{Field}'.")
            };
        }

        var (card, zone) = FindCard(replay);
        return Field.ToLowerInvariant() switch
        {
            "zone" => zone,
            "power" => card?.Power.ToString() ?? "-",
            "rested" => card?.IsRested.ToString() ?? "-",
            "attacheddon" => card?.AttachedDon.ToString() ?? "-",
            _ => throw new InvalidOperationException($"Unknown field '{Field}'.")
        };
    }

    // Open zones and the own hand. Deck and Life are hidden: "unknown".
    private (CardInstanceDto? Card, string Zone) FindCard(MatchRoomReplay replay)
    {
        foreach (var seat in new[] { 1, 2 })
        {
            var player = replay.Player(seat);
            var zones = new (string Zone, IEnumerable<CardInstanceDto> Cards)[]
            {
                ("leader", player.Leader is null ? [] : [player.Leader]),
                ("stage", player.Stage is null ? [] : [player.Stage]),
                ("character", player.BoardCards),
                ("hand", player.HandCards),
                ("trash", player.TrashCards)
            };

            foreach (var (zone, cards) in zones)
            {
                var card = cards.FirstOrDefault(entry => entry.InstanceId == InstanceId);
                if (card is not null)
                {
                    return (card, zone);
                }
            }
        }

        return (null, "unknown");
    }

    public override string ToString() => InstanceId is null ? $"{Field} (seat {Seat ?? 1})" : $"{Field} of {InstanceId}";
}
