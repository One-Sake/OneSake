# Handoff (Stand 21.11.2026)

Übergabe für die nächste Arbeitssitzung. Alles ist auf `origin/logic` und `origin/main`
(Merge `8fdadf0`). `dev` hat den `logic`-Stand noch **nicht**: `logic` ist 34 Commits vor `dev`.

## Arbeitsweise

- Regeln stehen in `AGENTS.md`: Regel-Engine-Arbeit auf `logic`, alles andere auf `dev`, `main` nur für
  stabile Stände. Kleine, getrennte Commits; nach Änderungen `dotnet build` / `dotnet test` (Release)
  und `npm run lint` / `npm run build` in `frontend/web`.
- Jede Commit-Nachricht endet mit den Zeilen
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` und
  `Co-Authored-By: dano-4 <bendradani@gmail.com>` (dano-4 hat die Effekt-Engine gebaut).
- Lokal läuft oft ein eigener Server (`OneSake.Server`, Debug), der die Debug-DLLs sperrt. Deshalb mit
  `-c Release` bauen und testen. Für Browser-Tests einen eigenen Server aus einer **Kopie** von
  `bin/Release` auf Port 5280 und Vite auf 5174 starten (`VITE_BACKEND_URL=http://localhost:5280`).
- Push auf `main` startet das Deployment (`.github/workflows/deploy.yml`).

## Sofort zu erledigen

1. **Produktion: Migration `AddMatchRecordings` anwenden.** Sie ist mit dem Merge auf `main` live
   gegangen. Ohne sie läuft das Spiel weiter, nur Aufzeichnungen und Fehlermeldungen werden nicht
   gespeichert (Warnung im Log). Befehl in `docs/deployment.md`:
   `dotnet ef database update --project OneSake.Persistence --startup-project OneSake.Server`
2. Optional `BugReports__AdminToken` auf dem Server setzen (sonst ist `GET /bugreports` aus).
3. Entscheiden, ob `logic` nach `dev` gemergt wird, damit `dev` nicht hinter `main` liegt.

## Was in dieser Sitzung gebaut wurde

| Bereich | Wo | Doku |
|---|---|---|
| Board: Feed-Splitter, Skalierung, Drag & Drop, Kartenrücken | `frontend/web/src/features/match/` | – |
| Manueller Modus (Kontextmenü, ⋯, Deck ansehen/durchsuchen, „Effekt manuell ausführen“) | `OneSake.Game/Actions/ManualActions.cs`, `board/Manual*.tsx` | – |
| Kartenskripte als geprüfte JSON-Daten (2493 Karten, 13 verified, Generator, Coverage-Test) | `OneSake.Game/CardScripts/`, `OneSake.CardScriptGen/` | `docs/card-scripts.md` |
| Regel-Engine: Events, Trigger-Reihenfolge (Regel 8-6), Regelverarbeitung (Regel 9), ORDER_TRIGGERS | `OneSake.Game/Rules/` | `docs/rules-open-questions.md` |
| Determinismus, Aufzeichnung, Replay, „Fehler melden“ | `OneSake.Game/Randomness`, `OneSake.Game/Replay`, `OneSake.ReplayTool/`, `/bugreports` | `docs/bug-reports.md` |
| Bots, Simulator, Invarianten, CI-Smoke + Nightly | `OneSake.Game/Simulation`, `OneSake.Server/Simulation`, `OneSake.Simulator/` | `docs/simulation-findings.md` |

Stand der Tests: 274 grün, 6 übersprungen (offene Regelfragen F1–F6). 5000 Simulator-Partien
(Meta-Decks) und 5000 (Zufallsdecks aus allen 2633 Karten) ohne Invariantenverletzung.

## Offene Punkte

- **Zurückgestellte Aufgabe „Karten der Top-20-Meta-Decks verified“** (CardWork-Tool: priorities,
  ai-draft mit Claude API offline, review/verify, gemeinsames Durchgehen der ersten 20–30 Karten).
  Die Voraussetzungen fehlten damals; Simulator und Meta-Decks gibt es jetzt
  (`backend/OneSake.Tests/Decks/`). Für den KI-Lauf muss `ANTHROPIC_API_KEY` gesetzt sein (oder
  `ant auth login`). Vor jedem bezahlten Lauf erst die Kostenschätzung zeigen und auf das OK warten.
- **Meta-Decks sind generiert**, keine echten Turnierlisten (Meta-Daten haben keine Decklisten).
  OP18-060 (Saint Gunko, Rang 6) fehlt in den Kartendaten; die optcgapi-Daten neu laden und den
  Generator laufen lassen (`docs/card-scripts.md`).
- **Simulator-Fund F3:** Cerberus zeigt `Blocker`, obwohl „cannot block“ aktiv ist (nur Anzeige).
- **Offene Regelfragen F1–F6** in `docs/rules-open-questions.md` (z. B. Reihenfolge neuer Effekte des
  Nicht-Zugspielers, Ersatz-Effekte „instead“ nicht automatisiert).
- **Nightly-Simulation** (`.github/workflows/simulation-nightly.yml`) läuft ab jetzt, weil die Datei
  auf `main` liegt; sie simuliert `logic` und `dev`.
- **Kleinigkeiten:** Doppelte React-Keys im MatchFeed im Dev-Modus (StrictMode, alt). Kartenrücken-
  Bilder `frontend/web/src/assets/cards/{card-back,don-back,don-front}.webp` fehlen noch (Fallback
  aktiv, README im Ordner).

## Nachtrag 21.11. (zweite Sitzung): Reconnect und Zug-Timer

Auf logic (noch nicht auf dev/main):

- Stabile Spieler-ID pro Platz + Rejoin-Token, RejoinRoom/CheckRejoin, Schonfrist 90 s, Budget 180 s.
- Serverseitiger Zug-Timer mit Zeitbank, automatische Entscheidungen bei Ablauf, Endgrund TIMEOUT.
- Regeln: docs/turn-timer.md, Konfiguration: docs/deployment.md (Abschnitt „Reconnect und Zug-Timer“).
- Bekannt, nicht behoben: Entscheidet der Würfelgewinner sehr schnell, bleibt beim Gegner das
  Würfel-Overlay stehen (DiceRoll.tsx, Animation überschreibt den Schritt „announce“).
- Ein Server-Neustart beendet laufende Matches weiterhin (Plätze liegen nur im Speicher).

## Nützliche Befehle

```bash
# Backend
dotnet build backend/OneSake.sln -c Release
dotnet test backend/OneSake.Tests/OneSake.Tests.csproj -c Release

# Simulator
dotnet run --project backend/OneSake.Simulator -c Release -- --games 5000 --bots random,greedy --decks meta,random --seed 1 --parallel 4 --out sim-failures

# Replay einer Meldung / eines Simulator-Fehlers
dotnet run --project backend/OneSake.ReplayTool -- export --report 123 --connection "<conn>"
dotnet run --project backend/OneSake.ReplayTool -- run backend/OneSake.Tests/Replays/bug-123.json --until 47 --print
dotnet run --project backend/OneSake.ReplayTool -- import sim-failures/<datei>.json

# Kartenskripte neu erzeugen
dotnet run --project backend/OneSake.CardScriptGen -- --cards https://optcgapi.com/api/allSetCards/ --cards https://optcgapi.com/api/allSTCards/
```
