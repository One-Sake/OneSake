# Anhang A – Vollständige Commit-Liste

Automatisch erzeugt aus der Git-Historie (Branch `dev`, ohne Merge-Commits), älteste zuerst.
Zeit = Autorenzeit des Commits. ★ = eigener Commit (WoWoWocal, Frontend).

Neu erzeugen:

```bash
git log --reverse --no-merges --date=format:'%Y-%m-%d %H:%M' --pretty='| %ad | %h | %an | %s |' dev
```

| Datum/Zeit | Commit | Wer | Beschreibung |
|---|---|---|---|
| 2026-04-17 14:12 | `2879eba` | ★ WoWoWocal | Initial commit |
| 2026-04-19 17:54 | `b4f6bde` | ★ WoWoWocal | chore: repo structure, team workflow, and initial docs |
| 2026-04-20 22:04 | `1adee21` | dano-4 | CI/CD implementiert |
| 2026-04-21 01:18 | `0f4d923` | ★ WoWoWocal | Add minimal ESLint config for React TypeScript frontend |
| 2026-04-21 01:40 | `213430f` | ★ WoWoWocal | Enhance README with project setup and Git workflow |
| 2026-04-21 01:41 | `d49ab81` | ★ WoWoWocal | Simplify README by removing setup and workflow details |
| 2026-04-21 02:15 | `d5dc881` | ★ WoWoWocal | chore: adopt dev+main workflow |
| 2026-04-21 02:32 | `d3447a4` | ★ WoWoWocal | chore: simplify repo workflow to dev and main |
| 2026-04-21 17:24 | `e63149e` | ★ WoWoWocal | Update README.md |
| 2026-04-23 16:43 | `d30fa10` | ★ WoWoWocal | Update CONTRIBUTING.md to simplify branch guidelines |
| 2026-04-23 20:43 | `c363bbb` | ★ WoWoWocal | Update README with backend and frontend setup instructions |
| 2026-04-23 20:45 | `6b5524c` | ★ WoWoWocal | Refactor README for clarity and structure |
| 2026-04-21 03:05 | `f993384` | ★ WoWoWocal | feat: add sprint 1 vertical slice foundation on dev |
| 2026-04-23 14:19 | `afa33a1` | dano-4 | Add card API integration with CardService and CardsController, furthermore added HealthController to check if API is responsive |
| 2026-04-26 13:40 | `46b7d5b` | dano-4 | Extended the CardDto Class to the full values of the cards |
| 2026-04-26 14:22 | `f86c00d` | dano-4 | Changed JsonPropertyNames to the actual ones of the Api |
| 2026-04-26 14:33 | `28354c3` | dano-4 | Added another Task Method to fetch all cards from a single set |
| 2026-04-26 14:54 | `f5b8840` | dano-4 | Task GetSetById to fetch all cards within a set has been added and implemented in the Controller |
| 2026-04-26 14:55 | `c3ebb63` | dano-4 | Task has been added to fetch a whole Dictionary of cards from a set with the specified Set-Id |
| 2026-04-29 17:18 | `158e412` | danielbna | Revised formatting problems |
| 2026-05-04 21:49 | `ec63ebc` | dano-4 | Modified gitignore |
| 2026-05-12 14:55 | `3fcf7f6` | ★ WoWoWocal | feat: add local deckbuilder editing |
| 2026-05-12 15:00 | `f76b28b` | ★ WoWoWocal | feat: persist deckbuilder drafts locally |
| 2026-05-12 16:19 | `2b6e969` | ★ WoWoWocal | feat: deckbuilder |
| 2026-05-12 17:13 | `f131f49` | danielbna | Update availableSets format by adding dashes |
| 2026-05-12 17:32 | `0e3d9fa` | ★ WoWoWocal | feat: add local deck library and tools foundation |
| 2026-05-12 17:42 | `8c15488` | ★ WoWoWocal | feat: add local deck library and tools foundation |
| 2026-05-12 17:32 | `424c6f5` | ★ WoWoWocal | feat: add local deck library and tools foundation |
| 2026-05-12 17:42 | `0b1ba4b` | ★ WoWoWocal | feat: add local deck library and tools foundation |
| 2026-05-12 23:14 | `bfd3ef1` | ★ WoWoWocal | fix: stabilize deckbuilder save navigation flow |
| 2026-05-13 00:19 | `fccf35c` | ★ WoWoWocal | feat: redesign match board layout |
| 2026-05-13 14:24 | `aa5d105` | ★ WoWoWocal | Calculator |
| 2026-05-13 14:34 | `b0ed497` | ★ WoWoWocal | feat: add match connection status |
| 2026-05-13 14:51 | `31fbbf8` | ★ WoWoWocal | feat: add saved deck selection to match lobby |
| 2026-05-13 14:58 | `0ae3cb5` | ★ WoWoWocal | feat: register selected deck in match room |
| 2026-05-13 17:01 | `cf21aeb` | ★ WoWoWocal | feat: initialize match decks and opening hands |
| 2026-05-13 17:10 | `f5de655` | ★ WoWoWocal | feat: resolve mulligan choices server-side |
| 2026-05-13 17:22 | `48be413` | ★ WoWoWocal | feat: add basic turn phase flow |
| 2026-05-13 17:30 | `262d013` | ★ WoWoWocal | feat: add basic main phase play card action |
| 2026-05-13 17:32 | `0dda3ac` | danielbna | Enhance deploy.yml with frontend build and Nginx reload |
| 2026-05-13 17:33 | `009e8cb` | 13-Tino-13 | Add PostgreSQL setup section to README |
| 2026-05-13 17:35 | `ca83cf3` | danielbna | Enhance deploy script for backend and frontend |
| 2026-05-13 17:35 | `01f1976` | ★ WoWoWocal | feat: add basic attack action |
| 2026-05-13 17:37 | `36d3eaf` | 13-Tino-13 | Update appsettings.Development.json for connection string |
| 2026-05-13 17:43 | `fd9e7ff` | 13-Tino-13 | Add backend setup instructions to README |
| 2026-05-13 18:30 | `fe1e15e` | danielbna | Add exception handling with logging in CardService |
| 2026-05-13 18:34 | `2a0a21b` | ★ WoWoWocal | feat: filter deckbuilder cards by leader color |
| 2026-05-13 18:40 | `23f892d` | danielbna | Update Life property type in CardDto |
| 2026-05-13 18:46 | `078512f` | danielbna | Change CardCost, CardPower, and CounterAmount to string |
| 2026-05-13 18:55 | `7b80023` | danielbna | Change CardDto properties to use int? with JsonNumberHandling |
| 2026-05-13 19:00 | `9f2de83` | danielbna | Change Life property type to string |
| 2026-05-13 22:36 | `2194ca1` | Tino Tomasic | Add PostgreSQL card database setup |
| 2026-05-13 23:19 | `054195c` | ★ WoWoWocal | feat: add deckbuilder color palette filter |
| 2026-05-13 23:32 | `aeac2cc` | ★ WoWoWocal | feat: simplify deckbuilder card grid |
| 2026-05-14 01:40 | `58d5493` | dano-4 | Troubleshooting Fetch problems for sets OP-02/03 etc... |
| 2026-05-14 01:42 | `d0a1d57` | ★ WoWoWocal | feat: add deckbuilder card size slider |
| 2026-05-14 02:09 | `24e372d` | dano-4 | Added Endpoint handling for Starter Decks |
| 2026-05-14 02:13 | `feb6b43` | ★ WoWoWocal | feat: add compact deckbuilder quantity controls |
| 2026-05-14 02:21 | `80ababe` | dano-4 | Added every existing Deck to the array |
| 2026-05-14 02:28 | `8780095` | ★ WoWoWocal | feat: add global website background image |
| 2026-05-14 02:34 | `0acbe0c` | dano-4 | Changed values to fix fetching problems |
| 2026-05-14 02:38 | `2ab371e` | dano-4 | Fixed type problems for fetching data |
| 2026-05-14 02:43 | `56cc868` | dano-4 | Switched to datatype object due to serialization problems |
| 2026-05-14 02:49 | `40556d1` | ★ WoWoWocal | …….. |
| 2026-05-14 02:52 | `0f0aea0` | dano-4 | Removed OP set 14 & 15 due to not findable sets |
| 2026-05-14 02:53 | `6dfe329` | ★ WoWoWocal | style: color deckbuilder quantity buttons |
| 2026-05-14 03:04 | `414d9e4` | ★ WoWoWocal | feat: replace deckbuilder header banner with custom image |
| 2026-05-14 03:12 | `690fc7a` | ★ WoWoWocal | style: make deckbuilder banner borderless |
| 2026-05-14 03:23 | `ff253e1` | ★ WoWoWocal | feat: integrate pirate board into match view |
| 2026-05-14 03:40 | `5f964c0` | Tino Tomasic | Add card import endpoint |
| 2026-05-14 14:20 | `901bc4f` | ★ WoWoWocal | Include image in README section |
| 2026-05-14 15:51 | `2b9828c` | Codex | feat: add visual match board layout |
| 2026-05-14 18:58 | `241f9f0` | Codex | style: update deckbuilder banner |
| 2026-05-14 21:22 | `75714d3` | ★ WoWoWocal | feat: add fullscreen start screen |
| 2026-05-14 21:45 | `b696b81` | ★ WoWoWocal | feat: update start screen artwork |
| 2026-05-14 22:00 | `aaa9849` | ★ WoWoWocal | feat: refine start screen play button |
| 2026-05-14 22:15 | `f03b52c` | ★ WoWoWocal | feat: align start button with subtitle |
| 2026-05-14 22:43 | `4a59717` | ★ WoWoWocal | style: enlarge and raise start screen logo |
| 2026-05-14 22:59 | `6a19d9e` | ★ WoWoWocal | feat: improve responsive start menu |
| 2026-05-15 13:43 | `0753aa5` | ★ WoWoWocal | feat: use image hotspots for start screen |
| 2026-05-15 15:02 | `66b7b8e` | ★ WoWoWocal | chore: remove global background and deckbuilder image |
| 2026-05-15 15:32 | `e55d4c8` | Codex | feat: play selected hand cards on match board |
| 2026-05-15 17:00 | `8489cbc` | ★ WoWoWocal | feat: open joined matches in fullscreen board mode |
| 2026-05-15 17:37 | `9cd168b` | ★ WoWoWocal | style: polish fullscreen match board ux |
| 2026-05-15 20:34 | `45950b6` | ★ WoWoWocal | style: calibrate match board layout |
| 2026-05-15 21:35 | `60affb5` | ★ WoWoWocal | style: improve match card presentation |
| 2026-05-15 21:48 | `bb2d487` | ★ WoWoWocal | style: improve match lobby setup ux |
| 2026-05-15 22:32 | `e9c9c49` | ★ WoWoWocal | style: add profile login placeholder ui |
| 2026-05-15 22:41 | `be47aea` | ★ WoWoWocal | style: polish tools page ux |
| 2026-05-15 22:58 | `b76b118` | ★ WoWoWocal | refactor: split frontend styles by feature |
| 2026-05-15 23:10 | `92b8f52` | ★ WoWoWocal | style: polish app ui for demo |
| 2026-05-16 14:25 | `0f093b8` | ★ WoWoWocal | style: improve deckbuilder playable card filtering |
| 2026-05-16 15:19 | `f7f4464` | ★ WoWoWocal | style: streamline match setup ui |
| 2026-05-16 15:49 | `a4bab4c` | ★ WoWoWocal | style: simplify match setup layout |
| 2026-05-16 16:26 | `6598c2d` | ★ WoWoWocal | fix: show match board after joining room |
| 2026-05-16 16:37 | `6cfe174` | ★ WoWoWocal | fix: render match board in joined room |
| 2026-05-16 17:38 | `89021bf` | ★ WoWoWocal | fix: resolve mobile match hub connection |
| 2026-05-16 17:55 | `890623c` | ★ WoWoWocal | feat: add pirate display name randomizer |
| 2026-05-16 18:05 | `4366295` | ★ WoWoWocal | style: remove redundant page descriptions |
| 2026-05-17 18:01 | `e24e0e2` | ★ WoWoWocal | style: improve tools deck selection and deckbuilder layout |
| 2026-05-17 20:46 | `6b2d546` | ★ WoWoWocal | style: add deckbuilder hover preview and click add |
| 2026-05-17 20:55 | `db472e2` | ★ WoWoWocal | style: refine deckbuilder card preview and counters |
| 2026-05-17 22:26 | `4f6af1f` | ★ WoWoWocal | style: simplify deckbuilder preview and counters |
| 2026-05-17 22:57 | `cee34db` | ★ WoWoWocal | style: simplify deckbuilder filters and leader summary |
| 2026-05-18 12:55 | `b9262de` | Codex | fix: resolve match lobby connection and simplify setup |
| 2026-05-18 13:02 | `6e440ef` | Codex | style: simplify deckbuilder panels and remove main menu button |
| 2026-05-18 13:29 | `cea01e6` | Codex | style: show deck list as card stacks |
| 2026-05-18 13:49 | `7afa33b` | Codex | style: improve deckbuilder deck stacks and card scrolling |
| 2026-05-18 14:17 | `b9a4221` | Codex | style: refine deckbuilder stacks filters and scrolling |
| 2026-05-18 14:28 | `5574664` | Codex | style: fix deckbuilder stack preview layout |
| 2026-05-18 14:48 | `04ca928` | Codex | style: refine deckbuilder search sort and preview |
| 2026-05-18 15:11 | `fc9d673` | Codex | style: fix deckbuilder card stacking and sorting |
| 2026-05-18 15:26 | `2a23304` | Codex | style: fix deckbuilder preview alignment |
| 2026-05-18 15:40 | `46813e4` | Codex | style: compact deckbuilder filter toolbar |
| 2026-05-18 16:39 | `184ffa4` | Codex | style: simplify deckbuilder filter toolbar |
| 2026-05-18 16:58 | `92048d1` | Codex | style: refine deck list actions and counters |
| 2026-05-18 17:08 | `44f7caa` | Codex | style: tune deckbuilder card spacing |
| 2026-05-18 17:22 | `92f8d4d` | Codex | style: fix deck list card stacks |
| 2026-05-18 18:36 | `41df9c0` | ★ WoWoWocal | style: improve deckbuilder scrolling and match deck selection |
| 2026-05-18 18:38 | `dff1d9d` | ★ WoWoWocal | style: load match deck leader previews |
| 2026-05-18 21:36 | `c0d450b` | Codex | style: enlarge deckbuilder card preview |
| 2026-05-19 01:16 | `3610a43` | Codex | style: move deck actions and fix playable card sorting |
| 2026-05-19 02:40 | `271b39e` | ★ WoWoWocal | style: reduce card corner radius |
| 2026-05-19 02:46 | `c8eac75` | Tino Tomasic | Read cards from database before API fallback |
| 2026-05-19 03:07 | `51d11f6` | ★ WoWoWocal | style: improve deckbuilder preview scroll and stack add |
| 2026-05-19 03:12 | `d43399e` | ★ WoWoWocal | Revert "style: improve deckbuilder preview scroll and stack add" |
| 2026-05-19 03:40 | `25ab6f2` | Tino Tomasic | Import missing card sets on demand |
| 2026-05-19 16:20 | `4b31627` | danielbna | Add database connection string to appsettings.json |
| 2026-05-19 16:55 | `b7b8be9` | Codex | style: refine match deck selection layout |
| 2026-05-19 17:07 | `8e82141` | Codex | style: improve deckbuilder preview hover and scrolling |
| 2026-05-19 20:20 | `aa3e930` | Codex | style: refine deckbuilder filters and library |
| 2026-05-20 22:36 | `8d0535a` | Tino Tomasic | Add endpoint for available card sets |
| 2026-05-20 22:41 | `be31497` | Codex | style: refine deckbuilder hover states and scrollbars |
| 2026-05-22 16:15 | `7f9da18` | dano-4 | refactor game architecture and extract core match actions |
| 2026-05-22 16:33 | `c11cdf4` | dano-4 | fixed problems of github backend tests |
| 2026-06-01 20:06 | `a1e016b` | danielbna | Extracted Main Action Handling and Mulligan Handling into own functions. |
| 2026-11-03 21:40 | `f983407` | dano-4 | Add server create/join/leave with live server list and auto cleanup |
| 2026-11-07 13:49 | `c263f41` | dano-4 | Implement playable OPTCG match rules and show real cards on the board |
| 2026-11-09 18:44 | `1f8e85e` | ★ WoWoWocal | chore: add script to create backlog and sprint issues on GitHub |
| 2026-11-11 18:18 | `b5a5dbe` | ★ WoWoWocal | Redesign match room board as CSS grid with Hearthstone-style hand |
| 2026-11-12 20:22 | `fdb4f36` | ★ WoWoWocal | Match room: bigger board, compact choice bar, side preview and log/chat feed |
| 2026-11-18 16:00 | `b65b1a5` | ★ WoWoWocal | Redesign match lobby as TCG-style entry screen |
| 2026-11-18 16:20 | `ed24cda` | ★ WoWoWocal | Add deck import and export as text |
| 2026-11-18 16:22 | `28b1aa8` | ★ WoWoWocal | Add stable guest ID for players |
| 2026-11-18 16:28 | `0e417b0` | ★ WoWoWocal | Save match results and show real profile statistics |
| 2026-11-18 16:32 | `a7ff8a5` | ★ WoWoWocal | Add Quick Match queue |
| 2026-11-18 16:34 | `99e535f` | ★ WoWoWocal | Add changelog and What's new modal |
| 2026-11-18 17:40 | `74459f9` | ★ WoWoWocal | Add shared ui.css design system and shared icons |
| 2026-11-18 17:44 | `c276b5f` | ★ WoWoWocal | Redesign Tools page with the shared design system |
| 2026-11-18 17:46 | `b6b0f1a` | ★ WoWoWocal | Redesign Profile and Start screen with the shared design system |
| 2026-11-18 17:52 | `f78d27b` | ★ WoWoWocal | Restyle Deckbuilder with the shared design system (visual only) |
| 2026-11-18 18:21 | `a47ff56` | ★ WoWoWocal | Decide the first player with a server-side dice roll |
| 2026-11-18 18:56 | `442676b` | ★ WoWoWocal | Add deterministic dice physics simulation (not wired into the UI yet) |
| 2026-11-18 19:27 | `0cde367` | ★ WoWoWocal | Animate the dice roll for the first player |
| 2026-11-18 19:30 | `b8ad80e` | ★ WoWoWocal | Store ranked results and ELO player ratings |
| 2026-11-18 19:41 | `7cfd9a1` | ★ WoWoWocal | Add bounty leaderboard API and bounty UI |
| 2026-11-18 19:46 | `f0e3b0b` | ★ WoWoWocal | Add external meta provider (Card Kaizoku) and /meta API |
| 2026-11-18 19:53 | `0d55be1` | ★ WoWoWocal | Add Meta tab with external provider data and document it |
| 2026-11-18 23:19 | `759cfec` | dano-4 | Add generic card effect engine with parser for card texts |
| 2026-11-20 13:35 | `1e38b5f` | ★ WoWoWocal | Make match log/chat feed resizable and collapsible |
| 2026-11-20 13:40 | `101bcf8` | ★ WoWoWocal | Scale match board to its container and improve readability |
| 2026-11-20 13:49 | `dd0004b` | ★ WoWoWocal | Add drag and drop for hand cards and DON!! |
| 2026-11-20 13:53 | `f9f890b` | ★ WoWoWocal | Use team card art for card backs and DON!! with neutral fallback |
| 2026-11-20 14:03 | `d06c5bc` | ★ WoWoWocal | Keep hint toast clear of the resting hand on short screens |
| 2026-11-20 17:54 | `a2c1403` | Claude | Enable Card Kaizoku meta data in production |
| 2026-11-20 18:21 | `b358dd8` | Claude | Allow a separate logic branch for the rules engine rework |
