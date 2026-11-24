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

<!-- Neue Einträge unten anfügen. Vorlage:

### JJJJ-MM-TT HH:MM – Titel ★/👥/🤖
- `commit` Was wurde gemacht?
- **Überlegung:** Warum so? Welche Alternativen gab es?
- **Probleme/Lessons Learned:** (optional)
- Screenshot: `screenshots/JJJJ-MM-TT_nr-name.png` (bei sichtbaren Änderungen)
-->
