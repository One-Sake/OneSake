using Microsoft.EntityFrameworkCore;
using OneSake.Game.Replay;
using OneSake.Persistence;
using OneSake.Server.Match;

// Replay tool (see docs/bug-reports.md).
//
//   dotnet run --project backend/OneSake.ReplayTool -- export --report 123 --connection "<conn>" [--out <folder>]
//     -> writes backend/OneSake.Tests/Replays/bug-123.json (names and guest IDs are removed)
//   dotnet run --project backend/OneSake.ReplayTool -- run <file> [--until 47] [--print]
//     -> plays the recording, prints every input, the state and (with --print) the match log

if (args.Length == 0)
{
    return Usage();
}

try
{
    return args[0] switch
    {
        "export" => await ExportAsync(args[1..]),
        "run" => Run(args[1..]),
        _ => Usage()
    };
}
catch (ReplayDivergenceException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static int Usage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  export --report <id> --connection \"<connection string>\" [--out <folder>]");
    Console.WriteLine("  run <file> [--until <seq>] [--print]");
    return 1;
}

static async Task<int> ExportAsync(string[] args)
{
    var reportId = int.Parse(Option(args, "--report") ?? throw new ArgumentException("--report is missing."));
    var connection = Option(args, "--connection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new ArgumentException("--connection is missing.");
    var folder = Option(args, "--out") ?? FindReplaysFolder();

    var options = new DbContextOptionsBuilder<OneSakeDbContext>().UseNpgsql(connection).Options;
    await using var dbContext = new OneSakeDbContext(options);
    var report = await dbContext.BugReports.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == reportId)
        ?? throw new ArgumentException($"Bug report {reportId} does not exist.");
    var data = await dbContext.MatchRecordings.AsNoTracking()
        .Where(entry => entry.Id == report.RecordingId)
        .Select(entry => entry.Data)
        .FirstOrDefaultAsync()
        ?? throw new ArgumentException($"Bug report {reportId} has no recording.");

    var recording = MatchRecordingJson.Decompress(data);
    // The file goes into the repository: no player names or guest IDs.
    recording = recording with
    {
        Players = recording.Players.Select(player => player with { DisplayName = $"Player {player.Seat}", GuestId = null }).ToArray()
    };

    var file = new ReplayFile
    {
        Report = new BugReportInfo
        {
            Id = report.Id,
            Category = report.Category,
            Description = report.Description,
            CardId = report.CardId,
            AtSeq = report.AtSeq,
            CreatedAtUtc = report.CreatedAtUtc
        },
        Recording = recording
    };

    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, $"bug-{report.Id}.json");
    await File.WriteAllTextAsync(path, file.ToJson());
    Console.WriteLine($"Bug report #{report.Id} ({report.Category}, seq {report.AtSeq}, {recording.Inputs.Count} inputs) -> {path}");
    return 0;
}

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        return Usage();
    }

    var file = ReplayFile.Load(args[0]);
    var until = Option(args, "--until") is { } value ? int.Parse(value) : (int?)null;
    var print = args.Contains("--print");
    var recording = file.Recording;

    if (file.Report is { } report)
    {
        Console.WriteLine($"Bug report #{report.Id} [{report.Category}] at seq {report.AtSeq}{(report.CardId is null ? string.Empty : $", card {report.CardId}")}");
        Console.WriteLine($"  \"{report.Description}\"");
    }

    Console.WriteLine($"Recording: {recording.Inputs.Count} inputs, seed {recording.Seed}, engine {recording.EngineVersion}");
    if (recording.EngineVersion != MatchRecordingJson.CurrentEngineVersion)
    {
        Console.WriteLine($"  Note: recorded with engine {recording.EngineVersion}, now {MatchRecordingJson.CurrentEngineVersion}.");
    }

    var replay = MatchRoomReplay.Start(recording);
    ReplayRunner.Run(recording, replay, until, step =>
        Console.WriteLine($"  seq {step.Input.Seq,4}  seat {step.Input.PlayerSlot}  {step.Input.Kind,-6}  {Summary(step.Input)}  ok"));

    Console.WriteLine();
    Console.WriteLine($"State after seq {until?.ToString() ?? recording.Inputs.Count.ToString()}:");
    Console.WriteLine(replay.Describe());

    if (print)
    {
        Console.WriteLine("Log:");
        foreach (var entry in replay.Log)
        {
            Console.WriteLine($"  T{entry.TurnNumber} {entry.Phase,-8} {entry.Type,-14} {entry.Text}");
        }
    }

    return 0;
}

static string Summary(RecordedInput input)
{
    var payload = input.Payload;
    string Read(string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    return input.Kind switch
    {
        RecordedInputKinds.Choice => $"{Read("selectedOption")} {Read("selectedCardInstanceId")} {Read("targetCardInstanceId")}".Trim(),
        RecordedInputKinds.Manual => $"{Read("kind")} {Read("cardInstanceId")} {Read("toZone")}".Trim(),
        _ => Read("endReason")
    };
}

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

// Walks up from the current folder to backend/OneSake.Tests/Replays.
static string FindReplaysFolder()
{
    for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
    {
        foreach (var candidate in new[] { Path.Combine(folder.FullName, "backend", "OneSake.Tests"), Path.Combine(folder.FullName, "OneSake.Tests") })
        {
            if (Directory.Exists(candidate))
            {
                return Path.Combine(candidate, "Replays");
            }
        }
    }

    throw new DirectoryNotFoundException("OneSake.Tests not found. Use --out <folder>.");
}
