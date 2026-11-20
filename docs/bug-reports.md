# Fehlermeldungen: von der Meldung zum Test

Jedes Match ist reproduzierbar: Der Server erzeugt pro Match einen geheimen Seed, aus dem jeder
Zufall kommt (Mischen, Würfel), und zeichnet jede Entscheidung der Spieler auf. Ein Match lässt
sich damit Schritt für Schritt nachspielen.

## Ablauf

1. **Meldung:** Ein Spieler klickt im Match (Aktionsleiste) oder auf dem Spielende-Screen auf
   „🐞 Fehler melden“. Er beschreibt den Fehler, wählt eine Kategorie und optional die betroffene
   Karte. Der Server speichert die Meldung in `BugReports`, dazu die Aufzeichnung des Matches bis zu
   diesem Moment in `MatchRecordings`. Der Spieler sieht „Danke! Meldung #123 gespeichert.“
2. **Sichten:** Die Liste der Meldungen gibt es unter `GET /bugreports` mit dem Header
   `X-Admin-Token` (Wert aus `BugReports:AdminToken`; ohne Konfiguration ist der Endpunkt aus):

   ```bash
   curl -H "X-Admin-Token: $TOKEN" https://<server>/bugreports
   ```

3. **Exportieren:** Die Meldung wird zur Testdatei `backend/OneSake.Tests/Replays/bug-123.json`.
   Namen und Gast-IDs entfernt der Export.

   ```bash
   dotnet run --project backend/OneSake.ReplayTool -- export --report 123 --connection "Host=...;Database=onesake;Username=...;Password=..."
   ```

4. **Reproduzieren:** Das Match bis zum gemeldeten Zug abspielen und Zustand und Log ansehen:

   ```bash
   dotnet run --project backend/OneSake.ReplayTool -- run backend/OneSake.Tests/Replays/bug-123.json --until 47 --print
   ```

   Weicht die Engine vom aufgezeichneten Match ab, bricht das Tool mit
   `Engine weicht ab bei seq 47: ...` ab. Nach jeder Eingabe wird ein Hash über den kompletten
   Zustand mit der Aufnahme verglichen.

5. **Fix:** Fehler im Code beheben.
6. **Erwartung festhalten:** Neben die Datei eine `bug-123.expect.json` legen, mit den Werten, die
   nach dem Fix an diesem Punkt stimmen müssen:

   ```json
   {
     "seq": 47,
     "note": "Zoro bekommt durch den Leader +1000",
     "checks": [
       { "instanceId": "s1:OP01-025:2", "field": "power", "expected": 6000 },
       { "instanceId": "s2:OP01-016:1", "field": "zone", "expected": "trash" },
       { "seat": 2, "field": "life", "expected": 3 }
     ]
   }
   ```

   Felder: Spieler (`seat`): `life`, `hand`, `deck`, `trash`, `activeDon`, `restedDon`.
   Karte (`instanceId`, siehe `run --print`): `zone` (`leader`, `stage`, `character`, `hand`,
   `trash`, `unknown` für Deck und Life), `power`, `rested`, `attachedDon`. Match: `phase`, `turn`,
   `winnerSeat`.
7. **Test grün:** `ReplayRegressionTests` spielt jede Datei in `Replays/` ab, prüft die Erwartung
   und dass sich der Rest des Matches noch abspielen lässt.

   ```bash
   dotnet test backend/OneSake.Tests/OneSake.Tests.csproj --filter ReplayRegressionTests
   ```

## Wichtig

- **Ein Fix ändert den Spielverlauf:** Die Aufnahme enthält nach jeder Eingabe den Hash des
  damaligen Zustands. Verändert der Fix den Zustand, stimmt der Hash ab dieser Stelle nicht mehr
  und der Test meldet `Engine weicht ab bei seq N`. Das ist gewollt. Danach die Datei bis zur
  gemeldeten Stelle kürzen (Eingaben nach `atSeq` und `finalStateHash` entfernen, `stateHash` der
  betroffenen Eingaben löschen) oder die Erwartung so wählen, dass sie vor der Abweichung liegt.
- **Datenschutz:** Meldungen und Aufzeichnungen bleiben auf unserem Server. Es werden keine
  GitHub-Issues erstellt und keine Daten an andere Dienste geschickt. Aufzeichnungen ohne Meldung
  werden nach 30 Tagen gelöscht.
- **Seed:** Der Seed erreicht nie einen Client. Er steht erst in der gespeicherten Aufzeichnung.
- **Rate-Limit:** 5 Meldungen pro Stunde und Gast-ID (ohne Gast-ID: pro IP).

## Determinismus

- `OneSake.Game/Randomness`: `IRandom` mit PCG32. Für jeden Zweck gibt es einen eigenen Strom aus
  dem Seed (`deck:1`, `deck:2`, `dice`), abgeleitet per SHA-256. Wer sein eigenes Deck kennt,
  erfährt dadurch nichts über das gegnerische Deck oder den Seed.
- Karten-IDs (`s1:OP01-016:3`) und Prompt-IDs (`c17`) sind Zähler, keine Guids.
- Ausgenommen sind nur Werte außerhalb der Spiellogik: Zeitstempel im Log, der Seed der
  Würfel-Animation, Raumcodes.
