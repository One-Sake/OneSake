# Effect building blocks

A card effect is put together from building blocks: one block for each word or phrase of a card
text that the engine understands ("[On Play]", "K.O.", "up to 1", "with a cost of 3 or less",
"during this turn" ...). The plan (`docs/spiellogik-plan.md`, A11) says: test the blocks
thoroughly, not 2,500 cards one by one. A card is correct when all its blocks are correct and its
script matches its text.

- The list of blocks is built from the effect model (`OneSake.Game/Effects/EffectModel.cs`):
  `OneSake.Tests/Blocks/BlockCatalog.cs`. A new enum value or filter property is a new block.
- Every block has at least one test marked with `[BlockTest("<block>")]` in
  `OneSake.Tests/Blocks/`. `BlockCoverageTests` fails when a block has no test, when a test
  names an unknown block, or when a block is missing in this document.
- The block tests use `BlockGame`: card texts are parsed like in the generator, and effects run
  the same way as in a match (game event → `TriggerCollector` → `TriggerQueue` → `EffectEngine`,
  with rule processing). Every question a player gets (Yes/No, choose a card) is answered in the
  test, so the tests also show where a player decides.

Status (2026-11-24): **153 blocks, 153 with a test.** "Scripts" = number of uses in the card
script files (`generated/` + `overrides/`) at that date.

## Player decisions

The player decides only where the card text allows it (rule 1-3-3 / 8-3):

| Text | Question |
|------|----------|
| cost in front of ":" of an auto effect (`[On Play] You may trash ...: ...`) | "Use the effect of X? Cost: ..." Yes/No |
| "You may ..." without a card choice | Yes/No |
| "up to N" | choose cards, "Done" stops early |
| exact number ("K.O. 1 of your Characters") | choose cards, no "Done" |
| `[Activate: Main]`, Event `[Main]`/`[Counter]`, `[Trigger]` | started by the player: no extra Yes/No |

Everything else runs without a question. There is no manual mode: a text that is not automated
is skipped and logged as `EFFECT_NOT_AUTOMATED`.

## Timings

| Block | Card text | Scripts | Notes |
|-------|-----------|--------:|-------|
| `Timing.OnPlay` | `[On Play]` | 641 | also when played by an effect |
| `Timing.WhenAttacking` | `[When Attacking]` | 159 | attacker only |
| `Timing.OnOpponentsAttack` | `[On Your Opponent's Attack]` | 35 | all field cards of the defender |
| `Timing.OnBlock` | `[On Block]` | 12 | |
| `Timing.OnKO` | `[On K.O.]` | 123 | checked on the field, resolved from the trash (rule 10-2-17) |
| `Timing.EndOfYourTurn` | `[End of Your Turn]` | 40 | |
| `Timing.EndOfOpponentsTurn` | `[End of Your Opponent's Turn]` | 0 | |
| `Timing.ActivateMain` | `[Activate: Main]` | 216 | |
| `Timing.EventMain` | Event `[Main]` | 181 | |
| `Timing.EventCounter` | Event `[Counter]` | 152 | |
| `Timing.Trigger` | `[Trigger]` | 443 | |
| `Timing.Static` | no timing (always on) | 522 | power, cost, flags, name alias only |

## Ability header

| Block | Card text | Scripts | Notes |
|-------|-----------|--------:|-------|
| `Ability.RequiredDon` | `[DON!! x1]` | 142 | static: checked all the time; auto: checked when it activates |
| `Ability.OncePerTurn` | `[Once Per Turn]` | 121 | |
| `Ability.OnlyYourTurn` | `[Your Turn]` | 39 | |
| `Ability.OnlyOpponentsTurn` | `[Opponent's Turn]` | 39 | |
| `Ability.Conditions` | `If ..., ` in front of a static ability | – | for auto effects the "If" goes on the step (`Option.StepConditions`) |

## Keywords

| Block | Card text | Notes |
|-------|-----------|-------|
| `Keyword.Rush` | `[Rush]` | can attack in the turn it is played |
| `Keyword.Blocker` | `[Blocker]` | |
| `Keyword.DoubleAttack` | `[Double Attack]` | 2 damage |
| `Keyword.Banish` | `[Banish]` | Life cards go to the trash |

Only keywords at the very start of a text count; `gains [Rush]` is `Step.GainFlag`.

## Costs

| Block | Card text | Scripts |
|-------|-----------|--------:|
| `Cost.ReturnDon` | `DON!! -2` (also `DON!! 2` without the minus) | 136 |
| `Cost.RestDon` | `(2)` / `rest 1 of your DON!! cards` | 82 |
| `Cost.RestThis` | `rest this Character` | 88 |
| `Cost.TrashThis` | `trash this Character` | 49 |
| `Cost.ReturnThis` | `return this Character to the owner's hand` | 4 |
| `Cost.TrashFromHand` | `trash 1 {X} type card from your hand` | (in `TrashFromHand`) |
| `Cost.RevealFromHand` | `reveal 1 {X} type card from your hand` | 20 |
| `Cost.LifeToHand` | `add 1 card from the top of your Life cards to your hand` | (in `LifeToHand`) |
| `Cost.TrashLife` | `trash 1 card from the top of your Life cards` | (in `TrashLife`) |
| `Cost.TurnLife` | `turn 1 card from the top of your Life cards face-up` | 12 |
| `Cost.TrashTopOfDeck` | `trash 2 cards from the top of your deck` | (in `TrashTopOfDeck`) |
| `Cost.RestOwnCards` | `rest 1 of your Characters` | 25 |
| `Cost.ReturnOwnCharacter` | `return 1 of your Characters to the owner's hand` | 16 |
| `Cost.TrashOwnCharacter` | `trash 1 of your Characters` | 7 |
| `Cost.TrashToDeckBottom` | `place 2 cards from your trash at the bottom of your deck` | 21 |

Known gap: `Cost.TurnLife` only checks the number of Life cards; face-up Life cards are not
shown differently yet.

## Conditions

| Block | Card text | Scripts |
|-------|-----------|--------:|
| `Condition.LeaderHasType` | `your Leader has the {X} type` / `your Leader's type includes {X}` | 202 |
| `Condition.LeaderIsName` | `your Leader is [Name]` | 65 |
| `Condition.LeaderIsMulticolored` | `your Leader is multicolored` | 17 |
| `Condition.LeaderColor` | `your Leader is green` | – |
| `Condition.LifeAtMost` / `LifeAtLeast` | `you have 2 or less/more Life cards` | 65 / 5 |
| `Condition.OpponentLifeAtMost` / `OpponentLifeAtLeast` | `your opponent has 3 or less/more Life cards` | 21 / 5 |
| `Condition.HandAtMost` / `HandAtLeast` | `you have 2 or less/more cards in your hand` | 32 / 4 |
| `Condition.OpponentHandAtMost` / `OpponentHandAtLeast` | `your opponent has 2 or less/more cards in their hand` | – / 12 |
| `Condition.DonOnFieldAtLeast` / `DonOnFieldAtMost` | `you have 3 or more/less DON!! cards on your field` | 24 / 7 |
| `Condition.OpponentDonOnFieldAtLeast` | `your opponent has 2 or more DON!! cards on their field` | 6 |
| `Condition.OpponentHasMoreDon` | `your opponent has more DON!! cards on their field than you` | 3 |
| `Condition.DonNotMoreThanOpponent` | `the number of DON!! cards on your field is equal to or less than ...` | 19 |
| `Condition.LifeLessThanOpponent` | `you have less Life cards than your opponent` | 8 |
| `Condition.TrashAtLeast` | `you have 10 or more cards in your trash` | 22 |
| `Condition.DeckAtMost` | `there are 9 or less cards in your deck` | 3 |
| `Condition.ThisIsRested` / `ThisIsActive` | `this Character is rested/active` | 3 / 2 |
| `Condition.GivenDonAtLeast` | `this Character has 2 or more DON!! cards given` | – |
| `Condition.CharactersAtLeast` / `CharactersAtMost` | `you have 2 or more/less {X} type Characters` | 45 / 1 |
| `Condition.HasCard` | `you have [Name]` / `there is a Character with ...` | 67 |
| `Condition.HasNoCard` | `you don't have [Name]` | 5 |
| `Condition.OpponentHasCard` | `your opponent has a/2 or more Character(s) with ...` | 15 |

## Actions

| Block | Card text | Scripts | Notes |
|-------|-----------|--------:|-------|
| `Step.Select` | `Select up to 1 ...` | 3 | only chooses; later steps say "that Character" |
| `Step.KO` | `K.O. up to 1 ...` | 234 | activates `[On K.O.]` |
| `Step.Rest` | `Rest up to 1 ...` | 122 | |
| `Step.SetActive` | `Set up to 1 ... as active` | 56 | |
| `Step.ReturnToHand` | `Return ... to the owner's hand` | 60 | given DON!! become rested |
| `Step.BottomOfDeck` / `TopOfDeck` | `Place ... at the bottom/top of the owner's deck` | 39 / 1 | |
| `Step.TrashCard` | `Trash up to 1 of your opponent's Characters ...` | 1 | no K.O. |
| `Step.PowerModifier` | `Give ... -2000 power` / `... gains +1000 power` | 472 | power can go below 0 |
| `Step.CostModifier` | `Give ... -3 cost` / `... gains +1 cost` | 96 | cost cannot go below 0 |
| `Step.GainFlag` | `... gains [Rush]`, `... cannot attack` ... | 511 | see Flags |
| `Step.GiveRestedDon` | `Give your Leader up to 1 rested DON!! card` | 60 | |
| `Step.SkipRefresh` | `... will not become active in your opponent's next Refresh Phase` | 16 | |
| `Step.Draw` | `Draw 2 cards` | 300 | |
| `Step.TrashFromHand` | `trash 1 card from your hand` | 251 | |
| `Step.OpponentTrashesFromHand` | `Your opponent trashes 1 card from their hand` | 14 | the opponent chooses |
| `Step.HandToDeckBottom` | `place 2 cards from your hand at the bottom of your deck` | 10 | |
| `Step.OpponentHandToDeckBottom` | `Your opponent places 1 card from their hand at the bottom ...` | 6 | the opponent chooses |
| `Step.TrashToHand` | `Add up to 1 ... from your trash to your hand` | 28 | |
| `Step.TrashTopOfDeck` | `Trash 2 cards from the top of your deck` | 41 | |
| `Step.ShuffleDeck` | `Shuffle your deck` | – | |
| `Step.LookAndReveal` | `Look at 5 cards ...; reveal up to 1 ... and add it to your hand` | 144 | |
| `Step.LookAndArrange` | `Look at 3 cards ... and place them at the top or bottom ...` | 15 | chosen cards go to the bottom |
| `Step.LifeToHand` | `Add 1 card from the top of your Life cards to your hand` | 50 | |
| `Step.TrashLife` | `Trash 1 card from the top of your (opponent's) Life cards` | 16 | |
| `Step.DeckTopToLife` | `Add up to 1 card from the top of your deck to the top of your Life cards` | 43 | |
| `Step.HandToLife` | `Add up to 1 card from your hand to the top of your Life cards` | 9 | |
| `Step.AddDonFromDeck` | `Add up to 1 DON!! card from your DON!! deck and rest it` | 96 | |
| `Step.SetDonActive` | `Set up to 2 of your DON!! cards as active` | 31 | |
| `Step.SetDonActiveAtEndOfTurn` | `... as active at the end of this turn` | 6 | done by the MatchRoom in the End Phase |
| `Step.ReturnDonToDeck` | `Return 1 DON!! card from your field to your DON!! deck` | 7 | |
| `Step.RestOpponentDon` | `Rest up to 1 of your opponent's DON!! cards` | 5 | |
| `Step.PlayCard` | `Play up to 1 ... from your hand/trash (rested)` | 176 | full Character area: only Stages |
| `Step.PlayThisCard` | `Play this card` | 118 | |
| `Step.ActivateOwnAbility` | `Activate this card's [Main] effect` | 74 | |
| `Step.NegateBlockers` | `Your opponent cannot activate [Blocker] during this turn/battle` | 3 | |
| `Step.NameAlias` | `Also treat this card's name as [X] ...` / `you may have any number of this card` | 9 | the second one is a deck rule only |

## Step options

| Block | Card text | Notes |
|-------|-----------|-------|
| `Option.Optional` | `You may ...` | Yes/No, but not when "up to" already allows choosing nothing |
| `Option.Rested` | `... rested` / `and rest it` | |
| `Option.StepConditions` | `Then, if ..., ...` / `[On Play] If ..., ...` | checked when the step resolves |
| `Option.PlayRevealed` | `reveal ... and play it` | |
| `Option.Side.Opponent` | `your opponent's Life cards`, `Your opponent returns ...` | |
| `Option.RestDestination.bottom` | `place the rest at the bottom of your deck` | default |
| `Option.RestDestination.top` | – | engine only; no card text produces it yet |
| `Option.RestDestination.trash` | `trash the rest` | |
| `Option.Zone.Hand` / `Trash` / `HandOrTrash` | `from your hand` / `from your trash` / `from your hand or trash` | |

Known gaps:
- `place the rest at the top or bottom of the deck` puts all cards at the bottom (the player
  cannot choose the top yet).
- "in any order" is not asked: the engine places the cards in a fixed order.
- `Trash up to 1 card from your hand` asks Yes/No first and then the card choice.
- `CardZone.Deck`, `CardZone.Life` exist in the model but are not used by the parser or the
  engine (`BlockCatalog.NotABlock`); a test checks that no script uses them.

## Durations

| Block | Card text | Scripts | Ends |
|-------|-----------|--------:|------|
| `Duration.ThisTurn` | `during this turn` | 2142 | end of the turn |
| `Duration.ThisBattle` | `during this battle` | 171 | end of the battle |
| `Duration.UntilEndOfOpponentsNextTurn` | `until the end of your opponent's next turn` / `until the start of your next turn` | 69 | end of the next turn |
| `Duration.Permanent` | no duration / static ability | 532 | when the card leaves the field |

## Flags

| Block | Card text | Scripts | Where it is checked |
|-------|-----------|--------:|---------------------|
| `Flag.CannotAttack` | `... cannot attack` | 32 | `Prohibitions.CanAttack` (wins over [Rush]) |
| `Flag.CannotBeKOdInBattle` | `... cannot be K.O.'d in battle` | 7 | `EffectEngine.KnockOut` |
| `Flag.CannotBeKOdByEffects` | `... cannot be K.O.'d by (your opponent's) effects` | 10 | only against the opponent's effects |
| `Flag.CannotBeRestedByEffects` | `... cannot be rested by your opponent's effects` | – | only against the opponent's effects |
| `Flag.CannotBeBlocked` | `[Unblockable]`, `cannot activate [Blocker] when this Character attacks` | 10 | MatchRoom block step |
| `Flag.CanAttackActiveCharacters` | `... can also attack active Characters` | 13 | `AttackAction` |
| `Flag.SkipNextRefresh` | (from `Step.SkipRefresh`) | 16 | `TurnPhases.Refresh` |
| `Flag.CannotBlock` | `... cannot be rested` (with `CannotAttack`) | 6 | `CardInstance.HasBlocker` |
| `Flag.RushCharacter` | `can attack Characters on the turn in which it is played` | 10 | `AttackAction` (not the Leader) |

## Targets

| Block | Card text |
|-------|-----------|
| `Target.Count` | `up to 2` |
| `Target.UpTo` | `up to N` (the player may choose fewer) |
| `Target.Exact` | `1 of your Characters`, `a Character` (must be chosen) |
| `Target.All` | `all of ...`, or a plural without a number (`your {X} type Characters`) |
| `Target.Self` | `this Character/Leader/card` |
| `Target.YourLeader` | `your Leader` |
| `Target.Previous` | `that Character`, `the selected/played/revealed card` |

## Filters

| Block | Card text | Scripts |
|-------|-----------|--------:|
| `Filter.Owner` | `your` / `your opponent's` / nothing (any player) | – |
| `Filter.AllowLeader` | `Leader or Character cards`, `your opponent's Leader` | 294 |
| `Filter.AllowCharacter` | `Character(s)` | – |
| `Filter.AllowStage` | `Stage(s)`, `card(s)` | 674 |
| `Filter.Category` | `Event card`, `Character card`, `Stage card` (hand/trash/deck) | 197 |
| `Filter.MaxCost` / `MinCost` | `with a cost of 3 or less/more`, `with a cost of 3` | 526 / 110 |
| `Filter.MaxPower` / `MinPower` | `with 4000 power or less/more` | 136 / 36 |
| `Filter.UseBaseValues` | `base cost`, `base power` | 63 |
| `Filter.Rested` | `rested` / `active` | – |
| `Filter.TypeIncludes` | `{Navy} type`, `{A} or {B} type` | 584 |
| `Filter.Name` | `[Nami]` | 190 |
| `Filter.NameNot` | `other than [Nami]` | 96 |
| `Filter.NameOrType` | `[Nami] or {Straw Hat Crew} type card` | 9 |
| `Filter.Color` | `green Character card` (multicolored cards have both colors) | 56 |
| `Filter.Attribute` | `<Slash> attribute` | 0 |
| `Filter.Keyword` | `[Blocker] Characters` | 2 |
| `Filter.ExcludeSource` | `other than this Character` | 12 |
| `Filter.RequiresTrigger` | `with a [Trigger]` | 24 |
| `Filter.MaxCostFrom` | `with a cost equal to or less than the number of DON!! cards on your field / your opponent's Life cards` | 8 |

## Adding a block

1. Add the enum value / property to `EffectModel.cs` and teach the parser and the engine.
2. Write a test with `[BlockTest("<block>")]` that uses the real card wording and checks both
   sides (works when it should, does nothing when it should not).
3. Add a row to this document. `BlockCoverageTests` fails until all three are done.
