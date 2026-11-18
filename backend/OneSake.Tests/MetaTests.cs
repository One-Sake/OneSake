using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneSake.Server.Services.Meta;

namespace OneSake.Tests;

public class MetaTests
{
    private static string SampleJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "cardkaizoku_sample.json"));

    #region PARSER

    [Fact]
    public void Parse_ReadsLeadersAndMatchupsInTheRealFormat()
    {
        var leaders = CardKaizokuParser.Parse(SampleJson);

        Assert.Equal(3, leaders.Count);
        var zoro = leaders[0];
        Assert.Equal("OP01-001", zoro.LeaderKey);
        Assert.Equal("Test Zoro", zoro.LeaderName);
        // The key is a card ID, so it is used for the image.
        Assert.Equal("OP01-001", zoro.LeaderCardId);
        Assert.Equal(2, zoro.Matchups.Count);

        var vsWhitebeard = zoro.Matchups[0];
        Assert.Equal("OP02-001", vsWhitebeard.OpponentKey);
        // "opponent" is a key; the name comes from the leader entry with that key.
        Assert.Equal("Test Whitebeard", vsWhitebeard.OpponentName);
        Assert.Equal(0.6, vsWhitebeard.FirstWinRate);
        Assert.Equal(0.75, vsWhitebeard.SecondWinRate);
        Assert.Equal(50, vsWhitebeard.Games);
        Assert.Equal(0.66, vsWhitebeard.WinRate!.Value, 6);

        // Opponent without its own leader entry keeps the key as name; null rate stays null.
        var vsStarter = zoro.Matchups[1];
        Assert.Equal("ST01-001", vsStarter.OpponentName);
        Assert.Null(vsStarter.SecondWinRate);
        Assert.Equal(0.2, vsStarter.WinRate!.Value, 6);

        // Unnamed promo leader: the key is the name.
        Assert.Equal("P-999", leaders[2].LeaderName);
    }

    [Fact]
    public void Parse_LeaderTotalsComeOnlyFromTheMatchups_WeightedByGames()
    {
        var zoro = CardKaizokuParser.Parse(SampleJson)[0];

        // The file says 999 games and 0.99, but only the matchups count: 50 + 10 games.
        Assert.Equal(60, zoro.Games);
        // (0.66 * 50 + 0.2 * 10) / 60
        Assert.Equal((0.66 * 50 + 0.2 * 10) / 60, zoro.WinRate!.Value, 6);
        // First: (0.6 * 30 + 0.2 * 10) / 40, second only from the 20 games: 0.75
        Assert.Equal((0.6 * 30 + 0.2 * 10) / 40, zoro.FirstWinRate!.Value, 6);
        Assert.Equal(0.75, zoro.SecondWinRate!.Value, 6);

        var promo = CardKaizokuParser.Parse(SampleJson)[2];
        Assert.Equal(0, promo.Games);
        Assert.Null(promo.WinRate);
    }

    [Fact]
    public void Parse_AcceptsPercentValues()
    {
        var json = SampleJson.Replace("\"first_win_rate\": 0.6,", "\"first_win_rate\": 60,")
            .Replace("\"second_win_rate\": 0.75", "\"second_win_rate\": 75")
            .Replace("\"first_win_rate\": 0.2,", "\"first_win_rate\": 20,")
            .Replace("\"first_win_rate\": 0.25,", "\"first_win_rate\": 25,")
            .Replace("\"second_win_rate\": 0.4 }", "\"second_win_rate\": 40 }");

        var zoro = CardKaizokuParser.Parse(json)[0];

        Assert.Equal(0.6, zoro.Matchups[0].FirstWinRate!.Value, 6);
        Assert.Equal(0.75, zoro.Matchups[0].SecondWinRate!.Value, 6);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[{\"leaderName\": \"No key\", \"matchups\": []}]")]
    [InlineData("[{\"leaderKey\": \"OP01-001\"}]")]
    [InlineData("[{\"leaderKey\": \"OP01-001\", \"matchups\": [{\"opponentKey\": \"OP02-001\", \"first_games\": -1, \"second_games\": 0}]}]")]
    [InlineData("[{\"leaderKey\": \"OP01-001\", \"matchups\": [{\"opponentKey\": \"OP02-001\", \"first_games\": 3, \"second_games\": 0, \"first_win_rate\": 150}]}]")]
    [InlineData("[{\"leaderKey\": \"OP01-001\", \"matchups\": [{\"opponentKey\": \"OP02-001\", \"first_games\": 3, \"second_games\": 0, \"first_win_rate\": -0.2}]}]")]
    [InlineData("[{\"leaderKey\": \"OP01-001\", \"matchups\": [{\"opponentKey\": \"OP02-001\", \"first_games\": \"3\", \"second_games\": 0}]}]")]
    public void Parse_RejectsBrokenData(string json)
    {
        Assert.Throws<MetaFormatException>(() => CardKaizokuParser.Parse(json));
    }

    #endregion

    #region PROVIDER

    [Fact]
    public async Task Provider_SearchesBackwardsFromTodayUntilASnapshotExists()
    {
        var handler = new FakeHandler(request =>
            request.RequestUri!.AbsoluteUri.EndsWith("stats_eb05_lw_p_top20_20261115.json")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleJson) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var provider = CreateProvider(handler);

        var snapshot = await provider.GetLatestAsync("eb05_lw_p_top20", CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(new DateOnly(2026, 11, 15), snapshot!.SnapshotDate);
        Assert.Equal("CardKaizoku", snapshot.Provider);
        Assert.Equal("https://cdn.example.test/stats/stats_eb05_lw_p_top20_20261115.json", snapshot.SourceUrl);
        Assert.Equal(
            [
                "stats_eb05_lw_p_top20_20261118.json",
                "stats_eb05_lw_p_top20_20261117.json",
                "stats_eb05_lw_p_top20_20261116.json",
                "stats_eb05_lw_p_top20_20261115.json"
            ],
            handler.Requests.Select(url => url.Split('/').Last()));
    }

    [Fact]
    public async Task Provider_GivesUpAfterMaxDaysBack()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var provider = CreateProvider(handler);

        Assert.Null(await provider.GetLatestAsync("eb05_lw_p_top20", CancellationToken.None));
        // Today plus 8 days back.
        Assert.Equal(9, handler.Requests.Count);
    }

    [Fact]
    public async Task Provider_RejectsInvalidJson_AndUnsafeFormats()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"broken\": true}]") });
        var provider = CreateProvider(handler);

        Assert.Null(await provider.GetLatestAsync("eb05_lw_p_top20", CancellationToken.None));
        Assert.Null(await provider.GetLatestAsync("../secret", CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Provider_UnreachableReturnsNull()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("offline"));

        Assert.Null(await CreateProvider(handler).GetLatestAsync("eb05_lw_p_top20", CancellationToken.None));
    }

    #endregion

    #region SERVICE

    [Fact]
    public async Task Service_FallsBackToTheLastSnapshotWhenTheProviderFails()
    {
        var fake = new FakeProvider();
        using var services = CreateServices(fake, enabled: true);
        var metaService = services.GetRequiredService<MetaService>();

        fake.Next = CreateSnapshot(new DateOnly(2026, 11, 17));
        var first = await metaService.GetAsync(null, CancellationToken.None);
        Assert.False(first.IsStale);
        Assert.Equal(new DateOnly(2026, 11, 17), first.Snapshot!.SnapshotDate);

        // Cached: the provider is not asked again.
        await metaService.GetAsync(null, CancellationToken.None);
        Assert.Equal(1, fake.Calls);

        fake.Next = null;
        var stale = await metaService.GetAsync(null, CancellationToken.None, forceRefresh: true);
        Assert.True(stale.IsStale);
        Assert.Equal(new DateOnly(2026, 11, 17), stale.Snapshot!.SnapshotDate);
    }

    [Fact]
    public async Task Service_WithoutAnySnapshot_ReturnsNullAndStale()
    {
        var fake = new FakeProvider();
        using var services = CreateServices(fake, enabled: true);

        var result = await services.GetRequiredService<MetaService>().GetAsync(null, CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task Service_Disabled_NeverAsksTheProvider()
    {
        var fake = new FakeProvider { Next = CreateSnapshot(new DateOnly(2026, 11, 17)) };
        using var services = CreateServices(fake, enabled: false);

        var result = await services.GetRequiredService<MetaService>().GetAsync(null, CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.Equal(0, fake.Calls);
    }

    #endregion

    #region HELPERS

    private static CardKaizokuMetaProvider CreateProvider(FakeHandler handler)
    {
        var options = Options.Create(new MetaOptions
        {
            Enabled = true,
            BaseUrl = "https://cdn.example.test/stats/",
            MaxDaysBack = 8
        });

        return new CardKaizokuMetaProvider(
            new HttpClient(handler),
            options,
            new FixedTime(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)),
            NullLogger<CardKaizokuMetaProvider>.Instance);
    }

    private static ServiceProvider CreateServices(FakeProvider provider, bool enabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.Configure<MetaOptions>(options => options.Enabled = enabled);
        services.AddSingleton<IMetaProvider>(provider);
        services.AddSingleton<MetaService>();
        return services.BuildServiceProvider();
    }

    private static MetaSnapshot CreateSnapshot(DateOnly date)
    {
        // All leaders have card IDs, so no database lookup is needed.
        var leaders = CardKaizokuParser.Parse(SampleJson).Take(2).ToList();
        return new MetaSnapshot("CardKaizoku", "https://example.test", date, "eb05_lw_p_top20", DateTimeOffset.UtcNow, leaders);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeProvider : IMetaProvider
    {
        public MetaSnapshot? Next { get; set; }
        public int Calls { get; private set; }

        public Task<MetaSnapshot?> GetLatestAsync(string format, CancellationToken cancellationToken)
        {
            Calls += 1;
            return Task.FromResult(Next);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    #endregion
}
