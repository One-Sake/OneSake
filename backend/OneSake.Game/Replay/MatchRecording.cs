using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneSake.Domain;

namespace OneSake.Game.Replay;

// Everything needed to play a match again: seed, decks and every decision of the players.
// Stored after the match (JSON, gzip). The seed is secret while the match is running.
public sealed record MatchRecording
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    // Version of OneSake.Game that recorded the match (a replay on another version can differ).
    public string EngineVersion { get; init; } = MatchRecordingJson.CurrentEngineVersion;
    // ulong as text: JSON numbers above 2^53 lose precision in JavaScript and some tools.
    public string Seed { get; init; } = "0";
    public string RoomCode { get; init; } = string.Empty;
    public bool IsQuickMatch { get; init; }
    public long StartedAtUnixMs { get; init; }
    public IReadOnlyList<RecordedPlayer> Players { get; init; } = [];
    // The dice that were rolled for the turn order (needed when a test used fixed dice).
    public IReadOnlyList<int> DiceRolls { get; init; } = [];
    public IReadOnlyList<RecordedInput> Inputs { get; init; } = [];
    public bool Finished { get; init; }
    public string? FinalStateHash { get; init; }
}

public sealed record RecordedPlayer
{
    // 1 = first player that joined the room, 2 = second.
    public int Seat { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public PlayerDeckSubmissionDto Deck { get; init; } = new();
}

// One decision of a player. Payload is the ChoiceSubmissionDto / ManualActionDto (without the
// connection ID), StateHash the fingerprint of the state right after this input.
public sealed record RecordedInput
{
    public int Seq { get; init; }
    public int PlayerSlot { get; init; }
    public string Kind { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
    public long TsUnixMs { get; init; }
    public string? StateHash { get; init; }
}

public static class RecordedInputKinds
{
    public const string Choice = "CHOICE";
    public const string Manual = "MANUAL";
    // The player left the running match (concede or disconnect). Payload: { "endReason": ... }.
    public const string Leave = "LEAVE";
    // Meta events: the connection of the player dropped / came back. They change nothing in the game.
    public const string Disconnect = "DISCONNECT";
    public const string Reconnect = "RECONNECT";
    // The reconnect deadline ran out: the player loses (end reason DISCONNECT).
    public const string DisconnectTimeout = "DISCONNECT_TIMEOUT";
}

public static class MatchRecordingJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string CurrentEngineVersion { get; } =
        typeof(MatchRecording).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MatchRecording).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static string Serialize(MatchRecording recording) => JsonSerializer.Serialize(recording, Options);

    public static MatchRecording Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MatchRecording>(json, Options)
            ?? throw new JsonException("The recording is empty.");
    }

    public static JsonElement ToPayload<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    public static T FromPayload<T>(JsonElement payload)
    {
        return payload.Deserialize<T>(Options) ?? throw new JsonException($"The payload is not a {typeof(T).Name}.");
    }

    public static byte[] Compress(MatchRecording recording)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            JsonSerializer.Serialize(gzip, recording, Options);
        }

        return output.ToArray();
    }

    public static MatchRecording Decompress(byte[] data)
    {
        using var input = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<MatchRecording>(input, Options)
            ?? throw new JsonException("The recording is empty.");
    }
}

// Collects the inputs of one running match.
public sealed class MatchRecorder
{
    private readonly List<RecordedInput> _inputs = [];
    private readonly List<int> _diceRolls = [];
    private readonly MatchRecording _start;
    private MatchRecording? _finished;

    public MatchRecorder(ulong seed, string roomCode, bool isQuickMatch, IReadOnlyList<RecordedPlayer> players, long startedAtUnixMs)
    {
        _start = new MatchRecording
        {
            Seed = seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RoomCode = roomCode,
            IsQuickMatch = isQuickMatch,
            Players = players,
            StartedAtUnixMs = startedAtUnixMs
        };
    }

    public bool IsFinished => _finished is not null;

    // Number of recorded inputs = the "seq" of the last input.
    public int InputCount => _inputs.Count;

    public void AddDie(int value) => _diceRolls.Add(value);

    public void Add(int playerSlot, string kind, JsonElement payload, string? stateHash, long tsUnixMs)
    {
        if (IsFinished)
        {
            return;
        }

        _inputs.Add(new RecordedInput
        {
            Seq = _inputs.Count + 1,
            PlayerSlot = playerSlot,
            Kind = kind,
            Payload = payload,
            StateHash = stateHash,
            TsUnixMs = tsUnixMs
        });
    }

    public MatchRecording Finish(string finalStateHash)
    {
        _finished ??= Snapshot() with { Finished = true, FinalStateHash = finalStateHash };
        return _finished;
    }

    // The recording so far (also used for bug reports while the match is running).
    public MatchRecording Snapshot()
    {
        return _finished ?? _start with { DiceRolls = _diceRolls.ToArray(), Inputs = _inputs.ToArray() };
    }
}
