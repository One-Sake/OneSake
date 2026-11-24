# Ideen, Entscheidungen und Überlegungen – dano-4

Stand: 2026-11-24.

## 1. Ideenliste mit Status

### 1.1 Umgesetzt

| Idee | Seit | Bemerkung |
|------|------|-----------|
| Manuellen Modus entfernen (Fair Play) | 24.11. | Client und Server; nicht automatisierte Effekte werden übersprungen und geloggt |

### 1.2 Offen

| Idee | Bemerkung |
|------|-----------|
| Bausteine für Karteneffekte ausbauen, jeder mit eigenen Tests | Plan A11 (`docs/spiellogik-plan.md`), etwa 50–80 Bausteine |
| Alle Kartenskripte prüfen und mit Tests auf `verified` bringen | in Wellen nach Plan Phase 4 |
| Screenshot vom Match ohne manuellen Modus | braucht lokal Postgres mit Kartendaten |

### 1.3 Verworfen

| Idee | Warum verworfen |
|------|-----------------|
| Manuellen Modus nur in Ranked/Quick Match abschalten (Plan 5.3) | auch an privaten Tischen kann geschummelt werden; das Team will ihn ganz weg |
| Nur das Menü im Client entfernen | die Hub-Methode `ManualAction` wäre weiter direkt aufrufbar |
| Effekte ohne Nachfrage ausführen („You may“ immer ja) | widerspricht dem Regelwerk; Entscheidungen auf der Karte bleiben beim Spieler |

## 2. Entscheidungen

| Entscheidung | Begründung | Alternative |
|--------------|-----------|-------------|
| Nicht automatisierter Effekttext wird übersprungen (Log `EFFECT_NOT_AUTOMATED`) | ohne manuellen Modus gibt es keinen fairen Weg, ihn auszuführen; der Log macht es für beide sichtbar | Karte ganz sperren (kommt später mit Plan 5.2) |
| „You may“, [Activate: Main], Ziele und Ja/Nein bei Kosten bleiben Spielerentscheidungen | so steht es im Regelwerk | alles automatisch ausführen |
| Zustands-Hash behält die leeren Felder `view=`/`viewed:` | alte Bug-Replays behalten ihre Hashes | Hash-Format ändern und Replays neu aufnehmen |
| Frontend- und Server-Teil in einem Commit auf `logic` | hängen über den Prompt `MANUAL_EFFECT` zusammen | Frontend auf `dev`, Server auf `logic` getrennt |

## 3. Abweichungen vom Projektauftrag und vom Teamplan

- `docs/spiellogik-plan.md` sah das Entfernen des manuellen Modus erst nach Welle 4 vor (5.3/5.4).
  Auf Wunsch des Teams wurde es vorgezogen, weil der Modus eine Schummel-Lücke war. Folge: Karten
  mit Status `manual` verlieren bis zu ihrer Umsetzung den nicht automatisierten Effektteil.
