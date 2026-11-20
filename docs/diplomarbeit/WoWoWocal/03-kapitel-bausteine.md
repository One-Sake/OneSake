# Kapitel-Bausteine für die Diplomarbeit

Vorschlag für eine Gliederung und welche Belege aus dem Repository zu welchem Kapitel passen.
Die Gliederung ist an den Projektauftrag (Abschnitt 10 „Ergebnisartefakte“) angelehnt.

| Kapitel | Inhalt | Belege / Quellen |
|---------|--------|------------------|
| 1. Einleitung | Motivation, Problem mobiler OPTCG-Simulatoren, Zielgruppe | Projektauftrag 1–2, `02-…` Abschnitt 1 |
| 2. Projektauftrag und Ziele | Haupt-/Nebenziele, Scope, Abnahmekriterien | Projektauftrag 2–3, 9 |
| 3. Projektmanagement | Scrum-Sprints, User Stories, Rollen, Git-Workflow (dev+main), CI/CD | `scripts/project-backlog/`, ADR 0002/0003, Tagebuch „Überblick“ |
| 4. Technologien | ASP.NET Core, SignalR, React/TypeScript/Vite, PostgreSQL/EF Core, Docker, GitHub Actions | `docs/architecture.md`, `02-…` Abschnitt 3 |
| 5. Architektur | Server-autoritativ, Choice-Driven, Hub-Methoden/Events, Action-Klassen, Datenmodell | ADR 0001, `docs/api-contract.md`, `docs/event-log-catalog.md`, Migrationen |
| 6. Umsetzung Frontend | Startbildschirm, Lobby, Deckbuilder, Tools, Profil, Meta, Match-Board, Designsystem | Tagebuch Sprint 2/3, Screenshots |
| 7. Umsetzung Backend | Karten-API + DB, MatchRoom, Regeln, Effekt-Engine, Matchmaking, Ergebnisse, ELO, Meta-Provider | Tagebuch, Commit-Beschreibungen `c263f41`, `759cfec`, `b8ad80e`, `f0e3b0b` |
| 8. Algorithmen im Detail | Monte-Carlo-Wahrscheinlichkeit, ELO-Formel, deterministische Würfel-Physik, Effekt-Parser | `probabilityLogic.ts`, `RatingCalculator.cs`, `diceSim.ts`, `EffectParser` |
| 9. UX-Design | Mobile First, Redesign-Schritte (Board v1 → Grid → skalierend), ein Haupt-Button, Leader-Farben | `02-…` Abschnitt 4, Screenshots, Commits `b5a5dbe`, `b65b1a5`, `101bcf8` |
| 10. Qualitätssicherung | 111 Backend-Tests, ESLint, CI, Playwright-Funktionstest | `backend/OneSake.Tests`, `.github/workflows/ci.yml` |
| 11. Deployment | Server, Nginx, Migrationen, Konfiguration | `docs/deployment.md` |
| 12. Recht und Datenschutz | Bildrechte, Meta-Daten-Quelle, Gast-ID | `02-…` Abschnitt 5 |
| 13. Abweichungen vom Plan | Ranked statt (noch) Berries, Drag & Drop, Pause Juni–Okt. | `02-…` Abschnitt 2.3 und 6 |
| 14. Evaluation | Usability-Test (noch offen), Erfüllung der Abnahmekriterien | Projektauftrag 9 |
| 15. Fazit und Ausblick | Offene User Stories, Lessons Learned | `02-…` Abschnitt 2.2 und 8 |
| Anhang | Commit-Liste, Screenshots, KI-Einsatz | [Anhang A](anhang-a-commit-liste.md), [Screenshots](screenshots/README.md) |

## Stand der Abnahmekriterien (Projektauftrag Abschnitt 9) – 2026-11-20

| Nr. | Kriterium | Stand |
|-----|-----------|-------|
| 1 | PvP-Match bis Sieg/Niederlage | ✅ erfüllt (Regeln seit 07.11.) |
| 2 | Spieler treffen nur Choices, Server führt aus | ✅ erfüllt (zusätzlich Drag & Drop als Eingabe) |
| 3 | Chat + Spielverlauf sichtbar | ✅ erfüllt (gemeinsamer Feed) |
| 4 | Deckbuilder: erstellen, validieren, speichern, Export | ✅ erfüllt (Text-Export; OPTCGSim-String offen) |
| 5 | Tools mit gespeicherten Decks | ✅ erfüllt |
| 6 | Progression: Berries, Shop, Cosmetics | ⏳ offen (stattdessen Ranked/Kopfgeld) |
| 7 | Persistenz nach Login | ⏳ teilweise (Ergebnisse/Wertung per Gast-ID; Login und Server-Decks offen) |
| 8 | Mobile-UX im Vollbild | 🟡 weitgehend (Usability-Test offen) |
