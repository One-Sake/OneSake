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

## Hinweis zur Gast-ID

Match-Ergebnisse werden einer Gast-ID zugeordnet, die der Browser selbst erzeugt
(`localStorage`, Schlüssel `onesake.guestId`). Das ist **keine** sichere Anmeldung.
Ein echter Login folgt später.
