# Sprint 6 – Konten fertigstellen, Decks auf dem Server, Berries und Abnahme-Vorbereitung

**Zeitraum:** 24.11.2026 – 20.12.2026 (geplant) · **Status:** läuft · **Übersicht:** [#108](https://github.com/One-Sake/OneSake/issues/108)

## Sprintziel

Das Abnahmekriterium aus dem Projektauftrag erreichen: Nach dem Login sind Decks und Fortschritt auf jedem
Gerät verfügbar, Spieler bekommen Berries nach einem Match, und der Produktionsserver ist stabil.

**Geplante Story Points:** 62 (51 + US90 und US91, am 24.11. dazugekommen) · **erledigt:** 11

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| US43 | [#56](https://github.com/One-Sake/OneSake/issues/56) | Decks auf dem Server speichern | Datenbank | 13-Tino-13 | 5 | ⬜ offen |
| US48 | [#61](https://github.com/One-Sake/OneSake/issues/61) | Registrierung/Login im Frontend, Decks vom Server laden | Frontend | WoWoWocal | 5 | 🟨 teilweise |
| US42 | [#55](https://github.com/One-Sake/OneSake/issues/55) | Konto in der Datenbank: ER-Diagramm, Migration auf Produktion | Datenbank | 13-Tino-13 | 5 | 🟨 teilweise |
| US10 | [#22](https://github.com/One-Sake/OneSake/issues/22) | Deck-Validierung auch auf dem Server | Backend | danielbna | 3 | 🟨 teilweise |
| US14 | [#27](https://github.com/One-Sake/OneSake/issues/27) | Produktiver Server stabil (Deploy mit `set -e`, Migrationen) | Backend | danielbna | 3 | 🟨 teilweise |
| US83 | [#99](https://github.com/One-Sake/OneSake/issues/99) | Bekannte Anzeige-Fehler im Match behoben sind | Frontend, Backend | WoWoWocal | 3 | ⬜ offen |
| US84 | [#100](https://github.com/One-Sake/OneSake/issues/100) | Die Karten der Top-20-Meta-Decks geprüft funktionieren | Backend | danielbna | 8 | ⬜ offen |
| US53 | [#66](https://github.com/One-Sake/OneSake/issues/66) | Berries nach dem Match | Backend | danielbna | 5 | ⬜ offen |
| US55 | [#68](https://github.com/One-Sake/OneSake/issues/68) | Berries und Cosmetics in der Datenbank | Datenbank | 13-Tino-13 | 5 | ⬜ offen |
| US44 | [#57](https://github.com/One-Sake/OneSake/issues/57) | Filter Alle / Chat / Aktionen im Feed | Frontend | WoWoWocal | 3 | 🟨 teilweise |
| US49 | [#62](https://github.com/One-Sake/OneSake/issues/62) | Automatisierte Frontend-Tests | Frontend | WoWoWocal | 3 | ⬜ offen |
| US85 | [#101](https://github.com/One-Sake/OneSake/issues/101) | Einen Usability-Test am Handy durchführen | Doku | WoWoWocal | 3 | ⬜ offen |
| US90 | (Issue folgt) | Keine manuellen Effekte mehr im Match (Fair Play) | Frontend, Backend | danielbna | 3 | ✅ erledigt |
| US91 | (Issue folgt) | Jeder Effekt-Baustein ist einzeln getestet | Backend | danielbna | 8 | ✅ erledigt |

Reihenfolge = Priorität. Stories mit „teilweise“ wurden in früheren Sprints begonnen; der Stand steht im
Kommentar des Issues.

## Neue Stories im Detail

### US83 – [#99](https://github.com/One-Sake/OneSake/issues/99)

Als Spieler möchte ich, dass bekannte Anzeige-Fehler im Match behoben sind, damit nichts verwirrt

- **Bereich:** Frontend, Backend · **Verantwortlich:** WoWoWocal · **Story Points:** 3 · **Priorität:** high

Akzeptanzkriterien:

- [ ] Würfel-Overlay bleibt beim Gegner nicht mehr stehen, wenn der Gewinner sehr schnell entscheidet
- [ ] Cerberus zeigt kein „Blocker“, wenn „cannot block“ aktiv ist (Simulator-Fund F3)
- [ ] Keine doppelten React-Keys im MatchFeed
- [ ] Anzeigename eingeloggter Spieler ist fest an den Benutzernamen gekoppelt

### US84 – [#100](https://github.com/One-Sake/OneSake/issues/100)

Als Spieler möchte ich, dass die Karten der Top-20-Meta-Decks geprüft funktionieren, damit die meistgespielten Decks ohne manuelle Eingriffe laufen

- **Bereich:** Backend · **Verantwortlich:** danielbna · **Story Points:** 8 · **Priorität:** high

Akzeptanzkriterien:

- [ ] Karten der Top-20-Meta-Leader nach Priorität sortiert
- [ ] Jede Karte hat einen Test ([CardTest]) und ist auf verified gesetzt
- [ ] Fehlende Karten (z. B. OP18-060) sind nachgeladen und generiert
- [ ] Simulator mit Meta-Decks läuft ohne Invariantenverletzung

### US85 – [#101](https://github.com/One-Sake/OneSake/issues/101)

Als Team möchten wir einen Usability-Test am Handy durchführen, damit wir die mobile Bedienung mit bestehenden Lösungen vergleichen können

- **Bereich:** Doku · **Verantwortlich:** WoWoWocal · **Story Points:** 3 · **Priorität:** medium

Akzeptanzkriterien:

- [ ] Testplan mit Aufgaben (Deck bauen, Match spielen, Tools nutzen)
- [ ] Mindestens 5 Testpersonen, OneSake im Vergleich zu einer bestehenden Lösung
- [ ] Ergebnisse und abgeleitete Verbesserungen in docs/ dokumentiert (Projektauftrag Phase 5)

### US90 – (Issue folgt)

Als Spieler möchte ich, dass niemand im Match Effekte von Hand auslösen kann, damit nicht geschummelt werden kann

- **Bereich:** Frontend, Backend · **Verantwortlich:** danielbna · **Story Points:** 3 · **Priorität:** high
- **Zeitraum:** 24.11.2026 · **Commits:** `96c67af` (Branch `logic`), am 28.11. in `dev` und `main` gemergt
- Nimmt den manuellen Modus aus US72 (Sprint 5) wieder heraus.

Akzeptanzkriterien:

- [x] Kein Kontextmenü, kein „⋯“-Knopf und kein „✋ Manuell“ mehr im Match
- [x] Die Hub-Methode `ManualAction` und der Prompt `MANUAL_EFFECT` gibt es am Server nicht mehr
- [x] Entscheidungen, die auf der Karte stehen („You may“, [Activate: Main], Ziele, Ja/Nein bei Kosten), bleiben beim Spieler
- [x] Nicht automatisierte Effekttexte werden übersprungen und für beide Spieler als `EFFECT_NOT_AUTOMATED` geloggt
- [x] Backend-Tests und Frontend `lint`/`build` grün
- [ ] Screenshot vom Match ohne manuellen Modus (braucht lokal eine Kartendatenbank)

### US91 – (Issue folgt)

Als Entwickler möchte ich, dass jeder Baustein der Karteneffekte einzeln getestet ist, damit eine Karte nur noch aus geprüften Teilen besteht

- **Bereich:** Backend · **Verantwortlich:** danielbna · **Story Points:** 8 · **Priorität:** high
- **Zeitraum:** 24.11.2026 · **Commits:** `d41ce2d` (Branch `logic`), am 28.11. in `dev` und `main` gemergt
- Plan `docs/spiellogik-plan.md`, A11.

Akzeptanzkriterien:

- [x] Liste aller Bausteine wird aus dem Effektmodell erzeugt (153 Bausteine)
- [x] Jeder Baustein hat mindestens einen Test mit `[BlockTest]`, der echten Kartentext benutzt und beide Seiten prüft
- [x] Ein Test schlägt fehl, wenn ein Baustein ohne Test oder ohne Eintrag in `docs/effect-building-blocks.md` ist
- [x] Katalog `docs/effect-building-blocks.md` mit Kartentext, Häufigkeit und bekannten Lücken
- [x] `dotnet build` und `dotnet test` grün
