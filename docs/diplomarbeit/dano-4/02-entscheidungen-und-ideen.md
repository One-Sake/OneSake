# Ideen, Entscheidungen und Überlegungen – dano-4

Stand: 2026-11-28.

## 1. Ideenliste mit Status

### 1.1 Umgesetzt

| Idee | Seit | Bemerkung |
|------|------|-----------|
| Manuellen Modus entfernen (Fair Play) | 24.11. | Client und Server; nicht automatisierte Effekte werden übersprungen und geloggt |
| Baustein-Katalog mit einem Test pro Baustein (Plan A11) | 24.11. | 153 Bausteine, alle getestet, `docs/effect-building-blocks.md` |

### 1.2 Offen

| Idee | Bemerkung |
|------|-----------|
| Neue Bausteine für die häufigsten fehlenden Muster (z. B. „Give this Leader or 1 of your Characters“, Look at mit Bedingungen, „Choose one“, „would be … instead“) | jeweils mit `[BlockTest]` und Katalogzeile |
| Lücken aus dem Katalog schließen: „top or bottom“ wählen lassen, „in any order“ fragen, offene Life-Karten | siehe `docs/effect-building-blocks.md`, Abschnitt Known gaps |
| Alle Kartenskripte prüfen und mit Tests auf `verified` bringen | in Wellen nach Plan Phase 4 |
| Screenshot vom Match ohne manuellen Modus | braucht lokal Postgres mit Kartendaten |
| Die 1.040 Karten mit Status `manual` automatisieren (Welle 1 zuerst, z. B. Starter-Leader ST01-001) | **hohe Priorität:** seit dem Entfernen des manuellen Modus (seit 28.11. auch auf `main`) tun diese Karten im Match nichts mehr, ihr Effekt wird nur als `EFFECT_NOT_AUTOMATED` geloggt. Stand 28.11.: 13 `verified`, 1.443 `generated`, 1.040 `manual` |
| GitHub-Issues für US90 und US91 anlegen und in `docs/sprints/sprint-6.md` verlinken | lokal ist die GitHub-CLI `gh` nicht installiert |

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
| Baustein-Liste wird aus den Enums und `CardFilter` erzeugt | ein neuer Baustein kann nicht ohne Test bleiben | Liste von Hand pflegen |
| Baustein-Tests benutzen echte Kartentexte und den Ablauf wie im Match (`BlockGame`) | prüft Parser und Engine zusammen, wie die Karte später läuft | Engine-Tests mit gebautem JSON |
| Unbenutzte Enum-Werte (`CardZone.Deck`, `CardZone.Life`) sind keine Bausteine | sie haben keine Wirkung; ein Test stellt sicher, dass kein Skript sie nutzt | sie im Modell löschen (wäre ein Umbau ohne Nutzen) |
| Katalog-Dokument auf Englisch wie die übrigen technischen Docs | passt zu `docs/card-scripts.md` | Deutsch |

## 3. Abweichungen vom Projektauftrag und vom Teamplan

- Plan A11 rechnete mit etwa 50–80 Bausteinen. Es sind 153, weil jede Bedingung, jedes Flag und
  jeder Filter einzeln zählt (Wunsch: jedes Wort mit Wirkung einzeln testbar).
- A11 ist vor dem Effektmodell v2 (Phase 2) umgesetzt, für das heutige Modell. Die Bausteine von v2
  kommen später dazu.
- `docs/spiellogik-plan.md` sah das Entfernen des manuellen Modus erst nach Welle 4 vor (5.3/5.4).
  Auf Wunsch des Teams wurde es vorgezogen, weil der Modus eine Schummel-Lücke war. Folge: Karten
  mit Status `manual` verlieren bis zu ihrer Umsetzung den nicht automatisierten Effektteil.
