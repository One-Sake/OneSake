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

Keep changes small, clear, and beginner-friendly.
