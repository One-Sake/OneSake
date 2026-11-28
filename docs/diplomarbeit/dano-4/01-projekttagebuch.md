# Projekttagebuch OneSake – dano-4

Chronologische Dokumentation der eigenen Arbeitsschritte mit Zeitstempeln und Überlegungen
(Format `JJJJ-MM-TT HH:MM`, lokale Zeit Wien).

**Legende:** ★ = eigene Arbeit · 🤖 = mit KI-Werkzeug erstellt · 👥 = Teammitglied

### 2026-11-24 14:35 – Manuellen Modus entfernen (Fair Play) ★ 🤖
- **Was:** Der manuelle Modus ist komplett entfernt, im Client und am Server:
  - Client: Rechtsklick-Kontextmenü und „⋯“-Knöpfe an Karten und Zonen, Knopf „✋ Manuell“,
    Ziehen eigener Characters in Trash/Hand/Deck, Deck ansehen/durchsuchen
    (`ManualMenu.tsx`, `ManualViewDialog.tsx`, `ManualEffectBanner.tsx`, `manualItems.ts` gelöscht),
    die manuellen Aktionen in der Karten-Detailansicht. Rechtsklick öffnet wieder nur die Detailansicht.
  - Server: Hub-Methode `ManualAction`, `MatchRoom.ManualAction`, `ManualActions.cs`, der Prompt
    `MANUAL_EFFECT` und die Deck-Ansicht im `PlayerState` bzw. `PlayerStateDto`.
  - Ein Effekttext, den die Engine noch nicht ausführen kann, wird übersprungen und für beide
    Spieler als `EFFECT_NOT_AUTOMATED` geloggt (im Feed gelb mit ⚠ markiert). Die Detailansicht
    zeigt solche Teile als „Not automated yet (skipped in the match)“.
  - Tests: `ManualActionTests` gelöscht, zwei Regeltests spielen die Karten jetzt normal aus statt
    sie manuell aufs Feld zu legen, neuer Test `EffectThatIsNotAutomated_IsSkippedAndLogged`.
    Backend 300 Tests grün (6 übersprungen wie vorher), Frontend `lint` und `build` grün.
- **Überlegung:** Mit dem Kontextmenü konnte jeder Spieler jeder Karte jeden Effekt geben
  (K.O., +Power, Karten ziehen, DON!! hinzufügen …), auch ohne passende Karte. Das ist für ein
  Online-Spiel zwischen Fremden eine Schummel-Lücke. Entscheidungen, die auf der Karte stehen
  („You may …“, [Activate: Main], Ziele wählen, Ja/Nein bei Kosten), bleiben beim Spieler, wie es
  das Regelwerk vorsieht.
  - *Nur den Client ändern* reicht nicht: Die Hub-Methode wäre weiter direkt aufrufbar. Deshalb
    ist auch der Server-Teil weg.
  - *Alternative „nur in Ranked/Quick Match abschalten“* (Plan 5.3): verworfen, weil auch an
    privaten Tischen geschummelt werden kann und das Team den Modus ganz weg haben will.
  - *Was passiert mit nicht automatisierten Karten?* Sie bleiben spielbar, nur der fehlende
    Effektteil fällt weg. Das ist ehrlich sichtbar (Log + Detailansicht). Ziel ist, dass es solche
    Karten bald nicht mehr gibt (nächste Schritte: Bausteine und Kartenskripte).
  - *Alte Aufzeichnungen:* Der Zustands-Hash enthält weiter die (jetzt immer leeren) Felder
    `view=`/`viewed:`, damit gespeicherte Bug-Replays dieselben Hashes behalten. Eine alte
    Aufzeichnung mit manueller Aktion lässt sich nur bis zu dieser Aktion abspielen.
- **Probleme/Lessons Learned:**
  - Laut `AGENTS.md` gehört Frontend auf `dev`, Spielablauf in `MatchRoom` auf `logic`. Hier hängen
    beide zusammen (der Server schickt den Prompt `MANUAL_EFFECT` nicht mehr), deshalb ein
    gemeinsamer Commit auf `logic`.
  - Vorher wurde geprüft, dass `main`, `dev` und `logic` (lokal und `origin`) auf demselben Commit
    stehen und Build und Tests grün sind.
  - Kein Screenshot: lokal gibt es weder Postgres noch Docker, ohne Kartendatenbank startet kein
    Match. Screenshot vom Match ohne „✋ Manuell“-Knopf und mit gelbem Log-Eintrag ist offen.

### 2026-11-24 14:55 – Bausteine der Karteneffekte: Katalog und ein Test pro Baustein ★ 🤖
- **Was:** Jeder Baustein, aus dem ein Karteneffekt besteht, hat jetzt einen eigenen Test (Plan A11).
  - `OneSake.Tests/Blocks/BlockCatalog.cs`: Liste aller Bausteine, automatisch aus dem Effektmodell
    gebaut (Zeitpunkte, Fähigkeits-Kopf, Keywords, Kosten, Bedingungen, Aktionen, Optionen, Dauer,
    Flags, Ziele, Filter): **153 Bausteine**.
  - `BlockCoverageTests`: schlägt fehl, wenn ein Baustein keinen `[BlockTest]` hat, ein Test einen
    unbekannten Baustein nennt oder ein Baustein in `docs/effect-building-blocks.md` fehlt.
  - `BlockGame`: kleines Spiel für die Tests. Die Kartentexte werden wie im Generator geparst, die
    Effekte laufen wie im Match über Ereignis → TriggerCollector → TriggerQueue → EffectEngine mit
    Regelverarbeitung. Jede Spielerentscheidung (Ja/Nein, Karte wählen) wird im Test beantwortet.
  - 7 Testklassen mit 140 neuen Tests; jeder Test prüft beide Seiten (wirkt, wenn er soll; wirkt
    nicht, wenn er nicht soll). Die bestehenden Keyword-Tests in `GameActionTests` sind mitgezählt.
  - `docs/effect-building-blocks.md`: Katalog mit Kartentext, Häufigkeit in den Skripten,
    Spielerentscheidungen und bekannten Lücken.
  - Backend: 472 Tests grün (6 übersprungen wie vorher).
- **Überlegung:**
  - *Katalog automatisch aus den Enums statt von Hand:* Ein neuer Baustein im Modell taucht sofort
    als „ohne Test“ auf, man kann ihn nicht vergessen.
  - *Feiner als die geplanten 50–80:* Jede Bedingung, jeder Filter und jedes Flag zählt einzeln,
    so ist jedes Wort, das eine Wirkung hat, getrennt testbar (Wunsch des Teams).
  - *Tests mit echtem Kartentext statt mit gebautem JSON:* So wird gleichzeitig geprüft, dass der
    Parser die Formulierung versteht und dass die Engine das Richtige tut. Nur
    `Option.RestDestination.top` ist ein reiner Engine-Test, weil kein Kartentext ihn erzeugt.
  - *Ablauf wie im Match statt Engine direkt:* Fehler im Zusammenspiel (Trigger sammeln, Bedingungen
    beim Auflösen, `[Once Per Turn]`) fallen so auch auf.
  - *Bausteine des heutigen Modells (v1):* Das Modell v2 (Phase 2) ist noch nicht gebaut. Neue
    Bausteine kommen dazu und brauchen dann ebenfalls einen Test.
- **Probleme/Lessons Learned:**
  - Gefundene Lücken (im Katalog dokumentiert, noch nicht behoben): „place the rest at the top or
    bottom“ legt immer nach unten; „in any order“ wird nicht gefragt; `Cost.TurnLife` zählt nur
    die Life-Karten (offene Life-Karten werden nicht angezeigt); „Trash up to 1 card from your hand“
    fragt erst Ja/Nein und dann die Karte.
  - `CardZone.Deck` und `CardZone.Life` werden weder vom Parser noch von der Engine benutzt. Sie
    sind keine Bausteine; ein Test sichert, dass auch kein Skript sie verwendet.
  - Bei `[On Play] If …` liegt die Bedingung am Schritt, nicht an der Fähigkeit. Für das Spiel ist das
    gleich (geprüft beim Auflösen), der Test war zuerst falsch formuliert.
  - Beim Durchsehen aufgefallen: Der Leader ST01-001 (Monkey.D.Luffy) ist noch `manual`
    („Give this Leader or 1 of your Characters …“) – gehört in Welle 1.
  - Kein Screenshot, nichts Sichtbares geändert.

### 2026-11-28 02:31 – `logic` in `dev` und `main` gemergt, Doku-Lücken nachgetragen 🤖
- `d41ce2d` (manueller Modus entfernt, Baustein-Tests) per Fast-Forward von `logic` nach `dev` und
  `main` übernommen und gepusht. Vorher geprüft: `dotnet build` ohne Fehler, `dotnet test` 473 grün
  (6 übersprungen, offene Regelfragen F1–F6), Frontend `lint` und `build` grün.
- Doku: Commit-Hash bei US91 eingetragen, Merge bei US90 und US91 vermerkt, Sprint-6-Status auf
  „läuft“ gesetzt (wie in der Übersicht in `docs/sprints/README.md`). Offene Punkte in
  `02-entscheidungen-und-ideen.md` ergänzt.
- **Überlegung:** Der Merge nach `main` war ausdrücklich gewünscht. Damit ist das Entfernen des
  manuellen Modus jetzt auch im stabilen Stand.
- **Probleme/Lessons Learned:**
  - Folge für Demos: 1.040 von 2.496 Kartenskripten sind noch `manual` und tun im Match nichts mehr
    (nur 13 `verified`, 1.443 `generated`). Die Karten in Wellen zu automatisieren (Plan Phase 4)
    ist damit dringender geworden.
  - Die GitHub-Issues für US90 und US91 fehlen noch, weil die GitHub-CLI `gh` lokal nicht installiert ist.
  - Der Screenshot für US90 fehlt weiter (braucht lokal eine Kartendatenbank).

<!-- Neue Einträge unten anfügen. Vorlage:

### JJJJ-MM-TT HH:MM – Titel ★/👥/🤖
- `commit` Was wurde gemacht?
- **Überlegung:** Warum so? Welche Alternativen gab es?
- **Probleme/Lessons Learned:** (optional)
- Screenshot: `screenshots/JJJJ-MM-TT_nr-name.png` (bei sichtbaren Änderungen)
-->
