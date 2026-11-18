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

## Meta-Daten (externer Anbieter)

Der Meta-Reiter zeigt **ausschließlich** Daten eines externen Anbieters (zuerst Card Kaizoku,
tägliche JSON-Snapshots unter `https://cdn.cardkaizoku.com/stats/`). Es gibt keine eigene
Meta-Auswertung aus OneSake-Matches. Das Backend lädt die Daten (der Browser ruft den
Anbieter nie direkt auf), cached sie 6 Stunden und liefert bei Ausfall den letzten Snapshot
mit `isStale` aus.

> **Meta-Daten erst aktivieren, wenn die schriftliche Erlaubnis von Card Kaizoku vorliegt.**

Die Nutzung dieser Daten ist nicht offiziell dokumentiert. Darum ist Meta in
`appsettings.json` und `appsettings.Production.json` **deaktiviert** (`"Meta": { "Enabled": false }`).
Nur `appsettings.Development.json` schaltet es für die lokale Entwicklung ein.
Solange Meta aus ist, antworten alle `/meta/...`-Endpunkte mit `200` und `enabled: false`,
das Frontend zeigt „Meta-Daten sind noch nicht verfügbar.“ (die Bounty-Rangliste funktioniert trotzdem).

Aktivieren nach Erlaubnis (z. B. als Umgebungsvariable auf dem Server):

```bash
Meta__Enabled=true
# optional: Meta__Format=eb05_lw_p_top20  (Teil des Dateinamens stats_<Format>_<YYYYMMDD>.json)
```

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
