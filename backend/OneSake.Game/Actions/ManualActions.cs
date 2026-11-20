using OneSake.Domain;
using OneSake.Game.Effects;
using OneSake.Game.Randomness;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

public static class ManualActionKinds
{
    public const string Draw = "DRAW";
    public const string MoveCard = "MOVE_CARD";
    public const string KoCard = "KO_CARD";
    public const string ReturnToHand = "RETURN_TO_HAND";
    public const string ReturnToDeck = "RETURN_TO_DECK";
    public const string SetRest = "SET_REST";
    public const string SetActive = "SET_ACTIVE";
    public const string ModifyPower = "MODIFY_POWER";
    public const string ModifyCost = "MODIFY_COST";
    public const string DonAdd = "DON_ADD";
    public const string DonReturn = "DON_RETURN";
    public const string DonAttach = "DON_ATTACH";
    public const string DonSetRest = "DON_SET_REST";
    public const string DonSetActive = "DON_SET_ACTIVE";
    public const string LookTop = "LOOK_TOP";
    public const string SearchDeck = "SEARCH_DECK";
    public const string CloseView = "CLOSE_VIEW";
    public const string ShuffleDeck = "SHUFFLE_DECK";
    public const string RevealCard = "REVEAL_CARD";
    public const string TrashFromHand = "TRASH_FROM_HAND";
    public const string LifeToHand = "LIFE_TO_HAND";
}

// Manual mode: a toolbox for card effects the engine does not automate.
// It only moves cards and changes values - turn flow, costs, battles and winning stay automatic.
// The checks WHO may act (turn player or open decision) are done by MatchRoom.
// Execute returns the log text (without the player name). Hidden cards are never named in it.
public sealed class ManualActions
{
    public const string LookView = "LOOK";
    public const string SearchView = "SEARCH";

    private const int MaxDrawOrLook = 10;
    private const int MaxPowerChange = 20000;
    private const int MaxCostChange = 10;

    private static class Zones
    {
        public const string Hand = "hand";
        public const string Character = "character";
        public const string Stage = "stage";
        public const string Leader = "leader";
        public const string Trash = "trash";
        public const string Deck = "deck";
        public const string Life = "life";
    }

    private readonly EffectEngine _effects;

    public ManualActions(EffectEngine effects)
    {
        _effects = effects;
    }

    public string Execute(ManualActionDto action, PlayerState actor, PlayerState opponent, IGameContext context)
    {
        var text = (action.Kind ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            ManualActionKinds.Draw => Draw(actor, action),
            ManualActionKinds.MoveCard => MoveCard(actor, action, context),
            ManualActionKinds.KoCard => KnockOut(actor, opponent, action, context),
            ManualActionKinds.ReturnToHand => ReturnToHand(actor, opponent, action, context),
            ManualActionKinds.ReturnToDeck => ReturnToDeck(actor, opponent, action, context),
            ManualActionKinds.SetRest => SetRested(actor, opponent, action, context, rested: true),
            ManualActionKinds.SetActive => SetRested(actor, opponent, action, context, rested: false),
            ManualActionKinds.ModifyPower => Modify(actor, opponent, action, context, isPower: true),
            ManualActionKinds.ModifyCost => Modify(actor, opponent, action, context, isPower: false),
            ManualActionKinds.DonAdd => DonAdd(actor, action),
            ManualActionKinds.DonReturn => DonReturn(actor, action),
            ManualActionKinds.DonAttach => DonAttach(actor, action, context),
            ManualActionKinds.DonSetRest => DonSetState(actor, action, rested: true),
            ManualActionKinds.DonSetActive => DonSetState(actor, action, rested: false),
            ManualActionKinds.LookTop => LookTop(actor, action),
            ManualActionKinds.SearchDeck => SearchDeck(actor),
            ManualActionKinds.CloseView => CloseView(actor),
            ManualActionKinds.ShuffleDeck => ShuffleDeck(actor),
            ManualActionKinds.RevealCard => RevealCard(actor, action),
            ManualActionKinds.TrashFromHand => TrashFromHand(actor, action, context),
            ManualActionKinds.LifeToHand => LifeToHand(actor, context),
            _ => throw new InvalidOperationException($"Unknown manual action '{action.Kind}'.")
        };

        SyncView(actor);
        SyncView(opponent);
        return text;
    }

    #region CARDS

    private static string Draw(PlayerState actor, ManualActionDto action)
    {
        var amount = Math.Clamp(action.Amount ?? 1, 1, MaxDrawOrLook);
        var drawn = Math.Min(amount, actor.DrawDeck.Count);
        actor.DrawCards(drawn);

        return drawn == 0
            ? "kann nicht ziehen, das Deck ist leer"
            : $"zieht {CardCount(drawn)}";
    }

    private static string MoveCard(PlayerState actor, ManualActionDto action, IGameContext context)
    {
        var (card, from) = FindOwnCard(actor, action.CardInstanceId);
        var to = NormalizeZone(action.ToZone);

        if (from == Zones.Leader || to == Zones.Leader)
        {
            throw new InvalidOperationException("The Leader cannot be moved.");
        }

        if (from == to && from != Zones.Deck && from != Zones.Life)
        {
            throw new InvalidOperationException($"{card.Name} is already there.");
        }

        switch (to)
        {
            case Zones.Character:
                if (!card.IsCharacter)
                {
                    throw new InvalidOperationException("Only Characters can be placed in the Character area.");
                }

                if (actor.Characters.Count >= PlayerState.MaxCharacters)
                {
                    throw new InvalidOperationException($"Your Character area is full (max {PlayerState.MaxCharacters}).");
                }

                break;

            case Zones.Stage:
                if (!card.IsStage)
                {
                    throw new InvalidOperationException("Only Stage cards can be placed in the Stage area.");
                }

                if (actor.Stage is not null)
                {
                    throw new InvalidOperationException("You already have a Stage (max 1).");
                }

                break;

            case Zones.Hand:
            case Zones.Trash:
            case Zones.Deck:
            case Zones.Life:
                break;

            default:
                throw new InvalidOperationException($"Unknown zone '{action.ToZone}'.");
        }

        RemoveFromZone(actor, card, from);
        var position = PlaceInZone(actor, card, to, action.ToPosition, context);
        // Manual moves are never "played" or "K.O.'d": no [On Play] / [On K.O.].
        context.Events?.Publish(new CardMovedZone(card, actor.PlayerId, from, to));

        // Hidden zones (hand, deck, life) on both sides: the opponent only learns that a card moved.
        var isPublic = IsPublicZone(from) || IsPublicZone(to);
        return isPublic
            ? $"{card.Name}: {ZoneName(from, null)} → {ZoneName(to, position)}"
            : $"legt 1 Karte ({ZoneName(from, null)}) {ZoneTarget(to, position)}";
    }

    private string KnockOut(PlayerState actor, PlayerState opponent, ManualActionDto action, IGameContext context)
    {
        var (card, owner) = FindFieldCard(actor, opponent, action.CardInstanceId);
        if (!card.IsCharacter || !_effects.KnockOut(card, context, byEffect: true))
        {
            throw new InvalidOperationException("Only Characters can be K.O.'d.");
        }

        return $"K.O. für {CardLabel(card, owner, actor)}";
    }

    private static string ReturnToHand(PlayerState actor, PlayerState opponent, ManualActionDto action, IGameContext context)
    {
        var (card, owner) = FindFieldCard(actor, opponent, action.CardInstanceId);
        EnsureNotLeader(card);

        owner.RemoveFromField(card);
        owner.Hand.Add(card);
        context.Events?.Publish(new CardMovedZone(card, owner.PlayerId, Zones.Character, Zones.Hand));
        return $"{CardLabel(card, owner, actor)} → Hand";
    }

    private static string ReturnToDeck(PlayerState actor, PlayerState opponent, ManualActionDto action, IGameContext context)
    {
        var (card, owner) = FindFieldCard(actor, opponent, action.CardInstanceId);
        EnsureNotLeader(card);

        owner.RemoveFromField(card);
        var position = InsertAt(owner.DrawDeck, card, action.ToPosition, defaultTop: false, topIsEnd: false);
        context.Events?.Publish(new CardMovedZone(card, owner.PlayerId, Zones.Character, Zones.Deck));
        return $"{CardLabel(card, owner, actor)} → {ZoneName(Zones.Deck, position)}";
    }

    private static string SetRested(PlayerState actor, PlayerState opponent, ManualActionDto action, IGameContext context, bool rested)
    {
        var (card, owner) = FindFieldCard(actor, opponent, action.CardInstanceId);
        if (card.IsRested == rested)
        {
            throw new InvalidOperationException($"{card.Name} is already {(rested ? "rested" : "active")}.");
        }

        card.IsRested = rested;
        if (rested)
        {
            context.Events?.Publish(new CardRested(card, owner.PlayerId));
        }

        return $"{CardLabel(card, owner, actor)} → {(rested ? "rest" : "aktiv")}";
    }

    private static string Modify(
        PlayerState actor,
        PlayerState opponent,
        ManualActionDto action,
        IGameContext context,
        bool isPower)
    {
        var (card, owner) = FindFieldCard(actor, opponent, action.CardInstanceId);
        var amount = action.Amount ?? 0;
        var limit = isPower ? MaxPowerChange : MaxCostChange;
        if (amount == 0 || Math.Abs(amount) > limit)
        {
            throw new InvalidOperationException($"Choose a value between -{limit} and +{limit}.");
        }

        var isBattle = string.Equals(action.Duration?.Trim(), "battle", StringComparison.OrdinalIgnoreCase);
        if (isBattle && context.Battle is null)
        {
            throw new InvalidOperationException("There is no battle running.");
        }

        card.Modifiers.Add(new CardModifier
        {
            Power = isPower ? amount : 0,
            Cost = isPower ? 0 : amount,
            Duration = isBattle ? Duration.ThisBattle : Duration.ThisTurn,
            CreatedOnTurn = context.TurnNumber
        });

        var value = amount > 0 ? $"+{amount}" : amount.ToString();
        var what = isPower ? "Power" : "Kosten";
        var duration = isBattle ? "dieser Kampf" : "dieser Zug";
        return $"{CardLabel(card, owner, actor)} {value} {what} ({duration})";
    }

    private static string RevealCard(PlayerState actor, ManualActionDto action)
    {
        var card = actor.FindHandCard(action.CardInstanceId)
            ?? throw new InvalidOperationException("Choose a card from your hand.");
        return $"zeigt {card.Name} ({card.CardId}) aus der Hand";
    }

    private static string TrashFromHand(PlayerState actor, ManualActionDto action, IGameContext context)
    {
        var card = actor.FindHandCard(action.CardInstanceId)
            ?? throw new InvalidOperationException("Choose a card from your hand.");
        actor.Hand.Remove(card);
        actor.TrashCards.Add(card);
        context.Events?.Publish(new CardMovedZone(card, actor.PlayerId, Zones.Hand, Zones.Trash));
        return $"{card.Name}: Hand → Trash";
    }

    // Not damage: 0 Life after this does not end the game.
    private static string LifeToHand(PlayerState actor, IGameContext context)
    {
        if (actor.LifeCards.Count == 0)
        {
            throw new InvalidOperationException("You have no Life cards.");
        }

        var card = actor.LifeCards[^1];
        actor.LifeCards.RemoveAt(actor.LifeCards.Count - 1);
        actor.Hand.Add(card);
        context.Events?.Publish(new LifeCardAdded(card, actor.PlayerId, FromDamage: false));
        return $"nimmt die oberste Life-Karte auf die Hand ({actor.LifeCount} Life übrig)";
    }

    #endregion

    #region DON!!

    private static string DonAdd(PlayerState actor, ManualActionDto action)
    {
        var amount = Math.Min(Math.Max(action.Amount ?? 1, 1), actor.DonDeckCount);
        if (amount == 0)
        {
            throw new InvalidOperationException("Your DON!! deck is empty.");
        }

        var rested = IsRestedZone(action.ToZone);
        actor.DonDeckCount -= amount;
        if (rested)
        {
            actor.RestedDon += amount;
        }
        else
        {
            actor.ActiveDon += amount;
        }

        return $"legt {amount} DON!! aus dem DON!!-Deck ({(rested ? "rest" : "aktiv")})";
    }

    private static string DonReturn(PlayerState actor, ManualActionDto action)
    {
        var amount = Math.Max(action.Amount ?? 1, 1);
        if (amount > actor.ActiveDon + actor.RestedDon)
        {
            throw new InvalidOperationException("Not enough DON!! in your cost area.");
        }

        // Rested DON!! first, unless active ones were chosen.
        var preferActive = string.Equals(action.FromZone?.Trim(), "active", StringComparison.OrdinalIgnoreCase);
        var first = preferActive ? Math.Min(amount, actor.ActiveDon) : Math.Min(amount, actor.RestedDon);
        var second = amount - first;
        if (preferActive)
        {
            actor.ActiveDon -= first;
            actor.RestedDon -= second;
        }
        else
        {
            actor.RestedDon -= first;
            actor.ActiveDon -= second;
        }

        actor.DonDeckCount += amount;
        return $"legt {amount} DON!! zurück ins DON!!-Deck";
    }

    private static string DonAttach(PlayerState actor, ManualActionDto action, IGameContext context)
    {
        var card = actor.FindFieldCard(action.CardInstanceId)
            ?? throw new InvalidOperationException("Choose your Leader or one of your Characters.");
        var amount = Math.Max(action.Amount ?? 1, 1);
        var fromRested = IsRestedZone(action.FromZone);
        var available = fromRested ? actor.RestedDon : actor.ActiveDon;
        if (amount > available)
        {
            throw new InvalidOperationException($"Not enough {(fromRested ? "rested" : "active")} DON!!.");
        }

        if (fromRested)
        {
            actor.RestedDon -= amount;
        }
        else
        {
            actor.ActiveDon -= amount;
        }

        card.AttachedDon += amount;
        context.Events?.Publish(new DonAttached(card, actor.PlayerId, amount));
        return $"gibt {amount} DON!! an {card.Name}";
    }

    private static string DonSetState(PlayerState actor, ManualActionDto action, bool rested)
    {
        var amount = Math.Max(action.Amount ?? 1, 1);
        var available = rested ? actor.ActiveDon : actor.RestedDon;
        if (amount > available)
        {
            throw new InvalidOperationException($"Not enough {(rested ? "active" : "rested")} DON!!.");
        }

        actor.ActiveDon += rested ? -amount : amount;
        actor.RestedDon += rested ? amount : -amount;
        return $"setzt {amount} DON!! {(rested ? "rest" : "aktiv")}";
    }

    #endregion

    #region DECK

    private static string LookTop(PlayerState actor, ManualActionDto action)
    {
        if (actor.DrawDeck.Count == 0)
        {
            throw new InvalidOperationException("Your deck is empty.");
        }

        var amount = Math.Min(Math.Clamp(action.Amount ?? 1, 1, MaxDrawOrLook), actor.DrawDeck.Count);
        actor.CloseManualView();
        actor.ManualViewKind = LookView;
        actor.ManualViewCards.AddRange(actor.DrawDeck.Take(amount));
        return $"sieht sich die obersten {CardCount(amount)} des Decks an";
    }

    private static string SearchDeck(PlayerState actor)
    {
        if (actor.DrawDeck.Count == 0)
        {
            throw new InvalidOperationException("Your deck is empty.");
        }

        actor.CloseManualView();
        actor.ManualViewKind = SearchView;
        actor.ManualViewCards.AddRange(actor.DrawDeck);
        return "durchsucht das Deck";
    }

    private static string CloseView(PlayerState actor)
    {
        var kind = actor.ManualViewKind;
        var left = actor.ManualViewCards.Count;
        actor.CloseManualView();

        if (kind == SearchView)
        {
            actor.DrawDeck.Shuffle(actor.Random);
            return "beendet die Suche und mischt das Deck";
        }

        return kind == LookView && left > 0
            ? $"lässt {CardCount(left)} oben auf dem Deck"
            : "schließt die Kartenansicht";
    }

    private static string ShuffleDeck(PlayerState actor)
    {
        actor.CloseManualView();
        actor.DrawDeck.Shuffle(actor.Random);
        return "mischt das Deck";
    }

    #endregion

    #region HELPERS

    // Finds a card in the player's own zones. Deck cards only while the player is looking at them.
    private static (CardInstance Card, string Zone) FindOwnCard(PlayerState player, string? instanceId)
    {
        var id = ActionGuard.RequireId(instanceId, "Choose a card.");

        if (player.Leader?.InstanceId == id)
        {
            return (player.Leader, Zones.Leader);
        }

        if (player.Stage?.InstanceId == id)
        {
            return (player.Stage, Zones.Stage);
        }

        var zones = new (List<CardInstance> Cards, string Zone)[]
        {
            (player.Hand, Zones.Hand),
            (player.Characters, Zones.Character),
            (player.TrashCards, Zones.Trash),
            (player.ManualViewCards, Zones.Deck),
        };

        foreach (var (cards, zone) in zones)
        {
            var card = cards.FirstOrDefault(entry => entry.InstanceId == id);
            if (card is not null)
            {
                return (card, zone);
            }
        }

        throw new InvalidOperationException("You can only move your own visible cards.");
    }

    // Leader, Character or Stage of either player. Hand and deck of the opponent are never searched.
    private static (CardInstance Card, PlayerState Owner) FindFieldCard(
        PlayerState actor,
        PlayerState opponent,
        string? instanceId)
    {
        var id = ActionGuard.RequireId(instanceId, "Choose a card on the field.");

        foreach (var player in new[] { actor, opponent })
        {
            var card = player.FieldCards().FirstOrDefault(entry => entry.InstanceId == id);
            if (card is not null)
            {
                return (card, player);
            }
        }

        throw new InvalidOperationException("Choose a Leader, Character or Stage on the field.");
    }

    private static void EnsureNotLeader(CardInstance card)
    {
        if (card.IsLeader)
        {
            throw new InvalidOperationException("The Leader cannot leave the field.");
        }
    }

    private static void RemoveFromZone(PlayerState player, CardInstance card, string zone)
    {
        switch (zone)
        {
            case Zones.Character:
            case Zones.Stage:
                // Given DON!! go back to the cost area rested, like after a K.O.
                player.RemoveFromField(card);
                break;
            case Zones.Hand:
                player.Hand.Remove(card);
                break;
            case Zones.Trash:
                player.TrashCards.Remove(card);
                break;
            case Zones.Deck:
                player.DrawDeck.Remove(card);
                player.ManualViewCards.Remove(card);
                break;
        }
    }

    private static string? PlaceInZone(
        PlayerState player,
        CardInstance card,
        string zone,
        string? position,
        IGameContext context)
    {
        switch (zone)
        {
            case Zones.Character:
                card.ResetFieldState();
                card.PlayedOnTurn = context.TurnNumber;
                player.Characters.Add(card);
                return null;
            case Zones.Stage:
                card.ResetFieldState();
                card.PlayedOnTurn = context.TurnNumber;
                player.Stage = card;
                return null;
            case Zones.Hand:
                player.Hand.Add(card);
                return null;
            case Zones.Trash:
                player.TrashCards.Add(card);
                return null;
            case Zones.Deck:
                // DrawDeck[0] is the top card.
                return InsertAt(player.DrawDeck, card, position, defaultTop: false, topIsEnd: false);
            default:
                // LifeCards[^1] is the top Life card.
                return InsertAt(player.LifeCards, card, position, defaultTop: true, topIsEnd: true);
        }
    }

    // Returns "top", "bottom" or the index that was used.
    private static string InsertAt(List<CardInstance> cards, CardInstance card, string? position, bool defaultTop, bool topIsEnd)
    {
        var value = (position ?? string.Empty).Trim().ToLowerInvariant();
        if (int.TryParse(value, out var index))
        {
            index = Math.Clamp(index, 0, cards.Count);
            cards.Insert(index, card);
            return index.ToString();
        }

        var top = value switch
        {
            "top" => true,
            "bottom" => false,
            _ => defaultTop
        };

        if (top == topIsEnd)
        {
            cards.Add(card);
        }
        else
        {
            cards.Insert(0, card);
        }

        return top ? "top" : "bottom";
    }

    // Keeps the open deck view in line with the deck after a card moved.
    private static void SyncView(PlayerState player)
    {
        if (player.ManualViewKind == SearchView)
        {
            player.ManualViewCards.Clear();
            player.ManualViewCards.AddRange(player.DrawDeck);
        }
        else
        {
            player.ManualViewCards.RemoveAll(card => !player.DrawDeck.Contains(card));
        }
    }

    private static string NormalizeZone(string? zone)
    {
        var value = (zone ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "board" or "characters" => Zones.Character,
            _ => value
        };
    }

    private static bool IsRestedZone(string? zone)
    {
        return string.Equals(zone?.Trim(), "rested", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicZone(string zone)
    {
        return zone is Zones.Character or Zones.Stage or Zones.Trash or Zones.Leader;
    }

    private static string ZoneName(string zone, string? position)
    {
        return zone switch
        {
            Zones.Hand => "Hand",
            Zones.Character => "Character-Bereich",
            Zones.Stage => "Stage",
            Zones.Leader => "Leader",
            Zones.Trash => "Trash",
            // Without a position it is the zone the card came from.
            Zones.Deck => position switch { null => "Deck", "top" => "oben aufs Deck", "bottom" => "unter das Deck", _ => "ins Deck" },
            Zones.Life => position switch { null => "Life", "bottom" => "unter die Life", _ => "oben auf die Life" },
            _ => zone
        };
    }

    // "legt 1 Karte (Hand) unter das Deck" / "... auf die Hand"
    private static string ZoneTarget(string zone, string? position)
    {
        return zone switch
        {
            Zones.Hand => "auf die Hand",
            Zones.Deck or Zones.Life => ZoneName(zone, position),
            _ => $"in {ZoneName(zone, position)}"
        };
    }

    private static string CardLabel(CardInstance card, PlayerState owner, PlayerState actor)
    {
        return owner == actor ? card.Name : $"{card.Name} ({owner.DisplayName})";
    }

    private static string CardCount(int amount)
    {
        return amount == 1 ? "1 Karte" : $"{amount} Karten";
    }

    #endregion
}
