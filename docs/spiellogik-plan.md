# Arbeitsplan: Vollautomatische, regelkonforme Spiellogik

Stand: 23.11.2026 · Branch: `logic` (siehe `AGENTS.md`) · Verantwortlich: WoWoWocal

**Ziel:** Jede Karte führt ihren Effekt automatisch und regelkonform aus. Niemand muss
„Effekt bitte manuell ausführen“ benutzen. Zwei Menschen können fehlerfrei gegeneinander spielen.
Die Simulation soll mit OPTCGSim und anderen Simulatoren mithalten können.

Dieses Dokument ist der Arbeitsplan. Jede Aufgabe hat eine Checkbox, die abgehakt wird, sobald sie
erledigt ist. Dabei kommt der Commit dazu und ein Eintrag ins Projekttagebuch.

---

## 1. Wann ist das Ziel erreicht? (Definition of Done)

„Einwandfrei“ muss messbar sein, sonst ist es nie fertig. Das Ziel gilt als erreicht, wenn alle
Punkte erfüllt sind:

| Nr. | Kriterium | Messung |
|---|---|---|
| D1 | Kein spielbarer Karteneffekt öffnet den manuellen Prompt. | `CardScriptCoverageTests`: 0 Karten mit Status `manual` im spielbaren Pool |
| D2 | Jede spielbare Karte hat für jede Fähigkeit mindestens einen Test. | Coverage-Test (Status `verified` = Test vorhanden) |
| D3 | Jede Regel des Comprehensive Rules (Abschnitte 1–11) ist entweder getestet oder bewusst als nicht relevant markiert. | Regel-Matrix `docs/rules-coverage.md` |
| D4 | Die offenen Regelfragen F1–F6 sind entschieden und mit Quelle belegt. | `docs/rules-open-questions.md` leer |
| D5 | Die nächtliche Simulation läuft mit **allen** spielbaren Karten fehlerfrei: keine verletzte Invariante, kein „Effekt-Orakel“-Fehler (siehe 6.3). Jede Fähigkeit wurde mindestens 20-mal ausgelöst. | `simulation-nightly.yml` und Coverage-Report des Simulators |
| D6 | In der Beta gibt es weniger als 1 bestätigten Regelfehler pro 50 Matches. Jeder gemeldete Fehler wird zum Regressionstest. | „Fehler melden“-Berichte (`docs/bug-reports.md`) |

---

## 2. Ausgangslage (Analyse vom 23.11.2026)

**Was schon da ist und bleibt:**

- **Server-autoritative Engine:** Der Client schickt nur Entscheidungen, Zufall gibt es nur am Server.
- **Ein Skript pro Karte:** JSON-Datei pro Karte mit Status `verified`/`generated`/`manual`, dazu Overrides, ein Schema und ein Generator (`docs/card-scripts.md`).
- **Effekt-Warteschlange:** `TriggerQueue`, `TriggerCollector` und `EffectResolver` mit Spielerwahl der Reihenfolge.
- **Testwerkzeuge:** `CardScenario` für Kartentests, Simulator mit Bots und Invarianten, ReplayTool und „Fehler melden“.
- **Teststand:** 324 Tests, alle grün (lokal geprüft).

**Zahlen:**

| | verified | generated (ungeprüft) | manual | gesamt |
|---|---|---|---|---|
| alle Karten | 13 | 1.443 | 1.040 | 2.496 |
| Karten der 16 Meta-Decks (210 verschiedene) | 0 | 93 | **105** | 210 |

Heute ist also die Hälfte der Karten in den Meta-Decks nicht automatisiert. Die andere Hälfte läuft
ungeprüft.

**Befunde:**

1. **Möglicher Grund, warum gar kein Effekt funktioniert:**
   - Der Server vergleicht den Kartentext aus der Datenbank mit einem Hash im Skript. Der Text wurde im Mai importiert, die Skripte im November aus der API erzeugt.
   - Bei jeder Abweichung wird die Karte **stillschweigend** `manual` (`CardScriptRegistry.cs:68`). Dann gibt es auch keinen „Aktivieren“-Knopf, weil `canActivate` nur für automatische Fähigkeiten gesetzt wird.
   - Noch nicht geprüft, weil die Produktionsdatenbank hier nicht erreichbar ist. Das ist Phase 0.
2. **Der Parser stößt an seine Grenze.** Die 1.115 nicht verstandenen Textteile sind ein „langer Schwanz“: Das häufigste Muster („Look at N cards …“) kommt nur 48-mal vor, danach wird es schnell seltener. Mit weiteren regulären Ausdrücken wird die Abdeckung nur noch langsam besser.
3. **Das Effektmodell ist flach.** Eine Fähigkeit ist eine Liste von Schritten mit Bedingungen. Folgendes fehlt:
   - „Choose one“: Wahl zwischen zwei Effekten
   - „If you do“: der nächste Schritt hängt davon ab, ob der vorige passiert ist
   - „for every …“: Werte, die gezählt werden
   - Ersatz-Effekte („would be K.O.'d … instead“)
   - Auslöser bei Ereignissen („When a DON!! card on your field is returned …“, „When your opponent plays …“)
   - Zeitpunkt „at the start of your turn“
4. **Die Spiellogik liegt zum großen Teil in `MatchRoom.cs` (2.673 Zeilen, Server-Projekt).** Regeln, Netzwerk und Timer sind vermischt. Dadurch sind Regeltests teuer.
5. **Die Simulation prüft nur den Spielzustand, nicht ob ein Effekt das Richtige tut.** Bei `manual`-Karten klicken die Bots nur „Erledigt“.
6. **Der Server übernimmt die Kartendaten vom Browser.**
   - Der Browser lädt jede Deckkarte über `GET /cards/{id}` und schickt Text, Kosten, Power und Counter mit (`matchDeckMapper.ts`). `MatchRoom.BuildMatchDeck` übernimmt diese Werte ungeprüft.
   - Folge 1: Liefert `GET /cards/{id}` einen anderen Text als beim Erzeugen der Skripte, fällt die Karte auf `manual`. Liefert es gar keinen Text, hat die Karte im Spiel **überhaupt keinen Effekt**.
   - Folge 2: Ein manipulierter Browser könnte z. B. eine Power von 99.000 schicken. Das ist eine Schummel-Lücke.

---

## 3. Wie andere Simulatoren das Problem gelöst haben

| Projekt (Spiel) | Kern | Kartendefinition | Was passiert mit nicht umgesetzten Karten? |
|---|---|---|---|
| **MTG Arena** (Magic, offiziell) | C++ „Game Rules Engine“, Regeln als CLIPS-Regeln auf einem gemeinsamen „Whiteboard“ | Ein **Parser** übersetzt den Kartentext in CLIPS-Regeln. Etwa 80 % der neuen Karten funktionieren damit sofort, der Rest wird von Entwicklern ergänzt und getestet. | Kommen nicht ins Spiel |
| **EDOPro / YGOPro** (Yu-Gi-Oh) | `ocgcore` in C++ verwaltet den Zustand und die Chain, eine Lua-API für Skripte | **Ein Lua-Skript pro Karte**, von Hand geschrieben. Effekte sind Objekte mit Ereignis, Bedingung, Kosten, Ziel und Ausführung. | Nicht spielbar |
| **XMage** (Magic) | Java-Engine mit Ereignissen, Ersatz-Effekten und Ebenen für Dauer-Effekte | **Eine Java-Klasse pro Karte** aus wiederverwendbaren Bausteinen (Effect, Cost, Target, Condition), mit Kartentests | Nicht im Deckbuilder |
| **Forge** (Magic) | Java-Engine mit „AbilityFactory“ | **Eine Textdatei pro Karte** in einer eigenen Skriptsprache, z. B. `T:Mode$ ChangesZone` für „wenn eine Karte die Zone wechselt“ | Nicht spielbar |
| **LovecaSim** (Love Live! TCG) | Python/Rust | Parser macht aus dem Text typisierte Effekte, dazu **handgeschriebene Sonderfälle pro Karten-ID** | – |
| **OPTCGSim** (One Piece) | Unity, Quellcode nicht öffentlich | Wahrscheinlich Karte für Karte umgesetzt: neue Sets kommen erst Wochen nach Erscheinen. *Nicht überprüfbar.* | – |
| **Tabletop Simulator, untap** | keine Regeln | alles manuell | – |

**Was alle gemeinsam haben und wir übernehmen:**

1. **Regelkern und Karten sind streng getrennt.** Der Kern setzt das Regelwerk um (Phasen, Kampf,
   Effekt-Warteschlange, Ereignisse, Ersatz-Effekte, Dauer-Effekte). Karten sind nur Daten bzw. kleine
   Skripte, die Bausteine des Kerns benutzen.
2. **Alles läuft über Ereignisse.** Jede Änderung (Karte gespielt, K.O., DON!! zurück, Life verloren …)
   ist ein Ereignis. Auslöser hören auf Ereignisse, Ersatz-Effekte greifen davor ein. Feste Zeitpunkte
   wie „On Play“ sind nur Sonderfälle davon (Forge: `ChangesZone`, EDOPro: `EVENT_*`).
3. **Ein Parser liefert höchstens einen Entwurf (MTG Arena: ~80 %).** Jede Karte wird danach von
   einem Menschen geprüft und bekommt einen Test, bevor sie als fertig gilt.
4. **Für Sonderfälle gibt es einen Ausweg:** eigener Code für eine einzelne Karten-ID (XMage: jede Karte;
   LovecaSim: `execute_hardcoded_ability`).
5. **Nicht umgesetzte Karten sind nicht spielbar.** Es gibt keinen „bitte manuell ausführen“-Modus. Das
   ist der wichtigste Unterschied zu uns: Ein halb automatisches Spiel ist für zwei Fremde online
   nicht fair spielbar.

**Quellen:**
- [On Whiteboards, Naps, and Living Breakthrough (Wizards of the Coast)](https://magic.wizards.com/news/mtg-arena/on-whiteboards-naps-and-living-breakthrough)
- [ygopro-core](https://github.com/edo9300/ygopro-core)
- [EDOPro Scripting Guide](https://projectignis.github.io/scrapi-book)
- [Forge Card scripting API](https://github.com/Card-Forge/forge/wiki/Card-scripting-API)
- [Forge Triggers](https://github.com/Card-Forge/forge/wiki/Triggers)
- [XMage: nicht umgesetzte Kartengruppen](https://www.slightlymagic.net/forum/viewtopic.php?p=192361)
- [LovecaSim](https://huggingface.co/spaces/trioskosmos/LovecaSim/blob/main/engine/models/ability.py)
- [MOOgiwara (One Piece, TypeScript)](https://github.com/BAA-Studios/MOOgiwara)
- [Comprehensive Rules (Bandai)](https://en.onepiece-cardgame.com/pdf/rule_comprehensive.pdf)

---

## 4. Architektur-Entscheidungen

| Nr. | Entscheidung | Begründung | Verworfene Alternative |
|---|---|---|---|
| A1 | **Hybrid wie MTG Arena:** Der Parser erzeugt Entwürfe, jede Karte wird geprüft und getestet. Wenige Sonderfälle bekommen eigenen C#-Code pro Karten-ID. | OPTCG-Texte sind stark normiert, der Parser liefert schon gute Entwürfe (Stichprobe: meist korrekt). Den Rest von Hand zu machen ist schneller als alles neu. | (a) Nur Parser: die Abdeckung bleibt hängen, falsche Effekte bleiben unbemerkt. (b) Alle 2.496 Karten in C# wie XMage: zu viel Aufwand für das Team. |
| A2 | **Effektmodell v2 als Baum** statt flacher Liste: `Sequence`, `Optional` („you may“), `IfYouDo`, `ChooseOne`, `If`, `ForEach`, Zahl-Ausdrücke („for every“), Auslöser über Ereignisse, Ersatz-Effekte. JSON bleibt. | Die bestehenden Werkzeuge (Schema, Overrides, Generator, Review) bleiben nutzbar. Ein Baum deckt die fehlenden Muster ab. | Lua-Skripte wie EDOPro: neue Sprache, schwerer zu prüfen und zu sichern. |
| A3 | **Ereignis-Pipeline im Kern:** jede Zustandsänderung läuft über `Ereignis vorschlagen → Ersatz-Effekte → ausführen → Auslöser sammeln`. | Nur so funktionieren „When …“- und „would … instead“-Effekte allgemein. Das ist auch der Aufbau von XMage und Forge. | Jeden neuen Zeitpunkt einzeln in `MatchRoom` einbauen: genau das macht den Code heute schwer wartbar. |
| A4 | **Regeln wandern schrittweise aus `MatchRoom` nach `OneSake.Game`**, in kleinen Schritten mit grünen Tests. `MatchRoom` bleibt für Verbindung, Timer und Senden zuständig. | Reine, deterministische Engine: schnelle Tests, Bots und Replays ohne Server. | Großer Umbau auf einmal: zu riskant. |
| A5 | **Der offizielle Kartentext liegt im Repo** (`CardScripts/card-texts.json`, Stand mit Datum). Die Effekte hängen nur von der Karten-ID ab, nicht vom Datenbanktext. Weicht ein Text ab, schlägt die CI fehl, statt dass die Karte still `manual` wird. | Behebt den vermuteten Fehler aus Befund 1 dauerhaft. Errata werden bewusst übernommen. | Hash-Vergleich zur Laufzeit wie heute: schaltet still alles ab. |
| A6 | **Nur fertige Karten sind spielbar.** Der Deckbuilder zeigt „noch nicht spielbar“. Ranked und Quick Match erlauben nur `verified`. `generated` gibt es nur noch an privaten Tischen mit Hinweis, `manual` am Ende gar nicht mehr. | Wie XMage und Forge: fair und ehrlich gegenüber den Spielern. | Alles spielbar und manuell ergänzen (heute): unfair, verwirrend. |
| A7 | **Kartentests als Daten:** eine JSON-Datei pro Karte mit Aufbau, Aktionen und erwartetem Zustand, ausgeführt von einem einzigen Test-Runner. | Ein Test kostet so 2–5 Minuten statt 15. Das ist nötig, damit 2.500 Karten überhaupt machbar sind. | Ein C#-Test pro Karte: zu langsam zu schreiben. |
| A8 | **Review-Ansicht mit Rückübersetzung:** Das Skript wird wieder als Text angezeigt (z. B. „K.O. bis zu 1 gegnerischen Character mit Kosten ≤ 2“), direkt neben dem Original. | Prüfen geht schneller und sicherer als JSON lesen. Fehler fallen sofort auf. | JSON von Hand vergleichen. |
| A9 | **Optional: KI-Entwürfe** für Karten, die der Parser nicht versteht. Ein Sprachmodell schreibt JSON nach dem Schema, ein Mensch prüft es, der Test entscheidet. Die Entscheidung trifft das Team. | Beschleunigt den langen Schwanz. Durch Schema, Review und Test kommt kein ungeprüfter Effekt ins Spiel. | Nur von Hand schreiben. |
| A10 | **Rückübersetzung mit exaktem Vergleich als automatischer Prüfer:** Aus dem Skript wird wieder der englische Kartentext erzeugt und mit dem Original verglichen (nach `EffectParser.Clean`). Gleich = automatisch geprüft. | OPTCG-Texte sind stark normiert. Der Vergleich läuft gegen den echten Text, nicht gegen einen selbst geschriebenen Test. So rutscht ein Missverständnis nicht durch. | Jede Karte von einem Menschen prüfen lassen |
| A11 | **Bausteine statt Karten gründlich testen:** jeder Baustein (etwa 50–80) mit allen Grenzfällen und den passenden Regeln. Eine Karte ist korrekt, wenn ihre Bausteine korrekt sind **und** die Rückübersetzung exakt passt. | Kontrolliert werden müssen dann die Bausteine, nicht 2.500 Karten. | – |
| A12 | **Zwei unabhängige Herleitungen pro Karte:** Parser-Entwurf und ein frisch, ohne Blick auf den Entwurf geschriebenes Skript. Nur bei Widerspruch prüft ein Mensch. | Zwei Fehler, die genau gleich aussehen, sind unwahrscheinlich. | – |
| A13 | **Offizielle Q&A von Bandai als Tests:** die Antworten des Herstellers zu einzelnen Karten werden zu Kartentests. | Wahrheit von außen für die kniffligen Fälle. | Videos von Partien: zeigen nicht warum etwas passiert, enthalten Spielfehler und kaum Sonderfälle. |
| A14 | **Kartendaten kommen vom Server, nicht vom Browser:** Der Browser schickt nur Karten-IDs und Anzahl. Text, Kosten, Power und Counter liest der Server selbst. | Behebt Befund 6: keine Schummel-Lücke, und der Text stimmt immer mit den Skripten überein. | Werte vom Browser übernehmen (heute) |

---

## 5. Phasen und Aufgaben

Reihenfolge: **0 → 1 → 2 → 3**, dann **4** in Wellen. **5** und **6** laufen ab Welle 1 mit.
Jede Phase endet mit `dotnet build` und `dotnet test` grün und dem Simulator-Smoke grün. Danach wird
`logic` in `dev` gemergt.

### Phase 0 – Warum funktioniert kein Effekt? (sofort, 1–2 Tage)

- [ ] 0.1 Diagnose-Ausgabe: Zu Matchbeginn loggt der Server pro Deck, wie viele Karten `verified`, `generated` oder `manual` sind und welche Karten wegen geändertem Text `manual` wurden.
- [x] 0.2 Werkzeug `CardScriptGen --check-backend <URL>` / `--check-texts <Datei>`: macht für jede Karte dieselbe Anfrage wie der Browser (`GET /cards/{id}`), vergleicht den Text mit dem Skript und zählt, was das Spiel wirklich mit der Karte macht. Bericht als `card-text-check.md`. (23.11.)
- [ ] 0.3 Gegen den echten Server ausführen (aus dieser Umgebung ist `api.onesake.world` gesperrt, also durch WoWoWocal). Ergebnis hier eintragen.
- [ ] 0.4 Ursache beheben, als Sofortlösung Hash-Vergleich ohne Formatierungsunterschiede. Die echte Lösung kommt mit A5 in Phase 2.
- [ ] 0.6 Kartendaten serverseitig laden (A14): `MatchRoom` holt Text, Kosten, Power, Counter, Typen und Farbe pro Karten-ID aus der Datenbank. Die Werte vom Browser werden ignoriert.
- [ ] 0.5 Die Karten aus dem Nutzertest (Activate: Main, Search, End of Turn) als Tests nachbauen. Dann mit zwei Browsern ein echtes Match spielen (Playwright) und die Effekte prüfen.

**Abnahme:** Die gemeldeten Karten funktionieren im echten Match. Die Zahlen aus 0.1 stimmen mit der
Tabelle in Abschnitt 2 überein.

### Phase 1 – Regel-Fundament (ca. 2 Wochen)

- [ ] 1.1 `docs/rules-coverage.md` anlegen: jeder Abschnitt der Comprehensive Rules v1.2.1 (1–11) als Zeile mit Status `getestet` / `fehlt` / `nicht relevant` und Testname.
- [ ] 1.2 Für jede Zeile mit `fehlt` einen Regeltest unter `OneSake.Tests/Rules/` schreiben, benannt nach der Regelnummer (z. B. `Rule_7_1_4_DamageStep`), und Fehler beheben.
- [ ] 1.3 Die offenen Regelfragen F1–F6 klären. Quellen: offizielle Q&A von Bandai, Judge-Ruling-Dokumente, Card-Rulings pro Karte. Danach die übersprungenen Tests ausschreiben.
- [ ] 1.4 Ereignis-Pipeline (A3) in `OneSake.Game/Rules`:
  - Ereignistypen: `CardPlayed`, `CardKOd`, `CardLeftField`, `DonReturned`, `DonAttached`, `LifeLost`, `LifeAdded`, `CardDrawn`, `AttackDeclared`, `BlockerActivated`, `CounterUsed`, `EventActivated`, `TurnStarted`, `MainPhaseStarted`, `TurnEnding`
  - Ereignisse für „When …“-Auslöser an die `TriggerQueue` anbinden
- [ ] 1.5 Ersatz-Effekte („would be K.O.'d … instead“, Regel 8-1-3-4): Die Pipeline fragt vor dem Ausführen. Gibt es mehrere, wählt der betroffene Spieler.
- [ ] 1.6 Fehlende Zeitpunkte und Dauern:
  - „at the start of your turn / Main Phase“
  - „until the start of your next turn“ als eigene Dauer
- [ ] 1.7 Dauer-Effekte sauber berechnen (Reihenfolge: Grundwert → „base power becomes“ → Modifikatoren), Tests für jede Kombination.
- [ ] 1.8 Erste Regeln aus `MatchRoom` nach `OneSake.Game` verschieben (A4): Phasenwechsel, Kampf, Zugende. Ein Commit pro Bereich, Tests jeweils vorher und nachher grün.

**Abnahme:** Regel-Matrix ohne `fehlt` in den Abschnitten 6–10. F1–F6 entschieden. Bestehende Tests
grün.

### Phase 2 – Effektmodell v2 (ca. 1–2 Wochen)

- [ ] 2.1 Schema `card-script.v2.schema.json` (A2) mit Knoten und Bausteinen:
  - Knoten: `Sequence`, `Optional`, `IfYouDo`, `ChooseOne`, `If`, `ForEach`, `Value` (Zahl oder Zählung, z. B. „Anzahl deiner {Straw Hat Crew} Characters“)
  - Auslöser: `Trigger { event, filter }`
  - Ersatz-Effekte: `Replacement { event, filter, instead }`
  - Sonderfälle: `Custom { handler }` für C#-Code pro Karte
- [ ] 2.2 `EffectEngine` führt v2 aus. Alle Spielerentscheidungen laufen weiter über die bestehenden Prompts, auch für die Bots.
- [ ] 2.3 Automatischer Umbau v1 → v2 für alle 2.496 Dateien. Alle bisherigen Kartentests bleiben unverändert grün.
- [ ] 2.4 Kartentexte im Repo einfrieren (A5): `card-texts.json` plus CI-Prüfung. Den Hash-Vergleich zur Laufzeit entfernen.
- [ ] 2.5 Registry für Sonderfälle `Custom`: eine Klasse pro Karte unter `OneSake.Game/CardScripts/Custom/`.

**Abnahme:** Engine läuft nur noch mit v2. Alle Tests grün. Simulator-Smoke grün.

### Phase 3 – Werkzeuge für schnelles, sicheres Umsetzen (ca. 1 Woche)

- [ ] 3.1 Kartentests als Daten (A7): `OneSake.Tests/CardCases/<SET>/<ID>.json` mit Aufbau (Leader, Hand, Feld, DON!!), Aktionen, Antworten auf Prompts und erwartetem Zustand. Ein Test-Runner führt alle aus.
- [ ] 3.2 Rückübersetzung Skript → Text (A8). Anzeige in der Karten-Detailansicht und als Befehl `CardScriptGen --explain <ID>`.
- [ ] 3.3 Review-Seite (nur Entwicklung): Liste aller Karten einer Welle mit Original, Rückübersetzung, Status und Testergebnis. Knöpfe „passt“ und „falsch“, Notiz.
- [ ] 3.4 Generator v2:
  - erzeugt v2-Skripte
  - schlägt einen Test-Entwurf pro Karte vor
  - Muster, die nur in einer Karte vorkommen, gehen nicht in den Parser, sondern in einen Override
- [ ] 3.5 Abdeckungs-Bericht pro Deck, Set und Welle als Markdown, in der CI als Artefakt.
- [ ] 3.7 Rückübersetzung mit exaktem Vergleich (A10) für alle Karten. Bericht: wie viele Karten passen genau. **Erste wichtige Messung:** daraus ergibt sich die Größe der Prüfliste.
- [ ] 3.8 Zweite, unabhängige Herleitung pro Karte (A12). Vergleich beider Skripte, Widersprüche auf die Prüfliste.
- [ ] 3.9 Bandai-Q&A sammeln und als Kartentests ablegen (A13). Vorher prüfen, ob die Seiten erreichbar sind.
- [ ] 3.10 Prüfliste erzeugen. Darauf landen:
  - Karten mit Abweichung in 3.7 oder Widerspruch in 3.8
  - Karten mit `Custom`-Code oder Ersatz-Effekten
  - Karten mit unklarer Q&A
  - eine Stichprobe von 2–5 % der automatisch geprüften Karten
- [ ] 3.6 *(Optional, A9)* KI-Entwurf: `CardScriptGen --draft-ai <ID>` schreibt einen v2-Entwurf als Override mit Status `generated`. Danach Review und Test wie immer.

**Abnahme:** Eine Karte vom Entwurf bis `verified` dauert im Schnitt höchstens 10 Minuten
(an 20 Karten messen).

### Phase 4 – Karten in Wellen umsetzen

Ablauf pro Karte (Arbeitsteilung: Claude setzt um, ein Mensch bearbeitet nur die Prüfliste):

1. **Claude:** Skript korrigieren (Override), bei Bedarf neuer Baustein oder `Custom`-Handler. Ziel: Die Rückübersetzung passt exakt (3.7), beide Herleitungen stimmen überein (3.8).
2. **Claude:** Kartentest als JSON anlegen. Er deckt jede Fähigkeit ab sowie Grenzfälle wie „up to“ mit 0 Zielen, eine nicht bezahlbare Bedingung oder einen leeren Stapel.
3. **Automatisch:** Passen Rückübersetzung, zweite Herleitung und Test, wird die Karte `verified` (Notiz: „automatisch geprüft“).
4. **Mensch:** nur Karten auf der Prüfliste (3.10) in der Review-Seite freigeben oder ablehnen.
5. Die Baseline in `coverage-baseline.json` steigt.

| Welle | Inhalt | Karten (ca.) | Warum zuerst |
|---|---|---|---|
| W1 | Starter-Decks ST01–ST04 und alle Leader | 52 + ca. 150–200 (genaue Zahl aus dem Bericht 3.5) | Einstieg für neue Spieler, Leader-Effekte sind in jedem Match |
| W2 | Die 16 Meta-Decks aus dem Meta-Tab | 210 | Was tatsächlich gespielt wird (Card Kaizoku) |
| W3 | Restliche Starter-Decks ST05–ST36 | ~250 | Fertige Decks für alle Farben |
| W4 | Booster-Sets, neueste zuerst (OP17 → OP01, EB, PRB, Promos) | Rest | Neue Karten werden mehr gespielt |

- [ ] W1 fertig
- [ ] W2 fertig: **Ab hier können Menschen alle Meta-Decks vollautomatisch spielen.**
- [ ] W3 fertig
- [ ] W4 fertig: alle Karten

Neue Sets: Generator laufen lassen, die Karten sind bis zur Prüfung `generated`, danach neue Welle.

### Phase 5 – Nur fertige Karten spielen (ab W1)

- [ ] 5.1 Deckbuilder: Status pro Karte anzeigen (fertig / ungeprüft / noch nicht spielbar), Filter „nur spielbar“.
- [ ] 5.2 Deckprüfung am Server: Ranked und Quick Match nur mit `verified`-Karten. Private Tische erlauben `generated` mit Hinweis im Lobby-Fenster.
- [ ] 5.3 Den manuellen Effekt-Prompt in Ranked und Quick Match abschalten.
- [ ] 5.4 Nach W4 den manuellen Modus ganz entfernen. Den Rest des manuellen Modus nur noch zum Fehlermelden behalten.
- [ ] 5.5 Changelog („Was ist neu“) und Projekttagebuch zu jedem dieser Schritte.

### Phase 6 – Qualitätssicherung (laufend, ab Phase 2)

- [ ] 6.1 Effekt-Orakel im Simulator: Nach jedem Schritt prüft ein Prüfer die erwartete Wirkung.
  - Beispiele: `Draw 1` → Hand +1 (oder Deck leer → Niederlage); `KO` → Karte im Trash, außer ein Ersatz-Effekt greift; `LookAndReveal` → Deckgröße stimmt; Kosten wurden bezahlt.
  - Verletzungen werden als Aufzeichnung gespeichert wie die Invarianten.
- [ ] 6.2 Bots benutzen alle Möglichkeiten: Activate: Main, Counter-Events, Trigger, „you may“ mal ja und mal nein, Reihenfolge von Effekten.
- [ ] 6.3 Ausführungs-Abdeckung: Der Simulator zählt, wie oft jede Fähigkeit ausgelöst wurde. Die Nacht schlägt fehl, wenn eine spielbare Fähigkeit nie vorkam.
- [ ] 6.4 Nightly mit Decks aus allen `verified`-Karten, nicht nur der Fixture.
- [ ] 6.5 Testpartien mit Menschen nach W2: mindestens 50 Matches im Team und mit Testern. Jeder Fehlerbericht wird zum Replay-Regressionstest (`docs/bug-reports.md`).
- [ ] 6.6 Vergleich mit OPTCGSim: 10 ausgewählte knifflige Situationen in beiden Simulatoren nachspielen und das Ergebnis vergleichen (gut für die Diplomarbeit).

---

## 6. Teststrategie in Kurzform

| Ebene | Was | Wo |
|---|---|---|
| Regeltests | eine Regel des Comprehensive Rules, mit Regelnummer im Namen | `OneSake.Tests/Rules/` |
| Kartentests | jede Fähigkeit jeder Karte, als Daten | `OneSake.Tests/CardCases/` |
| Replay-Tests | echte Fehler aus Matches, exakt nachgespielt | `OneSake.Tests/Replays/` |
| Simulation | Invarianten und Effekt-Orakel, 200 Partien pro Push, 2 × 20.000 pro Nacht | `OneSake.Simulator` |
| Menschen | Testpartien, „Fehler melden“ | Beta |

---

## 7. Aufwand und Risiken

| Risiko | Gegenmaßnahme |
|---|---|
| 2.496 Karten sind viel. Ein Mensch allein bräuchte bei 10 Minuten pro Karte etwa 420 Stunden. | Claude setzt die Karten um (geschätzt 50–100 einfache Karten pro Sitzung, also etwa 30–50 Sitzungen für alle). Die automatischen Prüfer (A10–A13) halten die Prüfliste klein. Geschätzt 10–20 % der Karten plus die Bausteine, also etwa 10–15 Stunden menschliche Prüfung statt 60–80. Die echte Zahl liefert Messung 3.7. Wellen nach Wichtigkeit. |
| Claude versteht einen Kartentext falsch und schreibt Skript und Test mit demselben Fehler. | Der Test allein reicht nie: Rückübersetzung gegen den Originaltext (A10), unabhängige zweite Herleitung (A12), Bandai-Q&A (A13), Stichprobe durch Menschen. |
| Regelfragen ohne klare Antwort | Offizielle Q&A und Rulings als Quelle, Entscheidung in `rules-open-questions.md` begründen |
| Umbau bricht laufende Spiele | Kleine Schritte auf `logic`, Merge nach `dev` nur mit grünen Tests und Simulator |
| Neue Sets und Errata | CI erkennt geänderte Texte (A5). Generator für neue Sets. Karten bleiben bis zur Prüfung nicht ranked-fähig. |
| Bots finden falsche Effekte nicht | Effekt-Orakel (6.1) und Kartentests. Bots finden nur Abstürze und Zustandsfehler. |

---

## 8. Fortschritt

| Datum | Phase / Welle | verified | generated | manual | Bemerkung |
|---|---|---|---|---|---|
| 23.11.2026 | Start | 13 | 1.443 | 1.040 | Plan erstellt |
| 23.11.2026 | Phase 0 | 13 | 1.443 | 1.040 | Prüfwerkzeug 0.2 fertig, Plan um A10–A14 ergänzt |
