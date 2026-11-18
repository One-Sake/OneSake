namespace OneSake.Server.Services.Meta;

// Settings from appsettings ("Meta" section).
// The use of the Card Kaizoku data is not officially documented: keep Enabled = false in
// production until there is written permission (see docs/deployment.md).
public sealed class MetaOptions
{
    public const string SectionName = "Meta";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "CardKaizoku";
    public string BaseUrl { get; set; } = "https://cdn.cardkaizoku.com/stats/";
    // Part of the file name, e.g. "eb05_lw_p_top20" -> stats_eb05_lw_p_top20_20261118.json
    public string Format { get; set; } = "eb05_lw_p_top20";
    public string AttributionName { get; set; } = "Card Kaizoku";
    public string AttributionUrl { get; set; } = "https://cardkaizoku.com/";
    // How many days to look back for the newest snapshot.
    public int MaxDaysBack { get; set; } = 8;
    public int CacheHours { get; set; } = 6;

    public IReadOnlyList<string> Formats => [Format];
}

// Provider-independent model. Every number comes from the external provider only;
// OneSake's own match results are never mixed in. Rates are fractions from 0 to 1
// (null when there are no games).
public sealed record MetaMatchup(
    string OpponentKey,
    string OpponentName,
    string? OpponentCardId,
    double? FirstWinRate,
    double? SecondWinRate,
    int FirstGames,
    int SecondGames,
    int Games,
    double? WinRate);

public sealed record MetaLeader(
    string LeaderKey,
    string LeaderName,
    string? LeaderCardId,
    int Games,
    double? WinRate,
    double? FirstWinRate,
    double? SecondWinRate,
    IReadOnlyList<MetaMatchup> Matchups);

public sealed record MetaSnapshot(
    string Provider,
    string SourceUrl,
    DateOnly SnapshotDate,
    string Format,
    DateTimeOffset FetchedAtUtc,
    IReadOnlyList<MetaLeader> Leaders);

public interface IMetaProvider
{
    // Newest valid snapshot, or null when none was found or the data is invalid.
    Task<MetaSnapshot?> GetLatestAsync(string format, CancellationToken cancellationToken);
}

public sealed class MetaFormatException(string message) : Exception(message);
