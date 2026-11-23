# Sprint 3 – Server-Browser, echte Spielregeln und neues Spielbrett

**Zeitraum:** 06.10.2026 – 12.11.2026 · **Status:** abgeschlossen · **Übersicht:** [#73](https://github.com/One-Sake/OneSake/issues/73)

## Sprintziel

Ein vollständiges Match nach echten OPTCG-Regeln spielen, Räume über eine Server-Liste finden und die
Grundlage für Konten und gespeicherte Decks in der Datenbank legen.

## Ergebnis

Sprintziel größtenteils erreicht. Server-Liste, echte Spielregeln mit Kartenbildern und das neue Spielbrett
(CSS-Grid, Fächer-Hand, gemeinsamer Log/Chat-Feed) sind fertig. Konten und Decks in der Datenbank wurden
nicht begonnen und wandern in Sprint 6.

**Story Points erledigt:** 34 · **übertragen:** 10 (US42, US43) und 3 Stories ohne Schätzung

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| US2 | [#14](https://github.com/One-Sake/OneSake/issues/14) | Neues Spiel erstellen | Backend | danielbna | – | ✅ erledigt |
| US8 | [#20](https://github.com/One-Sake/OneSake/issues/20) | Karten ausspielen | Backend | danielbna | – | ✅ erledigt |
| US13 | [#26](https://github.com/One-Sake/OneSake/issues/26) | Spiel korrekt beenden | Backend | danielbna | – | ✅ erledigt |
| US15 | [#28](https://github.com/One-Sake/OneSake/issues/28) | Automatisierte Tests für die Spiellogik | Backend | danielbna | – | ✅ erledigt |
| US39 | [#52](https://github.com/One-Sake/OneSake/issues/52) | Liste offener Server | Frontend | WoWoWocal | 3 | ✅ erledigt |
| US40 | [#53](https://github.com/One-Sake/OneSake/issues/53) | Echte Karten auf dem Spielbrett, per Klick spielen | Frontend | WoWoWocal | 8 | ✅ erledigt |
| US41 | [#54](https://github.com/One-Sake/OneSake/issues/54) | Spielen nach den echten OPTCG-Regeln | Backend | danielbna | 13 | ✅ erledigt |
| US58 | [#74](https://github.com/One-Sake/OneSake/issues/74) | Ein übersichtliches Spielbrett mit Fächer-Hand und einem gemeinsamen Log/Chat-Feed | Frontend | WoWoWocal | 8 | ✅ erledigt |
| US59 | [#75](https://github.com/One-Sake/OneSake/issues/75) | Backlog, Sprints und User Stories nachvollziehbar auf GitHub führen | Doku | WoWoWocal | 2 | ✅ erledigt |
| US10 | [#22](https://github.com/One-Sake/OneSake/issues/22) | Spielaktionen validieren | Backend | danielbna | – | ➡️ Sprint 6 |
| US12 | [#24](https://github.com/One-Sake/OneSake/issues/24) | Spieländerungen in Echtzeit / Reconnect | Backend | danielbna | – | ➡️ Sprint 5 (erledigt) |
| US14 | [#27](https://github.com/One-Sake/OneSake/issues/27) | Mit dem produktiven Server verbinden | Backend | danielbna | – | ➡️ Sprint 6 |
| US42 | [#55](https://github.com/One-Sake/OneSake/issues/55) | Konto oder Gastkonto in der Datenbank | Datenbank | 13-Tino-13 | 5 | ➡️ Sprint 6 |
| US43 | [#56](https://github.com/One-Sake/OneSake/issues/56) | Decks auf dem Server speichern | Datenbank | 13-Tino-13 | 5 | ➡️ Sprint 6 |

## Neue Stories im Detail

### US58 – [#74](https://github.com/One-Sake/OneSake/issues/74)

Als Spieler möchte ich ein übersichtliches Spielbrett mit Fächer-Hand und einem gemeinsamen Log/Chat-Feed, damit ich das Match auf Desktop und Handy gut spielen kann

- **Bereich:** Frontend · **Verantwortlich:** WoWoWocal · **Story Points:** 8
- **Zeitraum:** 11.11.2026 – 12.11.2026 · **Commits:** b5a5dbe, fdb4f36

Akzeptanzkriterien:

- [x] Spielbrett als CSS-Grid, Hälfte des Gegners gespiegelt, keine absolut positionierten Slots mehr
- [x] Kartengröße skaliert mit dem Bildschirm (63:88), zweizeiliges Layout für niedrige Querformat-Bildschirme
- [x] Hand als Fächer am unteren Rand: Hover hebt an, Antippen wählt, zweites Antippen spielt
- [x] Rechtsklick / langes Drücken öffnet die Kartenansicht, Trash öffnet eine Kartenliste
- [x] Keep/Mulligan und andere Entscheidungen als kleine Leiste auf der Mittellinie (Hand bleibt sichtbar)
- [x] Desktop: 3 Spalten Log/Chat | Brett | Kartenvorschau; Log und Chat in einer chronologischen Liste mit Anzeigenamen
- [x] prefers-reduced-motion wird beachtet

### US59 – [#75](https://github.com/One-Sake/OneSake/issues/75)

Als Team möchten wir Backlog, Sprints und User Stories nachvollziehbar auf GitHub führen, damit der Projektfortschritt für die Diplomarbeit belegt ist

- **Bereich:** Doku · **Verantwortlich:** WoWoWocal · **Story Points:** 2
- **Zeitraum:** 09.11.2026 · **Commits:** 1f8e85e

Akzeptanzkriterien:

- [x] Skript legt Labels, User Stories und Sprint-Übersichten auf GitHub an
- [x] Bestehende Issues werden einem Sprint zugeordnet, Duplikate markiert
- [x] Abgeschlossene Stories enthalten Zeitraum und Commits
- [x] Mehrfaches Ausführen legt nichts doppelt an (Testlauf mit DRY_RUN=1)

## Retrospektive

- **Gut:** Mit `c263f41` ist das Spiel zum ersten Mal nach echten Regeln spielbar, abgesichert durch Tests.
- **Schlecht:** Datenbank-Stories (Konten, Decks) wurden eingeplant, aber nicht begonnen.
- **Maßnahme:** Sprints kürzer schneiden und offene Stories im Review aktiv übertragen.
