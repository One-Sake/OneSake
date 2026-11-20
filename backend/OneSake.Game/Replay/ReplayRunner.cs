using System.Text.Json;

namespace OneSake.Game.Replay;

// The match a recording is played on (MatchRoomReplay in OneSake.Server, where the match flow lives).
public interface IReplayTarget
{
    void Apply(RecordedInput input);

    string StateHash();
}

public sealed record ReplayStep(RecordedInput Input, string StateHash);

// The engine does not do the same as when the match was recorded.
public sealed class ReplayDivergenceException : Exception
{
    public ReplayDivergenceException(int seq, string message, Exception? inner = null)
        : base($"Engine weicht ab bei seq {seq}: {message}", inner)
    {
        Seq = seq;
    }

    public int Seq { get; }
}

// Plays a recording input by input and checks after every input that the state is the same
// as when the match was recorded.
public static class ReplayRunner
{
    public static IReadOnlyList<ReplayStep> Run(
        MatchRecording recording,
        IReplayTarget target,
        int? untilSeq = null,
        Action<ReplayStep>? onStep = null)
    {
        if (recording.FormatVersion > MatchRecording.CurrentFormatVersion)
        {
            throw new NotSupportedException($"Recording format {recording.FormatVersion} is newer than this engine ({MatchRecording.CurrentFormatVersion}).");
        }

        var steps = new List<ReplayStep>();
        foreach (var input in recording.Inputs.OrderBy(entry => entry.Seq))
        {
            if (untilSeq is not null && input.Seq > untilSeq)
            {
                break;
            }

            try
            {
                target.Apply(input);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentException)
            {
                throw new ReplayDivergenceException(
                    input.Seq,
                    $"die Eingabe ({input.Kind}, Spieler {input.PlayerSlot}) ist nicht mehr gültig - {exception.Message}",
                    exception);
            }

            var hash = target.StateHash();
            if (input.StateHash is not null && input.StateHash != hash)
            {
                throw new ReplayDivergenceException(input.Seq, $"der Zustand nach der Eingabe ({input.Kind}) ist anders als bei der Aufnahme.");
            }

            var step = new ReplayStep(input, hash);
            steps.Add(step);
            onStep?.Invoke(step);
        }

        var playedAll = untilSeq is null || untilSeq >= recording.Inputs.Count;
        if (playedAll && recording.FinalStateHash is not null && recording.FinalStateHash != target.StateHash())
        {
            throw new ReplayDivergenceException(recording.Inputs.Count, "der Endzustand ist anders als bei der Aufnahme.");
        }

        return steps;
    }
}

// A replay file in OneSake.Tests/Replays: the recording plus the bug report it came from.
public sealed record ReplayFile
{
    public BugReportInfo? Report { get; init; }
    public MatchRecording Recording { get; init; } = new();

    // Reads a replay file, or a plain recording.
    public static ReplayFile Load(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("recording", out _)
            ? JsonSerializer.Deserialize<ReplayFile>(json, MatchRecordingJson.Options)!
            : new ReplayFile { Recording = MatchRecordingJson.Deserialize(json) };
    }

    public string ToJson() => JsonSerializer.Serialize(this, MatchRecordingJson.Options);
}

public sealed record BugReportInfo
{
    public int Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? CardId { get; init; }
    public int AtSeq { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
