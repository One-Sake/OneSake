using OneSake.Domain;
using OneSake.Game.State;

namespace OneSake.Game.Rules;

// Something that happened in the game. Actions, manual actions and effect steps report these
// events to the GameEventBus; the TriggerCollector decides which auto effects they activate.
public abstract record GameEvent;

// Rule 3-7-3 / 3-8-3 / 10-2-6: a Character or Stage was played (from the hand or by an effect).
public sealed record CardPlayed(CardInstance Card, string PlayerId, bool ByEffect) : GameEvent;

// Rule 8-5-2: card activation = using an Event card from the hand ([Main] or [Counter]).
public sealed record EventActivated(CardInstance Card, string PlayerId, bool IsCounter) : GameEvent;

// Rule 10-2-1: a Character was K.O.'d by an effect or by losing a battle. Published while the card
// is still on the field, because [On K.O.] conditions are checked there (rule 10-2-17-1).
public sealed record CharacterKOd(CardInstance Card, string OwnerId, bool ByEffect) : GameEvent;

// Rule 7-1-1-1/2: an attack was declared.
public sealed record AttackDeclared(CardInstance Attacker, CardInstance Target, string AttackerPlayerId, string DefenderPlayerId) : GameEvent;

// Rule 7-1-2-2: a [Blocker] was activated.
public sealed record BlockerDeclared(CardInstance Blocker, string PlayerId) : GameEvent;

// Rule 7-1-3-1: a Counter was used in the Counter Step.
public sealed record CounterUsed(CardInstance Card, string PlayerId, bool IsEvent) : GameEvent;

// Rule 4-6: damage processing for a Leader. LifeBefore = Life cards when the damage was dealt.
public sealed record DamageDealt(string PlayerId, int Amount, int LifeBefore) : GameEvent;

// Rule 4-6-2-1: a Life card was added to the hand (FromDamage: during damage processing).
public sealed record LifeCardAdded(CardInstance Card, string PlayerId, bool FromDamage) : GameEvent;

// Rule 6-5-5-1: DON!! cards were given to a Leader or Character.
public sealed record DonAttached(CardInstance Card, string PlayerId, int Amount) : GameEvent;

public sealed record TurnStarted(string PlayerId, int TurnNumber) : GameEvent;

// Rule 6-6-1-1: the End Phase starts ([End of Your Turn] / [End of Your Opponent's Turn]).
public sealed record TurnEnding(string PlayerId, int TurnNumber) : GameEvent;

public sealed record PhaseChanged(MatchPhase Phase, string ActivePlayerId) : GameEvent;

// A card moved between areas without being played or K.O.'d (e.g. trashed, returned to the hand).
public sealed record CardMovedZone(CardInstance Card, string PlayerId, string FromZone, string ToZone) : GameEvent;

public sealed record CardRested(CardInstance Card, string PlayerId) : GameEvent;
