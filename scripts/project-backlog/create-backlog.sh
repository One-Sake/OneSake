#!/usr/bin/env bash
# Legt Backlog, Sprint 1-3 und User Stories für OneSake auf GitHub an.
#
# Voraussetzungen:
#   - GitHub CLI installiert: https://cli.github.com
#   - Angemeldet mit Schreibrechten auf One-Sake/OneSake:  gh auth login
#
# Testlauf (zeigt nur an, was passieren würde):
#   DRY_RUN=1 bash scripts/project-backlog/create-backlog.sh
# Echter Lauf:
#   bash scripts/project-backlog/create-backlog.sh
# Optional alle Issues zusätzlich in ein GitHub Project eintragen
# (vorher einmal: gh auth refresh -s project):
#   PROJECT_NUMBER=1 bash scripts/project-backlog/create-backlog.sh
#
# Das Skript kann mehrfach laufen: Issues mit gleichem Titel werden nicht doppelt angelegt.

set -euo pipefail

REPO="One-Sake/OneSake"
OWNER="One-Sake"
DRY_RUN="${DRY_RUN:-0}"
PROJECT_NUMBER="${PROJECT_NUMBER:-}"

FE="WoWoWocal"     # Frontend
BE="danielbna"     # Backend
DB="13-Tino-13"    # Datenbank

SPRINT1=()
SPRINT2=()
SPRINT3=()
BACKLOG=()
ALL_URLS=()

run() {
  if [ "$DRY_RUN" = "1" ]; then
    echo "  [dry-run] $*"
  else
    "$@"
  fi
}

# ---------------------------------------------------------------------------
# Labels
# ---------------------------------------------------------------------------
echo "== Labels =="
create_label() {
  run gh label create "$1" -R "$REPO" --color "$2" --description "$3" --force
}
create_label "user-story"   "1d76db" "User Story"
create_label "task"         "c5def5" "Technische Aufgabe"
create_label "frontend"     "0e8a16" "Frontend (WoWoWocal)"
create_label "backend"      "5319e7" "Backend (danielbna)"
create_label "database"     "fbca04" "Datenbank (13-Tino-13)"
create_label "docs"         "bfdadc" "Dokumentation und Team-Workflow"
create_label "Sprint 1"     "0052cc" "Sprint 1: 17.04.-10.05.2026"
create_label "Sprint 2"     "006b75" "Sprint 2: 11.05.-07.06.2026"
create_label "Sprint 3"     "b60205" "Sprint 3: ab 06.10.2026 (laufend)"
create_label "Backlog"      "ededed" "Product Backlog (noch keinem Sprint zugeordnet)"
create_label "prio: high"   "d93f0b" "Hohe Priorität"
create_label "prio: medium" "fbca04" "Mittlere Priorität"
create_label "prio: low"    "c2e0c6" "Niedrige Priorität"
create_label "sprint-overview" "000000" "Sprint-Übersicht"

# ---------------------------------------------------------------------------
# Vorhandene Issues einlesen (damit nichts doppelt angelegt wird)
# ---------------------------------------------------------------------------
EXISTING_FILE="$(mktemp)"
trap 'rm -f "$EXISTING_FILE"' EXIT
gh issue list -R "$REPO" --state all --limit 1000 --json number,title \
  --jq '.[] | "\(.number)\t\(.title)"' > "$EXISTING_FILE"

find_existing() {
  awk -F'\t' -v t="$1" '$2 == t { print $1; exit }' "$EXISTING_FILE"
}

add_to_sprint() {
  local sprint="$1" ref="$2"
  case "$sprint" in
    "Sprint 1") SPRINT1+=("$ref") ;;
    "Sprint 2") SPRINT2+=("$ref") ;;
    "Sprint 3") SPRINT3+=("$ref") ;;
    *)          BACKLOG+=("$ref") ;;
  esac
}

# story <sprint> <labels> <assignee> <open|closed> <title> <body>
story() {
  local sprint="$1" labels="$2" assignee="$3" state="$4" title="$5" body="$6"
  local number url

  number="$(find_existing "$title")"
  if [ -n "$number" ]; then
    echo "-- existiert schon: #$number $title"
    url="https://github.com/$REPO/issues/$number"
  elif [ "$DRY_RUN" = "1" ]; then
    echo "-- [dry-run] anlegen ($state, $assignee, $labels): $title"
    number="NEU"
    url="https://github.com/$REPO/issues/NEU"
  else
    url="$(gh issue create -R "$REPO" --title "$title" --body "$body" \
      --label "$labels" --assignee "$assignee")"
    number="${url##*/}"
    echo "-- angelegt: #$number $title"
    if [ "$state" = "closed" ]; then
      gh issue close "$number" -R "$REPO" --reason completed >/dev/null
    fi
  fi

  ALL_URLS+=("$url")
  add_to_sprint "$sprint" "- [$( [ "$state" = closed ] && echo x || echo ' ' )] #$number $title"
}

# existing <number> <sprint> <labels> <assignee|-> <close|open|keep> <comment|->
existing() {
  local number="$1" sprint="$2" labels="$3" assignee="$4" action="$5" comment="$6"
  local title
  title="$(awk -F'\t' -v n="$number" '$1 == n { print $2; exit }' "$EXISTING_FILE")"
  echo "-- bestehend: #$number $title"

  run gh issue edit "$number" -R "$REPO" --add-label "$labels"
  if [ "$assignee" != "-" ]; then
    run gh issue edit "$number" -R "$REPO" --add-assignee "$assignee"
  fi
  if [ "$comment" != "-" ]; then
    run gh issue comment "$number" -R "$REPO" --body "$comment"
  fi
  if [ "$action" = "close" ]; then
    run gh issue close "$number" -R "$REPO" --reason completed
  fi

  local done=' '
  if [ "$action" = "close" ] || [ "$action" = "closed" ]; then done='x'; fi
  ALL_URLS+=("https://github.com/$REPO/issues/$number")
  add_to_sprint "$sprint" "- [$done] #$number $title"
}

# ===========================================================================
# Bestehende Issues einordnen und aufräumen
# ===========================================================================
echo "== Bestehende Issues =="

# Sprint 1 – Infrastruktur-Tasks (bereits geschlossen)
existing 3  "Sprint 1" "task,backend,Sprint 1" "$BE" closed "-"
existing 4  "Sprint 1" "task,backend,Sprint 1" "$BE" closed "-"
existing 5  "Sprint 1" "task,backend,Sprint 1" "$BE" closed "-"
existing 6  "Sprint 1" "task,backend,Sprint 1" "$BE" closed "-"
existing 7  "Sprint 1" "task,backend,Sprint 1" "$BE" closed "-"

# Sprint 1 – Vertical Slice (bereits geschlossen)
existing 13 "Sprint 1" "user-story,backend,Sprint 1" - closed "-"
existing 15 "Sprint 1" "user-story,backend,Sprint 1" - closed "-"
existing 16 "Sprint 1" "user-story,backend,Sprint 1" - closed "-"
existing 17 "Sprint 1" "user-story,backend,Sprint 1" - closed "-"
existing 23 "Sprint 1" "user-story,backend,Sprint 1" - close \
"Erledigt in Sprint 1: END_TURN-Prompt nur für den aktiven Spieler, Zugwechsel, TURN_END/TURN_START-Events (f993384). Mit dem vollständigen Zugablauf (Refresh, Draw, DON!!, Main, End) erweitert in c263f41. Tests in MatchRoomTests, CI grün."

# Sprint 2
existing 18 "Sprint 2" "user-story,backend,Sprint 2" - closed "-"
existing 19 "Sprint 2" "user-story,backend,Sprint 2" - close \
"Erledigt in Sprint 2: Decks werden dem Spieler im Raum zugeordnet, gemischt und die Starthand gezogen (0ae3cb5, cf21aeb). Zonen (Hand, Deck, Leben, Board, Trash) verwaltet der Server; ungültige Operationen werden abgelehnt. Erweitert um DON!!-Zonen in c263f41."
existing 21 "Sprint 2" "user-story,backend,Sprint 2" - close \
"Erledigt in Sprint 2: MULLIGAN_DECISION-Prompt für beide Spieler, Entscheidung nur in der Mulligan-Phase, neue Hand wird gezogen, Phase wechselt erst nach beiden Entscheidungen (f5de655, a1e016b). Tests in MatchRoomTests."
existing 10 "Sprint 2" "task,backend,Sprint 2" - close \
"Erledigt: HandleMulliganDecision und HandleMainAction ausgelagert (a1e016b), weitere Schritte (Block, Counter, End Turn) als eigene Handler bzw. Actions in c263f41."

# Sprint 3
existing 14 "Sprint 3" "user-story,backend,Sprint 3" - closed "-"
existing 20 "Sprint 3" "user-story,backend,Sprint 3" - close \
"Erledigt in Sprint 3: Karten werden aus der Hand gespielt und mit DON!! bezahlt, der Server prüft Phase, aktiven Spieler, Kosten und Platz im Character-Bereich (PlayCardAction, ActionGuard). Umsetzung in c263f41, Tests in GameActionTests, CI grün."
existing 26 "Sprint 3" "user-story,backend,Sprint 3" - close \
"Erledigt in Sprint 3: Spielende durch Schaden bei 0 Leben oder leeres Deck, Phase GameOver, Gewinner wird geloggt und an beide Spieler geschickt, danach werden Spielaktionen abgelehnt (c263f41)."
existing 28 "Sprint 3" "user-story,backend,Sprint 3" - close \
"Erledigt in Sprint 3: GameActionTests und MatchRoomTests decken Match-Erstellung, Beitritt, Mulligan, Zugwechsel, Kartenaktionen, Kampf und ungültige Aktionen ab. Die Tests laufen in der CI bei jedem Push und sind grün."
existing 22 "Sprint 3" "user-story,backend,Sprint 3" - open \
"Stand Sprint 3: Phase und aktiver Spieler werden für alle Spielaktionen geprüft (ActionGuard). Noch offen: StartMatch und SendChat prüfen nicht, ob der Aufrufer im Raum ist; das Deck (50 Karten, max. 4 Kopien, Leader) wird nur im Client validiert."
existing 24 "Sprint 3" "user-story,backend,Sprint 3" - open \
"Stand Sprint 3: Updates werden in Echtzeit an beide Spieler geschickt, Disconnects werden erkannt. Noch offen: Wiederverbinden ohne Verlust des Matches (z. B. Rejoin-Token statt Verbindungs-ID als Spieler-ID)."
existing 27 "Sprint 3" "user-story,backend,Sprint 3" - open \
"Stand Sprint 3: Der Deploy-Workflow läuft bei jedem Push auf main erfolgreich durch. Noch offen: vollständiger Verbindungs- und Match-Test auf dem Server, Zugangsdaten über Environment-Variablen statt appsettings.json, EF-Migrationen beim Deploy."

# Backlog
existing 11 "Backlog" "task,backend,Backlog" - open "-"
existing 12 "Backlog" "task,backend,Backlog" - open "-"

# Duplikat
run gh issue edit 25 -R "$REPO" --add-label "duplicate"
run gh issue comment 25 -R "$REPO" --body "Duplikat von #26."

# ===========================================================================
# SPRINT 1 – Fundament & Vertical Slice (17.04.–10.05.2026)
# ===========================================================================
echo "== Sprint 1 =="

story "Sprint 1" "user-story,frontend,Sprint 1" "$FE" closed \
"UserStory #16 – Als Spieler möchte ich in einer Lobby Namen und Raumcode eingeben, damit ich einem Match beitreten und es starten kann" \
"**Akzeptanzkriterien**

- Es gibt Eingabefelder für Anzeigename und Raumcode
- Der Spieler kann einem Raum beitreten und das Match starten
- Raumcode, Zugnummer, Phase und aktiver Spieler werden angezeigt
- Beide Spieler werden mit Deck-, Hand- und Lebenszahl sowie Verbindungsstatus angezeigt
- Der Zustand aktualisiert sich live, sobald Aktionen passieren

**Zeitraum:** 19.04.2026 – 21.04.2026
**Umsetzung:** f993384

_Story Points: 5_"

story "Sprint 1" "user-story,frontend,Sprint 1" "$FE" closed \
"UserStory #17 – Als Spieler möchte ich Entscheidungen, Spielverlauf und Chat auf einem Bildschirm sehen, damit ich das Match verfolgen und mit meinem Gegner schreiben kann" \
"**Akzeptanzkriterien**

- Ein Choice-Sheet zeigt nur dem lokalen Spieler seine offenen Entscheidungen
- Buttons für KEEP, MULLIGAN und END_TURN senden die Entscheidung an den Server
- Das Match-Log zeigt Server-Ereignisse in Echtzeit an
- Chat-Nachrichten können gesendet werden und erscheinen bei beiden Spielern

**Zeitraum:** 19.04.2026 – 21.04.2026
**Umsetzung:** f993384

_Story Points: 5_"

story "Sprint 1" "user-story,docs,Sprint 1" "$FE" closed \
"UserStory #18 – Als Team möchten wir eine klare Repository-Struktur, Dokumentation und einen festen Git-Workflow, damit alle parallel arbeiten können" \
"**Akzeptanzkriterien**

- Ordnerstruktur für Frontend, Backend und Docs ist angelegt
- README, CONTRIBUTING, Team-Workflow und ADRs sind vorhanden
- Der Workflow mit den Branches dev und main ist festgelegt und dokumentiert
- Pull-Request- und Issue-Vorlagen sind vorhanden
- Das Frontend hat eine ESLint-Konfiguration

**Zeitraum:** 17.04.2026 – 23.04.2026
**Umsetzung:** b4f6bde, d5dc881, d3447a4, 0f4d923, c363bbb

_Story Points: 2_"

story "Sprint 1" "user-story,backend,Sprint 1" "$BE" closed \
"UserStory #19 – Als Spieler möchte ich, dass der Server Kartendaten aus der OPTCG-API abruft, damit ich echte Karten verwenden kann" \
"**Akzeptanzkriterien**

- CardService ruft einzelne Karten über die Karten-ID ab
- Alle Karten eines Sets können über die Set-ID abgerufen werden
- CardDto enthält alle Werte der API (Name, Farbe, Typ, Kosten, Power, Counter, Bild …)
- Ein Health-Endpoint zeigt, ob die API erreichbar ist
- Fehler der externen API werden abgefangen

**Zeitraum:** 23.04.2026 – 29.04.2026
**Umsetzung:** afa33a1, 46b7d5b, f86c00d, 28354c3, f5b8840, c3ebb63, 158e412

_Story Points: 5_"

story "Sprint 1" "user-story,backend,Sprint 1" "$BE" closed \
"UserStory #20 – Als Team möchten wir, dass jeder Push automatisch gebaut, getestet und ausgeliefert wird, damit wir Fehler früh bemerken" \
"**Akzeptanzkriterien**

- Die CI baut und testet Frontend und Backend bei jedem Push und Pull Request
- Ein Deploy-Workflow liefert die Anwendung auf den Droplet aus
- Die nötigen Secrets (SSH-Key, User, Host) sind in GitHub hinterlegt
- Das Backend läuft als systemd-Service, das Frontend wird über nginx ausgeliefert

**Tasks:** #3, #4, #5, #6, #7
**Zeitraum:** 20.04.2026 – 13.05.2026
**Umsetzung:** 1adee21, 0dda3ac, ca83cf3

_Story Points: 5_"

# ===========================================================================
# SPRINT 2 – Deckbuilder, Tools, Match-UI, Datenbank (11.05.–07.06.2026)
# ===========================================================================
echo "== Sprint 2 =="

# --- Frontend ---
story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #21 – Als Spieler möchte ich Karten im Deckbuilder suchen und filtern, damit ich schnell passende Karten für mein Deck finde" \
"**Akzeptanzkriterien**

- Karten können nach Set ausgewählt oder aus allen Sets angezeigt werden
- Volltextsuche über Name, Karten-ID, Typ und Kartentext
- Filter nach Farbe, Kartentyp, Kosten und Counter
- Nach Wahl eines Leaders werden nur Karten in seinen Farben angezeigt
- Karten sind sinnvoll sortiert (Farbe, Typ, Kosten)

**Zeitraum:** 12.05.2026 – 20.05.2026
**Umsetzung:** 2b6e969, 054195c, 2a0a21b, aeac2cc, 0f093b8, cee34db, 184ffa4, aa3e930

_Story Points: 8_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #22 – Als Spieler möchte ich ein Deck mit Leader zusammenstellen, damit ich ein regelkonformes Deck zum Spielen habe" \
"**Akzeptanzkriterien**

- Ein Leader kann gewählt und gewechselt werden
- Karten können per Klick hinzugefügt und entfernt werden
- Maximal 4 Kopien pro Karte, Karten müssen zur Leader-Farbe passen
- Die Validierung prüft 50 Karten, Leader und Farben und zeigt Fehler an
- Die Deckliste wird als Kartenstapel mit Anzahl angezeigt
- Der aktuelle Entwurf bleibt nach einem Neuladen erhalten

**Zeitraum:** 12.05.2026 – 19.05.2026
**Umsetzung:** 3fcf7f6, f76b28b, feb6b43, cea01e6, 3610a43

_Story Points: 5_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #23 – Als Spieler möchte ich mehrere Decks speichern, laden, duplizieren und löschen, damit ich verschiedene Decks verwalten kann" \
"**Akzeptanzkriterien**

- Decks werden in einer Deck-Bibliothek gespeichert (lokal im Browser)
- Ein gespeichertes Deck kann geladen und weiterbearbeitet werden
- Decks können dupliziert, umbenannt und gelöscht werden
- Die Bibliothek zeigt Leader und Kartenanzahl je Deck

**Zeitraum:** 12.05.2026 – 18.05.2026
**Umsetzung:** 0e3d9fa, bfd3ef1, 92048d1

_Story Points: 5_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #24 – Als Spieler möchte ich Karten groß ansehen und die Kartengröße einstellen, damit ich Kartentexte lesen kann" \
"**Akzeptanzkriterien**

- Beim Darüberfahren oder Antippen erscheint eine große Kartenvorschau
- Die Anzahl der Karten pro Reihe ist per Regler einstellbar und wird gespeichert
- Die Vorschau funktioniert auch für Karten in der Deckliste

**Zeitraum:** 14.05.2026 – 20.05.2026
**Umsetzung:** d0a1d57, 6b2d546, c0d450b, be31497

_Story Points: 3_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #25 – Als Spieler möchte ich mit einem Mulligan-Trainer Starthände üben, damit ich bessere Keep-/Mulligan-Entscheidungen treffe" \
"**Akzeptanzkriterien**

- Aus einem gespeicherten Deck wird eine zufällige Starthand mit 5 Karten gezogen
- Der Spieler entscheidet Keep oder Mulligan
- Kennzahlen der Hand werden angezeigt (Counter, Events, günstige Karten, Characters)
- Session-Statistik mit gezogenen Händen und Keep-Rate

**Zeitraum:** 12.05.2026 – 17.05.2026
**Umsetzung:** 0e3d9fa, e24e0e2

_Story Points: 5_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #26 – Als Spieler möchte ich berechnen, wie wahrscheinlich ich bestimmte Karten ziehe, damit ich mein Deck besser bauen kann" \
"**Akzeptanzkriterien**

- Zielkarten können einzeln oder als Gruppe (z. B. 2k-Counter, Events) gewählt werden
- Presets für Starthand sowie Zug 2 als erster bzw. zweiter Spieler
- Die Monte-Carlo-Simulation läuft im Web Worker, die Oberfläche bleibt flüssig
- Ergebnis wird in Prozent mit Anzahl der Simulationen angezeigt

**Zeitraum:** 12.05.2026 – 15.05.2026
**Umsetzung:** e1d46f8, aa5d105, be47aea

_Story Points: 5_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #27 – Als Spieler möchte ich ein Spielbrett im Vollbild sehen, damit ich das Match auch am Handy gut spielen kann" \
"**Akzeptanzkriterien**

- Das Brett zeigt alle Zonen beider Spieler (Leader, Characters, Leben, Deck, DON!!, Trash)
- Die eigene Hand ist unten sichtbar, Karten können ausgewählt und gespielt werden
- Nach dem Beitritt öffnet sich das Brett im Vollbild, am Handy im Querformat
- Log und Chat sind über ein Seitenpanel erreichbar
- Karten können vergrößert angesehen werden

**Zeitraum:** 13.05.2026 – 16.05.2026
**Umsetzung:** fccf35c, ff253e1, 2b9828c, 8489cbc, 9cd168b, 45950b6, 60affb5, e55d4c8, 6cfe174

_Story Points: 8_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #28 – Als Spieler möchte ich vor dem Match ein gespeichertes Deck wählen, damit ich mit meinem eigenen Deck spiele" \
"**Akzeptanzkriterien**

- In der Lobby werden die zuletzt genutzten Decks mit Leader-Bild angezeigt
- Alle Decks sind über die Deck-Bibliothek wählbar
- Nur gültige Decks können verwendet werden
- Das gewählte Deck wird beim Beitritt an den Server übermittelt

**Zeitraum:** 13.05.2026 – 19.05.2026
**Umsetzung:** 31fbbf8, 0ae3cb5, 41df9c0, dff1d9d, b7b8be9

_Story Points: 3_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #29 – Als Spieler möchte ich den Verbindungsstatus sehen und mich auch vom Handy verbinden können, damit ich weiß, ob ich spielen kann" \
"**Akzeptanzkriterien**

- Ein Badge zeigt verbunden, verbinde, wiederverbinden und Fehler an
- Verbindungsfehler werden verständlich erklärt
- Die Backend-Adresse funktioniert auch von anderen Geräten im Netzwerk (Handy)

**Zeitraum:** 13.05.2026 – 18.05.2026
**Umsetzung:** b0ed497, 89021bf, b9262de

_Story Points: 3_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #30 – Als Spieler möchte ich einen Startbildschirm sehen, damit ich direkt ins Spiel oder zum Login komme" \
"**Akzeptanzkriterien**

- Vollbild-Startbildschirm mit OneSake-Artwork
- Buttons für Spielen und Login
- Funktioniert auf Handy und Desktop

**Zeitraum:** 14.05.2026 – 15.05.2026
**Umsetzung:** 75714d3, b696b81, 6a19d9e, 0753aa5

_Story Points: 3_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #31 – Als Spieler möchte ich eine Profilseite mit Gast-Login, damit ich einen Namen festlegen kann" \
"**Akzeptanzkriterien**

- Der Spieler kann als Gast mit eigenem Namen fortfahren
- Der Name bleibt lokal gespeichert und kann geändert werden
- Platzhalter für Login und Statistiken (Decks, Matches, Siege, Berries)

**Zeitraum:** 15.05.2026
**Umsetzung:** e9c9c49

_Story Points: 2_"

story "Sprint 2" "user-story,frontend,Sprint 2" "$FE" closed \
"UserStory #32 – Als Spieler möchte ich eine übersichtliche Lobby mit Schritt-Anzeige, damit ich schnell ein Match einrichten kann" \
"**Akzeptanzkriterien**

- Schritte Spieler, Raum, Deck und Brett zeigen, was noch fehlt
- Raumcodes können erzeugt und kopiert werden
- Ein zufälliger Piratenname kann generiert werden
- Fehler werden direkt in der Lobby angezeigt

**Zeitraum:** 15.05.2026 – 18.05.2026
**Umsetzung:** bb2d487, f7f4464, a4bab4c, 890623c

_Story Points: 3_"

# --- Backend ---
story "Sprint 2" "user-story,backend,Sprint 2" "$BE" closed \
"UserStory #33 – Als Spieler möchte ich Karten aus allen Booster- und Starter-Sets abrufen können, damit der Deckbuilder alle Karten anbietet" \
"**Akzeptanzkriterien**

- Booster-Sets (OP-xx) und Starter-Decks (ST-xx) können abgerufen werden
- Zahlenwerte der API (Kosten, Power, Counter, Leben) werden korrekt gelesen
- Fehler beim Abruf werden geloggt
- Nicht verfügbare Sets werden nicht angeboten

**Zeitraum:** 12.05.2026 – 14.05.2026
**Umsetzung:** f131f49, 58d5493, 24e372d, 80ababe, 0acbe0c, 2ab371e, 56cc868, 0f0aea0, 7b80023, fe1e15e

_Story Points: 5_"

story "Sprint 2" "user-story,backend,Sprint 2" "$BE" closed \
"UserStory #34 – Als Entwickler möchte ich die Spielregeln in eigene Action-Klassen auslagern, damit MatchRoom übersichtlich und testbar bleibt" \
"**Akzeptanzkriterien**

- Spielaktionen (Karte spielen, Angriff, Zug beenden) liegen in OneSake.Game
- MatchRoom ruft die Actions auf, statt die Regeln selbst zu enthalten
- Mulligan und Main-Action haben eigene Handler-Methoden
- Alle Backend-Tests laufen nach dem Umbau weiterhin

**Tasks:** #10
**Zeitraum:** 22.05.2026 – 01.06.2026
**Umsetzung:** 7f9da18, c11cdf4, a1e016b

_Story Points: 5_"

# --- Datenbank ---
story "Sprint 2" "user-story,database,Sprint 2" "$DB" closed \
"UserStory #35 – Als Entwickler möchte ich eine PostgreSQL-Datenbank für Kartendaten, damit Karten nicht bei jeder Anfrage von der API geladen werden müssen" \
"**Akzeptanzkriterien**

- PostgreSQL läuft lokal über docker-compose
- EF Core mit DbContext und Card-Entity ist eingerichtet
- Connection-Strings für Entwicklung sind konfiguriert
- Die Einrichtung ist im README beschrieben

**Zeitraum:** 13.05.2026
**Umsetzung:** 2194ca1, 36d3eaf, 009e8cb, fd9e7ff, 4b31627

_Story Points: 5_"

story "Sprint 2" "user-story,database,Sprint 2" "$DB" closed \
"UserStory #36 – Als Entwickler möchte ich Karten aus der API in die Datenbank importieren, damit die Kartendaten dauerhaft gespeichert sind" \
"**Akzeptanzkriterien**

- Ein Import-Endpoint lädt alle Karten eines Sets und speichert sie
- Bereits vorhandene Karten werden nicht doppelt gespeichert
- Der Import meldet, wie viele Karten gespeichert wurden

**Zeitraum:** 14.05.2026
**Umsetzung:** 5f964c0

_Story Points: 5_"

story "Sprint 2" "user-story,database,Sprint 2" "$DB" closed \
"UserStory #37 – Als Spieler möchte ich, dass Karten zuerst aus der Datenbank geladen werden, damit der Deckbuilder schneller lädt" \
"**Akzeptanzkriterien**

- Kartenanfragen lesen zuerst aus der Datenbank
- Fehlt ein Set in der Datenbank, wird es von der API geholt und gespeichert
- Ist die Datenbank nicht erreichbar, wird die API als Fallback genutzt

**Zeitraum:** 19.05.2026
**Umsetzung:** c8eac75, 25ab6f2

_Story Points: 5_"

story "Sprint 2" "user-story,database,Sprint 2" "$DB" closed \
"UserStory #38 – Als Spieler möchte ich sehen, welche Kartensets verfügbar sind, damit ich gezielt Sets auswählen kann" \
"**Akzeptanzkriterien**

- Ein Endpoint liefert die Liste der verfügbaren Sets aus der Datenbank
- Die Liste ist sortiert

**Zeitraum:** 20.05.2026
**Umsetzung:** 8d0535a

_Story Points: 2_"

# ===========================================================================
# SPRINT 3 – Server-Browser & echte Spielregeln (ab 06.10.2026, laufend)
# ===========================================================================
echo "== Sprint 3 =="

story "Sprint 3" "user-story,frontend,Sprint 3" "$FE" closed \
"UserStory #39 – Als Spieler möchte ich eine Liste offener Server sehen, damit ich einem Match ohne Raumcode beitreten kann" \
"**Akzeptanzkriterien**

- Die Lobby verbindet sich beim Öffnen und zeigt alle offenen Räume live an
- Räume können erstellt, beigetreten und verlassen werden
- Volle Räume und laufende Matches sind erkennbar
- Fehlermeldungen des Servers werden verständlich angezeigt

**Zeitraum:** 03.11.2026
**Umsetzung:** f983407

_Story Points: 3_"

story "Sprint 3" "user-story,frontend,Sprint 3" "$FE" closed \
"UserStory #40 – Als Spieler möchte ich echte Karten auf dem Spielbrett sehen und per Klick spielen, damit sich das Match wie OPTCG anfühlt" \
"**Akzeptanzkriterien**

- Board und Hand zeigen Kartenbilder, Power und angehängte DON!!
- Erschöpfte (rested) Karten und der DON!!-Bereich sind sichtbar
- Karte spielen, DON!! geben, angreifen, blocken, countern und Zug beenden per Klick
- Das Deck wird mit Kartenwerten und Bild-URLs an den Server geschickt

**Zeitraum:** 07.11.2026
**Umsetzung:** c263f41

_Story Points: 8_"

story "Sprint 3" "user-story,backend,Sprint 3" "$BE" closed \
"UserStory #41 – Als Spieler möchte ich nach den echten OPTCG-Regeln spielen, damit das Match fair und regelkonform abläuft" \
"**Akzeptanzkriterien**

- Leader, Leben und DON!!-Deck werden beim Start aufgebaut
- Vollständiger Zugablauf: Refresh, Draw, DON!!, Main, End
- Karten kosten DON!!, DON!! können für +1000 Power angehängt werden
- Kampf mit Angriff, Blocker, Counter, Schadensschritt und K.O.
- Keywords Rush, Blocker, Double Attack und Banish
- Sieg durch Schaden bei 0 Leben oder leeres Deck
- Neue Regeln sind mit Tests abgedeckt

**Zeitraum:** 03.11.2026 – 07.11.2026
**Umsetzung:** c263f41

_Story Points: 13_"

story "Sprint 3" "user-story,database,Sprint 3,prio: high" "$DB" open \
"UserStory #42 – Als Spieler möchte ich ein Konto oder Gastkonto haben, das in der Datenbank gespeichert wird, damit mein Fortschritt erhalten bleibt" \
"**Akzeptanzkriterien**

- Tabellen für Benutzer und Gastkonten sind entworfen (ER-Diagramm in docs/)
- Passwörter werden nur gehasht gespeichert
- Ein Gastkonto kann später in ein richtiges Konto umgewandelt werden
- EF-Migration ist erstellt und läuft lokal und auf dem Server

_Story Points: 5_"

story "Sprint 3" "user-story,database,Sprint 3,prio: high" "$DB" open \
"UserStory #43 – Als Spieler möchte ich meine Decks auf dem Server speichern, damit ich sie auf jedem Gerät verwenden kann" \
"**Akzeptanzkriterien**

- Tabellen für Decks und Deckkarten mit Bezug zum Benutzer
- API-Endpoints zum Anlegen, Lesen, Ändern und Löschen von Decks
- Ein Benutzer sieht und ändert nur seine eigenen Decks
- Bestehende lokale Decks können übernommen werden

_Story Points: 5_"

# ===========================================================================
# PRODUCT BACKLOG
# ===========================================================================
echo "== Backlog =="

# --- Frontend ---
story "Backlog" "user-story,frontend,Backlog,prio: high" "$FE" open \
"UserStory #44 – Als Spieler möchte ich im Chat und Log Namen statt technischer IDs sehen und nach Chat oder Aktionen filtern können, damit ich den Spielverlauf verstehe" \
"**Akzeptanzkriterien**

- Chat, Log und Choice-Sheet zeigen den Anzeigenamen statt der Verbindungs-ID
- Gemeinsame Timeline mit Filter: Alle, Chat, Aktionen
- Neue Einträge scrollen automatisch ins Bild
- Senden einer Chat-Nachricht blockiert keine Spielaktionen

_Story Points: 3_"

story "Backlog" "user-story,frontend,Backlog,prio: high" "$FE" open \
"UserStory #45 – Als Spieler möchte ich, dass der Deckbuilder auch am Handy flüssig läuft, damit ich unterwegs Decks bauen kann" \
"**Akzeptanzkriterien**

- Kartendaten werden einmal geladen und zwischengespeichert (gemeinsamer Karten-Cache)
- Fehlschlagen eines Sets blendet nicht alle Karten aus
- Lange Kartenlisten werden virtualisiert oder seitenweise geladen
- Alt-Arts bleiben erhalten
- Große Bilder (Board, Startscreen) liegen als WebP vor

_Story Points: 5_"

story "Backlog" "user-story,frontend,Backlog,prio: medium" "$FE" open \
"UserStory #46 – Als Spieler möchte ich mein Deck als OPTCGSim-Importstring exportieren, damit ich es in anderen Programmen nutzen kann" \
"**Akzeptanzkriterien**

- Export erzeugt einen OPTCGSim-kompatiblen Text
- Text kann kopiert oder geteilt werden
- Import eines solchen Textes legt ein Deck an (optional)

_Story Points: 3_"

story "Backlog" "user-story,frontend,Backlog,prio: medium" "$FE" open \
"UserStory #47 – Als Spieler möchte ich nach einem kurzen Verbindungsabbruch ins Match zurückkehren, damit ein App-Wechsel am Handy das Spiel nicht beendet" \
"**Akzeptanzkriterien**

- Bei einem Abbruch zeigt die Oberfläche einen Hinweis mit Countdown
- Nach dem Wiederverbinden wird der Spielstand neu geladen
- Abhängig von #24 (Rejoin im Backend)

_Story Points: 3_"

story "Backlog" "user-story,frontend,Backlog,prio: medium" "$FE" open \
"UserStory #48 – Als Spieler möchte ich mich registrieren und anmelden, damit meine Decks und mein Fortschritt gespeichert werden" \
"**Akzeptanzkriterien**

- Formulare für Registrierung und Login mit Fehlermeldungen
- Nach dem Login werden Decks vom Server statt aus dem Browser geladen
- Gast-Spielen bleibt möglich
- Abhängig von UserStory 52 und UserStory 43

_Story Points: 5_"

story "Backlog" "user-story,frontend,Backlog,prio: medium" "$FE" open \
"UserStory #49 – Als Entwickler möchte ich automatisierte Frontend-Tests, damit Änderungen keine bestehenden Funktionen zerstören" \
"**Akzeptanzkriterien**

- Vitest ist eingerichtet und läuft in der CI
- Deck-Validierung, Mulligan- und Wahrscheinlichkeitslogik sind getestet
- Ein Playwright-Test spielt ein Match mit zwei Browsern an

_Story Points: 3_"

story "Backlog" "user-story,frontend,Backlog,prio: low" "$FE" open \
"UserStory #50 – Als Spieler möchte ich im Shop Cosmetics kaufen und ausrüsten, damit ich meine Berries einsetzen kann" \
"**Akzeptanzkriterien**

- Shop-Seite zeigt Katalog, Preise und den eigenen Berry-Stand
- Kauf und Ausrüsten mit Bestätigung
- Ausgerüstete Cosmetics sind im Match sichtbar (z. B. Kartenhülle, Hintergrund)
- Abhängig von UserStory 54

_Story Points: 5_"

story "Backlog" "user-story,frontend,Backlog,prio: low" "$FE" open \
"UserStory #51 – Als Spieler möchte ich einen Einladungslink teilen und die App installieren können, damit Freunde leicht beitreten können" \
"**Akzeptanzkriterien**

- Jede Seite hat eine eigene Adresse, der Zurück-Button funktioniert
- Ein Link mit Raumcode öffnet direkt den Beitritt
- Web-App-Manifest und Icon, App ist auf dem Handy installierbar

_Story Points: 3_"

# --- Backend ---
story "Backlog" "user-story,backend,Backlog,prio: high" "$BE" open \
"UserStory #52 – Als Spieler möchte ich mich mit einem Konto oder als Gast anmelden, damit der Server mich wiedererkennt" \
"**Akzeptanzkriterien**

- Registrierung und Login (z. B. ASP.NET Identity oder JWT)
- Gast-Login ohne Passwort
- Geschützte Endpoints prüfen die Anmeldung
- Abhängig von UserStory 42

_Story Points: 8_"

story "Backlog" "user-story,backend,Backlog,prio: medium" "$BE" open \
"UserStory #53 – Als Spieler möchte ich nach einem Match Berries erhalten, damit sich Spielen lohnt" \
"**Akzeptanzkriterien**

- Nach Spielende vergibt der Server Berries für Sieg und Niederlage
- Keine Belohnung für zu kurze Matches (Mindestdauer bzw. Mindestzüge)
- Der neue Berry-Stand wird gespeichert und an den Client geschickt

_Story Points: 5_"

story "Backlog" "user-story,backend,Backlog,prio: low" "$BE" open \
"UserStory #54 – Als Spieler möchte ich Cosmetics kaufen und ausrüsten, damit ich mein Profil und Spielbrett gestalten kann" \
"**Akzeptanzkriterien**

- Shop-Katalog wird vom Server geliefert
- Kauf prüft und bucht den Berry-Stand serverseitig ab
- Besitz und ausgerüstete Cosmetics werden gespeichert
- Abhängig von UserStory 55

_Story Points: 8_"

# --- Datenbank ---
story "Backlog" "user-story,database,Backlog,prio: medium" "$DB" open \
"UserStory #55 – Als Spieler möchte ich, dass Berries und Cosmetics in der Datenbank gespeichert werden, damit mein Fortschritt erhalten bleibt" \
"**Akzeptanzkriterien**

- Tabellen für Wallet, Cosmetics-Katalog, Besitz und Loadout
- Buchungen von Berries sind nachvollziehbar
- EF-Migration ist erstellt

_Story Points: 5_"

story "Backlog" "user-story,database,Backlog,prio: medium" "$DB" open \
"UserStory #56 – Als Spieler möchte ich alle Kartenversionen inklusive Alt-Arts sehen, damit ich meine Lieblingsversion spielen kann" \
"**Akzeptanzkriterien**

- Eindeutiger Schlüssel aus Karten-ID und Bild-ID, Alt-Arts werden nicht übersprungen
- Kartenimport läuft als Start- oder Admin-Job statt bei einer normalen Anfrage
- Doppelte Karten innerhalb eines Imports werden erkannt

_Story Points: 3_"

story "Backlog" "user-story,database,Backlog,prio: low" "$DB" open \
"UserStory #57 – Als Spieler möchte ich meine Match-Ergebnisse gespeichert haben, damit ich meine Statistik sehen kann" \
"**Akzeptanzkriterien**

- Tabelle für Match-Ergebnisse (Spieler, Gewinner, Dauer, Züge, Datum)
- Ein Endpoint liefert die Statistik eines Spielers
- Grundlage für die Berry-Vergabe in UserStory 53

_Story Points: 3_"

# ===========================================================================
# Sprint-Übersichten
# ===========================================================================
echo "== Sprint-Übersichten =="

join_lines() { local IFS=$'\n'; echo "$*"; }

story "Sprint 1" "sprint-overview,Sprint 1" "$BE" closed \
"Sprint 1 – Fundament und Vertical Slice (17.04.2026 – 10.05.2026)" \
"**Sprintziel:** Projektgrundlage schaffen: Repository, Workflow, CI/CD, Karten-API und ein erster spielbarer Ablauf (Raum beitreten, Match starten, Mulligan, Zug beenden, Chat und Log).

**Ergebnis:** Sprintziel erreicht. Zwei Spieler können einem Raum beitreten, ein Match starten und Züge beenden. Karten können von der OPTCG-API abgerufen werden. Jeder Push wird automatisch gebaut und getestet.

**Backlog-Einträge**

$(join_lines "${SPRINT1[@]}")"

story "Sprint 2" "sprint-overview,Sprint 2" "$BE" closed \
"Sprint 2 – Deckbuilder, Tools, Match-Oberfläche und Datenbank (11.05.2026 – 07.06.2026)" \
"**Sprintziel:** Spieler können Decks bauen, speichern und im Match verwenden. Die Tools (Mulligan-Trainer, Wahrscheinlichkeitsrechner) sind nutzbar. Kartendaten liegen in PostgreSQL.

**Ergebnis:** Sprintziel erreicht. Deckbuilder mit Filtern und Bibliothek, beide Tools, mobiles Vollbild-Spielbrett und Startbildschirm sind fertig. Karten werden aus der Datenbank geladen, die Spiellogik wurde in Action-Klassen ausgelagert.

**Backlog-Einträge**

$(join_lines "${SPRINT2[@]}")"

story "Sprint 3" "sprint-overview,Sprint 3" "$BE" open \
"Sprint 3 – Server-Browser, echte Spielregeln und Persistenz (ab 06.10.2026)" \
"**Sprintziel:** Ein vollständiges Match nach echten OPTCG-Regeln spielen, Räume über eine Server-Liste finden und die Grundlage für Konten und gespeicherte Decks in der Datenbank legen.

**Stand:** Server-Liste und echte Spielregeln mit Kartenbildern sind fertig. Offen sind die vollständige Validierung (#22), Reconnect (#24), der Produktionstest (#27) sowie die Datenbank-Tabellen für Konten und Decks.

**Backlog-Einträge**

$(join_lines "${SPRINT3[@]}")"

# ===========================================================================
# Optional: alles in ein GitHub Project eintragen
# ===========================================================================
if [ -n "$PROJECT_NUMBER" ]; then
  echo "== Project $PROJECT_NUMBER =="
  for url in "${ALL_URLS[@]}"; do
    run gh project item-add "$PROJECT_NUMBER" --owner "$OWNER" --url "$url" >/dev/null
  done
fi

echo
echo "Fertig. Sprint 1: ${#SPRINT1[@]}, Sprint 2: ${#SPRINT2[@]}, Sprint 3: ${#SPRINT3[@]}, Backlog: ${#BACKLOG[@]} Einträge."
