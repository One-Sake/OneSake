# Sprint 4 – Quick Match, Ranked, Meta und Effekt-Engine

**Zeitraum:** 13.11.2026 – 19.11.2026 · **Status:** abgeschlossen · **Übersicht:** [#106](https://github.com/One-Sake/OneSake/issues/106)

## Sprintziel

OneSake wird zu einer Online-Plattform: Spieler finden per Quick Match einen Gegner, Ergebnisse werden
gespeichert und gewertet (Kopfgeld), der Meta-Tab zeigt aktuelle Statistiken, und Karteneffekte laufen
automatisch.

## Ergebnis

Sprintziel erreicht. Alle Stories sind umgesetzt und in der CI getestet (Release v0.4.0 und v0.5.0 in
„Was ist neu“). Die Effekt-Engine wird in Sprint 5 auf dem Branch `logic` zur vollständigen Regel-Engine
ausgebaut.

**Story Points erledigt:** 63

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| US60 | [#76](https://github.com/One-Sake/OneSake/issues/76) | Eine Lobby im TCG-Stil mit meinem Leader im Mittelpunkt | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US46 | [#59](https://github.com/One-Sake/OneSake/issues/59) | Deck als Text exportieren und importieren (OPTCGSim-Format) | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US61 | [#77](https://github.com/One-Sake/OneSake/issues/77) | Eine feste Gast-ID in meinem Browser haben | Frontend, Backend | WoWoWocal | 2 | ✅ erledigt |
| US57 | [#70](https://github.com/One-Sake/OneSake/issues/70) | Match-Ergebnisse speichern, Statistik-Endpoint | Datenbank, Backend | WoWoWocal | 3 | ✅ erledigt |
| US62 | [#78](https://github.com/One-Sake/OneSake/issues/78) | In meinem Profil echte Statistiken sehen | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US63 | [#79](https://github.com/One-Sake/OneSake/issues/79) | Mit einem Klick per Quick Match einen Gegner finden | Frontend, Backend | WoWoWocal | 5 | ✅ erledigt |
| US64 | [#80](https://github.com/One-Sake/OneSake/issues/80) | Sehen, was in der neuen Version dazugekommen ist | Frontend | WoWoWocal | 1 | ✅ erledigt |
| US65 | [#81](https://github.com/One-Sake/OneSake/issues/81) | Alle Seiten einheitlich aussehen | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US66 | [#82](https://github.com/One-Sake/OneSake/issues/82) | Ein Würfelwurf entscheidet, wer anfangen darf | Frontend, Backend | WoWoWocal | 5 | ✅ erledigt |
| US67 | [#83](https://github.com/One-Sake/OneSake/issues/83) | Quick Matches gewertet werden (ELO) | Backend, Datenbank | WoWoWocal | 5 | ✅ erledigt |
| US68 | [#84](https://github.com/One-Sake/OneSake/issues/84) | Meinen Steckbrief mit Kopfgeld und eine Kopfgeld-Rangliste sehen | Frontend, Backend | WoWoWocal | 5 | ✅ erledigt |
| US69 | [#85](https://github.com/One-Sake/OneSake/issues/85) | Aktuelle Meta-Statistiken zu Leadern und Matchups sehen | Frontend, Backend | WoWoWocal | 8 | ✅ erledigt |
| US70 | [#86](https://github.com/One-Sake/OneSake/issues/86) | Karteneffekte automatisch ausgeführt werden | Backend | danielbna | 13 | ✅ erledigt |

Zu US46 ([#59](https://github.com/One-Sake/OneSake/issues/59)) und US57 ([#70](https://github.com/One-Sake/OneSake/issues/70)): Akzeptanzkriterien im Issue, Umsetzung `ed24cda` bzw. `0e417b0`.

## Stories im Detail

### US60 – [#76](https://github.com/One-Sake/OneSake/issues/76)

Als Spieler möchte ich eine Lobby im TCG-Stil mit meinem Leader im Mittelpunkt, damit ich schnell und mit Freude ein Match starte

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** b65b1a5

Akzeptanzkriterien:

- [x] Leader groß mit 3D-Neigung und Glanz, die Lobby färbt sich nach den Leader-Farben
- [x] Deck-Karussell mit Leader-Bildern und Kachel zur Deck-Bibliothek
- [x] Namensschild mit Würfel für zufällige Piratennamen
- [x] Ein goldener Haupt-Button (Erstellen / Warten / Starten / Zum Brett) mit einer Zeile Begründung
- [x] Offene Tische als Kacheln, freundlicher Leer-Zustand, ein Verbindungspunkt, rote Leiste bei Fehlern
- [x] Untere Navigation mit Icons, reduzierte Bewegung wird beachtet

### US61 – [#77](https://github.com/One-Sake/OneSake/issues/77)

Als Spieler möchte ich eine feste Gast-ID in meinem Browser haben, damit meine Ergebnisse mir zugeordnet werden, auch ohne Konto

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 2
- **Zeitraum:** 18.11.2026 · **Commits:** 28b1aa8

Akzeptanzkriterien:

- [x] Der Browser erzeugt einmal eine Gast-ID (crypto.randomUUID mit Fallback) und speichert sie in localStorage
- [x] Die ID wird beim Verbinden mitgeschickt, der Server akzeptiert nur GUIDs
- [x] Alte Clients ohne Gast-ID funktionieren weiter
- [x] Dokumentiert: Die Gast-ID ist keine sichere Anmeldung

### US62 – [#78](https://github.com/One-Sake/OneSake/issues/78)

Als Spieler möchte ich in meinem Profil echte Statistiken sehen, damit ich meinen Fortschritt verfolgen kann

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 3
- **Zeitraum:** 18.11.2026 · **Commits:** 0e417b0

Akzeptanzkriterien:

- [x] Profil zeigt Decks, Matches, Siege, Winrate und Lieblings-Leader
- [x] Letzte Matches mit Leader-Bildern, Balken pro Leader
- [x] Lade-, Leer- und Fehlerzustände; Berries ausgegraut (noch nicht verfügbar)

### US63 – [#79](https://github.com/One-Sake/OneSake/issues/79)

Als Spieler möchte ich mit einem Klick per Quick Match einen Gegner finden, damit ich nicht nach offenen Tischen suchen muss

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** a7ff8a5

Akzeptanzkriterien:

- [x] Warteschlange paart die ersten zwei wartenden Spieler, keine doppelten Einträge, nie gegen sich selbst
- [x] Hub-Methoden JoinQueue / LeaveQueue, bei einem Paar wird ein privater Raum erstellt (MatchFound)
- [x] Beim Trennen oder Betreten eines Raums verlässt der Spieler die Warteschlange
- [x] Lobby: Suchzustand mit Timer und Abbrechen; Quick-Match-Räume sind nicht in der öffentlichen Liste
- [x] Tests in MatchmakingQueueTests

### US64 – [#80](https://github.com/One-Sake/OneSake/issues/80)

Als Spieler möchte ich sehen, was in der neuen Version dazugekommen ist, damit ich neue Funktionen entdecke

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 1
- **Zeitraum:** 18.11.2026 · **Commits:** 99e535f

Akzeptanzkriterien:

- [x] Link „v0.x · Was ist neu?“ in der Lobby öffnet ein Fenster mit dem Changelog
- [x] Ein Punkt markiert ungelesene Neuigkeiten (gesehene Version in localStorage)
- [x] Einträge liegen typisiert in content/changelog.ts

### US65 – [#81](https://github.com/One-Sake/OneSake/issues/81)

Als Spieler möchte ich, dass alle Seiten einheitlich aussehen, damit sich OneSake wie eine fertige App anfühlt

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** 74459f9, c276b5f, b6b0f1a, f78d27b

Akzeptanzkriterien:

- [x] Gemeinsames Design-System styles/ui.css (Rahmen, Glas-Panel, Überschrift, Buttons, Kacheln, Toast) und gemeinsame Icons
- [x] Tools, Profil, Startbildschirm und Deckbuilder nutzen das Design-System
- [x] Deckbuilder: nur optische Änderungen, Layout-Maße und Playwright-Funktionstest unverändert
- [x] Mulligan-Trainer zeigt Kartenbilder

### US66 – [#82](https://github.com/One-Sake/OneSake/issues/82)

Als Spieler möchte ich, dass ein Würfelwurf entscheidet, wer anfangen darf, damit die Startreihenfolge fair und nachvollziehbar ist

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** a47ff56, 442676b, 0cde367

Akzeptanzkriterien:

- [x] Neue Phase TurnOrder zwischen Lobby und Mulligan, der Server würfelt (bei Gleichstand erneut)
- [x] Der Gewinner wählt GO_FIRST oder GO_SECOND, erst danach werden die Starthände gezogen
- [x] 3D-Würfel-Animation mit deterministischer Physik aus dem Server-Seed, das Server-Ergebnis liegt immer oben
- [x] Ohne Animation bei prefers-reduced-motion, optionaler Sound
- [x] Test-Hook Match:FixedDiceRolls nur in Development

### US67 – [#83](https://github.com/One-Sake/OneSake/issues/83)

Als Spieler möchte ich, dass Quick Matches gewertet werden (ELO), damit ich mich mit anderen messen kann

- **Bereich:** Backend, Datenbank · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** b8ad80e

Akzeptanzkriterien:

- [x] MatchResult speichert IsRanked, Startspieler und Rating vorher/nachher; neue Tabelle PlayerRatings
- [x] ELO mit K 40 für die ersten 20 Spiele, danach 24, Mindestwert 100, Kopfgeld = Rating × 1000
- [x] Gewertet nur mit zwei verschiedenen gültigen IDs, mind. 3 Züge oder Sieg durch Leben/Deck, max. 3 Wertungen pro Paar in 24 h
- [x] Ergebnis und Ratings in einer Transaktion; Fehler werden nur geloggt und stoppen das Spiel nie
- [x] Migration AddRankingAndTurnOrder, Tests in RatingTests

### US68 – [#84](https://github.com/One-Sake/OneSake/issues/84)

Als Spieler möchte ich meinen Steckbrief mit Kopfgeld und eine Kopfgeld-Rangliste sehen, damit ich meinen Rang kenne

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 5
- **Zeitraum:** 18.11.2026 · **Commits:** 7cfd9a1

Akzeptanzkriterien:

- [x] GET /leaderboard (Spieler mit mind. 5 gewerteten Spielen) und /leaderboard/player/{id}, Cache 10 Minuten
- [x] WANTED-Steckbrief im Profil mit Rating-Verlauf
- [x] Rangliste mit Top 3 in Gold/Silber/Bronze, eigene Zeile hervorgehoben
- [x] Kurzes Kopfgeld-Banner nach einem gewerteten Match, Ranked/Casual-Kennzeichnung in Lobby und Brett

### US69 – [#85](https://github.com/One-Sake/OneSake/issues/85)

Als Spieler möchte ich aktuelle Meta-Statistiken zu Leadern und Matchups sehen, damit ich mein Deck auf die Meta abstimmen kann

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 18.11.2026 – 20.11.2026 · **Commits:** f0e3b0b, 0d55be1, a2c1403

Akzeptanzkriterien:

- [x] Backend lädt die täglichen Snapshots von Card Kaizoku, prüft sie streng und cached sie 6 Stunden (bei Ausfall letzter Stand mit isStale)
- [x] Endpoints /meta/status, /meta/leaders, /meta/leader/{key}, /meta/matchups; der Browser ruft den Anbieter nie direkt auf
- [x] Meta-Tab mit Leader-Tabelle, Leader-Detail mit allen Matchups und Matchup-Heatmap
- [x] Quellenangabe unter jeder Ansicht, Hinweis bei veralteten Daten, Lade- und Fehlerzustände
- [x] In Produktion aktiviert, abschaltbar über Meta__Enabled (docs/deployment.md)

### US70 – [#86](https://github.com/One-Sake/OneSake/issues/86)

Als Spieler möchte ich, dass Karteneffekte automatisch ausgeführt werden, damit ich nicht jeden Kartentext selbst umsetzen muss

- **Bereich:** Backend · **Verantwortlich:** danielbna · **Story Points:** 13
- **Zeitraum:** 18.11.2026 · **Commits:** 759cfec (dano-4)

Akzeptanzkriterien:

- [x] Parser macht aus Kartentexten Effekt-Definitionen (Zeitpunkt, Kosten, Bedingungen, Schritte)
- [x] Engine löst On Play, When Attacking, On Block, On K.O., Activate: Main, Event Main/Counter, Trigger und Zugende auf
- [x] Spieler werden nach Zielen und Kosten gefragt (Effekt-Auswahl im Frontend, Activate-Button, Counter-Events)
- [x] Statische Effekte werden nach jeder Aktion neu berechnet
- [x] Nicht unterstützte Texte werden als manuell markiert; Tests in EffectParserTests und EffectEngineTests

## Retrospektive

- **Gut:** Viele kleine, sauber getrennte Commits; Backend-Logik (Queue, Rating, Meta-Parser) mit eigenen Tests.
- **Schlecht:** Meta-Daten eines Drittanbieters ohne veröffentlichte Nutzungsbedingungen (Risiko, siehe `docs/deployment.md`).
- **Maßnahme:** Meta über `Meta__Enabled` jederzeit abschaltbar, Quellenangabe unter jeder Ansicht.
