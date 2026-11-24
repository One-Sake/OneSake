# Sprint 1 – Fundament und Vertical Slice

**Zeitraum:** 17.04.2026 – 10.05.2026 · **Status:** abgeschlossen · **Übersicht:** [#71](https://github.com/One-Sake/OneSake/issues/71)

## Sprintziel

Projektgrundlage schaffen: Repository, Workflow, CI/CD, Karten-API und ein erster spielbarer Ablauf
(Raum beitreten, Match starten, Mulligan, Zug beenden, Chat und Log).

## Ergebnis

Sprintziel erreicht. Zwei Spieler können einem Raum beitreten, ein Match starten und Züge beenden.
Karten können von der OPTCG-API abgerufen werden. Jeder Push wird automatisch gebaut und getestet.

**Story Points erledigt:** 22 (die ersten Stories US1–US15 wurden noch ohne Story Points geschätzt)

## Sprint Backlog

| US | Issue | User Story | Bereich | Verantwortlich | SP | Status |
|----|-------|------------|---------|----------------|---:|--------|
| – | [#3](https://github.com/One-Sake/OneSake/issues/3) | Secrets für GitHub Actions einrichten (Task) | Backend | danielbna | – | ✅ erledigt |
| – | [#4](https://github.com/One-Sake/OneSake/issues/4) | .yaml-Datei für CI/CD anlegen (Task) | Backend | danielbna | – | ✅ erledigt |
| – | [#5](https://github.com/One-Sake/OneSake/issues/5) | Website erreichbar machen (Task) | Backend | danielbna | – | ✅ erledigt |
| – | [#6](https://github.com/One-Sake/OneSake/issues/6) | Frontend ausführen (Task) | Backend | danielbna | – | ✅ erledigt |
| – | [#7](https://github.com/One-Sake/OneSake/issues/7) | Backend umsetzen (Task) | Backend | danielbna | – | ✅ erledigt |
| US1 | [#13](https://github.com/One-Sake/OneSake/issues/13) | Mit dem Server verbinden | Backend | danielbna | – | ✅ erledigt |
| US3 | [#15](https://github.com/One-Sake/OneSake/issues/15) | Einem bestehenden Spiel beitreten | Backend | danielbna | – | ✅ erledigt |
| US4 | [#16](https://github.com/One-Sake/OneSake/issues/16) | Spiel starten, sobald alle Spieler da sind | Backend | danielbna | – | ✅ erledigt |
| US5 | [#17](https://github.com/One-Sake/OneSake/issues/17) | Synchronisierter Spielzustand | Backend | danielbna | – | ✅ erledigt |
| US11 | [#23](https://github.com/One-Sake/OneSake/issues/23) | Zug beenden | Backend | danielbna | – | ✅ erledigt |
| US16 | [#29](https://github.com/One-Sake/OneSake/issues/29) | Lobby mit Namen und Raumcode | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US17 | [#30](https://github.com/One-Sake/OneSake/issues/30) | Entscheidungen, Log und Chat auf einem Bildschirm | Frontend | WoWoWocal | 5 | ✅ erledigt |
| US18 | [#31](https://github.com/One-Sake/OneSake/issues/31) | Repository-Struktur, Doku und Git-Workflow | Doku | WoWoWocal | 2 | ✅ erledigt |
| US19 | [#32](https://github.com/One-Sake/OneSake/issues/32) | Kartendaten aus der OPTCG-API | Backend | danielbna | 5 | ✅ erledigt |
| US20 | [#33](https://github.com/One-Sake/OneSake/issues/33) | CI/CD: Build, Test und Deployment bei jedem Push | Backend | danielbna | 5 | ✅ erledigt |

Technische Aufteilung des Vertical Slice: [sprint-1-backlog.md](../sprint-1-backlog.md).
