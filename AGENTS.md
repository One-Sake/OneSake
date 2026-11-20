# OneSake Agent Rules

- Work on `dev` by default.
- Exception: the rules engine rework (card effects, trigger order, rules processing in
  `backend/OneSake.Game` and the game flow in `MatchRoom`) happens on `logic`.
  - Branch `logic` off `dev`, and merge `dev` into `logic` regularly to stay up to date.
  - Merge `logic` back into `dev` only when `dotnet build` and `dotnet test` are green.
  - Everything else (frontend, lobby, meta, small fixes) stays on `dev`.
- Do not create any other branches.
- Keep `main` for stable, demo-ready merges only.
- Run relevant checks after changes (`npm` and `dotnet` where available).
- Avoid unnecessary refactors.

## Thesis documentation and changelog (required for every change)

This project is a diploma thesis. After every change, in the same commit:

1. Add an entry at the end of `docs/diplomarbeit/01-projekttagebuch.md` (German):
   date and time (`JJJJ-MM-TT HH:MM`), commit/what was done, **why** (considerations,
   alternatives), problems or lessons learned. Use the template at the end of the file.
2. Add every new idea, decision, rejected idea or deviation from the Projektauftrag to
   `docs/diplomarbeit/02-entscheidungen-und-ideen.md` (and update its status tables).
3. For visible UI changes, save a screenshot as
   `docs/diplomarbeit/screenshots/JJJJ-MM-TT_nr-name.png` and list it in `screenshots/README.md`.
4. For every new user-visible feature, add a short German line to the newest entry in
   `frontend/web/src/content/changelog.ts` ("Was ist neu", currently v0.5.0) and update its date.

Keep changes small, clear, and beginner-friendly.
