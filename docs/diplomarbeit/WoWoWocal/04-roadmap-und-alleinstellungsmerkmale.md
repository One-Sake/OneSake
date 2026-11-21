# Roadmap und Alleinstellungsmerkmale

Stand: 2026-11-21. Gespeichert als Nachschlagewerk für die weitere Planung.

Voraussetzung: Die vier Logik-Prompts auf Branch `logic` sind umgesetzt.
1. Ereignis-System und Effekt-Warteschlange
2. Determinismus und Aufzeichnung, dazu „Fehler melden“
3. Bot-Simulation als Dauertest
4. Karten nach Meta abarbeiten

## Teil A: Was noch fehlt, damit sich das Spielen gut anfühlt

Das haben die anderen Simulatoren größtenteils, ohne fühlt sich OneSake unfertig an:

| Thema | Warum | Aufwand |
|-------|-------|---------|
| Reconnect (60–90 s Schonfrist) | Ein App-Wechsel am Handy beendet heute das Match. Mit Ranked ist das das größte Ärgernis. | mittel |
| Zug-Timer und Zeitbank | Sonst kann jemand im Ranked endlos warten. | klein |
| Konten (E-Mail oder Discord-Login) | Fälschungssichere Rangliste, Decks auf jedem Gerät, Freunde | mittel |
| Decks auf dem Server | Heute liegen sie nur im Browser. | klein, sobald es Konten gibt |
| Spiel-Gefühl | Angriffs-Animation, K.O.-Effekt, Schadensanzeige, Sounds, Tastenkürzel (Leertaste = Zug beenden), „Auto-Pass“, wenn man nichts tun kann | mittel |
| Tutorial | Geführtes erstes Spiel gegen einen Bot. Senkt die Einstiegshürde enorm. | mittel |
| KI-Gegner zum Üben | Die Bots aus Prompt 3 gibt es dann schon. Es fehlen ein besserer Bot und Schwierigkeitsgrade. | mittel |
| Replay-Viewer | Die Aufzeichnung aus Prompt 2 gibt es dann, es fehlt die Ansicht: Zeitleiste, vor/zurück, teilbarer Link | mittel |
| Zuschauen und Freunde einladen | Einladungslink, Zuschauer-Modus ohne Handkarten | mittel |
| Handy als App (PWA) | Installierbar, Vollbild, kein Browser-Rahmen | klein |
| Deutsch und Englisch | Breitere Zielgruppe | klein bis mittel |

## Teil B: Was OneSake von allen anderen abheben würde

Das sind Ideen, die genau auf dem aufbauen, was gerade gebaut wird. Für die anderen
Simulatoren wäre das viel schwerer nachzumachen.

### 1. 🧪 Deck-Labor: „Wie gut ist mein Deck gegen die Meta?“

- Du baust ein Deck, und die Bots spielen es auf dem Server ein paar hundert Mal gegen die
  aktuellen Top-Meta-Decks.
- **Ergebnis:** geschätzte Winrate pro Matchup, als 1. und als 2. Spieler, im gleichen Stil wie
  der Meta-Reiter. Dazu z. B.: „Gegen Luffy & Ace verlierst du meistens in Zug 6–7.“
- **Warum einzigartig:** Kein anderer Simulator verbindet Meta-Daten, Deckbuilder und
  automatische Simulation. Die Bausteine gibt es nach Prompt 3 und 4 fast alle.
- **Einschränkung:** Bots spielen schlechter als Menschen. Ehrlich als „Richtwert“ kennzeichnen.

### 2. 🎓 Persönlicher Coach nach jedem Match

- **Verpasster Lethal:** Nach dem Spiel rechnet die Engine an jedem Zug nach, ob es einen Weg
  zum Sieg gab. Sie probiert alle Kombinationen durch, was bei deterministischer Engine geht.
  Dann heißt es z. B.: „In Zug 6 hättest du gewinnen können: DON!! auf Zoro, dann …“
- **Weitere Hinweise:** Counter verschwendet, schlecht geblockt, DON!! nicht genutzt.
- **Verknüpfung mit den Tools:** „Deine Starthand war schlecht. Übe das im Mulligan-Trainer.“
  Die Tools gibt es nur bei OneSake.
- SeaKing hat etwas Ähnliches für Solo-Spiele, aber nicht für echte PvP-Partien mit Rangliste.

### 3. ⏪ „Was wäre wenn?“: Replay mit Abzweigung

- Im Replay an jeder Stelle auf „Ab hier selbst spielen“ klicken und eine andere Entscheidung
  ausprobieren, gegen den Bot oder einen Freund.
- **Warum einzigartig:** Das geht nur mit voll deterministischer Engine (Prompt 2). Das hat
  meines Wissens kein anderer OPTCG-Simulator.

### 4. 🧩 Tägliches Lethal-Puzzle

- Jeden Tag eine echte Spielsituation aus Replays mit der Aufgabe „Gewinne in diesem Zug“.
  Die Engine prüft automatisch, ob es mindestens eine Lösung gibt.
- Bestenliste und Serie („7 Tage in Folge gelöst“), Teilen als Bild wie bei Wordle.
- Bringt Spieler **täglich** zurück, auch wenn sie keine Zeit für ein ganzes Match haben.

### 5. 🏴‍☠️ One-Piece-Gefühl statt nur Kartentisch

- **Ränge mit Titeln:** Rookie → Supernova → Shichibukai → Yonko, verbunden mit dem Kopfgeld.
- **Banden (Crews):** Freunde gründen eine Piratenbande. Gesamt-Kopfgeld der Bande,
  wöchentliche Bandenkriege und ein eigener Banden-Chat. Gemeinschaft hält Spieler länger als
  jede Funktion.
- **Steckbrief als teilbares Bild** für Instagram oder WhatsApp. Kostenlose Werbung.
- **Wichtig:** keine offiziellen Bilder oder Logos, aber Begriffe und Stil passen zum Thema.

### 6. 🎥 Streamer- und Turnier-Modus

- **Zuschauer-Ansicht für Caster:** beide Hände sichtbar, mit Verzögerung gegen Schummeln,
  und ein OBS-Overlay mit Namen, Leadern, Life und Kopfgeld.
- **Eigene Turniere** im Swiss-System mit automatischen Paarungen, oder über Limitless mit
  Webhook.
- Content-Creator wie Card Kaizoku brauchen genau so etwas. Das wäre auch eine gute Antwort
  auf die Anfrage an ihn.

## Empfohlene Reihenfolge

1. **Reconnect, Zug-Timer und Spiel-Gefühl** (Animationen, Sounds): Ohne das spielt niemand lange.
2. **Tutorial, KI-Gegner zum Üben und PWA:** Neue Spieler bleiben.
3. **Replay-Viewer mit „Was wäre wenn?“:** Der erste Alleinstellungs-Effekt, die Basis gibt es nach Prompt 2.
4. **Coach mit „verpasster Lethal“ und Lethal-Puzzle:** Täglicher Grund, wiederzukommen.
5. **Deck-Labor:** Das Highlight, das OneSake klar von allen abhebt.
6. **Konten, danach Banden und Titel:** Gemeinschaft und faire Rangliste.
7. **Streamer- und Turnier-Modus:** Wenn die Community wächst.

## Für die Diplomarbeit

Deck-Labor, Coach und „Was wäre wenn?“ zeigen alle drei, was technisch hinter OneSake steckt
(deterministische Engine, Simulation, Datenanalyse). Das lässt sich in einer Präsentation sehr
gut vorführen.
