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

## Stand 2026-11-21 – Reconnect

Aufgenommen mit Playwright (Chromium, 1280 × 760, zwei Browser-Kontexte), Backend lokal mit
verkürzter Schonfrist (`Match__ReconnectGraceSeconds=25`), Testdeck ohne Kartenbilder.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_01-reconnect-banner.png](2026-11-21_01-reconnect-banner.png) | Eigene Verbindung weg: Banner „Verbindung verloren – verbinde neu … (0:24)“, Board ausgegraut |
| [2026-11-21_02-gegner-getrennt-countdown.png](2026-11-21_02-gegner-getrennt-countdown.png) | Gegner getrennt: „Getrennt · 0:25“ am Namensschild, Log-Eintrag |
| [2026-11-21_03-wieder-verbunden.png](2026-11-21_03-wieder-verbunden.png) | Nach dem Rejoin: „Wieder verbunden ✓“ |
| [2026-11-21_04-zurueck-ins-match.png](2026-11-21_04-zurueck-ins-match.png) | Nach dem Neuladen: Karte „Laufendes Match gefunden – Zurück ins Match“ |
| [2026-11-21_05-sieg-gegner-getrennt.png](2026-11-21_05-sieg-gegner-getrennt.png) | Frist abgelaufen, Sicht des Gewinners: „Gegner hat die Verbindung verloren – Sieg“ |
| [2026-11-21_06-niederlage-verbindung.png](2026-11-21_06-niederlage-verbindung.png) | Frist abgelaufen, Sicht des Getrennten nach der Rückkehr: „Verbindung verloren – Niederlage“ |

## Stand 2026-11-21 – Zug-Timer

Wie oben, Backend mit kurzen Timer-Werten (Zugzeit 20 s, Zeitbank 10 s, Antwortzeit 5 s).

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_07-timer-normal.png](2026-11-21_07-timer-normal.png) | Eigener Zug: Timer-Ring am Namensschild, Balken „Zugzeit“ über der Aktionsleiste, Zeitbank ⌛ als kleine Zahl |
| [2026-11-21_08-timer-orange.png](2026-11-21_08-timer-orange.png) | Weniger als 15 s Gesamtzeit: Ring und Balken orange |
| [2026-11-21_09-timer-rot-zeitbank.png](2026-11-21_09-timer-rot-zeitbank.png) | Zeitbank läuft, unter 5 s: rot und pulsierend, Hinweis „Zeitbank“ |
| [2026-11-21_10-niederlage-zeit-abgelaufen.png](2026-11-21_10-niederlage-zeit-abgelaufen.png) | Zweiter Zug in Folge ohne Zeit: „Zeit abgelaufen – Niederlage“ |
| [2026-11-21_11-antwortzeit-counter.png](2026-11-21_11-antwortzeit-counter.png) | Counter-Entscheidung des Verteidigers mit „Antwortzeit“ (im Hintergrund hängt das Würfel-Overlay – bekannter, älterer Anzeigefehler) |

## Stand 2026-11-21 – Match-Animationen (Branch dev)

Playwright (Chromium, 1280 × 760, zwei Browser), lokales Backend von `dev`, rotes OP01-Testdeck. Nummern ab `_20`, damit sie nicht mit den Bildern vom Branch `logic` kollidieren.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_20-match-einstellungen.png](2026-11-21_20-match-einstellungen.png) | Zahnrad im Match: Animationen Normal / Schnell / Aus, Vibration |

## Bildfolgen: Ziehen, Spielen, Events

Kontaktblätter: 10 Bilder im Abstand von ~100 ms (Zeit links oben), Sicht des Gegners.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_21-bildfolge-gegner-spielt-karte.png](2026-11-21_21-bildfolge-gegner-spielt-karte.png) | Gegner spielt eine Character-Karte: groß in der Mitte, dann in den Slot, danach kurz markiert |
| [2026-11-21_22-bildfolge-gegner-spielt-event.png](2026-11-21_22-bildfolge-gegner-spielt-event.png) | Gegner nutzt ein [Counter]-Event: großer Reveal mit Goldleuchten, dann in den Trash |

## Bildfolgen: Kampf

Kontaktblätter wie oben. Der Bot spielt beide Seiten über die echte Oberfläche.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_23-bildfolge-angriff-zielpfeil.png](2026-11-21_23-bildfolge-angriff-zielpfeil.png) | Angriffsziel wählen: goldener Zielpfeil folgt dem Mauszeiger, gültige Ziele leuchten |
| [2026-11-21_24-bildfolge-angriff.png](2026-11-21_24-bildfolge-angriff.png) | Angriff aus Sicht des Verteidigers: Ausfallschritt, Drehung beim Resten, „5000 ⚔ 5000“ |
| [2026-11-21_25-bildfolge-ko.png](2026-11-21_25-bildfolge-ko.png) | K.O. (Bild 6–8): Karte zerfällt in Splitter, Geist fliegt in den Trash |
| [2026-11-21_26-bildfolge-life-trigger.png](2026-11-21_26-bildfolge-life-trigger.png) | Life verloren mit [Trigger] (Sicht des Gegners): Karte groß mit „TRIGGER“ |
| [2026-11-21_27-bildfolge-counter.png](2026-11-21_27-bildfolge-counter.png) | Counter: „+1000“ steigt über dem Ziel auf, Power zählt hoch |

## Bildfolgen: DON!! und Effekte

Kontaktblätter wie oben.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_28-bildfolge-don.png](2026-11-21_28-bildfolge-don.png) | DON!! an den Leader (Sicht des Gegners): DON!!-Karte fliegt auf den Leader, „+1000“ |
| [2026-11-21_29-bildfolge-effekt.png](2026-11-21_29-bildfolge-effekt.png) | Otama [On Play] −2000: nach der Auswahl goldener Puls an der Quelle und Linie zum Ziel, Power rot |

## Bildfolgen: Zugwechsel und Spielende

Kontaktblätter wie oben.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_30-bildfolge-zugwechsel.png](2026-11-21_30-bildfolge-zugwechsel.png) | „DEIN ZUG · Zug 2“ läuft über das Board, danach leuchtet die Aktionsleiste |
| [2026-11-21_31-bildfolge-sieg.png](2026-11-21_31-bildfolge-sieg.png) | Letzter Treffer, dann „SIEG“ mit Goldkonfetti, Leader, Zügen und Kopfgeld (+฿ 20.000, kommt nach ~1,5 s vom Server) |
| [2026-11-21_32-niederlage.png](2026-11-21_32-niederlage.png) | „NIEDERLAGE“ mit dunklem Schleier, Kopfgeld −฿ 20.000, Revanche-Tisch / Zurück zur Lobby |

## Sound-Einstellungen

Playwright wie oben.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_33-sound-einstellungen.png](2026-11-21_33-sound-einstellungen.png) | Zahnrad: Animationen, Sound an/aus, Lautstärke-Regler, Vibration |

## Tastenkürzel und Hinweise

Playwright wie oben.

| Datei | Zeigt |
|-------|-------|
| [2026-11-21_34-tastenkuerzel.png](2026-11-21_34-tastenkuerzel.png) | „?“ öffnet die Übersicht aller Tastenkürzel |
| [2026-11-21_35-leertaste-bestaetigen.png](2026-11-21_35-leertaste-bestaetigen.png) | Einmal Leertaste: Hinweis „nochmal drücken (oder halten)“, der Zug endet erst beim zweiten Druck |
| [2026-11-21_36-gegnerkarte-hinweise.png](2026-11-21_36-gegnerkarte-hinweise.png) | Hover über eine Gegnerkarte: Mini-Icon „✦ [Trigger]“ |
| [2026-11-21_37-zug-beenden-hinweis.png](2026-11-21_37-zug-beenden-hinweis.png) | Keine sinnvolle Aktion mehr: „End Turn“ leuchtet stärker; Kürzel D / A / E / Leer auf den Buttons |

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
