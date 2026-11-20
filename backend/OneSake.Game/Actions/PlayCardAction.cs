using OneSake.Domain;
using OneSake.Game.Rules;
using OneSake.Game.State;

namespace OneSake.Game.Actions;

// Rule 6-5-3: pay the cost (rest DON!!) and play a Character, Stage or Event card from the hand.
public sealed class PlayCardAction
{
    public CardInstance Execute(
        PlayerState player,
        MatchPhase phase,
        string activePlayerId,
        string? selectedCardInstanceId,
        string? replaceCharacterInstanceId,
        int turnNumber,
        GameEventBus? events = null)
    {
        ActionGuard.EnsureMainPhase(phase);
        ActionGuard.EnsureActivePlayer(player, activePlayerId);

        if (player.Hand.Count == 0)
        {
            throw new InvalidOperationException("Player has no cards in hand.");
        }

        var cardId = ActionGuard.RequireId(
            selectedCardInstanceId,
            "Playing a card requires selectedCardInstanceId.");

        var card = player.FindHandCard(cardId)
            ?? throw new InvalidOperationException("Selected card is not in the player's hand.");

        if (card.Cost > player.ActiveDon)
        {
            throw new InvalidOperationException(
                $"Not enough DON!!: {card.Name} costs {card.Cost}, you have {player.ActiveDon} active DON!!.");
        }

        // Rule 3-7-6-1: with 5 Characters you have to trash one of them to play a new one.
        CardInstance? characterToReplace = null;
        if (card.IsCharacter && player.Characters.Count >= PlayerState.MaxCharacters)
        {
            characterToReplace = player.Characters.FirstOrDefault(
                character => character.InstanceId == replaceCharacterInstanceId)
                ?? throw new InvalidOperationException(
                    "Your Character area is full. Choose one of your Characters to trash.");
        }

        // Pay the cost.
        player.ActiveDon -= card.Cost;
        player.RestedDon += card.Cost;
        player.Hand.Remove(card);

        if (card.IsCharacter)
        {
            if (characterToReplace is not null)
            {
                // Rule 3-7-6-1-1: this is rule processing, not a K.O. - no [On K.O.].
                player.TrashCharacter(characterToReplace);
                events?.Publish(new CardMovedZone(characterToReplace, player.PlayerId, "character", "trash"));
            }

            card.ResetFieldState();
            card.PlayedOnTurn = turnNumber;
            player.Characters.Add(card);
            events?.Publish(new CardPlayed(card, player.PlayerId, ByEffect: false));
        }
        else if (card.IsStage)
        {
            // Only one Stage is allowed - the old one goes to the trash.
            // Rule 3-8-5-1: only one Stage - the old one goes to the trash.
            if (player.Stage is not null)
            {
                var oldStage = player.Stage;
                oldStage.ResetFieldState();
                player.TrashCards.Add(oldStage);
                events?.Publish(new CardMovedZone(oldStage, player.PlayerId, "stage", "trash"));
            }

            card.ResetFieldState();
            card.PlayedOnTurn = turnNumber;
            player.Stage = card;
            events?.Publish(new CardPlayed(card, player.PlayerId, ByEffect: false));
        }
        else
        {
            // Rule 8-4-2: an Event card is trashed and its effect is carried out.
            player.TrashCards.Add(card);
            events?.Publish(new EventActivated(card, player.PlayerId, IsCounter: false));
        }

        return card;
    }
}
