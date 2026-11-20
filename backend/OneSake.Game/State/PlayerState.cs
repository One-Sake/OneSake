using OneSake.Domain;

namespace OneSake.Game.State;

public sealed class PlayerState
{
    public const int MaxCharacters = 5;
    public const int DonDeckSize = 10;

    public string PlayerId { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Connected { get; set; }

    // Stable guest ID from the browser (localStorage). Null for old clients.
    // NOT secure authentication - anyone can send any ID. A real login follows later (stage 2).
    public string? GuestId { get; set; }

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

    // Flags for this player that end with the turn (e.g. "your opponent cannot activate [Blocker]").
    public HashSet<string> TurnFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    // "Set up to 2 of your DON!! cards as active at the end of this turn"
    public int DonToActivateAtEndOfTurn { get; set; }

    // Manual mode: deck cards only this player sees right now ("LOOK" or "SEARCH", empty when closed).
    public string ManualViewKind { get; set; } = string.Empty;
    public List<CardInstance> ManualViewCards { get; } = [];

    public void CloseManualView()
    {
        ManualViewKind = string.Empty;
        ManualViewCards.Clear();
    }

    // All DON!! on the field: cost area (active + rested) and given to the Leader/Characters.
    public int DonOnField => ActiveDon + RestedDon + FieldCards().Sum(card => card.AttachedDon);

    // Leader, Characters and Stage.
    public IEnumerable<CardInstance> FieldCards()
    {
        if (Leader is not null)
        {
            yield return Leader;
        }

        foreach (var character in Characters)
        {
            yield return character;
        }

        if (Stage is not null)
        {
            yield return Stage;
        }
    }

    // Removes a card from the Character area or Stage area without moving it anywhere.
    // Given DON!! go back to the cost area rested. Returns false when the card was not there.
    public bool RemoveFromField(CardInstance card)
    {
        var removed = Characters.Remove(card);
        if (!removed && Stage == card)
        {
            Stage = null;
            removed = true;
        }

        if (removed)
        {
            RestedDon += card.AttachedDon;
            card.ResetFieldState();
        }

        return removed;
    }

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
