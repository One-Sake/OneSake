# Ideen, Entscheidungen und Überlegungen

Sammlung aller Ideen und Entscheidungen rund um OneSake – auch der, die (noch) nicht umgesetzt
oder bewusst verworfen wurden. Quellen: Projektauftrag, ADRs, Backlog (User Stories #1–#57),
Commit-Beschreibungen, Dokumentation. Stand: 2026-11-20.

## 1. Ausgangsidee und Motivation

- **Problem:** Bestehende OPTCG-Simulatoren (z. B. Project Raftel, OPTCGSim) sind am Handy schwer
  bedienbar: zu viele Panels, kleine Touch-Ziele, kein echter Vollbildmodus.
- **Idee:** Eine mobile-first Plattform im Browser, die Spielen (PvP), Deckbau, Trainings-Tools und
  Motivation (Progression) an einem Ort vereint.
- **Name „OneSake“ / Slogan „Sake it till you make it“:** Wortspiel auf „One Piece“ und den Sake,
  den die Piraten trinken; Piraten-Thema zieht sich durch die ganze App (Piratennamen, Kopfgeld,
  Steckbrief, „Captain Tools“, „Bereit für die See?“).

## 2. Ideenliste mit Status

### 2.1 Umgesetzt

| Idee | Seit | Quelle / Commit | Bemerkung |
|------|------|-----------------|-----------|
| Server-autoritative, choice-driven Spielengine | 21.04. | ADR 0001, `f993384` | Client sendet nur Entscheidungen |
| Vertical Slice (Raum → Start → Mulligan → Zug → Chat/Log) | 21.04. | Sprint-1-Backlog | |
| CI/CD mit GitHub Actions, Deployment auf Server mit Nginx | 20.04./13.05. | `1adee21`, `0dda3ac` | |
| Deckbuilder mit Suche, Farbfiltern, Validierung, Bibliothek | 12.05. | `2b6e969` u. a. | Speicherung lokal im Browser |
| Kartengrößen-Slider, Hover-Vorschau, Kartenstapel | 14.–18.05. | `d0a1d57`, `6b2d546`, `cea01e6` | |
| Wahrscheinlichkeitsrechner (Monte-Carlo im Web Worker) | 12.05. | `e1d46f8` | Projektauftrag 4.2 |
| Mulligan-Trainer | 12.05. | Tools-Grundlage | mit Kartenbildern seit 18.11. |
| PostgreSQL-Kartendatenbank, API nur als Fallback | 13.–19.05. | `2194ca1`, `c8eac75` | |
| Vollbild-Startbildschirm mit Bild-Hotspots | 14.–15.05. | `75714d3`, `0753aa5` | |
| Zufällige Piratennamen | 16.05. | `890623c` | Würfel-Button neben dem Namen |
| Verbindung vom Handy im lokalen Netz | 16.05. | `89021bf` | |
| Server-Liste (offene Tische), Auto-Löschen leerer Räume | 03.11. | `f983407` | v0.1.0 |
| Echte OPTCG-Regeln inkl. Keywords | 07.11. | `c263f41` | v0.2.0 |
| Board als CSS-Grid, Hand im Hearthstone-Stil | 11.11. | `b5a5dbe` | v0.3.0 |
| Log und Chat in einer gemeinsamen Zeitleiste | 12.11. | `fdb4f36` | Projektauftrag 5.4 |
| Lobby im TCG-Stil, Farbe nach Leader | 18.11. | `b65b1a5` | v0.4.0 |
| Deck-Import/-Export als Text | 18.11. | `ed24cda` | |
| Gast-ID statt Login | 18.11. | `28b1aa8` | Zwischenlösung |
| Match-Ergebnisse und Profilstatistik | 18.11. | `0e417b0` | |
| Quick Match (Matchmaking-Warteschlange) | 18.11. | `a7ff8a5` | |
| „Was ist neu“ mit Versionsnummer | 18.11. | `99e535f` | wird laufend ergänzt |
| Gemeinsames Designsystem | 18.11. | `74459f9` | |
| Würfelwurf für Startspieler mit Physik-Animation | 18.11. | `a47ff56`, `0cde367` | |
| Ranked mit ELO, Kopfgeld, Steckbrief, Rangliste | 18.11. | `b8ad80e`, `7cfd9a1` | |
| Meta-Statistiken (Card Kaizoku) | 18.11. | `f0e3b0b`, `0d55be1` | Produktion seit 20.11. |
| Generische Effekt-Engine für Kartentexte | 18.11. | `759cfec` | Ausbau auf Branch `logic` |
| Drag & Drop, skalierendes Board, verstellbarer Feed | 20.11. | `dd0004b`, `101bcf8`, `1e38b5f` | |
| Eigene Kartenrückseiten | 20.11. | `f9f890b` | |
| Wiederverbinden nach Verbindungsabbruch (#47), „Zurück ins Match“ nach dem Neuladen | 21.11. | Branch `logic` | Schonfrist 90 s, Budget 180 s |
| Zug-Timer mit Zeitbank, wählbar bei eigenen Tischen | 21.11. | Branch `logic` | Regeln in `docs/turn-timer.md` |

### 2.2 Offen (geplant, Backlog)

| Idee | User Story | Herkunft |
|------|------------|----------|
| Konto registrieren / anmelden, Upgrade Gast → Konto | #42, #48, #52 | Projektauftrag 3.1 (6) |
| Decks auf dem Server speichern (geräteübergreifend) | #43 | Projektauftrag |
| Berries nach dem Match verdienen | #53 | Projektauftrag 3.1 (4) |
| Shop mit Cosmetics (Alt Arts, Kartenhüllen, Hintergründe, Profilbilder), ausrüsten, im Match sichtbar | #50, #54, #55 | Projektauftrag Phase 4 |
| Namen statt IDs im Chat/Log, Filter Chat/Aktionen | #44 | Projektauftrag 5.4 |
| Deckbuilder am Handy flüssig (Virtualisierung, Lazy Loading) | #45 | Projektauftrag 4.2 |
| OPTCGSim-Importstring | #46 | Projektauftrag 3.1 (2) – teilweise durch Text-Export erfüllt |
| Automatisierte Frontend-Tests | #49 | Qualität |
| Einladungslink teilen, App installierbar (PWA) | #51 | Projektauftrag (Web/PWA) |
| Alle Kartenversionen inkl. Alt-Arts | #56 | Community-Wunsch |
| Vollständige Effektabdeckung, richtige Trigger-Reihenfolge | – | Branch `logic`, Arbeitsplan `docs/spiellogik-plan.md` (23.11.) |
| Nur fertige (geprüfte) Karten in Ranked/Quick Match, Status im Deckbuilder | – | Plan A6 (23.11.), wie XMage/Forge |
| Effekt-Orakel im Simulator (prüft die Wirkung jedes Effektschritts) | – | Plan 6.1 (23.11.) |
| Review-Ansicht mit Rückübersetzung Skript → Text | – | Plan A8 (23.11.) |
| KI-Entwürfe für Kartenskripte (mit Review und Test) | – | Plan A9 (23.11.) – Entscheidung im Team offen |
| Usability-Test: bestehende Lösung vs. OneSake am Handy | – | Projektauftrag Phase 5 |
| Demo-Video, Abnahme | – | Projektauftrag Phase 5 |
| Zug-Timer mit Zeitbank | – | Roadmap 21.11. (`04-roadmap-und-alleinstellungsmerkmale.md`) |
| Spiel-Gefühl: Animationen, Sounds, Tastenkürzel, Auto-Pass | – | Roadmap 21.11. |
| Tutorial und KI-Gegner mit Schwierigkeitsgraden | – | Roadmap 21.11. |
| Replay-Viewer mit „Was wäre wenn?“ (ab beliebiger Stelle selbst weiterspielen) | – | Roadmap 21.11. – Alleinstellungsmerkmal |
| Coach nach dem Match („verpasster Lethal“) und tägliches Lethal-Puzzle | – | Roadmap 21.11. – Alleinstellungsmerkmal |
| Deck-Labor: eigenes Deck per Bot-Simulation gegen die Meta testen | – | Roadmap 21.11. – Alleinstellungsmerkmal |
| Ränge mit One-Piece-Titeln, Banden (Crews), teilbarer Steckbrief | – | Roadmap 21.11. |
| Streamer-/Turnier-Modus (Caster-Ansicht, OBS-Overlay, Swiss-Turniere) | – | Roadmap 21.11. |
| Deutsch und Englisch | – | Roadmap 21.11. |

### 2.3 Verworfen oder geändert

| Idee | Wann | Warum verworfen / geändert |
|------|------|----------------------------|
| Strenger Workflow mit Feature-Branches + Pflicht-Review (ADR 0002) | 21.04. | Zu viel Aufwand für ein kleines Team → dev+main (ADR 0003) |
| Ausführliche README mit allen Details | 21.04. | Kurz gehalten, Details in `docs/` |
| Globales Hintergrundbild und Deckbuilder-Bild | 15.05. (`66b7b8e`) | Zu unruhig, lenkt von Karten ab |
| Spielbrett als Hintergrundbild mit %-Positionen (`ff253e1`) | 11.11. (`b5a5dbe`) | Passte nur bei einer Bildschirmgröße → CSS-Grid |
| Bottom-Sheet für Keep/Mulligan | 12.11. | Verdeckte die Hand → kleine Leiste auf der Mittellinie |
| Sets OP-14/15 abrufen | 14.05. (`0f0aea0`) | Damals in der API nicht vorhanden |
| Vorschau-Scroll-Änderung im Deckbuilder | 19.05. (`d43399e`) | Nach 5 Minuten zurückgenommen (Verschlechterung) |
| Eigene Tier-Liste im Meta-Tab | 18.11. | Anbieter liefert keine Tiers; keine eigenen Bewertungen erfinden |
| „Nur Choices, kein Drag & Drop“ (Projektauftrag, Risiken) | 20.11. | Drag & Drop zusätzlich eingeführt, weil es sich natürlicher anfühlt; Server-Validierung bleibt gleich |
| Manueller Effekt-Modus als Dauerlösung („Effekt bitte manuell ausführen“) | 23.11. | Online zwischen Fremden nicht fair spielbar; andere Simulatoren sperren nicht umgesetzte Karten → nur noch Übergang, später entfernt (Plan A6) |
| Spiellogik aus YouTube-Videos lernen | 23.11. | Videos zeigen nur das Ergebnis, nicht die Regel dahinter; Spielfehler; kaum Sonderfälle → Regelwerk und Bandai-Q&A (Plan A13) |
| Parser allein weiter ausbauen | 23.11. | 1.115 unverstandene Textteile als langer Schwanz (häufigstes Muster nur 48×) → Hybrid aus Parser-Entwurf, Review und Test (Plan A1) |
| Ranked/MMR „out of scope“ (Projektauftrag 3.2) | 18.11. | Doch umgesetzt (ELO + Kopfgeld) als Motivation, solange Berries/Shop fehlen |

## 3. Architektur- und Technikentscheidungen

| Entscheidung | Begründung | Alternativen |
|--------------|-----------|--------------|
| ASP.NET Core (C#, .NET 8) + SignalR | Echtzeit über WebSockets mit automatischem Fallback; Team kennt C# | Node.js + Socket.IO |
| React + TypeScript + Vite | Typsicherheit, schneller Dev-Server, große Community | Angular, Vue |
| PostgreSQL + Entity Framework Core (Docker) | Kostenlos, verbreitet, Migrationen | SQLite, MongoDB |
| Server-autoritativ (ADR 0001) | Fairness, kein Schummeln, eine „Wahrheit“ | Peer-to-Peer / Client-Logik |
| Spielregeln als Action-Klassen (`OneSake.Game/Actions`) | Einzeln testbar, `MatchRoom` übersichtlich | Alles in `MatchRoom` |
| Zufall nur am Server (Mischen, Würfel) | Fairness, nachvollziehbar | Zufall im Browser |
| Deterministische Würfel-Physik mit Server-Seed | Beide Spieler sehen dieselbe Animation, Ergebnis ist trotzdem serverseitig | Nur Zahl anzeigen |
| Gast-ID im Browser (localStorage, UUID) | Ergebnisse ohne Login zuordnen | Sofort echter Login (aufwändiger) |
| Ergebnisse im Hintergrund speichern, Fehler nur loggen | Spiel darf nie wegen der Datenbank abbrechen | Synchron speichern |
| Migrationen von Hand, kein automatisches `Database.Migrate()` | Kontrolle über Produktion | Automatisch beim Start |
| ELO-Wertung (K 40 → 24, Minimum 100) | Bekannt, einfach erklärbar | Glicko-2, TrueSkill |
| Anti-Missbrauch im Ranked (mind. 3 Züge, max. 3 Spiele pro Paar/24 h) | Kein „Farmen“ von Punkten | – |
| Meta-Daten nur über eigene API mit 6-h-Cache | Anbieter wird geschont, Frontend unabhängig, Quellenangabe Pflicht | Direkt im Browser abrufen |
| Monte-Carlo im Web Worker | UI bleibt flüssig | Hypergeometrie (nur für einfache Fälle) |
| Gemeinsames Designsystem `ui.css` | Einheitliches Aussehen | Jede Seite eigenes CSS |
| CSS-Grid-Board mit `--card-w` und `ResizeObserver` | Passt auf jedes Gerät | Festes Hintergrundbild |
| `prefers-reduced-motion` überall respektiert | Barrierefreiheit | – |
| Zug-Timer serverseitig: 90 s Zug + Zeitbank 180 s (+10 s/Zug, max. 240 s), 25 s Antwortzeit, 2 s Toleranz (21.11.) | Niemand kann das Match endlos aufhalten; Zeitbank verzeiht einzelne lange Züge | Feste Zeit pro Aktion (zu hektisch), Zeit nur im Client (manipulierbar) |
| Nur eine Uhr läuft (Schachuhr-Prinzip), Pause während Reconnect (21.11.) | Fair: Wer auf den Gegner wartet, verliert keine Zeit | Beide Uhren gleichzeitig |
| Auto-Aktion bei Timeout: ablehnen bzw. erste gültige Option, Zug beenden; 2 Zug-Timeouts in Folge = Niederlage `TIMEOUT` (21.11.) | Vorhersagbar und deterministisch (Replay identisch) | Zufällige Auswahl, sofortige Niederlage beim ersten Timeout |
| Timer bei privaten Tischen wählbar: Standard / Entspannt / Aus; Ranked immer Standard (21.11.) | Freundschaftsspiele und Lernpartien ohne Zeitdruck, Ranked fair für alle | Timer überall Pflicht, gar kein Timer |
| Timer-Farbe nach Gesamtrestzeit (Zeit + Zeitbank), Tick-Geräusch abschaltbar (21.11.) | Kein unnötiger Stress, solange noch Zeitbank da ist; Ton ist optional | Farbe nur nach Zugzeit |
| Countdowns mit Serverzeit-Offset (21.11.) | Falsch gehende Handyuhren verfälschen keine Fristen | Lokale Uhr (ungenau), nur Restsekunden schicken (springt bei Lag) |
| Effekte hybrid wie MTG Arena: Parser-Entwurf → menschliches Review → Kartentest (23.11.) | Texte sind normiert, Parser trifft viel; nur Review + Test garantieren Korrektheit | Nur Parser; jede Karte als C#-Klasse (XMage); Lua-Skripte (EDOPro) |
| Effektmodell v2 als Baum, Ereignis-Pipeline mit Ersatz-Effekten (23.11.) | Deckt „Choose one“, „If you do“, „When …“, „instead“ ab; Aufbau wie XMage/Forge | Flache Schrittliste erweitern |
| Kartentext im Repo einfrieren, CI statt stillem Hash-Fallback (23.11.) | Abweichende DB-Texte schalten sonst unbemerkt alle Effekte ab | Laufzeit-Hash-Vergleich |
| Automatische Prüfer statt Kontrolle jeder Karte: Rückübersetzung mit exaktem Vergleich, zwei Herleitungen, Bandai-Q&A (23.11.) | Menschliche Prüfung nur noch für eine Prüfliste (ca. 10–20 %) | Jede Karte von Hand prüfen (ca. 60–80 h) |
| Kartendaten (Text, Kosten, Power) nur vom Server, nicht vom Browser (23.11.) | Kein Schummeln, Text passt immer zu den Skripten | Werte vom Browser übernehmen |
| Rejoin-Daten in sessionStorage **und** localStorage (21.11.) | Neuladen und App neu öffnen funktionieren; Daten werden nach Spielende gelöscht | Nur sessionStorage (App neu öffnen ginge nicht), Cookie |
| Eigener Branch `logic` für die Regel-Engine | Großer Umbau, ohne `dev` zu blockieren | Alles auf `dev` |
| Reconnect-Schonfrist 90 s + Budget 180 s pro Match, Lobby 30 s (21.11.) | Funkloch/App-Wechsel am Handy kostet nicht mehr das Match; Budget verhindert Zeitschinden; Werte konfigurierbar | Kein Reconnect (bisher), unbegrenzte Frist (Gegner wartet ewig), Pausieren auf Wunsch |
| Stabile Spieler-ID pro Platz + geheimes Rejoin-Token (21.11.) | Die Verbindungs-ID ändert sich bei jedem Reconnect; das Token beweist, dass man auf diesen Platz gehört | Gast-ID (nicht geheim, gilt für alle Tabs), Verbindungs-ID behalten (kein Reconnect möglich) |

| Auto-Pass nur bei öffentlichen Informationen, Counter-Auto-Pass nur auf Wunsch mit zufälliger Pause (21.11.) | Sofortiges Passen beim Counter würde verraten, dass keine Counter auf der Hand sind | Immer automatisch passen (Informationsleck), nie automatisch (mehr Leerlauf) |
| Tastenkürzel mit Bestätigung für „Zug beenden“ (zweimal Leertaste oder 400 ms halten) (21.11.) | Schneller am Desktop, aber kein versehentliches Zugende | Einfacher Druck (zu riskant), keine Kürzel |
| Positionen nie im React-Commit lesen, Animationen im nächsten Frame starten (21.11.) | Kein erzwungenes Layout: Erkennung von bis zu 355 ms auf 1,5 ms (4× gedrosselt) | Alles im Layout-Effekt (einfacher, aber ruckelt am Handy) |
| Sounds mit der Web Audio API synthetisiert, keine Audiodateien (21.11.) | Keine Lizenzfragen, keine Downloads, Lautstärke zentral steuerbar | Audiodateien (Rechte, Ladezeit), keine Sounds |
| Effekt-Ziele aus Kartennamen im Log und Power-Änderungen ableiten (21.11.) | Rein optisch, keine Server-Änderung nötig | Ziele vom Server mitschicken (genauer, aber Backend-Änderung) |
| Spielereignisse aus dem Vergleich zweier Snapshots erkennen (21.11.) | Funktioniert ohne Änderung an Server und DTOs; Logs liefern nur Text | Strukturierte Ereignisse vom Server (Backend-Änderung, nicht Teil dieses Auftrags) |
| Animationen als Schicht über dem sofort gültigen Zustand (21.11.) | Board ist immer korrekt, Eingaben werden nie blockiert, Klick überspringt | Zustand erst nach der Animation zeigen (Wartezeiten, Gefahr von Abweichungen) |
| Web Animations API, nur `transform`/`opacity`, keine neue Bibliothek (21.11.) | Flüssig auch am Handy (GPU), jede Animation lässt sich sofort beenden | Animationsbibliothek (zusätzliche Abhängigkeit), CSS-Klassen mit Timern (schwer abzubrechen) |

## 4. UX- und Designüberlegungen

- **Mobile First:** Touch-Ziele ≥ 44 px, Vollbild-Board, Long-Press statt Rechtsklick, Hand bleibt sichtbar.
- **Ein Haupt-Button:** In der Lobby eine goldene Hauptaktion (Quick Match) statt vieler gleichwertiger Buttons.
- **Leader im Mittelpunkt:** Lobby und Seiten färben sich nach der Leader-Farbe des aktuellen Decks.
- **Vertraute Muster:** Hand wie in Hearthstone, Steckbrief/Kopfgeld wie in One Piece.
- **Fehlerzustände sichtbar machen:** Lade-Skelette, leere Zustände mit Aktion, Fehler mit „Erneut versuchen“.
- **Fortschritt zeigen:** „Was ist neu“ mit Versionsnummer und Punkt für ungelesene Neuigkeiten.

## 5. Recht, Daten und Fairness

- **Bildrechte:** Kartenbilder kommen aus der Karten-API; eigene Kartenrückseiten nur mit geklärten
  Rechten, sonst neutraler Ersatz.
- **Meta-Daten:** Card Kaizoku hat keine Nutzungsbedingungen veröffentlicht; Quelle wird immer
  angezeigt; laut Doku nur mit schriftlicher Erlaubnis aktivieren. *(Für die Arbeit: Stand der Erlaubnis festhalten.)*
- **Datenschutz:** Nur nötige Daten (Gast-ID, Anzeigename, Ergebnisse). Gast-ID ist keine sichere
  Anmeldung – das ist bewusst so und dokumentiert.
- **Fairness:** Server-autoritativ, Zufall serverseitig, Anti-Missbrauch im Ranked.

## 6. Risiken aus dem Projektauftrag und wie es gelaufen ist

| Risiko | Maßnahme laut Plan | Tatsächlich |
|--------|-------------------|-------------|
| Regelkomplexität | Vertical Slice, begrenzter Kartensatz | Regeln vollständig, Effekte über Parser; schwierige Effekte „manuell“ |
| Mobile-UI-Overload | Choice-Driven, Drawer/Sheets | Mehrere Redesigns (Board, Feed, Choice-Leiste) |
| Cheat/Desync | Server-autoritativ + Tests | 111 Backend-Tests, Zufall nur am Server |
| Datenpflege Karten-DB | – | API-Typen uneinheitlich → eigene DB + Import bei Bedarf |
| (neu) Lange Pause Juni–Oktober | – | Sprint 3 mit hohem Tempo, KI-Werkzeuge zur Beschleunigung |

## 7. Werkzeuge und KI-Einsatz

- **Git/GitHub** (Issues, PRs, Actions), **Visual Studio / VS Code**, **Docker** (PostgreSQL),
  **Playwright** (UI-Funktionstest, Screenshots), **ESLint**, **xUnit**.
- **KI-Werkzeuge:** OpenAI Codex (24 Commits, v. a. Deckbuilder-Feinschliff im Mai) und Claude Code
  (Konfiguration, Branch-Regeln, diese Dokumentation). Für die Diplomarbeit offenlegen, wofür KI
  eingesetzt wurde und wie die Ergebnisse geprüft wurden (Build, Tests, manuelles Testen).

## 8. Lessons Learned (laufend ergänzen)

- Externe APIs liefern nicht immer einheitliche Datentypen → eigene Datenbank als Puffer.
- Bilder als Layout-Grundlage skalieren schlecht → CSS-Grid.
- Kleine, häufige Commits auf `dev` machen Rückschritte (Revert) einfach.
- Ein Spiel darf nie an Nebensachen (Datenbank, Meta-Anbieter) scheitern → Fehler abfangen, weiterspielen.
- Dokumentation sofort mitschreiben spart später viel Rekonstruktionsarbeit (deshalb ab 20.11. Pflicht).
