# Sprints, User Stories und Product Backlog

Hier wird der Projektfortschritt von OneSake nach Scrum dokumentiert. Die User Stories sind zusätzlich als
GitHub-Issues angelegt (Labels `user-story`, `Sprint N`, `Backlog`, `prio: …`, Übersichten mit `sprint-overview`).
**Diese Dateien und die Issues müssen bei jedem Commit aktuell gehalten werden** (siehe unten und `AGENTS.md`).

Stand: 23.11.2026 (DA-Review)

## Team und Rollen

| Bereich | Verantwortlich |
|---------|----------------|
| Frontend (und seit Sprint 4 viele Fullstack-Features) | WoWoWocal |
| Backend, Spiellogik, Effekt-Engine | danielbna (Commits auch als `dano-4`) |
| Datenbank, Konten, Kartendaten | 13-Tino-13 |

## Sprint-Übersicht

| Sprint | Zeitraum | Ziel | SP erledigt | Status | Details |
|--------|----------|------|------------:|--------|---------|
| 1 | 17.04. – 10.05.2026 | Fundament und Vertical Slice (Repo, CI/CD, Karten-API, erster Spielablauf) | 22 | ✅ abgeschlossen | [sprint-1.md](sprint-1.md) · [#71](https://github.com/One-Sake/OneSake/issues/71) |
| 2 | 11.05. – 07.06.2026 | Deckbuilder, Tools, Match-Oberfläche und Datenbank | 80 | ✅ abgeschlossen | [sprint-2.md](sprint-2.md) · [#72](https://github.com/One-Sake/OneSake/issues/72) |
| – | 08.06. – 05.10.2026 | Sommerpause (kein Sprint) | – | – | – |
| 3 | 06.10. – 12.11.2026 | Server-Browser, echte Spielregeln, neues Spielbrett | 34 | ✅ abgeschlossen | [sprint-3.md](sprint-3.md) · [#73](https://github.com/One-Sake/OneSake/issues/73) |
| 4 | 13.11. – 19.11.2026 | Quick Match, Ranked, Meta und Effekt-Engine | 63 | ✅ abgeschlossen | [sprint-4.md](sprint-4.md) · [#106](https://github.com/One-Sake/OneSake/issues/106) |
| 5 | 20.11. – 23.11.2026 | Regel-Engine, Stabilität, Spielgefühl und Konten (Abschluss DA-Review) | 91 | ✅ abgeschlossen | [sprint-5.md](sprint-5.md) · [#107](https://github.com/One-Sake/OneSake/issues/107) |
| 6 | 24.11. – 20.12.2026 | Konten fertigstellen, Decks auf dem Server, Berries, Abnahme-Vorbereitung | 51 geplant | ⏳ geplant | [sprint-6.md](sprint-6.md) · [#108](https://github.com/One-Sake/OneSake/issues/108) |

Hinweise:

- Story Points (SP) nach Fibonacci (1, 2, 3, 5, 8, 13). Die ersten Stories US1–US15 wurden ohne SP angelegt.
- Sprint 3 bis 5 wurden am 23.11.2026 nachträglich aus der Git-Historie geschnitten: Sprint 3 endet mit dem
  Board-Redesign (12.11.), Sprint 4 umfasst den großen Feature-Block vom 18.11., Sprint 5 die Regel-Engine
  vom Branch `logic` und alles bis zum DA-Review.
- Die hohe Geschwindigkeit in Sprint 4 und 5 kommt auch durch KI-gestützte Entwicklung (Claude Code);
  die Commits sind mit `Co-Authored-By` gekennzeichnet.

## Stand gegenüber dem Projektauftrag

| Bereich (Projektauftrag 3.1) | Stand 23.11.2026 |
|------------------------------|------------------|
| 1. PvP-Simulation (Lobby, Startphase, Turn-Loop, Choices, Win/Loss) | ✅ fertig, dazu Quick Match, Ranked, Reconnect, Zug-Timer |
| 2. Deckbuilder (Suche, Validierung, Speichern, Export) | ✅ fertig (Decks noch lokal im Browser → Sprint 6) |
| 3. Tools (Wahrscheinlichkeit, Mulligan-Trainer) | ✅ fertig |
| 4. Progression und Cosmetics (Berries, Shop) | ⬜ offen → Berries in Sprint 6, Shop im Backlog |
| 5. Chat und Spielverlauf | ✅ gemeinsame Timeline; Filter Chat/Aktionen offen (Sprint 6) |
| 6. Accounts und Gäste | 🟨 Login, Gäste und Übernahme des Gast-Verlaufs fertig; Decks am Konto offen (Sprint 6) |

## Product Backlog (noch keinem Sprint zugeordnet)

| US | Issue | User Story | Bereich | SP | Priorität |
|----|-------|------------|---------|---:|-----------|
| US45 | [#58](https://github.com/One-Sake/OneSake/issues/58) | Deckbuilder auch am Handy flüssig (Cache, Virtualisierung, WebP) | Frontend | 5 | high |
| US86 | [#102](https://github.com/One-Sake/OneSake/issues/102) | Geführtes Tutorial und KI-Gegner zum Üben | Frontend, Backend | 8 | medium |
| US87 | [#103](https://github.com/One-Sake/OneSake/issues/103) | Replay ansehen und ab jeder Stelle selbst weiterspielen („Was wäre wenn?“) | Frontend, Backend | 8 | medium |
| US56 | [#69](https://github.com/One-Sake/OneSake/issues/69) | Alle Kartenversionen inkl. Alt-Arts | Datenbank | 3 | medium |
| US50 | [#63](https://github.com/One-Sake/OneSake/issues/63) | Shop: Cosmetics kaufen und ausrüsten (Frontend) | Frontend | 5 | low |
| US54 | [#67](https://github.com/One-Sake/OneSake/issues/67) | Cosmetics kaufen und ausrüsten (Backend) | Backend | 8 | low |
| US51 | [#64](https://github.com/One-Sake/OneSake/issues/64) | Einladungslink teilen, App installierbar (PWA) | Frontend | 3 | low |
| US88 | [#104](https://github.com/One-Sake/OneSake/issues/104) | Deck-Labor: eigenes Deck per Simulation gegen die Meta testen | Frontend, Backend | 13 | low |
| US89 | [#105](https://github.com/One-Sake/OneSake/issues/105) | Coach nach dem Match und tägliches Lethal-Puzzle | Frontend, Backend | 8 | low |

Weitere Ideen (noch ohne Story): Ränge mit One-Piece-Titeln und Banden, Streamer-/Turnier-Modus, Deutsch und
Englisch – siehe `docs/diplomarbeit/WoWoWocal/04-roadmap-und-alleinstellungsmerkmale.md`.

## Definition of Done

Eine User Story ist fertig, wenn

1. alle Akzeptanzkriterien erfüllt sind,
2. `dotnet build` / `dotnet test` (Release) und `npm run lint` / `npm run build` grün sind (CI grün),
3. neue Logik Tests hat (Backend: xUnit; Regeln zusätzlich Simulator-Smoke-Test),
4. die Doku aktualisiert ist (`docs/`, „Was ist neu“ bei sichtbaren Features, Projekttagebuch),
5. die Story in diesem Ordner und im GitHub-Issue auf „erledigt“ steht, mit Zeitraum und Commits,
6. der Stand auf `dev` gepusht ist.

## So bleibt die Doku aktuell (bei jedem Commit)

1. **Gehört der Commit zu einer bestehenden Story?** Dann in `sprint-N.md` die Commits und ggf. den Zeitraum
   ergänzen, erfüllte Akzeptanzkriterien abhaken und den Status in der Tabelle setzen. Im Issue dasselbe
   (Kommentar mit Commit-Hash, bei Abschluss schließen).
2. **Neue Funktion ohne Story?** Neue User Story anlegen: nächste freie Nummer (zurzeit **US90**),
   Format „Als … möchte ich …, damit …“, Akzeptanzkriterien, Story Points, Bereich, Verantwortliche/r.
   Issue mit Labels `user-story`, Bereich, `Sprint N` anlegen und in `sprint-N.md` eintragen.
3. **Story abgeschlossen?** Status ✅, Story Points zählen in „SP erledigt“ (Tabelle oben und Sprint-Übersichts-Issue).
4. **Sprint-Ende:** Ergebnis und Retrospektive in `sprint-N.md`, offene Stories in den nächsten Sprint
   übertragen (Status „➡️ Sprint N+1“), neue `sprint-(N+1).md` anlegen und die Übersicht oben ergänzen.
