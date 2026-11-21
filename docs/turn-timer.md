# Zug-Timer und Reconnect

Der Server ist maßgeblich: `MatchRoom` speichert die Fristen und Zeitbanken, `MatchDeadlineService`
prüft jede Sekunde alle Räume (`MatchRoom.Tick`). Die Uhr kommt über `IClock` (in Tests eine
`FakeClock`). Der Client zeigt nur an.

## Werte (Standard, konfigurierbar unter `Match:Timer`)

| Wert | Standard | Schlüssel |
|------|----------|-----------|
| Zugzeit pro eigenem Zug | 90 s | `Match:Timer:TurnSeconds` |
| Antwortzeit (Block, Counter, Trigger, Effekt-Entscheidung im fremden Zug, ORDER_TRIGGERS) | 25 s | `Match:Timer:ResponseSeconds` |
| Mulligan (beide gleichzeitig, ohne Zeitbank) | 30 s | `Match:Timer:MulliganSeconds` |
| Würfelentscheidung Erster/Zweiter (ohne Zeitbank) | 20 s | `Match:Timer:TurnOrderSeconds` |
| Zeitbank zu Beginn | 180 s | `Match:Timer:TimeBankStartSeconds` |
| Bonus nach jedem eigenen Zug | +10 s | `Match:Timer:TimeBankBonusSeconds` |
| Maximum der Zeitbank | 240 s | `Match:Timer:TimeBankMaxSeconds` |
| Toleranz für Lag/Animationen | 2 s | `Match:Timer:ToleranceSeconds` |

Modi: **Standard**, **Entspannt** (alle Zeiten doppelt), **Aus**. Quick Match (Ranked) hat immer
Standard, private Tische wählen beim Erstellen.

## Wessen Uhr läuft?

Es läuft immer genau eine Uhr („Segment“):

1. Würfelphase: der Würfelgewinner (`TURN_ORDER`).
2. Mulligan: beide Spieler gemeinsam (`MULLIGAN`).
3. Hat der Nicht-Zugspieler eine offene Entscheidung: seine Antwortzeit (`RESPONSE`). Die Zugzeit des
   Zugspielers steht so lange.
4. Sonst der Zugspieler (`TURN`). Die 90 s gelten für den ganzen Zug, nicht pro Aktion.

Erst wird die normale Zeit verbraucht, dann die Zeitbank. Während ein Spieler getrennt ist, steht die
Uhr komplett (für beide); danach läuft sie mit der Restzeit weiter.

## Automatische Entscheidungen bei Ablauf (feste Reihenfolge)

| Entscheidung | Automatisch |
|--------------|-------------|
| Würfelentscheidung | `GO_FIRST` (selbst beginnen) |
| Mulligan | `KEEP` |
| Block | `NO_BLOCK` |
| Counter | `PASS` |
| [Trigger] | `DECLINE` (Karte auf die Hand) |
| Manueller Effekt | `DONE` |
| Reihenfolge gleichzeitiger Effekte | in der angebotenen Reihenfolge |
| Effekt-Auswahl | `NO` → `DONE` → erste wählbare Karte (`SELECT`, in der angebotenen Reihenfolge) → `YES` → übrige Optionen der Reihe nach; die erste gültige Antwort gilt |
| Eigener Zug | alle offenen eigenen Entscheidungen wie oben, dann `END_TURN` |

Bei einer Antwortzeit wird nur die abgelaufene Entscheidung automatisch getroffen; die nächste
Entscheidung bekommt wieder ihre eigene Antwortzeit (Zeitbank ist dann leer).

**Zwei eigene Züge in Folge ohne Zeit** (Zugzeit und Zeitbank aufgebraucht) → Niederlage mit
Endgrund `TIMEOUT` (zählt im Ranked). Beendet der Spieler einen Zug selbst, beginnt die Zählung neu.

Log: `⏱ Zeit abgelaufen – <Name>: automatisch …`. In der Aufzeichnung steht ein Input
`TIMEOUT` mit `{ "kind": "TURN" | "RESPONSE" | "MULLIGAN" | "TURN_ORDER" }`. Das Replay ruft dafür
`MatchRoom.ApplyTimeout` auf – derselbe Code wie live, nur ohne Uhr (Replays laufen mit Timer „Aus“).

## Reconnect

| Wert | Standard | Schlüssel |
|------|----------|-----------|
| Schonfrist im laufenden Match | 90 s | `Match:ReconnectGraceSeconds` |
| Gesamtbudget pro Spieler und Match | 180 s | `Match:ReconnectBudgetSeconds` |
| Schonfrist in der Lobby | 30 s | `Match:LobbyReconnectGraceSeconds` |

- Jeder Platz hat eine stabile Spieler-ID (`p1-…`) und ein geheimes Rejoin-Token (nur an diesen
  Spieler, Nachricht `SeatAssigned`).
- Verbindung weg: Platz bleibt reserviert, beide sehen `disconnectedUntilUnixMs`. Rest-Budget < 1 s
  → sofortige Niederlage.
- `RejoinRoom(roomCode, playerId, token)`: Token-Vergleich in konstanter Zeit, neue Verbindung wird
  dem Platz zugeordnet (auch wenn die alte noch nicht als getrennt erkannt war). Danach `Resync` mit
  offenen Entscheidungen, Log und Chat.
- Frist abgelaufen: Niederlage `DISCONNECT`. Ein später Rejoin bekommt nur noch den Endstand.
- Aufzeichnung: `DISCONNECT`/`RECONNECT` sind Meta-Ereignisse (ändern nichts), `DISCONNECT_TIMEOUT`
  beendet das Match.
- Concede/Leave bleiben sofortig, die Quick-Match-Warteschlange ist unverändert.
