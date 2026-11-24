# Simulation: Ablauf und Funde

Bots spielen automatisch Partien gegeneinander, über denselben Weg wie echte Spieler (Prompts
und Aktionen des `MatchRoom`). Nach jedem Schritt prüft der `InvariantChecker` die Grundregeln.
Jede Verletzung wird als Aufzeichnung gespeichert und lässt sich exakt nachspielen.

## Ausführen

```bash
dotnet run --project backend/OneSake.Simulator -c Release -- --games 5000 --bots random,greedy --decks meta,random --seed 1 --parallel 4 --out sim-failures
```

| Option | Bedeutung |
|---|---|
| `--bots random,greedy` | `RandomBot` (zufällig unter allen gültigen Entscheidungen), `GreedyBot` (teuerste Karte, DON!! auf den Leader, Angriff bei genug Power, Block bei tödlichem Angriff, Counter bei wenig Life) |
| `--decks meta,random` | `meta`: Decks aus `backend/OneSake.Tests/Decks/*.txt`, `random`: zufällige gültige Decks (Leader + 50 Karten seiner Farben) |
| `--cards <datei>` | Kartendaten im optcgapi-Format. Standard: `ONESAKE_CARDS_JSON`, sonst die Fixture `backend/OneSake.Tests/Decks/cards-fixture.json` |
| `--determinism-every 50` | Jede 50. Partie wird ein zweites Mal gespielt; der Endzustand muss gleich sein |

Jede Partie N folgt aus `--seed` und N. Bei Fehlern entsteht `sim-failures/<datum>-<seed>.json`
(Invariante, seq, letzte 20 Log-Zeilen, Aufzeichnung). Exit-Code 1, wenn es Fehler gab.

**Fehler nachspielen und als Test ablegen:**

```bash
dotnet run --project backend/OneSake.ReplayTool -- run sim-failures/20261121-1000005.json --until 47 --print
dotnet run --project backend/OneSake.ReplayTool -- import sim-failures/20261121-1000005.json
# -> backend/OneSake.Tests/Replays/sim-1000005.json, danach wie in docs/bug-reports.md (expect-Datei)
```

**CI:** Der Schritt „Simulation smoke“ spielt 200 Partien (Seed 1, Fixture-Decks) und schlägt bei
jeder Verletzung fehl. `simulation-nightly.yml` spielt jede Nacht 20.000 Partien für `logic` und
`dev`. GitHub startet geplante Workflows nur vom Standard-Branch: Die Datei muss also auf `main`
liegen, damit der Zeitplan läuft. Manuell geht es jederzeit über „Run workflow“.

## Invarianten

Kartenerhaltung (Leader + 50 pro Spieler, inklusive einer `[Trigger]`-Karte während ihrer
Auflösung), jede Karte nur einmal, 10 DON!! pro Spieler, keine negativen DON!!, höchstens 5
Characters, keine negativen Kosten (negative Power ist erlaubt, Regel 1-3-6-1), der Leader bleibt im
Leader-Bereich, gültige Phase und gültiger Spieler am Zug, Prompts gehören einem Spieler und haben
Optionen, nach Spielende nichts offen, während des Spiels immer ein offener Prompt. Außerdem prüft
der Simulator: keine Exception, kein Effekt-Abbruch (`EFFECT_ERROR`), kein Hängen (100 Züge /
5000 Schritte), nach Spielende wird nichts mehr angenommen, Determinismus (Stichprobe).

## Statistik (lokal, 21.11.2026)

| Lauf | Partien | Partien/s | Fehler | Ø Länge | abgelehnte Bot-Entscheidungen |
|---|---|---|---|---|---|
| vor den Fixes, Meta+Zufall, Seed 1 | 200 | 22 | 73 (alle Kartenerhaltung) | – | 9 |
| nach F1, Meta+Zufall, Seed 1 | 5000 | 27 | 0 | 13,9 Züge, 110 Entscheidungen | 470 (alle F2) |
| nach F1+F2, Meta+Zufall, Seed 1 | 5000 | 25 | 0 | 13,9 Züge, 110 Entscheidungen | 0 |
| nach F1+F2, Zufallsdecks aus allen 2633 Karten, Seed 2 | 5000 | 30 | 0 | 12,7 Züge, 94 Entscheidungen | 1 (F3) |

In 100 % der Partien kommen Karten mit Status „manual“ vor. Die Bots klicken dort nur „Erledigt“,
solche Effekte passieren in der Simulation also nicht.

## Funde

| Nr. | Invariante | Karte(n) | Ursache | Status |
|---|---|---|---|---|
| F1 | Kartenerhaltung (73 von 200 Partien) | alle „Look at N cards … reveal up to 1“-Effekte, z. B. OP09-057 Cross Guild | Die angesehenen Karten wurden aus dem Deck genommen, bis der Spieler gewählt hatte. Laut Regel 11-3-2 bleiben sie im Deck. | **behoben** (`7e149b1`, Tests `FindingRegressionTests`) |
| F2 | abgelehnte Entscheidungen (95 von 95) | Characters mit `[Rush: Character]`, z. B. Shiki | Im Zug des Ausspielens zeigte die Karte „kann angreifen“, und das Board bot den gegnerischen Leader als Ziel an, den der Server ablehnt (Regel 10-1-6-1). Echte Spieler bekamen dieselbe Fehlermeldung. | **behoben** (`f10fbf2`, neues Feld `CanAttackOnlyCharacters`, Test `RushCharacterRegressionTests`) |
| F3 | abgelehnte Entscheidung (1 von 5000 Partien) | Cerberus | Die Karte zeigt das Keyword `Blocker`, obwohl ein „cannot block“-Effekt aktiv ist; der Server lehnt den Block ab. Nur eine Anzeige-Frage, keine Regelverletzung. | offen |

## Grenzen

- Die Meta-Decks sind automatisch aus Leader-Farbe und -Typ zusammengestellt, keine echten
  Turnierlisten (die Meta-Daten enthalten keine Decklisten). Echte Listen einfach als `.txt` im
  gleichen Format ablegen.
- Es gibt keine Minimierung: Eine fehlerhafte Aufzeichnung wird nicht automatisch gekürzt.
- Die Invarianten finden Zustandsfehler, aber keine falschen Effekte. Ob eine Karte das Richtige
  tut, prüfen die Kartentests (`docs/card-scripts.md`).
