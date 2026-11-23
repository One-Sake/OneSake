using Microsoft.AspNetCore.Mvc;
using OneSake.Server.Services;

namespace OneSake.Server.Controllers;

// Marks this as an API Controller
[ApiController]

// Base Route -> /cards
[Route("[controller]")]
public class CardsController : ControllerBase
{
    private readonly CardService _cardService;
    private readonly CardImportService _cardImportService;
    private readonly CardQueryService _cardQueryService;

    // inject services
    public CardsController(
        CardService cardService,
        CardImportService cardImportService,
        CardQueryService cardQueryService)
    {
        _cardService = cardService;
        _cardImportService = cardImportService;
        _cardQueryService = cardQueryService;
    }

        // GET /cards/sets
    [HttpGet("sets")]
    public async Task<IActionResult> GetAvailableSets()
    {
        var sets = await _cardQueryService.GetAvailableSetsAsync();

        return Ok(sets);
    }

    // GET /cards/don
    // Liefert die DON!! Karten aus der Datenbank. Ist noch keine da, wird die Standard-Karte importiert.
    [HttpGet("don")]
    public async Task<IActionResult> GetDonCards()
    {
        var donFromDatabase = await _cardQueryService.GetDonCardsAsync();

        if (donFromDatabase.Count > 0)
        {
            return Ok(donFromDatabase);
        }

        var importResult = await _cardImportService.ImportDonCardsAsync();

        if (importResult.TotalFromApi == 0)
        {
            return NotFound("No DON!! cards found.");
        }

        return Ok(await _cardQueryService.GetDonCardsAsync());
    }

    // POST /cards/import/don
    // Importiert die Standard DON!! Karte. Mit ?all=true werden alle Varianten importiert (spaeter fuer Belohnungen).
    [HttpPost("import/don")]
    public async Task<IActionResult> ImportDonCards([FromQuery] bool all = false)
    {
        var result = await _cardImportService.ImportDonCardsAsync(standardOnly: !all);

        if (result.TotalFromApi == 0)
        {
            return NotFound("No DON!! cards found.");
        }

        return Ok(result);
    }

    // GET /cards/OP01-001
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCardsById(string id)
    {
        var cardsFromDatabase = await _cardQueryService.GetCardsByIdAsync(id);

        if (cardsFromDatabase.Count > 0)
        {
            return Ok(cardsFromDatabase);
        }

        var cardsFromApi = await _cardService.GetCardsByIdAsync(id);

        // Falls nichts gefunden wurde return 404
        if (cardsFromApi == null || cardsFromApi.Count == 0) return NotFound();

        return Ok(cardsFromApi);
    }

    // GET /cards/set/OP-01
    [HttpGet("set/{set_id}")]
    public async Task<IActionResult> GetSetById(string set_id)
    {
        var setFromDatabase = await _cardQueryService.GetSetByIdAsync(set_id);

        if (setFromDatabase.Count > 0)
        {
            Console.WriteLine($"Fetching Set {set_id} from database");
            Console.WriteLine($"Count: {setFromDatabase.Count}");

            return Ok(setFromDatabase);
        }

        Console.WriteLine($"Set {set_id} not found in database. Importing from external API...");

        var importResult = await _cardImportService.ImportSetAsync(set_id);

        if (importResult.TotalFromApi == 0)
        {
            return NotFound($"No cards found for set {set_id}.");
        }

        var importedSetFromDatabase = await _cardQueryService.GetSetByIdAsync(set_id);

        Console.WriteLine($"Imported Set {set_id} into database");
        Console.WriteLine($"Imported: {importResult.Imported}, Skipped: {importResult.Skipped}");
        Console.WriteLine($"Count from database: {importedSetFromDatabase.Count}");

        return Ok(importedSetFromDatabase);
    }

    // POST /cards/import/OP-01
    [HttpPost("import/{setId}")]
    public async Task<IActionResult> ImportSet(string setId)
    {
        var result = await _cardImportService.ImportSetAsync(setId);

        if (result.TotalFromApi == 0)
        {
            return NotFound($"No cards found for set {setId}.");
        }

        return Ok(result);
    }
}