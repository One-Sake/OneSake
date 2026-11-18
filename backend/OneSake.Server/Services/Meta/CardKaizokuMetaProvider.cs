using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace OneSake.Server.Services.Meta;

// First meta provider: daily JSON snapshots on the Card Kaizoku CDN
// (stats_<format>_<YYYYMMDD>.json, about 8 days are kept). No HTML scraping,
// no personal data is sent (only a generic User-Agent).
public sealed partial class CardKaizokuMetaProvider : IMetaProvider
{
    public const string ProviderName = "CardKaizoku";

    private readonly HttpClient _http;
    private readonly MetaOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CardKaizokuMetaProvider> _logger;

    public CardKaizokuMetaProvider(
        HttpClient http,
        IOptions<MetaOptions> options,
        TimeProvider time,
        ILogger<CardKaizokuMetaProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    [GeneratedRegex("^[a-z0-9_]{1,40}$")]
    private static partial Regex FormatPattern();

    public static string BuildFileName(string format, DateOnly date) => $"stats_{format}_{date:yyyyMMdd}.json";

    public async Task<MetaSnapshot?> GetLatestAsync(string format, CancellationToken cancellationToken)
    {
        if (!FormatPattern().IsMatch(format))
        {
            _logger.LogWarning("Meta format {Format} is not allowed.", format);
            return null;
        }

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var baseUrl = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";

        // Newest first: today, yesterday, ... A 404 only means that day is not published (yet).
        for (var daysBack = 0; daysBack <= _options.MaxDaysBack; daysBack += 1)
        {
            var date = today.AddDays(-daysBack);
            var url = baseUrl + BuildFileName(format, date);
            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Meta provider not reachable ({Url}).", url);
                return null;
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Meta provider answered {Status} for {Url}.", (int)response.StatusCode, url);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    var leaders = CardKaizokuParser.Parse(json);
                    return new MetaSnapshot(ProviderName, url, date, format, _time.GetUtcNow(), leaders);
                }
                catch (MetaFormatException exception)
                {
                    // Strict: broken data is never shown.
                    _logger.LogWarning("Meta snapshot {Url} rejected: {Reason}", url, exception.Message);
                    return null;
                }
            }
        }

        _logger.LogWarning("No meta snapshot found in the last {Days} days for format {Format}.", _options.MaxDaysBack, format);
        return null;
    }
}
