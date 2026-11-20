# Screenshots

Dateiname: `JJJJ-MM-TT_nr-name.png`. Neue Screenshots bei jeder sichtbaren Änderung ergänzen.

## Stand 2026-11-20 (v0.5.0)

Aufgenommen mit Playwright (Chromium, 1440 × 900), Frontend lokal per `npm run dev`.
**Hinweis:** Das Backend lief dabei nicht – daher zeigen Lobby, Deckbuilder, Meta und Profil
Fehlerzustände („Connection error“, „Failed to fetch“, „Erneut versuchen“). Für die Arbeit sollten
zusätzlich Screenshots mit laufendem Backend gemacht werden (siehe Liste unten).

| Datei | Zeigt |
|-------|-------|
| [2026-11-20_01-startscreen.png](2026-11-20_01-startscreen.png) | Startbildschirm mit Artwork und Bild-Hotspots (Play/Login) |
| [2026-11-20_02-lobby.png](2026-11-20_02-lobby.png) | Match-Lobby (v0.4.0-Design), offene Tische, „v0.5.0 · Was ist neu?“ |
| [2026-11-20_03-was-ist-neu.png](2026-11-20_03-was-ist-neu.png) | „Was ist neu“-Fenster mit Versionen 0.1.0–0.5.0 (vor der Ergänzung vom 20.11.) |
| [2026-11-20_04-deckbuilder.png](2026-11-20_04-deckbuilder.png) | Deckbuilder im Designsystem (ohne Kartendaten) |
| [2026-11-20_05-meta.png](2026-11-20_05-meta.png) | Meta-Tab mit Tabs Leader / Matchups / Bounty-Rangliste (Fehlerzustand) |
| [2026-11-20_06-tools.png](2026-11-20_06-tools.png) | Captain Tools: Mulligan-Trainer und Wahrscheinlichkeitsrechner |
| [2026-11-20_07-profile.png](2026-11-20_07-profile.png) | Captain Profile mit WANTED-Steckbrief |

## Noch aufzunehmen (mit laufendem Backend)

- Deckbuilder mit Karten, Filtern und fertigem Deck
- Lobby mit gewähltem Leader (Leader-Farbe), Quick-Match-Suche
- Würfelwurf-Animation zu Spielbeginn
- Match-Board: Hand, Drag & Drop, Angriff/Counter, Effekt-Auswahl, Log/Chat-Feed
- Match-Board am Handy (Querformat)
- Profil mit echter Statistik und Kopfgeld-Verlauf, Bounty-Rangliste
- Meta-Tab mit Leader-Tabelle und Matchup-Heatmap
- Ältere Stände zum Vergleich (Board vor/nach 11.11., Lobby vor/nach 18.11.), z. B. per
  `git worktree add ../onesake-alt <commit>` und Screenshot

## Ältere Screenshots

- README (Mai 2026): Bild der damaligen Oberfläche, eingebunden in `README.md` (Commit `901bc4f`, 2026-05-14 14:20).
