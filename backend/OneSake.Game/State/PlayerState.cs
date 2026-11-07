using OneSake.Domain;

namespace OneSake.Game.State;

public sealed class PlayerState
{
    public const int MaxCharacters = 5;
    public const int DonDeckSize = 10;

    public string PlayerId { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Connected { get; set; }

    // Deck registration (lobby)
    public string DeckId { get; set; } = string.Empty;
    public string DeckName { get; set; } = string.Empty;
    public string LeaderCardId { get; set; } = string.Empty;
    public PlayerDeckSubmissionDto? DeckSubmission { get; set; }
    public int MainDeckCount { get; set; }
    public bool HasDeck { get; set; }
    public List<PlayerDeckCardDto> DeckCards { get; set; } = [];

    // Match areas
    public CardInstance? Leader { get; set; }
    public CardInstance? Stage { get; set; }
    public List<CardInstance> DrawDeck { get; set; } = [];
    public List<CardInstance> Hand { get; set; } = [];
    // Character area. The first card in the list is the oldest Character.
    public List<CardInstance> Characters { get; set; } = [];
    // Life area. The LAST card in the list is the top Life card.
    public List<CardInstance> LifeCards { get; set; } = [];
    public List<CardInstance> TrashCards { get; set; } = [];

    // DON!! cards are all the same, so counting them is enough.
    public int DonDeckCount { get; set; }
    public int ActiveDon { get; set; }
    public int RestedDon { get; set; }

    // How many turns this player has started. Used for "no battle on your first turn".
    public int TurnsStarted { get; set; }

    public int DeckCount => DrawDeck.Count;
    public int HandCount => Hand.Count;
    public int LifeCount => LifeCards.Count;

    public CardInstance? FindFieldCard(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        if (Leader is not null && Leader.InstanceId == instanceId)
        {
            return Leader;
        }

        return Characters.FirstOrDefault(card => card.InstanceId == instanceId);
    }

    public CardInstance? FindHandCard(string? instanceId)
    {
        return string.IsNullOrWhiteSpace(instanceId)
            ? null
            : Hand.FirstOrDefault(card => card.InstanceId == instanceId);
    }

    // Moves a Character from the field to the trash. Its DON!! go back to the cost area rested (6-5-5-4).
    public void TrashCharacter(CardInstance character)
    {
        if (!Characters.Remove(character))
        {
            return;
        }

        RestedDon += character.AttachedDon;
        character.ResetFieldState();
        TrashCards.Add(character);
    }

    public void DrawCards(int amount)
    {
        var cardsToDraw = Math.Min(Math.Max(0, amount), DrawDeck.Count);

        for (var index = 0; index < cardsToDraw; index += 1)
        {
            Hand.Add(DrawDeck[0]);
            DrawDeck.RemoveAt(0);
        }
    }
}
