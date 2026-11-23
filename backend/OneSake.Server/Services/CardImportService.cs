using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using OneSake.Persistence.Entities;

namespace OneSake.Server.Services;

public class CardImportService
{
    private readonly CardService _cardService;
    private readonly OneSakeDbContext _dbContext;

    public CardImportService(CardService cardService, OneSakeDbContext dbContext)
    {
        _cardService = cardService;
        _dbContext = dbContext;
    }

    public async Task<CardImportResult> ImportSetAsync(string setId)
    {
        var cardsFromApi = await _cardService.GetSetByIdAsync(setId);

        if (cardsFromApi.Count == 0)
        {
            return new CardImportResult(setId, 0, 0, 0);
        }

        var imported = 0;
        var skipped = 0;

        foreach (var cardDto in cardsFromApi)
        {
            if (string.IsNullOrWhiteSpace(cardDto.CardId))
            {
                skipped++;
                continue;
            }

            var alreadyExists = await _dbContext.Cards
                .AnyAsync(card => card.CardId == cardDto.CardId);

            if (alreadyExists)
            {
                skipped++;
                continue;
            }

            var card = MapToEntity(cardDto);

            _dbContext.Cards.Add(card);
            imported++;
        }

        await _dbContext.SaveChangesAsync();

        return new CardImportResult(setId, cardsFromApi.Count, imported, skipped);
    }

    // Kartentyp, den optcgapi fuer alle DON!! Karten liefert.
    public const string DonCardType = "DON!!";

    // Name der normalen DON!! Karte. Alle anderen Eintraege sind Varianten (zB. "DON!! Card (Egghead)").
    public const string StandardDonCardName = "DON!! Card";

    // Importiert DON!! Karten. Zurzeit nur die Standard-Karte, spaeter (standardOnly: false)
    // alle Varianten, zB. als Belohnungen.
    public async Task<CardImportResult> ImportDonCardsAsync(bool standardOnly = true)
    {
        var donCardsFromApi = await _cardService.GetAllDonCardsAsync();

        var selected = standardOnly
            ? donCardsFromApi.Where(IsStandardDon).ToList()
            : donCardsFromApi;

        if (selected.Count == 0)
        {
            return new CardImportResult(DonCardType, 0, 0, 0);
        }

        var imported = 0;
        var skipped = 0;

        foreach (var donDto in selected)
        {
            // DON!! Karten haben kein card_set_id, daher dient card_image_id (zB. don_8) als CardId.
            if (string.IsNullOrWhiteSpace(donDto.CardImageId))
            {
                skipped++;
                continue;
            }

            var alreadyExists = await _dbContext.Cards
                .AnyAsync(card => card.CardId == donDto.CardImageId);

            if (alreadyExists)
            {
                skipped++;
                continue;
            }

            var card = MapToEntity(donDto);
            card.CardId = donDto.CardImageId;
            card.Type = DonCardType;

            _dbContext.Cards.Add(card);
            imported++;
        }

        await _dbContext.SaveChangesAsync();

        return new CardImportResult(DonCardType, selected.Count, imported, skipped);
    }

    private static bool IsStandardDon(CardDto dto)
    {
        return dto.Type == DonCardType && dto.CardName.Trim() == StandardDonCardName;
    }

    private static Card MapToEntity(CardDto dto)
    {
        return new Card
        {
            CardId = dto.CardId ?? "",
            CardName = dto.CardName ?? "",
            SetId = dto.SetId ?? "",
            SetName = dto.SetName ?? "",
            Rarity = dto.Rarity ?? "",
            Color = dto.Color ?? "",
            Type = dto.Type ?? "",
            CardText = dto.CardText ?? "",

            Life = ToNullableInt(dto.Life),
            CardCost = ToNullableInt(dto.CardCost),
            CardPower = ToNullableInt(dto.CardPower),
            CounterAmount = ToNullableInt(dto.CounterAmount),

            SubTypes = dto.SubTypes ?? "",
            Attribute = dto.Attribute ?? "",

            InventoryPrice = ToNullableDecimal(dto.InventoryPrice),
            MarketPrice = ToNullableDecimal(dto.MarketPrice),

            CardImageId = dto.CardImageId ?? "",
            ImageUrl = dto.ImageURL ?? "",

            DateScraped = ToUtcDateTime(dto.DateScraped)
        };
    }

    private static int? ToNullableInt(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue)
        {
            return (int)longValue;
        }

        if (value is double doubleValue)
        {
            return (int)doubleValue;
        }

        if (value is decimal decimalValue)
        {
            return (int)decimalValue;
        }

        var text = value.ToString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text, out var result)
            ? result
            : null;
    }

    private static decimal? ToNullableDecimal(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is decimal decimalValue)
        {
            return decimalValue;
        }

        if (value is float floatValue)
        {
            return (decimal)floatValue;
        }

        if (value is double doubleValue)
        {
            return (decimal)doubleValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        var text = value.ToString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text, out var result)
            ? result
            : null;
    }

    private static DateTime ToUtcDateTime(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
        }

        var text = value?.ToString();

        if (!string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, out var parsedDate))
        {
            return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }
}

public record CardImportResult(
    string SetId,
    int TotalFromApi,
    int Imported,
    int Skipped
);