# Diplomarbeit OneSake – Dokumentationsmappe von WoWoWocal

Persönlicher Ordner – nur für die eigene Mitschrift. Teamkollegen legen ihre Mitschrift in
ihrem eigenen Ordner `docs/diplomarbeit/<GitHub-Name>/` an.

Diese Mappe sammelt alles, was für das Schreiben der Diplomarbeit gebraucht wird:
was wann gemacht wurde, warum es so gemacht wurde, welche Ideen es gab und wie sich die
Anwendung sichtbar verändert hat.

| Datei | Inhalt |
|-------|--------|
| [01-projekttagebuch.md](01-projekttagebuch.md) | Chronologisches Projekttagebuch mit Datum/Uhrzeit, Commit, Person und Überlegungen |
| [02-entscheidungen-und-ideen.md](02-entscheidungen-und-ideen.md) | Alle Ideen (umgesetzt, offen, verworfen), Architektur- und Designentscheidungen, Abweichungen vom Projektauftrag, Risiken, Lessons Learned |
| [03-kapitel-bausteine.md](03-kapitel-bausteine.md) | Vorschlag für die Gliederung der Arbeit und welche Quellen/Belege zu welchem Kapitel passen |
| [04-roadmap-und-alleinstellungsmerkmale.md](04-roadmap-und-alleinstellungsmerkmale.md) | Was nach den Logik-Prompts noch fehlt, Ideen, die OneSake von anderen Simulatoren abheben, und empfohlene Reihenfolge |
| [screenshots/](screenshots/README.md) | Screenshots mit Datum (Dateiname `JJJJ-MM-TT_nr-name.png`) |

Weitere Quellen im Repository:

- `docs/Projektauftrag_OPTCG_Web_Simulator.md` – ursprünglicher Projektauftrag (Ziele, Scope, Phasen, Abnahmekriterien)
- `docs/adr/` – Architecture Decision Records (Server-autoritativ, Branching, dev+main)
- `docs/sprint-1-backlog.md` – Backlog des ersten Sprints
- `scripts/project-backlog/` – User Stories #16–#57 und Sprint-Einteilung
- `docs/deployment.md` – Deployment, Migrationen, Meta-Konfiguration
- `frontend/web/src/content/changelog.ts` – „Was ist neu“ in der App (Versionen 0.1.0–0.5.0)
- Git-Historie: `git log --reverse --date=format:'%Y-%m-%d %H:%M' --pretty='%ad %h %an %s'`

## Wie diese Mappe aktuell bleibt

Ab 20.11.2026 gilt (siehe `AGENTS.md`): Bei **jeder** eigenen Änderung am Projekt wird in diesem Ordner

1. im Projekttagebuch ein Eintrag mit Datum, Uhrzeit, Was, Warum und Überlegungen ergänzt,
2. jede neue Idee oder Entscheidung in `02-entscheidungen-und-ideen.md` eingetragen,
3. bei sichtbaren Änderungen ein Screenshot unter `screenshots/` abgelegt,
4. jede neue sichtbare Funktion im neuesten Eintrag von `changelog.ts` („Was ist neu“, aktuell v0.5.0) ergänzt.

## Hinweise zur Nachvollziehbarkeit

- Alle Zeitstempel stammen aus der Git-Historie (Autorenzeit, lokale Zeit des Committers).
- Einträge vor dem 20.11.2026 wurden **nachträglich** aus Commits, Commit-Beschreibungen,
  Projektauftrag, ADRs und Backlog rekonstruiert. Überlegungen, die nur mündlich besprochen
  wurden, fehlen dort natürlich – am besten beim Lesen ergänzen.
- Ein Teil der Commits stammt von KI-Werkzeugen (Autor „Codex“ und „Claude“). Für die
  Diplomarbeit sollte der Einsatz von KI-Werkzeugen offen angegeben werden (siehe
  `02-entscheidungen-und-ideen.md`, Abschnitt „Werkzeuge und KI-Einsatz“).
