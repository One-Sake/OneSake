# Sprint 2 – Deckbuilder, Tools, Match-Oberfläche und Datenbank

**Zeitraum:** 11.05.2026 – 07.06.2026 · **Status:** abgeschlossen · **Übersicht:** [#72](https://github.com/One-Sake/OneSake/issues/72)

## Sprintziel

Spieler können Decks bauen, speichern und im Match verwenden. Die Tools (Mulligan-Trainer,
Wahrscheinlichkeitsrechner) sind nutzbar. Kartendaten liegen in PostgreSQL.

## Ergebnis

Sprintziel erreicht. Deckbuilder mit Filtern und Bibliothek, beide Tools, mobiles Vollbild-Spielbrett und
Startbildschirm sind fertig. Karten werden aus der Datenbank geladen, die Spiellogik wurde in Action-Klassen
ausgelagert.

**Story Points erledigt:** 80

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| US6 | [#18](https://github.com/One-Sake/OneSake/issues/18) | Zug durchführen | Backend | danielbna | – | ✅ erledigt |
| US7 | [#19](https://github.com/One-Sake/OneSake/issues/19) | Karten verwalten (Zonen) | Backend | danielbna | – | ✅ erledigt |
| US9 | [#21](https://github.com/One-Sake/OneSake/issues/21) | Mulligan durchführen | Backend | danielbna | – | ✅ erledigt |
| – | [#10](https://github.com/One-Sake/OneSake/issues/10) | SubmitChoice in eigene Handler aufteilen (Task) | Backend | danielbna | – | ✅ erledigt |
| US21 | [#34](https://github.com/One-Sake/OneSake/issues/34) | Karten im Deckbuilder suchen und filtern | Frontend | WoWoWocal | 8 | ✅ erledigt |
| US22 | [#35](https://github.com/One-Sake/OneSake/issues/35) | Deck mit Leader zusammenstellen | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US23 | [#36](https://github.com/One-Sake/OneSake/issues/36) | Mehrere Decks speichern, laden, duplizieren, löschen | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US24 | [#37](https://github.com/One-Sake/OneSake/issues/37) | Karten groß ansehen, Kartengröße einstellen | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US25 | [#38](https://github.com/One-Sake/OneSake/issues/38) | Mulligan-Trainer | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US26 | [#39](https://github.com/One-Sake/OneSake/issues/39) | Wahrscheinlichkeitsrechner (Monte-Carlo im Web Worker) | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US27 | [#40](https://github.com/One-Sake/OneSake/issues/40) | Spielbrett im Vollbild (Handy) | Frontend | WoWoWocal | 8 | ✅ erledigt |
| US28 | [#41](https://github.com/One-Sake/OneSake/issues/41) | Gespeichertes Deck vor dem Match wählen | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US29 | [#42](https://github.com/One-Sake/OneSake/issues/42) | Verbindungsstatus, Verbindung vom Handy | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US30 | [#43](https://github.com/One-Sake/OneSake/issues/43) | Startbildschirm | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US31 | [#44](https://github.com/One-Sake/OneSake/issues/44) | Profilseite mit Gast-Login | Frontend | WoWoWocal | 2 | ✅ erledigt |
| US32 | [#45](https://github.com/One-Sake/OneSake/issues/45) | Lobby mit Schritt-Anzeige | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US33 | [#46](https://github.com/One-Sake/OneSake/issues/46) | Karten aus allen Booster- und Starter-Sets | Backend | danielbna | 5 | ✅ erledigt |
| US34 | [#47](https://github.com/One-Sake/OneSake/issues/47) | Spielregeln in Action-Klassen auslagern | Backend | danielbna | 5 | ✅ erledigt |
| US35 | [#48](https://github.com/One-Sake/OneSake/issues/48) | PostgreSQL-Datenbank für Kartendaten | Datenbank | 13-Tino-13 | 5 | ✅ erledigt |
| US36 | [#49](https://github.com/One-Sake/OneSake/issues/49) | Karten aus der API in die Datenbank importieren | Datenbank | 13-Tino-13 | 5 | ✅ erledigt |
| US37 | [#50](https://github.com/One-Sake/OneSake/issues/50) | Karten zuerst aus der Datenbank laden | Datenbank | 13-Tino-13 | 5 | ✅ erledigt |
| US38 | [#51](https://github.com/One-Sake/OneSake/issues/51) | Verfügbare Kartensets anzeigen | Datenbank | 13-Tino-13 | 2 | ✅ erledigt |

Akzeptanzkriterien, Zeiträume und Commits stehen in den Issues.
