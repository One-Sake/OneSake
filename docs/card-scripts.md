# Card scripts

Card effects are no longer parsed from the card text while a match runs. Every card has a
versioned JSON file with its script. The server loads all files once at start-up
(`CardScriptRegistry`) and looks them up by card ID.

```
backend/OneSake.Game/CardScripts/
  generated/<SET>/<CARD-ID>.json   written by the generator
  overrides/<SET>/<CARD-ID>.json   written by hand, always win over generated/
  schema/card-script.schema.json   JSON schema of the format
  coverage-baseline.json           minimum number of verified cards (CI)
```

The files are embedded into `OneSake.Game.dll` (see `OneSake.Game.csproj`), so a deploy needs
no extra files.

## Format

```json
{
  "cardId": "OP04-049",
  "status": "verified",
  "sourceTextHash": "<SHA-256 of the cleaned card text>",
  "parserVersion": "cardscriptgen-1",
  "notes": "optional",
  "script": {
    "abilities": [
      {
        "timing": "OnKO",
        "text": "[On K.O.] Draw 1 card.",
        "steps": [ { "kind": "Draw", "amount": 1, "...": "..." } ]
      }
    ],
    "unsupportedTexts": [],
    "failedParts": []
  }
}
```

`script` is the `CardScript` from `backend/OneSake.Game/Effects/EffectModel.cs`
(camelCase, enums as strings). The easiest way to write one by hand: let the generator create
the script for a similar text and copy the parts you need.

`sourceTextHash` is `CardScriptJson.HashText(cardText)`: the SHA-256 of the text after
`EffectParser.Clean` (reminder texts and whitespace do not count). If the card text in a deck
does not match the hash any more (errata), the card is played as **manual** and the server logs
a warning once.

## Status

| Status      | Meaning |
|-------------|---------|
| `verified`  | Checked by hand and covered by a test (`[CardTest("...")]`). |
| `generated` | The parser understood every part. Runs automatically, not checked by hand ("ungeprüft" in the card inspect overlay). |
| `manual`    | Not everything is automated. Parts in `unsupportedTexts` open the "Effekt bitte manuell ausführen" prompt; the other abilities in `abilities` still run automatically, exactly like before the files existed. A file with an empty `abilities` list is completely manual. |

A card without any file (for example a new set before the generator ran) is parsed at runtime
like before. It counts as `generated` and the server logs it once.

## Verify a card

1. Read the card text and the script file. Does every step match the text?
2. Add a test to `backend/OneSake.Tests/CardScriptTests.cs` with `[CardTest("OP01-016")]`.
   `CardScenario` builds a small game: `game.AddCharacter(game.P1, "OP01-016")`,
   `game.Trigger(card, EffectTiming.OnPlay)`, `game.Answer(EffectChoiceRequest.Select, id)`.
   Call `CardScenario.AssertVerifiedScript(card)` so the test really uses the file.
   The official card text for the test goes into `OneSake.Tests/TestData/card-script-cards.json`
   (copy the entry from the optcgapi data).
3. Set `"status": "verified"` in the file.
4. Increase `verified` in `coverage-baseline.json`.

`CardScriptCoverageTests` (runs in the CI) fails when a verified card has no test, a
`[CardTest]` points to a card that is not verified, a file cannot be loaded, or there are fewer
verified cards than the baseline. It also prints the share of each status.

## Write an override

Use an override when the parser gets a card wrong or cannot read it, but the effect can be
expressed with the existing steps.

1. Copy the generated file to `overrides/<SET>/<CARD-ID>.json` (keep `cardId` and
   `sourceTextHash`).
2. Fix the `script`. Set `parserVersion` to `"override"` and explain the reason in `notes`.
3. Write a test as above and set the status to `verified`.

The generator never writes into `overrides/`.

## Run the generator (new set, parser change)

```
dotnet run --project backend/OneSake.CardScriptGen -- --cards https://optcgapi.com/api/allSetCards/ --cards https://optcgapi.com/api/allSTCards/
```

Options:

- `--cards <file or URL>`: card data in the optcgapi format (`card_set_id`, `card_text`,
  `card_type`). Can be used more than once.
- `--set OP12`: only this set.
- `--only-missing`: only cards without a file (keeps all existing files).
- `--out <folder>`: a different `CardScripts` folder (default: found from the current folder).

Rules: `generated` when the parser understands everything, otherwise `manual` (the unsupported
texts go into `notes`). A `verified` file is only replaced when its card text changed; it then
goes back to `generated` and the generator prints the card ID. At the end the generator prints
the number of cards per status and set. Commit the data in its own commit.

## Check the card texts of a backend

In a match the browser loads every deck card from `GET /cards/{id}` and sends its text to the
server. A script only runs if that text has the same hash as the script file. The check does the
same request for every card script and reports what the game really does with each card:

```
dotnet run --project backend/OneSake.CardScriptGen -- --check-backend https://api.onesake.world
dotnet run --project backend/OneSake.CardScriptGen -- --check-texts cards.json --set OP04
```

It only reads (GET) and writes the report `card-text-check.md` into the current folder: counts
per result and set, every card with a different text (script text and delivered text), and the
cards without text. A backend that does not have a card in its database asks optcgapi for it,
like for the browser.
