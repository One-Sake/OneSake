using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using OneSake.Domain;
using OneSake.Game.Randomness;
using OneSake.Game.Replay;
using OneSake.Game.Simulation;
using OneSake.Server.Simulation;

// Match simulator (see docs/simulation-findings.md).
//
//   dotnet run --project backend/OneSake.Simulator -c Release -- --games 5000 --bots random,greedy --decks meta,random --seed 1 --parallel 4 --out sim-failures
//
// Options: --cards <optcgapi json> (default: ONESAKE_CARDS_JSON or the checked-in fixture),
// --deck-folder <folder> (default: backend/OneSake.Tests/Decks), --determinism-every <n> (default 50).
// Exit code 1 when at least one match broke an invariant.

var games = int.Parse(Option("--games") ?? "200");
var seed = ulong.Parse(Option("--seed") ?? "1");
var parallel = int.Parse(Option("--parallel") ?? Environment.ProcessorCount.ToString());
var output = Option("--out") ?? "sim-failures";
var botNames = (Option("--bots") ?? "random,greedy").Split(',', StringSplitOptions.RemoveEmptyEntries);
var deckSources = (Option("--decks") ?? "meta,random").Split(',', StringSplitOptions.RemoveEmptyEntries);
var determinismEvery = int.Parse(Option("--determinism-every") ?? "50");
var deckFolder = Option("--deck-folder") ?? FindDeckFolder();
var cardsPath = Option("--cards") ?? Environment.GetEnvironmentVariable("ONESAKE_CARDS_JSON") ?? Path.Combine(deckFolder, "cards-fixture.json");

var pool = CardPool.Load(cardsPath);
var metaDecks = DeckList.LoadFolder(deckFolder, pool);
Console.WriteLine($"{games} games, seed {seed}, bots {string.Join('/', botNames)}, decks {string.Join('/', deckSources)} ({metaDecks.Count} meta decks, {pool.Cards.Count} cards from {Path.GetFileName(cardsPath)}), {parallel} threads");

var results = new SimulationResult[games];
var stopwatch = Stopwatch.StartNew();
var done = 0;
Parallel.For(0, games, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, index =>
{
    // Everything about game N follows from the base seed and N.
    var gameSeed = unchecked(seed * 1_000_003UL + (ulong)index);
    var random = Pcg32.FromSeed(gameSeed, "setup");
    var source = deckSources[index % deckSources.Length];
    var deck1 = PickDeck(source, random);
    var deck2 = PickDeck(source, random);
    var bot1 = botNames[random.Next(botNames.Length)];
    var bot2 = botNames[random.Next(botNames.Length)];

    results[index] = MatchSimulator.Play(gameSeed, deck1, deck2, Bot(bot1), Bot(bot2), new SimulationOptions
    {
        CheckDeterminism = determinismEvery > 0 && index % determinismEvery == 0
    });

    var count = Interlocked.Increment(ref done);
    if (count % 500 == 0)
    {
        Console.WriteLine($"  {count}/{games} ...");
    }
});
stopwatch.Stop();

// ---------- Report ----------
var failed = results.Where(result => result.Failed).ToList();
Console.WriteLine();
Console.WriteLine($"Games: {games} in {stopwatch.Elapsed.TotalSeconds:0.0} s ({games / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds):0.0} games/s)");
Console.WriteLine($"Finished: {results.Count(result => result.Finished)}, failed: {failed.Count}");
Console.WriteLine($"Average length: {results.Average(result => result.Turns):0.0} turns, {results.Average(result => result.Steps):0} decisions");
Console.WriteLine($"Rejected bot decisions: {results.Sum(result => result.Rejections)}");
Console.WriteLine($"Games with \"manual\" cards: {100.0 * results.Count(result => result.ManualCardIds.Count > 0) / games:0.0} %");
Console.WriteLine($"Winner seat 1 / 2: {results.Count(result => result.WinnerSeat == "1")} / {results.Count(result => result.WinnerSeat == "2")}");

Console.WriteLine();
Console.WriteLine("Failures per invariant:");
foreach (var group in failed.SelectMany(result => result.Violations.Select(violation => violation.Invariant).Distinct()).GroupBy(name => name).OrderByDescending(group => group.Count()))
{
    Console.WriteLine($"  {group.Key,-22} {group.Count()}");
}

if (failed.Count > 0)
{
    // Cards that are in failed games much more often than in all games.
    var all = results.SelectMany(result => result.CardIds).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
    Console.WriteLine();
    Console.WriteLine("Top cards in failed games (failed games with the card / all games with the card):");
    foreach (var entry in failed.SelectMany(result => result.CardIds).GroupBy(id => id)
        .Select(group => (Card: group.Key, Failed: group.Count(), All: all[group.Key]))
        .OrderByDescending(entry => (double)entry.Failed / entry.All).ThenByDescending(entry => entry.Failed).Take(10))
    {
        Console.WriteLine($"  {entry.Card,-10} {entry.Failed,5} / {entry.All,-5} ({100.0 * entry.Failed / entry.All:0} %)");
    }

    Directory.CreateDirectory(output);
    var date = DateTime.UtcNow.ToString("yyyyMMdd");
    foreach (var result in failed)
    {
        var file = new
        {
            simulation = new
            {
                result.Seed,
                result.Bots,
                result.Decks,
                Invariants = result.Violations.Select(violation => violation.ToString()),
                result.FailedAtSeq,
                result.Turns,
                result.LastLog
            },
            recording = result.Recording
        };
        File.WriteAllText(Path.Combine(output, $"{date}-{result.Seed}.json"), JsonSerializer.Serialize(file, MatchRecordingJson.Options));
    }

    Console.WriteLine();
    Console.WriteLine($"{failed.Count} recording(s) in {Path.GetFullPath(output)}");
    foreach (var result in failed.Take(10))
    {
        Console.WriteLine($"  seed {result.Seed}: {result.Violations[0]}");
    }
}

return failed.Count == 0 ? 0 : 1;

PlayerDeckSubmissionDto PickDeck(string source, IRandom random)
{
    return source == "random" ? RandomDeckBuilder.Build(pool, random) : metaDecks[random.Next(metaDecks.Count)];
}

static Func<IRandom, IBotPlayer> Bot(string name)
{
    return name switch
    {
        "random" => random => new RandomBot(random),
        "greedy" => random => new GreedyBot(random),
        _ => throw new ArgumentException($"Unknown bot '{name}' (random, greedy).")
    };
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindDeckFolder()
{
    for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
    {
        foreach (var candidate in new[] { Path.Combine(folder.FullName, "backend", "OneSake.Tests", "Decks"), Path.Combine(folder.FullName, "OneSake.Tests", "Decks") })
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    throw new DirectoryNotFoundException("OneSake.Tests/Decks not found. Use --deck-folder <folder>.");
}
