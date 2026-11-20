namespace OneSake.Tests.Rules;

// Questions the Comprehensive Rules (v1.2.1) do not answer clearly. The current behavior is
// described in docs/rules-open-questions.md. When a question is answered, write the real test
// here and remove the Skip.
public class OpenRuleQuestionTests
{
    [Fact(Skip = "Regelklärung nötig: F1 - Rule 8-6-1 regelt nur neue Effekte des Spielers am Zug. Kommt ein NEUER Effekt des Nicht-Zugspielers, der während der Auflösung entsteht, vor oder nach seinen bereits wartenden Effekten?")]
    public void NewEffectOfTheNonTurnPlayer_DuringResolution_Order()
    {
    }

    [Fact(Skip = "Regelklärung nötig: F2 - Darf ein Spieler die Reihenfolge seiner gleichzeitigen Effekte auch außerhalb der End Phase frei wählen (nur 6-6-1-1-3/4 sagt das ausdrücklich, 8-6-1-1 setzt es voraus)?")]
    public void OrderOfOwnSimultaneousEffects_OutsideTheEndPhase()
    {
    }

    [Fact(Skip = "Regelklärung nötig: F3 - Rule 8-3-2-3: Muss [DON!! xX] nur beim Erfüllen der Aktivierungszeit gelten oder auch noch, wenn der wartende Effekt später aktiviert wird?")]
    public void DonCondition_AtTimingOrAtActivation()
    {
    }

    [Fact(Skip = "Regelklärung nötig: F4 - [On K.O.] nach einem K.O. im Kampf: vor oder nach dem Ende von \"during this battle\"-Effekten (7-1-5-3/4) auflösen?")]
    public void OnKOAfterBattleKO_BeforeOrAfterEndOfBattle()
    {
    }

    [Fact(Skip = "Regelklärung nötig: F5 - Beide Spieler erfüllen gleichzeitig eine Niederlagebedingung (9-2-1): Wie wird das Ergebnis gewertet (Unentschieden, Bounty)?")]
    public void BothPlayersLose_Result()
    {
    }

    [Fact(Skip = "Regelklärung nötig: F6 - \"cannot be K.O.'d by effects\" gegenüber \"by your opponent's effects\": Gilt die erste Form auch gegen eigene Effekte (der Parser unterscheidet die Formen bisher nicht)?")]
    public void CannotBeKOdByEffects_AlsoAgainstOwnEffects()
    {
    }
}
