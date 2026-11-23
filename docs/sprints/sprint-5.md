# Sprint 5 – Regel-Engine, Stabilität, Spielgefühl und Konten

**Zeitraum:** 20.11.2026 – 23.11.2026 (Abschluss mit dem DA-Review) · **Status:** abgeschlossen · **Übersicht:** [#107](https://github.com/One-Sake/OneSake/issues/107)

## Sprintziel

Matches laufen regelkonform, reproduzierbar und stabil (Reconnect, Zug-Timer), fühlen sich gut an
(Animationen, Sounds, Tastenkürzel), und Spieler können ein Konto anlegen.

## Ergebnis

Sprintziel erreicht. Regel-Engine nach den offiziellen Regeln (Trigger-Reihenfolge 8-6, Regelverarbeitung 9),
deterministische Aufzeichnung und Replay, Bot-Simulation in der CI (200 Partien pro Push, 20.000 pro Nacht),
Reconnect, Zug-Timer, Animationen, Sounds, Login mit Wiederherstellungscode und die echte DON!!-Karte.
Rund 300 Backend-Tests sind grün, 6 Tests sind wegen offener Regelfragen bewusst übersprungen
(`docs/rules-open-questions.md`).

Die Regel-Engine wurde auf dem Branch `logic` entwickelt (Ausnahme in `AGENTS.md`) und am 21.11. über `dev`
nach `main` gebracht.

**Story Points erledigt:** 91

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| US71 | [#87](https://github.com/One-Sake/OneSake/issues/87) | Karten per Drag & Drop spielen und ein Brett, das sich jeder Fenstergröße anpasst | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US72 | [#88](https://github.com/One-Sake/OneSake/issues/88) | Effekte, die der Server nicht automatisch kann, manuell ausführen können | Frontend, Backend | WoWoWocal | 8 | ✅ erledigt |
| US73 | [#89](https://github.com/One-Sake/OneSake/issues/89) | Kartenskripte als geprüfte, versionierte JSON-Daten haben | Backend | WoWoWocal | 8 | ✅ erledigt |
| US74 | [#90](https://github.com/One-Sake/OneSake/issues/90) | Gleichzeitige Effekte in der richtigen Reihenfolge nach den offiziellen Regeln ablaufen | Backend | WoWoWocal | 13 | ✅ erledigt |
| US75 | [#91](https://github.com/One-Sake/OneSake/issues/91) | Jedes Match deterministisch aufzeichnen und wieder abspielen können | Backend, Datenbank | WoWoWocal | 8 | ✅ erledigt |
| US76 | [#92](https://github.com/One-Sake/OneSake/issues/92) | Im Match einen Fehler melden können | Frontend, Backend | WoWoWocal | 3 | ✅ erledigt |
| US77 | [#93](https://github.com/One-Sake/OneSake/issues/93) | Bots tausende Matches spielen lassen | Backend | WoWoWocal | 13 | ✅ erledigt |
| US12 | [#24](https://github.com/One-Sake/OneSake/issues/24) | Spieländerungen in Echtzeit / Reconnect (Backend) | Backend | WoWoWocal | – | ✅ erledigt |
| US47 | [#60](https://github.com/One-Sake/OneSake/issues/60) | Zurück ins Match nach Verbindungsabbruch | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US78 | [#94](https://github.com/One-Sake/OneSake/issues/94) | Einen Zug-Timer mit Zeitbank | Frontend, Backend | WoWoWocal | 5 | ✅ erledigt |
| US79 | [#95](https://github.com/One-Sake/OneSake/issues/95) | Animationen für Ziehen, Spielen, Angriffe und K.O.s sehen | Frontend | WoWoWocal | 8 | ✅ erledigt |
| US80 | [#96](https://github.com/One-Sake/OneSake/issues/96) | Sounds und Tastenkürzel im Match | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US52 | [#65](https://github.com/One-Sake/OneSake/issues/65) | Mit Konto oder als Gast anmelden | Backend, Datenbank | 13-Tino-13 | 8 | ✅ erledigt |
| US81 | [#97](https://github.com/One-Sake/OneSake/issues/97) | Auf dem Spielbrett die echte DON!!-Karte sehen | Datenbank, Frontend | 13-Tino-13 | 2 | ✅ erledigt |
| US82 | [#98](https://github.com/One-Sake/OneSake/issues/98) | Unsere Arbeit laufend für die Diplomarbeit dokumentieren | Doku | WoWoWocal | 2 | ✅ erledigt |

Zu US12 ([#24](https://github.com/One-Sake/OneSake/issues/24)), US47 ([#60](https://github.com/One-Sake/OneSake/issues/60)) und US52 ([#65](https://github.com/One-Sake/OneSake/issues/65)): Umsetzung `7fe00cf`, `caccf72`, `6f02753`
bzw. `4fa812c` (13-Tino-13), Details in den Kommentaren der Issues.

## Stories im Detail

### US71 – [#87](https://github.com/One-Sake/OneSake/issues/87)

Als Spieler möchte ich Karten per Drag & Drop spielen und ein Brett, das sich jeder Fenstergröße anpasst, damit sich das Spielen natürlich anfühlt

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 20.11.2026 · **Commits:** 1e38b5f, 101bcf8, dd0004b, f9f890b, d06c5bc

Akzeptanzkriterien:

- [x] Brett misst seinen Rahmen und wählt die größte passende Kartengröße; Beschriftungen sind größer und lesbarer
- [x] Handkarten und DON!! per Drag & Drop (Maus, Stift, Touch) auf gültige Ziele, gültige Ziele leuchten
- [x] Log/Chat-Leiste mit Splitter in der Breite verstellbar und einklappbar (Badge für ungelesenen Chat)
- [x] Eigene Kartenrückseiten für Deck, Leben und DON!! mit neutralem Fallback

### US72 – [#88](https://github.com/One-Sake/OneSake/issues/88)

Als Spieler möchte ich Effekte, die der Server nicht automatisch kann, manuell ausführen können, damit jede Karte spielbar ist

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 20.11.2026 · **Commits:** 1b616eb, fdba347, e0157b3, 4a717de

Akzeptanzkriterien:

- [x] Hub-Methode ManualAction: ziehen, Karten verschieben, K.O., Power/Kosten ändern, DON!!, Deck ansehen/durchsuchen/mischen u. a.
- [x] Nur der Spieler am Zug oder mit offener Entscheidung darf manuell handeln, gegnerische Karten nur auf dem Feld
- [x] Jede manuelle Aktion wird als MANUAL geloggt, ohne verdeckte Karten zu verraten
- [x] Kontextmenü, „⋯“-Button und Schalter „Manuell“ im Frontend, Dialoge für Deck ansehen und durchsuchen
- [x] Manuelle Aktionen sind im Feed hervorgehoben; [On K.O.] läuft auch nach einem manuellen K.O.

### US73 – [#89](https://github.com/One-Sake/OneSake/issues/89)

Als Entwickler möchte ich Kartenskripte als geprüfte, versionierte JSON-Daten haben, damit Effekte nachvollziehbar und testbar sind

- **Bereich:** Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 20.11.2026 · **Commits:** bc88e33, e50a969, 7e73cb6, 5130999, 0126186, 5e2f26d

Akzeptanzkriterien:

- [x] Generator OneSake.CardScriptGen erzeugt Skripte für alle Sets (2493 Karten, Text der Basis-Version)
- [x] CardScriptRegistry lädt Skripte nach Karten-ID, Overrides vor generierten Dateien; bei geändertem Text (Errata) wird die Karte manuell
- [x] Testhelfer CardScenario / [CardTest], erste Karten auf verified gesetzt
- [x] Coverage-Test läuft in der CI; Doku in docs/card-scripts.md

### US74 – [#90](https://github.com/One-Sake/OneSake/issues/90)

Als Spieler möchte ich, dass gleichzeitige Effekte in der richtigen Reihenfolge nach den offiziellen Regeln ablaufen, damit komplexe Züge korrekt aufgelöst werden

- **Bereich:** Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 13
- **Zeitraum:** 20.11.2026 · **Commits:** 688692f, 4e989e0, 95c2807, 3777a23, c8f1653, cde7e24, 636435d

Akzeptanzkriterien:

- [x] Spielereignisse und Event-Bus (CardPlayed, CharacterKOd, AttackDeclared, DamageDealt …)
- [x] TriggerCollector, TriggerQueue und EffectResolver nach Regel 8-6 (Spieler am Zug zuerst, eigene Reihenfolge wählbar)
- [x] Regelverarbeitung nach Regel 9 (Niederlage bei 0 Karten / Schaden bei 0 Leben), Effektdauern und Verbote zentral
- [x] Prompt ORDER_TRIGGERS mit Sortier-Overlay im Client
- [x] Regel-Tests; offene Regelfragen F1–F6 dokumentiert (docs/rules-open-questions.md)

### US75 – [#91](https://github.com/One-Sake/OneSake/issues/91)

Als Entwickler möchte ich jedes Match deterministisch aufzeichnen und wieder abspielen können, damit Fehler reproduzierbar sind

- **Bereich:** Backend, Datenbank · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 20.11.2026 – 21.11.2026 · **Commits:** cfdd0c4, ee8c1f1, 5478556, 2da4d2f

Akzeptanzkriterien:

- [x] Jedes Match bekommt einen geheimen Seed (PCG32, eigene Zufalls-Streams pro Zweck)
- [x] Aufzeichnung aller gültigen Eingaben mit State-Hash, nach dem Match gzip-komprimiert gespeichert (30 Tage)
- [x] ReplayRunner und ReplayTool spielen Aufzeichnungen nach und melden Abweichungen
- [x] Replay-Regressionstests; Migration AddMatchRecordings

### US76 – [#92](https://github.com/One-Sake/OneSake/issues/92)

Als Spieler möchte ich im Match einen Fehler melden können, damit das Team falsche Regeln schnell findet

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 3
- **Zeitraum:** 20.11.2026 – 21.11.2026 · **Commits:** 799d9f0, d4adf12, e4641d0

Akzeptanzkriterien:

- [x] Button „Fehler melden“ in der Aktionsleiste und auf dem Spielende-Screen
- [x] Kategorie, Beschreibung (max. 1000 Zeichen) und optional die betroffene Karte; der aktuelle Schritt wird mitgeschickt
- [x] POST /bugreports speichert die Meldung mit der Aufzeichnung, max. 5 Meldungen pro Stunde
- [x] GET /bugreports nur mit Admin-Token; Doku in docs/bug-reports.md

### US77 – [#93](https://github.com/One-Sake/OneSake/issues/93)

Als Entwickler möchte ich Bots tausende Matches spielen lassen, damit Regelfehler automatisch gefunden werden

- **Bereich:** Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 13
- **Zeitraum:** 21.11.2026 · **Commits:** 125fafe, 915e460, 8ad9192, dedab57, caadfb1, 1cd2ed9, f10fbf2, 7e149b1

Akzeptanzkriterien:

- [x] RandomBot und GreedyBot bekommen denselben Zustand wie echte Spieler, keine Abkürzungen an der Spiellogik vorbei
- [x] InvariantChecker prüft nach jedem Schritt (Kartenerhaltung, 10 DON!!, max. 5 Characters …)
- [x] Simulator-Konsole mit Meta-Decks und Zufallsdecks
- [x] CI: 200 Partien als Smoke-Test, nächtlich 20.000 Partien (simulation-nightly.yml)
- [x] Gefundene Fehler behoben (Rush: Character, angesehene Karten bleiben im Deck); Doku docs/simulation-findings.md

### US78 – [#94](https://github.com/One-Sake/OneSake/issues/94)

Als Spieler möchte ich einen Zug-Timer mit Zeitbank, damit niemand ein Match endlos aufhalten kann

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 21.11.2026 · **Commits:** e360ca5, 25c0420, 3498ba5

Akzeptanzkriterien:

- [x] Serverseitig: 90 s pro Zug, 25 s für Antworten, 30 s Mulligan, danach persönliche Zeitbank (180 s, +10 s pro Zug)
- [x] Bei Ablauf entscheidet der Server automatisch; zwei Züge ohne Zeit verlieren mit Endgrund TIMEOUT
- [x] Ring mit Sekunden an beiden Namensschildern, Farbwechsel und Ticken kurz vor Ablauf
- [x] Eigene Tische: Timer wählbar (Standard / Entspannt / Aus); Regeln in docs/turn-timer.md

### US79 – [#95](https://github.com/One-Sake/OneSake/issues/95)

Als Spieler möchte ich Animationen für Ziehen, Spielen, Angriffe und K.O.s sehen, damit ich verstehe, was gerade passiert

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 21.11.2026 · **Commits:** ae0d571, 1184834, f51ee26, 9c315a0, 484dfec

Akzeptanzkriterien:

- [x] Animations-Ebene mit Warteschlange über dem Brett, der Spielstand wird immer sofort gezeigt
- [x] Animationen für Ziehen, Spielen, Events, Angriff, Block, Counter, Treffer, K.O., Leben, DON!! und Power-Änderungen
- [x] Zugwechsel-Banner „DEIN ZUG“ / „ZUG DES GEGNERS“
- [x] Neuer Spielende-Screen mit Endgrund, Kopfgeld-Änderung, Revanche-Tisch und Zurück zur Lobby
- [x] Einstellung Normal / Schnell / Aus, aus bei prefers-reduced-motion

### US80 – [#96](https://github.com/One-Sake/OneSake/issues/96)

Als Spieler möchte ich Sounds und Tastenkürzel im Match, damit ich schneller und angenehmer spielen kann

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 21.11.2026 · **Commits:** eede1b1, 9075631

Akzeptanzkriterien:

- [x] Selbst erzeugte Sounds (Web Audio, keine Audiodateien) mit Lautstärke und Stummschalten
- [x] Tastenkürzel: Leertaste, A, D, E, Tab, 1–9, Enter, Esc und „?“ für die Übersicht
- [x] Kein-Blocker-Schritt wird automatisch übersprungen, „Auto-Pass Counter“ optional
- [x] „Zug beenden“ leuchtet, wenn keine Aktion mehr möglich ist

### US81 – [#97](https://github.com/One-Sake/OneSake/issues/97)

Als Spieler möchte ich auf dem Spielbrett die echte DON!!-Karte sehen, damit das Brett wie am echten Tisch aussieht

- **Bereich:** Datenbank, Frontend · **Verantwortlich:** 13-Tino-13 · **Story Points:** 2
- **Zeitraum:** 23.11.2026 · **Commits:** 885ebcd, 1ace0ee

Akzeptanzkriterien:

- [x] Import der Standard-DON!!-Karte von optcgapi (POST /cards/import/don), abrufbar über GET /cards/don
- [x] DON!! erscheint nicht mehr in den Set-Listen des Deckbuilders
- [x] Das Brett nutzt das Bild als DON!!-Vorderseite (aktiv, gerastet, Drag-Ghost); ohne API bleibt der Platzhalter

### US82 – [#98](https://github.com/One-Sake/OneSake/issues/98)

Als Team möchten wir unsere Arbeit laufend für die Diplomarbeit dokumentieren, damit beim Schreiben nichts verloren geht

- **Bereich:** Doku · **Verantwortlich:** WoWoWocal · **Story Points:** 2
- **Zeitraum:** 20.11.2026 – 21.11.2026 · **Commits:** cccae15, 75ed5d8, 85df829, ad3d2f2

Akzeptanzkriterien:

- [x] Eigener Ordner pro Person unter docs/diplomarbeit/ (Projekttagebuch, Ideen und Entscheidungen, Screenshots)
- [x] AGENTS.md verlangt die Doku in jedem Commit
- [x] Roadmap und Alleinstellungsmerkmale sowie Übergabe-Notiz (docs/handoff.md) gespeichert

## Retrospektive

- **Gut:** Der Simulator hat echte Regelfehler gefunden, bevor Spieler sie bemerkt haben (`docs/simulation-findings.md`).
- **Gut:** Reconnect und Zug-Timer machen Ranked fair; „Fehler melden“ liefert reproduzierbare Aufzeichnungen.
- **Schlecht:** Das Deployment meldete Erfolg, obwohl `dotnet publish` auf dem Server fehlschlug (älteres .NET-8-SDK).
  Behoben in `f106e80`; das Deploy-Skript bricht aber noch nicht bei Fehlern ab (→ Sprint 6, [#27](https://github.com/One-Sake/OneSake/issues/27)).
- **Schlecht:** Sehr viel Umfang in wenigen Tagen, Merge `logic` ↔ `dev` mit Konflikten im `MatchBoard.tsx`.
- **Maßnahme:** `logic` regelmäßig mit `dev` abgleichen; nach jedem Merge gemeinsam testen.
