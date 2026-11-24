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

This project is a diploma thesis. Every team member has their **own** folder
`docs/diplomarbeit/<GitHub-Name>/` (e.g. `docs/diplomarbeit/WoWoWocal/`).
Only write into the folder of the person you are working for. Never write into another
person's folder. If it is unclear who you are working for, ask. If the person has no folder
yet, create it with the same structure as `docs/diplomarbeit/WoWoWocal/`.

After every change, in the same commit, in that person's folder:

1. Add an entry at the end of `01-projekttagebuch.md` (German):
   date and time (`JJJJ-MM-TT HH:MM`), commit/what was done, **why** (considerations,
   alternatives), problems or lessons learned. Use the template at the end of the file.
2. Add every new idea, decision, rejected idea or deviation from the Projektauftrag to
   `02-entscheidungen-und-ideen.md` (and update its status tables).
3. For visible UI changes, save a screenshot as `screenshots/JJJJ-MM-TT_nr-name.png`
   and list it in `screenshots/README.md`.
4. For every new user-visible feature, add a short German line to the newest entry in
   `frontend/web/src/content/changelog.ts` ("Was ist neu", currently v0.5.0) and update
   its date. This changelog is shared by the whole team.

## Sprints and user stories (required for every commit)

The Scrum documentation lives in `docs/sprints/` (one file per sprint, `README.md` with overview,
product backlog and Definition of Done) and as GitHub issues in `One-Sake/OneSake`
(labels `user-story`, `Sprint N`, `Backlog`, `prio: …`; sprint overviews with `sprint-overview`).
Keep both up to date in the same commit as the change:

1. If the commit belongs to an existing user story: add the commit hash (and date range) to the story
   in `docs/sprints/sprint-N.md`, tick fulfilled acceptance criteria, update its status. Comment the
   commit on the GitHub issue and close it when the story is done.
2. If the commit adds something that has no story yet: create a new user story with the next free number
   (see `docs/sprints/README.md`), format "Als … möchte ich …, damit …", acceptance criteria, story points,
   area and owner. Create the GitHub issue and add it to the current sprint file.
3. Update the story points in the sprint file, the overview table in `docs/sprints/README.md` and the
   sprint overview issue. At the end of a sprint write the result and retrospective, move open stories to
   the next sprint and create the next sprint file.
