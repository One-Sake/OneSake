# Deployment: Datenbank-Migrationen

Das Backend führt beim Start **keine** Migrationen automatisch aus (kein `Database.Migrate()`).
Neue Migrationen müssen einmalig von Hand auf die Datenbank angewendet werden, bevor
bzw. direkt nachdem die neue Backend-Version läuft.

Fehlt eine Migration, läuft das Spiel trotzdem weiter. Nur das Speichern von
Match-Ergebnissen schlägt fehl (Warnung im Log), und `/stats/...` antwortet mit `503`.

## Voraussetzungen

- .NET 8 SDK und das EF-Tool: `dotnet tool install --global dotnet-ef --version 8.*`
- Connection String der Ziel-Datenbank (wie `ConnectionStrings:DefaultConnection`)

## Variante A: direkt mit `dotnet ef` (empfohlen)

Im Ordner `backend/` ausführen. Der Connection String kommt als Umgebungsvariable,
damit er nicht in einer Datei landen muss:

```bash
cd backend
ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=onesake;Username=<user>;Password=<pw>" \
  dotnet ef database update --project OneSake.Persistence --startup-project OneSake.Server
```

PowerShell:

```powershell
cd backend
$env:ConnectionStrings__DefaultConnection = "Host=<host>;Port=5432;Database=onesake;Username=<user>;Password=<pw>"
dotnet ef database update --project OneSake.Persistence --startup-project OneSake.Server
```

`database update` wendet nur Migrationen an, die noch fehlen. Mehrfaches Ausführen ist unkritisch.

## Variante B: SQL-Skript (wenn auf dem Server kein .NET SDK installiert ist)

Lokal ein idempotentes Skript erzeugen und auf dem Server mit `psql` ausführen:

```bash
cd backend
dotnet ef migrations script --idempotent --project OneSake.Persistence --startup-project OneSake.Server -o migrate.sql
psql "postgresql://<user>:<pw>@<host>:5432/onesake" -f migrate.sql
```

Das Skript prüft selbst über `__EFMigrationsHistory`, welche Migrationen schon angewendet sind.

## Lokal (docker-compose)

```bash
docker compose up -d postgres
cd backend
dotnet ef database update --project OneSake.Persistence --startup-project OneSake.Server
```

## Migrationen

| Migration | Inhalt |
| --- | --- |
| `InitialCardDatabase` | Tabelle `Cards` |
| `AddMatchResults` | Tabelle `MatchResults` (Ergebnisse für die Profil-Statistik), Indizes auf `WinnerGuestId` und `LoserGuestId` |
| `AddRankingAndTurnOrder` | Neue Spalten in `MatchResults` (`IsRanked`, `FirstPlayerGuestId`, `WinnerWentFirst`, Rating vorher/nachher für Gewinner und Verlierer), Index auf `EndedAtUtc`, neue Tabelle `PlayerRatings` (ELO/Kopfgeld pro Gast-ID) |
| `AddMatchRecordings` | Tabellen `MatchRecordings` (Aufzeichnung jedes Matches als gzip-JSON, Bezug auf `MatchResults`, Index auf `CreatedAtUtc`) und `BugReports` („Fehler melden“, Bezug auf die Aufzeichnung) |

## Match-Aufzeichnungen und Fehlermeldungen

Nach jedem Match speichert das Backend die Aufzeichnung (Seed, Decks, alle Entscheidungen) in
`MatchRecordings`. Ohne die Migration `AddMatchRecordings` läuft das Spiel normal weiter, nur das
Speichern schlägt fehl (Warnung im Log). Aufzeichnungen älter als 30 Tage löscht der Server
automatisch (`MatchRecordingRetentionService`, alle 6 Stunden), außer eine Fehlermeldung verweist darauf.

Die Admin-Liste `GET /bugreports` ist nur aktiv, wenn ein Token gesetzt ist (Header `X-Admin-Token`):

```bash
BugReports__AdminToken=<langer zufälliger Wert>
```

Ablauf Meldung → Test: siehe [bug-reports.md](bug-reports.md).

## Meta-Daten (externer Anbieter)

Der Meta-Reiter zeigt **ausschließlich** Daten eines externen Anbieters (zuerst Card Kaizoku,
tägliche JSON-Snapshots unter `https://cdn.cardkaizoku.com/stats/`). Es gibt keine eigene
Meta-Auswertung aus OneSake-Matches. Das Backend lädt die Daten (der Browser ruft den
Anbieter nie direkt auf), cached sie 6 Stunden und liefert bei Ausfall den letzten Snapshot
mit `isStale` aus.

Meta ist in **Production aktiv** (`appsettings.Production.json`: `"Meta": { "Enabled": true }`).
`appsettings.json` (Standard) lässt es aus, `appsettings.Development.json` schaltet es lokal ein.

> **Hinweis:** Card Kaizoku hat für den Feed keine Nutzungsbedingungen oder Lizenz veröffentlicht.
> Die Nutzung ist nicht offiziell freigegeben. Bittet der Anbieter darum, Meta sofort wieder abschalten.

Ist der Anbieter nicht erreichbar oder der Snapshot ungültig, zeigt der Meta-Reiter einen
Fehler bzw. den letzten Snapshot mit `isStale`. Die Bounty-Rangliste funktioniert immer.

Abschalten oder Format ändern ohne neuen Build (Umgebungsvariable auf dem Server, dann Backend neu starten):

```bash
Meta__Enabled=false
# optional: Meta__Format=eb05_lw_p_top20  (Teil des Dateinamens stats_<Format>_<YYYYMMDD>.json)
```

Wenn ein neues Set erscheint, ändert sich der Format-Teil im Dateinamen. Liefert der Meta-Reiter
„kein Snapshot gefunden“, im Log nach `No meta snapshot found` suchen und `Meta:Format` anpassen.

Quellenangabe: Unter jeder Meta-Ansicht steht „Daten: Card Kaizoku · Snapshot <Datum>“.

## Würfel-Test-Hook (nur Development)

`Match:FixedDiceRolls` gibt die Würfe fest vor, z. B. `[3, 3, 5, 2]` (erst Gleichstand, dann 5:2).
Wird außerhalb von Development ignoriert.

```bash
Match__FixedDiceRolls__0=3 Match__FixedDiceRolls__1=3 Match__FixedDiceRolls__2=5 Match__FixedDiceRolls__3=2
```

## Hinweis zur Gast-ID

Match-Ergebnisse werden einer Gast-ID zugeordnet, die der Browser selbst erzeugt
(`localStorage`, Schlüssel `onesake.guestId`). Das ist **keine** sichere Anmeldung.
Ein echter Login folgt später.
