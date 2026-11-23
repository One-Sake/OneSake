using System.Net.Http.Json;

namespace OneSake.Server.Services;

// Dieser Service ist dafuer zustaendig externe API (optcgapi) aufzurufen
public class CardService

{
    private readonly HttpClient _http;
    // HttpClient wird automatisch von Asp.Net injected
    public CardService(HttpClient http)
    {
        _http = http;
    }

    private readonly Dictionary<string, List<CardDto>> _cardCache = new(); // Dictionary mit String als key und liste von den karten da es mehrere karten gibt per person zb. Zoro Parallel und Zoro Normal fuer zb. OP01-001
    private readonly Dictionary<string, List<CardDto>> _setCache = new(); // Dictionary mit key um ganze sets zu bekommen, bruachbar um Deckbuilder zu machen

    // Holt Karten von der externen API
    public async Task<List<CardDto>> GetCardsByIdAsync(string id)
    {
        if (_cardCache.ContainsKey(id)) return _cardCache[id];
        // API Endpoint

        var url = "";

        if (id.StartsWith("ST")) url = $"https://www.optcgapi.com/api/decks/card/{id}/";
        else url = $"https://optcgapi.com/api/sets/card/{id}/";

        try
        {
            var cards = await _http.GetFromJsonAsync<List<CardDto>>(url) ?? new List<CardDto>();

            // Storing Cache
            _cardCache[id] = cards;

            return cards;
        }

        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return new List<CardDto>();
        }
    }

    public async Task<List<CardDto>> GetSetByIdAsync(string set_id)
    {
        if (_setCache.ContainsKey(set_id)) return _setCache[set_id];

        // API Endpoint
        var url = "";

        if (set_id.StartsWith("ST")) url = $"https://www.optcgapi.com/api/decks/{set_id}/";
        else url = $"https://optcgapi.com/api/sets/{set_id}/";

        try
        {
            var set = await _http.GetFromJsonAsync<List<CardDto>>(url) ?? new List<CardDto>();

            _setCache[set_id] = set;

            return set;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return new List<CardDto>();
        }
    }

    private List<CardDto>? _donCache;

    // Holt alle DON!! Karten (Standard + alle Varianten) von der externen API.
    // DON!! Karten haben kein "card_set_id", ihre eindeutige ID steht in "card_image_id" (zB. don_8).
    public async Task<List<CardDto>> GetAllDonCardsAsync()
    {
        if (_donCache != null) return _donCache;

        try
        {
            var cards = await _http.GetFromJsonAsync<List<CardDto>>("https://optcgapi.com/api/allDonCards/") ?? new List<CardDto>();

            _donCache = cards;

            return cards;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return new List<CardDto>();
        }
    }

}


