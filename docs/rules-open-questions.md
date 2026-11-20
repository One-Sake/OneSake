# Offene Regelfragen

Quelle: ONE PIECE CARD GAME Comprehensive Rules, Version 1.2.1 (Stand 28.08.2026),
<https://en.onepiece-cardgame.com/pdf/rule_comprehensive.pdf>. Das PDF liegt nicht im Repo.

Zu jeder Frage gibt es einen übersprungenen Test in
`backend/OneSake.Tests/Rules/OpenRuleQuestionTests.cs`. Wenn eine Frage geklärt ist: Verhalten
anpassen, den Test ausschreiben und `Skip` entfernen.

| Nr. | Frage | Regel | Aktuelles Verhalten |
|-----|-------|-------|---------------------|
| F1 | Ein Effekt des Spielers am Zug lässt einen **neuen** Effekt des Nicht-Zugspielers entstehen, während dieser schon wartende Effekte hat. Kommt der neue vor oder nach den wartenden? | 8-6-1 regelt nur neue Effekte des Zugspielers | Neue Effekte kommen immer hinter alle wartenden (wie bei 8-6-1-1). |
| F2 | Darf ein Spieler die Reihenfolge seiner gleichzeitigen Effekte auch außerhalb der End Phase frei wählen (z. B. zwei Effekte durch denselben K.O.)? | 6-6-1-1-3/4 (nur End Phase), 8-6-1-1 setzt eine Reihenfolge voraus | Ja: bei mehr als einem Effekt kommt immer der Prompt `ORDER_TRIGGERS`. |
| F3 | Muss die Bedingung `[DON!! xX]` nur beim Erfüllen der Aktivierungszeit gelten oder auch noch, wenn der wartende Effekt später aktiviert wird? | 8-3-2-3 ("at that time"), 8-4-1-1 | Beides wird geprüft. Ausnahme `[On K.O.]` (nur auf dem Feld, 10-2-17-1). |
| F4 | `[On K.O.]` nach einem K.O. im Kampf: vor oder nach dem Ende der "during this battle"-Effekte auflösen? | 7-1-4-1-2, 7-1-5-3/4 | Nach dem Kampfende (wie vor dem Umbau). |
| F5 | Beide Spieler verlieren gleichzeitig: Wie wird das Match gewertet? | 9-2-1 ("all of those players lose") | Spielende ohne Gewinner, kein Ergebnis und keine Bounty-Änderung. |
| F6 | Wirkt "cannot be K.O.'d by effects" auch gegen eigene Effekte? Der Parser macht aus beiden Formen dasselbe Flag. | 1-3-3, 10-2-1-3 | Schützt nur gegen Effekte des Gegners. |

## Bekannte Lücken (Regeln klar, noch nicht umgesetzt)

- **Ersatz-Effekte ("instead", 8-1-3-4):** Der Parser versteht sie nicht. Solche Karten
  bekommen den Hinweis "Effekt bitte manuell ausführen".
- **"until the start of your next turn" (6-2-1):** Das Modell kennt nur eine gemeinsame Dauer
  mit "until the end of your opponent's next turn". Sie endet am Ende des gegnerischen Zugs, also
  kurz vor der Refresh Phase.
- **"at the start of your turn / Main Phase" (6-2-2, 6-5-1):** Diese Zeitpunkte gibt es noch
  nicht als Timing. Die Karten werden manuell ausgeführt.
- **Endlosschleifen (11-1):** Nach 1000 Effekten in Folge bricht der Resolver ab und schreibt
  einen Log-Eintrag.
