# Backlog und Sprints auf GitHub anlegen

`create-backlog.sh` legt die User Stories, Sprint-Labels und Sprint-Übersichten für OneSake an
und ordnet die vorhandenen Issues ein. Grundlage ist die Git-Historie und der Projektauftrag.

## Aufteilung

| Bereich   | Verantwortlich |
|-----------|----------------|
| Frontend  | WoWoWocal      |
| Backend   | danielbna      |
| Datenbank | 13-Tino-13     |

## Sprints

| Sprint   | Zeitraum              | Ziel                                                        | Stand        |
|----------|-----------------------|-------------------------------------------------------------|--------------|
| Sprint 1 | 17.04. – 10.05.2026   | Repo, Workflow, CI/CD, Karten-API, Vertical Slice           | abgeschlossen |
| Sprint 2 | 11.05. – 07.06.2026   | Deckbuilder, Tools, Match-Oberfläche, PostgreSQL            | abgeschlossen |
| Sprint 3 | 06.10. – 12.11.2026   | Server-Liste, echte OPTCG-Regeln, neues Spielbrett          | abgeschlossen |
| Sprint 4 | 13.11. – 19.11.2026   | Quick Match, Ranked, Meta, Effekt-Engine                    | abgeschlossen |
| Sprint 5 | 20.11. – 23.11.2026   | Regel-Engine, Reconnect, Zug-Timer, Spielgefühl, Konten     | abgeschlossen |
| Sprint 6 | 24.11. – 20.12.2026   | Decks am Konto, Berries, Produktion, Abnahme-Vorbereitung   | geplant       |

> Das Skript deckt nur Sprint 1–3 ab (Stand 09.11.2026). Sprint 3 bis 6 und die User Stories ab #58 wurden
> am 23.11.2026 direkt auf GitHub angelegt und sind in [docs/sprints/](../../docs/sprints/README.md) dokumentiert.
> Neue Stories bitte dort und als Issue anlegen, nicht mehr über dieses Skript.

Neue User Stories: UserStory #16 – #57 (Fortsetzung der bestehenden #1 – #15).
Abgeschlossene Stories enthalten Zeitraum und Commits, offene Stories liegen in Sprint 3 oder im Backlog
(mit Label `prio: high | medium | low`).

## Ausführen

1. GitHub CLI installieren: https://cli.github.com
2. Anmelden (Konto mit Schreibrechten auf `One-Sake/OneSake`):

   ```bash
   gh auth login
   ```

3. Testlauf, ändert nichts:

   ```bash
   DRY_RUN=1 bash scripts/project-backlog/create-backlog.sh
   ```

4. Echter Lauf:

   ```bash
   bash scripts/project-backlog/create-backlog.sh
   ```

Das Skript kann erneut laufen, Issues mit gleichem Titel werden nicht doppelt angelegt.

## Optional: GitHub Project (Board)

1. In der Organisation `One-Sake` unter **Projects → New project** ein Projekt anlegen (Vorlage „Team planning“ oder „Iterative development“).
2. Projektrechte für die CLI holen:

   ```bash
   gh auth refresh -s project
   ```

3. Skript mit der Projektnummer aus der URL starten (`.../projects/1` → `1`):

   ```bash
   PROJECT_NUMBER=1 bash scripts/project-backlog/create-backlog.sh
   ```

4. Im Project in der Tabellenansicht nach Label gruppieren und das Feld **Iteration/Sprint** pro Gruppe setzen
   (alle markieren → Feld ändern). Geschlossene Issues landen automatisch in „Done“.
