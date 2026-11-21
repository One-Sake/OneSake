# Projekttagebuch OneSake

Chronologische Dokumentation aller Arbeitsschritte mit Zeitstempeln und Überlegungen.
Zeitangaben aus der Git-Historie (Format `JJJJ-MM-TT HH:MM`). Die vollständige Liste aller
Commits steht in [Anhang A](anhang-a-commit-liste.md).

**Legende:** ★ = eigene Arbeit (WoWoWocal, Frontend-Verantwortung) · 🤖 = mit KI-Werkzeug erstellt
(Autor „Codex“ oder „Claude“) · 👥 = Teammitglied

## Team und Rollen

| Bereich | Verantwortlich (GitHub) | Commits bis 20.11.2026 |
|---------|-------------------------|------------------------|
| Frontend, Projektleitung, UI/UX, Dokumentation | WoWoWocal ★ | 110 (+ 24 Codex, 2 Claude) |
| Backend / Game Engine, CI/CD, Deployment | danielbna / dano-4 👥 | 14 + 24 |
| Datenbank (PostgreSQL, Kartenimport) | 13-Tino-13 / Tino Tomasic 👥 | 3 + 5 |

Gesamt: 175 Commits auf `dev` (17.04.2026 – 20.11.2026), 111 Backend-Tests (xUnit).

## Überblick: Sprints und Versionen

| Abschnitt | Zeitraum | Ziel | App-Version |
|-----------|----------|------|-------------|
| Sprint 1 | 17.04. – 10.05.2026 | Repository, Workflow, CI/CD, Karten-API, Vertical Slice | – |
| Sprint 2 | 11.05. – 07.06.2026 | Deckbuilder, Tools, Match-Oberfläche, PostgreSQL | – |
| Pause | 02.06. – 02.11.2026 | keine Commits (Sommer/Schule) | – |
| Sprint 3 | ab 06.10.2026 (Commits ab 03.11.) | Server-Liste, echte Regeln, Persistenz, Ranked, Meta | 0.1.0 – 0.5.0 |

Versionen laut „Was ist neu“ (`changelog.ts`): 0.1.0 (03.11.) Server-Liste · 0.2.0 (07.11.)
Spielbare Regeln · 0.3.0 (12.11.) Board-Redesign · 0.4.0 (18.11.) Neue Lobby · 0.5.0
(18.–20.11.) Quick Match, Ranked, Meta, neues Spielfeld.

---

## Sprint 1 – Fundament und Vertical Slice (17.04. – 10.05.2026)

### 2026-04-17 14:12 – Projektstart ★
- `2879eba` Initial commit des Repositories.
- **Überlegung:** Projektidee aus dem Projektauftrag – eine mobile-optimierte OPTCG-Plattform
  im Browser, weil bestehende Tools (z. B. Project Raftel, OPTCGSim) laut Community-Feedback
  am Handy unübersichtlich sind (Panel-Overload, schlechte Touch-Bedienung).

### 2026-04-19 17:54 – Repository-Struktur, Team-Workflow, erste Doku ★
- `b4f6bde` Ordnerstruktur `backend/` (ASP.NET Core, C#) und `frontend/web/` (React, TypeScript, Vite),
  `docs/` mit Architektur, API-Vertrag, Event-Log-Katalog, Mobile-Spec, ADR 0001/0002, Templates für Issues/PRs.
- **Überlegung:** ADR 0001 „Server-autoritativ“ – der Server verwaltet den gesamten Spielzustand,
  Clients senden nur Aktionen → kein Schummeln, dafür Echtzeitkommunikation (SignalR) nötig.

### 2026-04-20 22:04 – CI/CD 👥
- `1adee21` (dano-4) GitHub Actions: Backend Build + Test, Frontend Build; Deployment-Workflow.
- PR #1 `feature/sprint1-vertical-slice-foundation` gemerged (22:17).

### 2026-04-21 01:18 – 03:05 – Workflow-Vereinfachung und Vertical Slice ★
- `0f4d923` ESLint-Konfiguration für das Frontend.
- `d5dc881` / `d3447a4` Umstellung auf **dev + main** (ADR 0003, 21.04.2026).
- `f993384` Sprint-1-Vertical-Slice auf `dev`: DTOs, In-Memory-`MatchRoom`, `MatchRoomManager`,
  `StartMatch`, Choice-Prompts, Mulligan, Zug beenden, SignalR-Hub, Lobby, Choice-Sheet, Log und Chat.
- **Überlegung:** Statt vieler Feature-Branches mit Pflicht-Reviews (ADR 0002) ein einfacheres Modell:
  `dev` zum täglichen Integrieren, `main` nur für stabile Demo-Stände. Grund: kleines Team,
  Einsteigerfreundlichkeit, weniger Prozessaufwand.
- **Überlegung:** „Vertical Slice“ – zuerst ein dünner, aber durchgehender Ablauf (Raum → Start →
  Mulligan → Zug beenden → Chat/Log), bevor einzelne Teile ausgebaut werden.

### 2026-04-21 – 04-23 – README und CONTRIBUTING ★
- Mehrere README-Überarbeitungen (Setup Backend/Frontend, täglicher Git-Workflow, Spickzettel),
  `CONTRIBUTING.md` vereinfacht (keine festen Branch-Namenskonventionen mehr).
- **Überlegung:** README zuerst sehr ausführlich, dann bewusst gekürzt – lieber kurz und aktuell
  als lang und veraltet.

### 2026-04-23 – 04-26 – Karten-API 👥
- `afa33a1` (dano-4) `CardService`, `CardsController` (externe OPTCG-API), `HealthController`.
- `46b7d5b`–`c3ebb63` `CardDto` vollständig, Karten pro Set abrufen.

---

## Sprint 2 – Deckbuilder, Tools, Match-Oberfläche, Datenbank (11.05. – 07.06.2026)

### 2026-05-12 14:55 – 23:14 – Deckbuilder und Tools ★
- `3fcf7f6` lokales Deck-Bearbeiten, `f76b28b` Entwürfe im Browser speichern (localStorage),
  `2b6e969` Deckbuilder, `0e3d9fa` lokale Deck-Bibliothek + Grundlage der Tools,
  `e1d46f8` **Wahrscheinlichkeitsrechner** für gespeicherte Decks, `bfd3ef1` Fix Speichern/Navigation.
- **Überlegung:** Decks zunächst nur lokal im Browser speichern – schneller umsetzbar, kein Login nötig;
  Server-Speicherung ist als User Story #43 geplant.
- **Überlegung:** Wahrscheinlichkeiten per Monte-Carlo-Simulation in einem Web Worker
  (`probabilityWorker.ts`), damit die Oberfläche bei vielen Durchläufen nicht ruckelt (Projektauftrag 4.2).

### 2026-05-13 00:19 – 17:35 – Erstes spielbares Match ★
- `fccf35c` Match-Board-Layout, `b0ed497` Verbindungsstatus, `31fbbf8` gespeichertes Deck in der Lobby wählen,
  `0ae3cb5` Deck im Raum registrieren, `cf21aeb` Decks + Starthände, `f5de655` Mulligan serverseitig,
  `48be413` Zugphasen, `262d013` Karte in der Main Phase spielen, `01f1976` einfache Angriffe.
- **Überlegung:** Alles, was die Regeln betrifft (Mischen, Ziehen, Mulligan), passiert serverseitig –
  der Client zeigt nur an und sendet Entscheidungen (Choice-Driven, Projektauftrag 5.1).

### 2026-05-13 – Deployment und Datenbank 👥
- 17:32 `0dda3ac` (danielbna) Deploy mit Frontend-Build und Nginx-Reload.
- 17:58 PR #8 `dev → main`: erster echter Deploy-Test.
- 18:30 – 19:00 Probleme mit der Karten-API: Werte kommen je nach Set mal als Zahl, mal als Text
  (`Life`, `Cost`, `Power`, `Counter`) → Typen angepasst, Fehler-Logging ergänzt.
- 22:36 `2194ca1` (Tino) PostgreSQL-Kartendatenbank mit Entity Framework Core.
- **Lessons Learned:** Externe APIs sind nicht einheitlich – eigene Datenbank als Puffer reduziert Abhängigkeit.

### 2026-05-13 18:34 – 2026-05-14 03:23 – Deckbuilder-Ausbau ★
- `2a0a21b` Karten nach Leader-Farbe filtern, `054195c` Farbpaletten-Filter, `aeac2cc` vereinfachtes Kartenraster,
  `d0a1d57` Kartengrößen-Slider, `feb6b43` kompakte Mengen-Buttons, `8780095` Hintergrundbild,
  `414d9e4` eigenes Banner, `ff253e1` Piraten-Spielbrett in die Match-Ansicht integriert.
- Parallel (dano-4): Starter-Decks, Set-Abruf repariert, OP-14/15 entfernt (Sets in der API nicht vorhanden).

### 2026-05-14 15:51 – 22:59 – Startbildschirm ★ 🤖
- `2b9828c` 🤖 visuelles Match-Board-Layout.
- `75714d3` Vollbild-Startbildschirm, danach `b696b81`–`6a19d9e` Artwork, Play-Button, Logo, Responsivität.

### 2026-05-15 – Demo-Vorbereitung ★
- 13:43 `0753aa5` Startbildschirm mit **Bild-Hotspots** (Buttons liegen unsichtbar über dem Artwork).
- 15:02 `66b7b8e` globales Hintergrundbild und Deckbuilder-Bild wieder entfernt (**verworfen**: zu unruhig, Ladezeit).
- 17:00 `8489cbc` Match öffnet im **Vollbild-Board-Modus**; 17:37 – 21:48 Board kalibriert, Kartenanzeige, Lobby-UX.
- 22:32 `e9c9c49` Profilseite mit Login-Platzhalter, 22:41 Tools poliert.
- 22:58 `b76b118` CSS nach Features aufgeteilt (`features/*/…css`) – bessere Übersicht.
- 23:10 `92b8f52` UI-Feinschliff für die Demo.

### 2026-05-16 – Mobile-Verbindung und Namen ★
- `6598c2d` / `6cfe174` Board nach dem Beitreten anzeigen.
- 17:38 `89021bf` Fix: Verbindung zum Match-Hub vom **Handy** im lokalen Netz
  (Backend-URL wird vom Hostnamen des Browsers abgeleitet, siehe `docs/mobile-match-troubleshooting.md`).
- 17:55 `890623c` **Piratennamen-Generator** für Anzeigenamen.

### 2026-05-17 – 2026-05-20 – Deckbuilder-Feinschliff ★ 🤖
- ★ Hover-Vorschau, Klick zum Hinzufügen, vereinfachte Filter, Leader-Zusammenfassung.
- 🤖 (Codex, 18.05., 12:55 – 21:36): Deckliste als **Kartenstapel**, Sortierung, kompakte Filterleiste,
  Scrollverhalten, Fix der Lobby-Verbindung.
- 2026-05-19 03:07 `51d11f6` Änderung an Vorschau-Scroll → 03:12 wieder **zurückgenommen** (`d43399e`).
- 👥 (Tino) 02:46 Karten zuerst aus der Datenbank, API nur als Fallback; 03:40 fehlende Sets bei Bedarf importieren;
  20.05. Endpunkt für verfügbare Sets.

### 2026-05-22 – 2026-06-01 – Spiel-Architektur 👥
- `7f9da18` (dano-4) Spiel-Architektur umgebaut: Aktionen als eigene Klassen (`OneSake.Game/Actions`).
- `a1e016b` (danielbna) Main-Action- und Mulligan-Behandlung ausgelagert.
- **Überlegung:** `MatchRoom` war zu groß; eigene Action-Klassen sind einzeln testbar (User Story #34).

---

## Pause (02.06. – 02.11.2026)
Keine Commits. Sprint 3 wurde ab 06.10.2026 geplant.

---

## Sprint 3 – Server-Browser, echte Regeln, Persistenz, Ranked, Meta (ab 06.10.2026)

### 2026-11-03 21:40 – v0.1.0 Server-Liste 👥
- `f983407` (dano-4) Räume erstellen/beitreten/verlassen, Live-Liste offener Server,
  leere Räume werden nach 5 Minuten gelöscht (`RoomCleanupService`), Tests.

### 2026-11-07 13:49 – v0.2.0 Spielbare OPTCG-Regeln 👥
- `c263f41` (dano-4) Leader, Leben, DON!!, vollständiger Zugablauf, Angriff/Block/Counter/Schaden,
  Keywords (Rush, Blocker, Double Attack, Banish), Sieg durch 0 Leben oder leeres Deck; echte Kartenbilder.

### 2026-11-09 18:44 – Backlog auf GitHub ★
- `1f8e85e` Skript `scripts/project-backlog/create-backlog.sh`: User Stories #16–#57, Sprint-Labels,
  Verantwortlichkeiten, Prioritäten – rekonstruiert aus Git-Historie und Projektauftrag.
- **Überlegung:** Nachträglich saubere Scrum-Artefakte für die Diplomarbeit (Sprints, Stories, Akzeptanzkriterien).

### 2026-11-11 18:18 – v0.3.0 Board-Redesign ★
- `b5a5dbe` Spielfeld als **CSS-Grid** statt Hintergrundbild mit absoluten %-Positionen;
  Gegnerhälfte gespiegelt; Kartengröße über `--card-w` (Seitenverhältnis 63:88);
  **Hand im Hearthstone-Stil** (aufgefächert, hebt sich beim Hover); Rechtsklick/Long-Press = Karte ansehen;
  `prefers-reduced-motion` respektiert.
- **Überlegung:** Das Hintergrundbild passte nur bei einer Bildschirmgröße – ein reines CSS-Raster skaliert
  auf jedem Gerät.

### 2026-11-12 20:22 – Größeres Board, Feed ★
- `fdb4f36` Keep/Mulligan als kleine Leiste auf der Mittellinie statt Bottom-Sheet (Hand bleibt sichtbar);
  Desktop als 3 Spalten (Log/Chat | Board | Kartenvorschau); Log und Chat in **einer** chronologischen Liste.

### 2026-11-18 – Großer Ausbautag ★ (v0.4.0 und v0.5.0)
| Zeit | Commit | Was | Überlegung |
|------|--------|-----|------------|
| 16:00 | `b65b1a5` | **v0.4.0 Lobby-Redesign** im TCG-Stil: Leader-Karte groß mit 3D-Neigung, Lobby färbt sich nach Leader-Farbe, Deck-Karussell, ein goldener Haupt-Button | Ein klarer Haupt-Button statt vieler gleichwertiger Buttons – weniger Entscheidungen für Einsteiger |
| 16:20 | `ed24cda` | Deck-**Import/Export als Text** (`4xOP01-016`) | Community-Format, damit Decks mit anderen Tools austauschbar sind (statt reinem OPTCGSim-String aus dem Projektauftrag) |
| 16:22 | `28b1aa8` | Feste **Gast-ID** pro Browser | Ergebnisse zuordnen ohne Login; bewusst **keine** sichere Authentifizierung, echter Login folgt |
| 16:28 | `0e417b0` | **Match-Ergebnisse** in der DB, echte Profilstatistik | Speichern im Hintergrund – fällt die DB aus, läuft das Spiel trotzdem weiter |
| 16:32 | `a7ff8a5` | **Quick Match** (Warteschlange) | Schneller Einstieg ohne Raumcode; gleiche Gast-ID wird nie gegen sich selbst gepaart |
| 16:34 | `99e535f` | Changelog + „Was ist neu“-Fenster | Spieler sehen Fortschritt; Punkt markiert ungelesene Neuigkeiten |
| 17:40 | `74459f9` | Gemeinsames **Designsystem** `styles/ui.css` + Icons | Einheitlicher Look, Wiederverwendung statt Kopieren |
| 17:44 – 17:52 | `c276b5f`, `b6b0f1a`, `f78d27b` | Tools, Profil, Startbildschirm, Deckbuilder im neuen Design | Deckbuilder „nur visuell“: Layout-Maße identisch, 12-Schritte-Playwright-Test gleich |
| 18:21 | `a47ff56` | Startspieler per **serverseitigem Würfelwurf** | Fairness: Zufall nur am Server (kryptografischer Zufallsgenerator), Gleichstand → neu würfeln |
| 18:56 / 19:27 | `442676b`, `0cde367` | Deterministische **Würfel-Physik** + 3D-Animation | Server schickt Seed + Ergebnis, Client simuliert identisch; Augen werden nachträglich so verteilt, dass das Server-Ergebnis oben liegt |
| 19:30 | `b8ad80e` | **Ranked + ELO-Wertung** | Formel `E = 1/(1+10^((Rb−Ra)/400))`, K=40 für die ersten 20 Spiele, dann 24; Anti-Missbrauch: mind. 3 Züge, max. 3 gewertete Spiele pro Paar in 24 h |
| 19:41 | `7cfd9a1` | **Kopfgeld-Rangliste** + Steckbrief | Wertung × 1000 = Kopfgeld (Berry) – passt zum One-Piece-Thema |
| 19:46 / 19:53 | `f0e3b0b`, `0d55be1` | **Meta-Tab** mit Daten von Card Kaizoku | Frontend fragt nur die eigene API; 6 h Cache; Quelle wird immer angezeigt; nur mit Erlaubnis des Anbieters aktivieren |
| 23:19 | `759cfec` | 👥 (dano-4) **Effekt-Engine**: Kartentexte werden in Effekte übersetzt | Unbekannte Texte werden als „manuell“ markiert statt falsch ausgeführt |

### 2026-11-20 – Spielfeld-Bedienung ★
- 13:35 `1e38b5f` Log/Chat-Leiste **verstellbar** (Splitter, Tastatur, Doppelklick = Standard) und einklappbar, Breite wird gespeichert.
- 13:40 `101bcf8` Board **skaliert** mit `ResizeObserver` auf die größte passende Kartengröße; größere Beschriftungen.
- 13:49 `dd0004b` **Drag & Drop** für Handkarten und DON!! (Maus, Stift, Touch).
  **Überlegung:** Drag & Drop ruft dieselbe `submit()`-Funktion wie die Klick-Steuerung auf – der Server
  prüft weiterhin alles; Klick/Tippen funktioniert weiterhin.
- 13:53 `f9f890b` Eigene Kartenrückseiten (Team-Artwork) mit neutralem Ersatz, falls die Bilder fehlen
  (Bildrechte: Dateien werden vom Team mit geklärten Rechten hinzugefügt).
- 14:03 `d06c5bc` Hinweis-Toast auf kleinen Bildschirmen nicht mehr von der Hand verdeckt.
- 17:54 `a2c1403` 🤖 Meta-Daten von Card Kaizoku in Produktion aktiviert (Anbieter hat keine Nutzungsbedingungen veröffentlicht – dokumentiert).
- 18:21 `b358dd8` 🤖 Eigener Branch `logic` für den Umbau der Regel-Engine (Effekte, Trigger-Reihenfolge),
  Merge zurück nach `dev` nur mit grünen Backend-Tests.
- 19:50 ★ 🤖 **Diplomarbeits-Dokumentation** angelegt (dieses Tagebuch, Ideen/Entscheidungen, Kapitel-Bausteine,
  Commit-Liste, Screenshots), „Was ist neu“ v0.5.0 um alle Neuerungen seit 18.11. ergänzt, Regel in `AGENTS.md`,
  dass Doku und Changelog künftig bei jeder Änderung mitgepflegt werden.
  Screenshots: [screenshots/README.md](screenshots/README.md) (Stand 2026-11-20).

---

### 2026-11-20 20:10 – Persönlicher Doku-Ordner ★ 🤖
- Alle Diplomarbeits-Dateien nach `docs/diplomarbeit/WoWoWocal/` verschoben; Regel in `AGENTS.md`:
  jedes Teammitglied schreibt nur in seinen eigenen Ordner `docs/diplomarbeit/<GitHub-Name>/`.
- **Überlegung:** Die Mitschrift ist persönliche Grundlage für die eigene Diplomarbeit. Getrennte
  Ordner verhindern, dass Teamkollegen (oder ihre KI-Agenten) in dieselben Dateien schreiben.
  Nur der „Was ist neu“-Changelog in der App bleibt gemeinsam.

### 2026-11-21 01:21 – Stabile Spieler-Identität (Grundlage für Reconnect) ★ 🤖
- Commit „Give every seat a stable player ID and a rejoin token“ (Branch `logic`): Jeder Platz im
  Raum bekommt beim Betreten eine eigene Spieler-ID (`p1-…`) und ein geheimes Rejoin-Token
  (32 Byte aus `RandomNumberGenerator`, Base64Url). Das Token geht nur an diesen Spieler
  (Nachricht `SeatAssigned`). Alle Hub-Methoden ermitteln den Spieler über `ResolvePlayerId`
  (Verbindung → Platz), Snapshots und Prompts werden an die aktuelle Verbindung des Platzes geschickt.
  Das Senden steckt jetzt in `MatchUpdatePublisher`, damit es später auch ein Hintergrunddienst nutzen kann.
- **Überlegung:** Bisher war die Spieler-ID die SignalR-Verbindungs-ID. Nach einem Verbindungsabbruch
  bekommt der Browser aber eine neue Verbindung – der Server erkannte den Spieler nicht mehr.
  Alternative wäre die Gast-ID gewesen; sie ist aber nicht geheim (steht in der URL, ist im Profil
  sichtbar) und gilt für alle Tabs eines Browsers. Ein eigenes Token pro Platz ist sicherer und eindeutig.
- **Probleme/Lessons Learned:** Viele Tests rufen `JoinPlayer("p1")` direkt auf. Damit sie unverändert
  bleiben, gilt dort „Spieler-ID = Verbindung“; nur der Hub nutzt die neue Methode `JoinConnection`.

### 2026-11-21 01:28 – Reconnect im Backend ★ 🤖
- Commit „Keep the seat of a disconnected player for a grace period“: Bricht die Verbindung im
  laufenden Match ab, bleibt der Platz 90 s reserviert (`Match:ReconnectGraceSeconds`). Pro Spieler und
  Match gibt es zusätzlich ein Gesamtbudget von 180 s (`Match:ReconnectBudgetSeconds`); ist es fast
  aufgebraucht (< 1 s), verliert man sofort. Neue Hub-Methode `RejoinRoom(roomCode, playerId, token)`
  (Token-Vergleich in konstanter Zeit) – danach bekommt der Spieler Snapshot, alle offenen Entscheidungen
  und die letzten Log-/Chat-Einträge (`Resync`). Läuft die Frist ab, endet das Match mit Niederlage
  `DISCONNECT` (zählt im Ranked). In der Lobby gilt eine Frist von 30 s, nach dem Spielende wird der
  Platz wie bisher sofort frei. Ein `MatchDeadlineService` prüft jede Sekunde alle Räume.
  Die Uhr kommt über `IClock`, damit Tests die Zeit vorspulen können (12 neue Tests).
- **Überlegung:** Am Handy reicht schon ein App-Wechsel, damit der Browser die Verbindung kappt – bisher
  war das Match dann sofort verloren. Die Frist ist ein Kompromiss: lang genug für ein Funkloch, kurz genug,
  dass der Gegner nicht ewig wartet. Das Budget verhindert, dass jemand durch ständiges Trennen Zeit schindet.
  Disconnect/Reconnect landen als Meta-Ereignisse in der Aufzeichnung, der Fristablauf als Eingabe
  `DISCONNECT_TIMEOUT` – so bleibt das Replay deterministisch.
- **Probleme/Lessons Learned:** Nach einem Neuladen kommt die neue Verbindung oft an, *bevor* der Server
  merkt, dass die alte weg ist. Deshalb übernimmt ein gültiger Rejoin den Platz auch von einer noch
  „lebenden“ Verbindung. Außerdem kann man das Budget nie „genau auf 0“ verbrauchen (die Frist endet
  genau dann) – darum zählt weniger als 1 s Rest als aufgebraucht.

### 2026-11-21 12:27 – Reconnect im Frontend ★ 🤖
- Commit „Bring players back to their seat after a lost connection“: Der Client speichert Raum,
  Spieler-ID und Rejoin-Token in `sessionStorage` und `localStorage` (alles in try/catch) und ruft nach
  jeder neuen Verbindung automatisch `RejoinRoom` auf. SignalR versucht es jetzt bis zu 10 Minuten
  lang erneut (Standard: nach ~40 s Schluss); beim Zurückkehren in die App (`visibilitychange`) oder wenn
  das Netz wiederkommt (`online`) wird sofort neu verbunden. Eigene Trennung: Banner
  „Verbindung verloren – verbinde neu … (0:24)“, Board ausgegraut; danach kurz „Wieder verbunden ✓“.
  Gegner getrennt: „Getrennt · 1:12“ am Namensschild. Nach dem Neuladen springt die App direkt zur
  Match-Seite und zeigt „Laufendes Match gefunden – Zurück ins Match“ (neue Hub-Methode `CheckRejoin`,
  die nur prüft und den Platz nicht übernimmt). Spielende-Fenster zeigt jetzt den Grund
  (`endReason` im Snapshot), z. B. „Gegner hat die Verbindung verloren – Sieg“.
- **Überlegung:** Countdowns rechnen mit der Serverzeit (jeder Snapshot schickt `serverTimeUnixMs`),
  damit eine falsch gehende Handyuhr keine Rolle spielt. Nur das eigene Banner rechnet lokal, weil die
  Frist ja genau ab dem eigenen Verbindungsverlust läuft. Wer zu spät zurückkommt, sieht trotzdem das
  Ergebnis und das Log bis zum Ende, kann aber nichts mehr tun.
- **Geprüft:** Playwright mit zwei Browsern (Offline schalten, Neuladen, Frist ablaufen lassen) –
  alle drei Szenarien grün; Screenshots 2026-11-21_01 bis _06.
- **Probleme/Lessons Learned:** Ohne Anpassung wäre „ein Klick zurück“ nach dem Neuladen zwei Klicks
  gewesen (erst der Startbildschirm). Außerdem: Die Standard-Wiederverbindung von SignalR gibt nach etwa
  40 Sekunden auf – kürzer als die Schonfrist.
- Screenshots: `screenshots/2026-11-21_01-reconnect-banner.png` bis `screenshots/2026-11-21_06-niederlage-verbindung.png`

### 2026-11-21 12:33 – Zug-Timer im Backend ★ 🤖
- Commit „Add a server-side turn timer with a time bank“: 90 s pro Zug, danach die persönliche
  Zeitbank (Start 180 s, +10 s nach jedem eigenen Zug, max. 240 s); 25 s Antwortzeit für Block, Counter,
  Trigger usw.; Mulligan 30 s und Würfelentscheidung 20 s ohne Zeitbank; 2 s Toleranz für Lag. Läuft die
  Zeit ab, entscheidet der Server automatisch in einer festen, dokumentierten Reihenfolge
  (`docs/turn-timer.md`), z. B. Counter → `PASS`, eigener Zug → Zug beenden. Zwei eigene Züge in Folge
  ohne Zeit = Niederlage mit neuem Endgrund `TIMEOUT`. Modi Standard / Entspannt (doppelt) / Aus,
  Quick Match immer Standard. Der Snapshot enthält jetzt `timer` (wessen Uhr, Art, Frist, Zeitbanken).
  11 neue Tests mit der Fake-Uhr (u. a. Zeitbank, Counter-Timeout, Pause bei Disconnect, Replay).
- **Überlegung:** Es läuft immer nur *eine* Uhr – wie bei einer Schachuhr. Während der Gegner über
  einen Counter nachdenkt, steht die Zugzeit des Angreifers. Die automatische Entscheidung lehnt immer
  ab, wo es geht („nichts tun“ ist am wenigsten überraschend), und nimmt sonst die erste gültige
  Option – so ist das Ergebnis vorhersagbar und im Replay identisch. Alternativen: Zufällige Auswahl
  (nicht deterministisch), sofortige Niederlage beim ersten Timeout (zu hart bei kurzem Lag).
- **Probleme/Lessons Learned:** Die Logik von `SubmitChoice` musste in „prüfen/ausführen“ und
  „aufzeichnen“ geteilt werden, damit ein Timeout als *ein* Input `TIMEOUT` aufgezeichnet wird und das
  Replay ihn mit demselben Code nachspielt. In den Tests fiel auf, dass hier niemand im eigenen ersten
  Zug angreifen darf – der Counter-Test greift deshalb in Zug 3 an.

### 2026-11-21 12:45 – Zug-Timer im Frontend, Timer-Wahl für eigene Tische ★ 🤖
- Commit „Show the turn timer and let private tables choose it“: Ring mit Sekunden am Namensschild
  (eigener und gegnerischer Spieler), Zeitbank als kleine Zahl ⌛ daneben, Balken über der
  Aktionsleiste für die eigene Uhr („Zugzeit“ / „Antwortzeit“ / „Zeitbank“). Ab 15 s Gesamtzeit
  orange, ab 5 s rot und pulsierend mit leisem Tick-Geräusch (Web Audio, per 🔊/🔇 abschaltbar);
  bei `prefers-reduced-motion` kein Pulsieren. In der Lobby „Timer für eigene Tische:
  Standard / Entspannt / Aus“ (neue Hub-Methode `CreateRoomWithTimer`, `CreateRoom` bleibt für ältere
  Clients), die Tisch-Liste zeigt die Einstellung. Profil-Statistik und Spielende kennen den Endgrund
  „Zeit abgelaufen“.
- **Überlegung:** Die Farbe richtet sich nach der *gesamten* Restzeit (normale Zeit + Zeitbank) – sonst
  würde der Ring bei vollen 180 s Zeitbank schon nach 75 s rot werden und unnötig Stress machen.
  Der Ring füllt sich je Abschnitt (erst normale Zeit, dann Zeitbank) neu, damit man sieht, dass die
  Zeitbank angebrochen ist.
- **Probleme/Lessons Learned:** Beim Browser-Test (Counter läuft ab) blieb beim Verteidiger der Knopf
  „Pass“ stehen – der Server hatte schon automatisch entschieden, aber der Client wusste es nicht.
  Lösung: Der Snapshot enthält jetzt `openChoiceIds` (eigene offene Entscheidungen); veraltete Prompts
  verwirft der Client. Außerdem gefunden (älter, nicht behoben): Entscheidet der Würfelgewinner sehr
  schnell, bleibt beim Gegner das Würfel-Overlay hängen.
- **Geprüft:** Playwright mit zwei Browsern und kurzen Timer-Werten: automatisches Zugende, Zeitbank,
  zwei Timeouts → Niederlage `TIMEOUT` bei beiden; Counter läuft ab → Kampf geht ohne Counter weiter.
- Screenshots: `screenshots/2026-11-21_07-timer-normal.png` bis `screenshots/2026-11-21_11-antwortzeit-counter.png`

### 2026-11-21 12:47 – Konfiguration dokumentiert, Abschluss Reconnect/Timer 🤖
- Commit „Document the reconnect and turn timer settings“: Alle neuen Werte (`Match:ReconnectGraceSeconds`,
  `Match:ReconnectBudgetSeconds`, `Match:LobbyReconnectGraceSeconds`, `Match:Timer:*`) mit Standardwerten
  und Beispiel für kurze Testwerte in `docs/deployment.md`; Nachtrag in `docs/handoff.md`.
- **Gesamtprüfung:** `dotnet build`/`dotnet test` grün (300 Tests, 6 übersprungen wie bisher),
  `npm run lint`/`npm run build` grün, Simulator-Smoke wie in CI (200 Partien, 0 Fehler),
  Playwright-Szenarien a)–e) grün.
- **Überlegung/Grenze:** Die Plätze liegen nur im Speicher des Servers. Ein Neustart (Deployment) beendet
  laufende Matches weiterhin – dafür bräuchte es eine gemeinsame Speicherung (z. B. Redis), was für den
  aktuellen Ein-Server-Betrieb zu viel Aufwand wäre.

### 2026-11-21 – Roadmap und Alleinstellungsmerkmale festgehalten 🤖
- Neue Datei `04-roadmap-und-alleinstellungsmerkmale.md`: was nach den vier Logik-Prompts
  (Ereignis-System, Determinismus/Aufzeichnung, Bot-Simulation, Karten nach Meta) noch fehlt,
  welche Ideen OneSake von anderen OPTCG-Simulatoren abheben (Deck-Labor, Coach,
  „Was wäre wenn?“-Replay, Lethal-Puzzle, Banden/Titel, Streamer-/Turnier-Modus) und eine
  empfohlene Reihenfolge. Neue Ideen in `02-entscheidungen-und-ideen.md` (2.2 Offen) eingetragen.
- **Überlegung:** Die Ideen bauen bewusst auf der deterministischen Engine, der Bot-Simulation und
  den Meta-Daten auf – das ist schwer nachzumachen und gut in der Diplomarbeit vorzuführen.
  Grundlagen (Reconnect, Timer, Spiel-Gefühl) kommen trotzdem zuerst, weil ohne sie niemand lange spielt.

### 2026-11-21 13:25 – Animations-System für das Match ★ 🤖
- Commit „Add an animation layer and queue to the match board“: neuer Ordner
  `frontend/web/src/features/match/fx/` mit `fxQueue.ts` (Warteschlange: nacheinander, ab 4 wartenden
  Animationen doppelte Geschwindigkeit, Überspringen bei Klick/Taste), `FxLayer.tsx` (Overlay über dem
  Board, `pointer-events: none`), `useBoardFx.ts` (vergleicht zwei Snapshots und liest neue Log-Einträge),
  `fxDom.ts` (fliegende Kopien, nur `transform`/`opacity`) und Zahnrad-Menü „Animationen: Normal /
  Schnell / Aus“ (localStorage mit try/catch, bei `prefers-reduced-motion` standardmäßig „Aus“).
  Karten und Zonen tragen `data-fx-card` / `data-fx-zone`, damit die Animationen ihre Positionen finden.
- **Überlegung:** Der neue Spielzustand wird **sofort** übernommen, die Animation ist nur eine Schicht
  darüber (eine Kopie fliegt dorthin, wo die Karte schon liegt). So ist das Board nie falsch, Eingaben
  werden nie blockiert und ein Klick springt einfach ans Ende. Alternative wäre, den Zustand erst nach
  der Animation zu zeigen (wie viele TCG-Clients) – dann wartet man auf Animationen und es kann zu
  Abweichungen zwischen Anzeige und Server kommen. Animiert wird mit der Web Animations API
  (`element.animate`), weil man damit jede Animation sofort beenden kann; keine neue Bibliothek.
- **Probleme/Lessons Learned:** Die Topbar schneidet überstehende Inhalte ab – das Menü liegt deshalb
  `fixed` unter dem Zahnrad. Wartende Animationen verstecken ihre Zielkarte schon beim Einreihen; damit
  diese nie unsichtbar bleibt, führt „Überspringen“ jeden wartenden Schritt kurz aus und räumt auf.
- Screenshot: `screenshots/2026-11-21_20-match-einstellungen.png`

### 2026-11-21 14:05 – Animationen: Ziehen, Spielen, Events ★ 🤖
- Commit „Animate drawing, playing and Event cards“: Gezogene Karten fliegen vom Deck in die Hand
  (Gegner: Kartenrücken in die Gegnerhand). Spielt der Gegner eine Karte, wird sie 0,6 s groß in der
  Mitte gezeigt und fliegt dann in ihren Slot; danach bleibt sie 2 s leicht markiert. Eigene Karten
  per Klick bekommen dieselbe Landung wie bei Drag & Drop (bei Drag & Drop bleibt die bestehende
  Landung). Events (auch [Counter]-Events) erscheinen groß mit goldenem Leuchten und fliegen in den Trash.
- **Überlegung:** Die Ereignisse werden aus dem Vergleich zweier Snapshots erkannt (welche Karte ist
  neu auf dem Feld, im Trash, in der Hand), weil die Log-Einträge auf `dev` nur Text enthalten.
  Die echte Karte liegt schon an ihrem Platz und ist nur unsichtbar, bis die Kopie landet.
- **Geprüft:** Playwright-Bot spielt mit zwei Browsern ein Match über die echte Oberfläche und nimmt
  bei bestimmten Ereignissen Bildfolgen auf (Screenshot alle 100 ms).
- **Probleme/Lessons Learned:** Karten im Effekt-Dialog tragen dieselbe Karten-ID wie auf dem Board –
  sie werden bei der Positionssuche ausgeschlossen. Das Bild einer noch nie gezeigten Gegnerkarte lädt
  erst während des Reveals; bis dahin ist der Kartenname zu sehen. Ein Reveal wird höchstens um 30 %
  beschleunigt, damit man die Karte lesen kann.
- Bildfolgen: `screenshots/2026-11-21_21-bildfolge-gegner-spielt-karte.png`,
  `screenshots/2026-11-21_22-bildfolge-gegner-spielt-event.png`

### 2026-11-21 14:50 – Animationen: Angriff, Block, Counter, Treffer, K.O., Life ★ 🤖
- Commit „Animate attacks, blocks, counters, hits, K.O.s and Life cards“:
  Beim Wählen des Angriffsziels läuft ein geschwungener goldener Zielpfeil (SVG) von der angreifenden
  Karte zum Mauszeiger/Finger, gültige Ziele pulsieren. Nach dem Angriff springt der Angreifer Richtung
  Ziel und zurück, das Resten dreht sich animiert, über dem Ziel erscheint „5000 ⚔ 5000“. Block: der
  Blocker gleitet kurz vor, Schild-Symbol. Counter: „+2000“ steigt grün auf, die Power zählt hoch.
  Treffer: Leader wackelt, roter Rand, am Handy `navigator.vibrate(30)` (abschaltbar). K.O.: die Karte
  zerfällt in 12 Splitter, ein blasser Geist fliegt in den Trash. Life: die Karte dreht sich auf dem Weg
  in die Hand um (Gegner: Rückseite). [Trigger]: die Karte erscheint groß mit „TRIGGER“.
- **Überlegung:** Angriff, Block und Counter lassen sich direkt am `battle`-Objekt im Snapshot ablesen
  (neuer Angreifer = Angriff, anderes Ziel = Block, höherer Counter-Bonus = Counter). Der K.O. ist ein
  Character, der vom Feld in den Trash wechselt. Die Splitter nutzen das Kartenbild als Hintergrund –
  keine Bilddateien nötig.
- **Probleme/Lessons Learned:** Die Karte-Dialoge liegen im selben Stapelkontext wie das Board; die
  fx-Schicht liegt deshalb darüber (sie fängt keine Klicks ab). Während ein gegnerischer [Trigger]
  aufgelöst wird, liegt die Karte in keiner Zone – sie wird über den Namen im Log gefunden.
- Bildfolgen: `screenshots/2026-11-21_23-bildfolge-angriff-zielpfeil.png`,
  `…_24-bildfolge-angriff.png`, `…_25-bildfolge-ko.png`, `…_26-bildfolge-life-trigger.png`,
  `…_27-bildfolge-counter.png`

### 2026-11-21 15:30 – Animationen: DON!!, Refresh, Effekte, Power ★ 🤖
- Commit „Animate DON!!, refresh, effects and power changes“: DON!! anlegen – eine DON!!-Karte fliegt
  aus der Cost-Area auf die Karte, „+1000“ steigt auf. DON!!-Phase – neue DON!!-Karten fliegen vom
  DON!!-Deck in die Cost-Area. Refresh – alle gerestete Karten drehen sich gleichzeitig zurück (CSS-Übergang
  auf `rotate`/`scale`). Effekt löst aus – die Quelle bekommt einen goldenen Puls, Linien führen zu den
  Karten, die der Effekt nennt oder gerade verändert hat. Power – das Power-Feld bläht sich kurz auf,
  grün bei Plus, rot bei Minus; geänderte Power bleibt grün/rot, beim Hover steht die Basis-Power
  im Tooltip. Angriffsbereite Karten haben einen feinen pulsierenden Rand.
- **Überlegung:** Effekt-Logs beginnen mit „Kartenname: …“ – daraus wird die Quelle gelesen. Ziele
  stehen nicht strukturiert im Log; deshalb werden genannte Kartennamen und Karten mit gerade geänderter
  Power als Ziel genommen (kann in seltenen Fällen ungenau sein, ist aber rein optisch).
- **Probleme/Lessons Learned:** Das Power-Feld fängt keine Mauszeiger-Ereignisse – der Basis-Power-Tooltip
  sitzt deshalb auf der Karte selbst.
- Bildfolgen: `screenshots/2026-11-21_28-bildfolge-don.png`, `…_29-bildfolge-effekt.png`

### 2026-11-21 16:10 – Zugwechsel-Banner und Spielende-Screen ★ 🤖
- Commit „Show a turn banner and a new game over screen“: Bei jedem Zugwechsel läuft 0,9 s ein Banner
  quer über das Board – „DEIN ZUG“ in Gold bzw. „ZUG DES GEGNERS“ gedämpft, mit Zugnummer; danach leuchtet
  die eigene Aktionsleiste kurz auf. Spielende: „SIEG“ mit Goldkonfetti bzw. „NIEDERLAGE“ mit dunklem
  Schleier, darunter beide Spieler mit Leader, Zahl der Züge, Modus und – bei Ranked – die
  Kopfgeld-Änderung. Buttons „Revanche-Tisch erstellen“ (bestehender Erstellen-Ablauf mit neuem Code)
  und „Zurück zur Lobby“.
- **Überlegung:** Die Kopfgeld-Abfrage war im `BountyChangeBanner` versteckt; sie steckt jetzt im Hook
  `useBountyChange`, den Banner und Spielende-Screen gemeinsam nutzen (eine Abfrage statt zwei). Der
  Banner hält Eingaben nicht auf (`pointer-events: none`).
- **Probleme/Lessons Learned:** Der Test-Bot hat den Sieg zuerst bei einem abgewehrten Angriff
  aufgenommen – die Aufnahme wird jetzt wiederholt, bis das Spiel wirklich vorbei ist.
- Bildfolgen/Screenshots: `screenshots/2026-11-21_30-bildfolge-zugwechsel.png`,
  `…_31-bildfolge-sieg.png`, `…_32-niederlage.png`

### 2026-11-21 16:45 – Sounds im Match ★ 🤖
- Commit „Add synthesized match sounds“: `fx/sounds.ts` erzeugt alle Klänge mit der Web Audio API
  (Oszillatoren und gefiltertes Rauschen): Karte ziehen, Karte spielen, Event, Angriff (Whoosh),
  Treffer, Block (Schild), Counter, K.O., Life verloren, DON!!, Zugwechsel (eigener/gegnerischer),
  Sieg, Niederlage und ein Timer-Tick für später. Im Zahnrad: „Sound an“ und Lautstärke-Regler
  (Standard: an, 50 %), gespeichert in localStorage. Der Klang startet mit seinem Animationsschritt;
  beim Überspringen bleibt es still.
- **Überlegung:** Synthetisierte Klänge statt Audiodateien – keine Lizenzfragen, keine Downloads, sehr
  klein, und sie passen automatisch zur Lautstärke. Der AudioContext wird erst nach dem ersten Klick oder
  Tastendruck erzeugt (Browser-Regel, und niemand wird beim Öffnen der Seite überrascht).
- **Geprüft:** Playwright zählt AudioContexts und gestartete Töne: 0 vor der ersten Interaktion,
  Töne im Match, stumm = keine Töne bei zwei Zugwechseln, Einstellungen nach dem Neuladen erhalten.
- Screenshot: `screenshots/2026-11-21_33-sound-einstellungen.png`

### 2026-11-21 18:20 – Tastenkürzel, Auto-Pass, Hinweise; Leistung ★ 🤖
- Commit „Add keyboard shortcuts, auto pass and board hints“: Tastenkürzel (nur wenn kein Eingabefeld
  aktiv ist): Leertaste = Zug beenden (zweimal drücken oder 400 ms halten), A = Angriff, D = DON!!,
  E = Effekt, Tab = nächste eigene Feldkarte, 1–9 = Handkarte auswählen (Enter spielt sie), Enter =
  im Dialog bestätigen, Esc = abbrechen, ? = Übersicht. Kleine Kürzel-Hinweise auf den Buttons
  (nur Desktop, für Screenreader ausgeblendet). Auto-Pass: Block-Schritt ohne möglichen Blocker
  (öffentliche Information) nach 0,7 s; Counter-Schritt nur mit selbst eingeschaltetem
  „Auto-Pass Counter“ und zufälliger Pause von 0,8–1,6 s. „End Turn“ leuchtet, wenn nichts Sinnvolles
  mehr möglich ist (nur Hinweis). Gegnerkarten zeigen beim Hover Mini-Icons (🛡 Blocker, 💀 On K.O.,
  ✦ Trigger …).
- **Leistung:** Messung mit Playwright im Handy-Querformat (844 × 390) und 4-facher CPU-Drosselung
  (Chrome DevTools Protocol) auf dem Produktions-Build. Erst lag die Erkennung im React-Commit bei bis zu
  355 ms, weil direkt nach den DOM-Änderungen Positionen gelesen wurden (erzwungenes Layout). Jetzt liest
  die Erkennung keine Positionen mehr, Animationen starten im nächsten Animation-Frame, Positionen werden
  nach dem Zeichnen gemerkt: Erkennung max. 1,5 ms, Animationsschritt max. 7 ms, in ~2100 Frames mit
  laufender Animation nur 0–1 Frame über 50 ms (50,1 ms). Pulsierende Ränder animieren nur noch die
  `opacity` einer Leucht-Ebene statt `box-shadow` (das hätte jeden Frame neu gezeichnet).
  Messmarken `fx-snapshot`, `fx-step`, `fx-boxes` sind im DevTools-Performance-Panel sichtbar.
- **Geprüft:** Tastenkürzel im Browser (Hilfe, 1/Enter, Tab/D, Leertaste zweimal und halten);
  mit `prefers-reduced-motion` 8 Züge gespielt – keine einzige bewegte Animation, Board voll bedienbar.
- **Probleme/Lessons Learned:** (1) Der pulsierende „End Turn“ hat sich anfangs skaliert – ein ständig
  bewegtes Klickziel (Playwright wartete vergeblich auf „stabil“); jetzt pulsiert nur das Leuchten.
  (2) Die Text-Einblendungen stiegen auch im reduzierten Modus auf – jetzt nur Ein-/Ausblenden.
  (3) **Befund am Server:** Auf `dev` schickt der Server den Counter- und Block-Schritt gar nicht, wenn
  nichts nutzbar ist. Der Angreifer merkt dann am sofortigen Schaden, dass keine Counter auf der Hand sind –
  genau das Informationsleck, das der Auto-Pass vermeiden soll. Der Client-Schalter greift deshalb derzeit
  praktisch nie; die eigentliche Lösung (Counter-Schritt immer senden oder serverseitig kurz warten) ist
  eine Backend-Änderung und war nicht Teil dieses Auftrags.
- Screenshots: `screenshots/2026-11-21_34-tastenkuerzel.png`, `…_35-leertaste-bestaetigen.png`,
  `…_36-gegnerkarte-hinweise.png`, `…_37-zug-beenden-hinweis.png`

### 2026-11-21 15:28 – dev und logic zusammengeführt, alles auf main ★ 🤖
- Merge von `dev` (Animationen, Sounds, Tastenkürzel) in `logic` (Reconnect, Zug-Timer, Manueller Modus,
  Aufzeichnung), danach `logic` → `dev` → `main` (Deployment).
- **Konflikte:** vor allem `MatchBoard.tsx` – beide Branches haben das Board erweitert. Gelöst, indem beide
  Funktionen erhalten bleiben: Manueller Modus und „Fehler melden“ aus `logic`, Animationen, Mini-Icons und
  Tastenkürzel aus `dev`. Der neue Spielende-Screen zeigt jetzt zusätzlich den Endgrund aus `logic`
  (z. B. „Zeit abgelaufen – Niederlage“, „Gegner hat die Verbindung verloren – Sieg“) und „Fehler melden“.
  Doku und Changelog: beide Seiten behalten, zeitlich sortiert.
- **Geprüft (zusammengeführter Stand):** `dotnet build`/`dotnet test` grün (300 Tests, 6 übersprungen),
  Simulator-Smoke 200/200, `npm run lint`/`npm run build` grün; Playwright mit zwei Browsern:
  Reconnect a)–c), Timer d)–e), Animations-Bot über ein ganzes Match bis zum Sieg, Sounds, Tastenkürzel.
- **Probleme/Lessons Learned:** Ein Test erwartete nach dem Neuladen den Startbildschirm – mit Reconnect
  springt die App aber direkt zu „Zurück ins Match“. Die Funktionen der beiden Branches beeinflussen sich
  also; deshalb wurde nach dem Merge alles noch einmal gemeinsam getestet.

<!-- Neue Einträge unten anfügen. Vorlage:

### JJJJ-MM-TT HH:MM – Titel ★/👥/🤖
- `commit` Was wurde gemacht?
- **Überlegung:** Warum so? Welche Alternativen gab es?
- **Probleme/Lessons Learned:** (optional)
- Screenshot: `screenshots/JJJJ-MM-TT_nr-name.png` (bei sichtbaren Änderungen)
-->
