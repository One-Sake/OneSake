# Projekttagebuch OneSake – 13-Tino-13

Chronologische Dokumentation der eigenen Arbeitsschritte mit Zeitstempeln und Überlegungen
(Format `JJJJ-MM-TT HH:MM`, lokale Zeit Wien).

**Legende:** ★ = eigene Arbeit · 🤖 = mit KI-Werkzeug erstellt · 👥 = Teammitglied

### 2026-11-23 04:23 – Login für Spieler mit Benutzername und Passwort ★ 🤖
- **Was:** Accounts mit Benutzername + Passwort (Tabelle `Accounts`, Migration `AddAccounts`),
  `AuthController` (`/auth/register`, `/login`, `/logout`, `/logout-all`, `/me`, `/change-password`,
  `/reset-password`), Login-Bereich im Profil, Tests in `OneSake.Tests/Auth/AccountServiceTests.cs`.
- **Ausgangslage:** Bisher gab es nur die Gast-ID im `localStorage`. Der Server hat sie nicht geprüft:
  Wer die Gast-ID eines anderen kannte, war für den Server diese Person (Statistik, Kopfgeld, Ranked über
  `JoinQueue(displayName, guestId)`). Laut Projektauftrag (Abnahme Punkt 7) müssen Decks, Berries und
  Cosmetics „nach erneutem Login“ erhalten bleiben – der Login ist also die Grundlage für Progression.
- **Überlegungen:**
  - *Passwort speichern:* nur als Hash mit `PasswordHasher` von ASP.NET Core (PBKDF2 mit Salt, im
    Framework enthalten). Kein eigener SHA256-Hash, weil der ohne Salt und viele Runden schnell zu knacken ist.
  - *Eigene kleine Schicht statt voller ASP.NET Identity:* Identity bringt 7 Tabellen, Rollen, E-Mail usw.
    mit, die wir nicht brauchen. So ist jede Zeile erklärbar.
  - *Sitzung als HttpOnly-Cookie statt JWT im localStorage:* JavaScript kommt nicht an das Cookie, ein
    XSS-Fehler kann die Sitzung also nicht stehlen. SignalR schickt das Cookie automatisch mit.
  - *Passwort vergessen:* Wiederherstellungscode statt E-Mail (siehe Entscheidungen). Der Code wird bei der
    Registrierung einmal angezeigt, nur gehasht gespeichert und ist nur einmal gültig.
  - *Fremder Zugriff:* gleiche Fehlermeldung für falschen Namen und falsches Passwort (auch gleich lange
    Antwortzeit über einen Dummy-Hash), Sperre nach 5 Fehlversuchen für 15 Minuten, Rate Limit 10
    Versuche pro Minute und IP, Passwortregeln nach NIST SP 800-63B (mind. 10 Zeichen, Liste häufiger
    Passwörter, kein Benutzername im Passwort). Der Server nimmt die Spieler-ID immer aus der Sitzung,
    nie vom Client.
  - *Abmelden überall:* `SecurityStamp` im Account. Bei Passwortänderung, Reset oder „Auf allen Geräten
    abmelden“ ändert er sich, alte Cookies werden dann abgelehnt.
  - *Bestehende Gast-Daten:* Bei der Registrierung wird die Gast-ID des Browsers zur Spieler-ID des
    Accounts. So bleiben Matches und Kopfgeld ohne Datenumzug erhalten. Weil die Gast-ID nicht geheim ist,
    darf sie nur **ein** Account übernehmen; danach ist sie als Gast-ID gesperrt.
  - *Ranked nur mit Account:* Quick Match funktioniert weiter für Gäste, zählt aber nur für eingeloggte
    Spieler für die Wertung. Sonst könnte man weiter auf fremdes Rating spielen.
- **Probleme/Lessons Learned:**
  - In der Arbeitsumgebung gab es kein .NET-SDK. Die Migration wurde deshalb von Hand geschrieben und
    muss mit `dotnet ef migrations has-pending-model-changes` geprüft werden; Build und Tests müssen
    lokal laufen.
    **Ergebnis der Prüfung (lokal, 23.11.):**
    - Build (Release) sofort ohne Fehler, am Backend-Code musste nichts repariert werden.
    - `has-pending-model-changes`: „No changes have been made to the model since the last migration“.
      Die handgeschriebene Migration passt also zum Modell und bleibt (sie trägt `[DbContext]` und
      `[Migration]` selbst, deshalb braucht sie keine `.Designer.cs`).
    - Tests: 330 gesamt, 324 bestanden, 6 übersprungen (offene Regelfragen F1–F6), 0 fehlgeschlagen;
      davon 24 neue Auth-Tests.
    - `dotnet ef database update`: alle 5 Migrationen eingespielt, Tabelle `Accounts` mit beiden
      eindeutigen Indizes per `psql` geprüft.
    - Hindernis: Windows **Smart App Control** blockiert frisch gebaute, unsignierte DLLs
      (`0x800711C7`), `dotnet test` und `dotnet ef` liefen unter Windows gar nicht. Smart App Control
      bleibt an (ausschalten geht nur einmalig). Build, Tests, `dotnet ef` und der Server liefen deshalb
      im Container `mcr.microsoft.com/dotnet/sdk:8.0` (Repo nur lesend eingebunden, eigene
      `bin/obj` im Container, Windows-Buildordner unverändert).
    - Browser-Test (Registrieren, Abmelden, Anmelden, 5× falsch → Sperre, Passwort vergessen mit Code,
      alter Code ungültig, neues Passwort) und API-Test (Rate Limit 429, „Überall abmelden“ macht alte
      Cookies ungültig, Gast-Verlauf wandert in den Account und ist danach als Gast-ID gesperrt): alles ok.
    - Dabei gefunden und behoben: Nach Registrieren oder Reset blieb das Formular im Modus
      „Account erstellen“ bzw. „Passwort vergessen“. Nach dem Abmelden fehlte so das normale
      Anmeldeformular. Jetzt springt es wie nach „Passwort ändern“ auf „Anmelden“ zurück.
  - Hinter Nginx sieht der Server alle Spieler als `127.0.0.1`. Ohne `UseForwardedHeaders` hätte das
    Rate Limit für alle Spieler zusammen gegolten.
- Screenshot: `screenshots/2026-11-23_01-login.png` (Login-Bereich im Profil),
  `screenshots/2026-11-23_02-recovery-code.png` (Code nach der Registrierung).

### 2026-11-23 18:12 – Standard DON!! Karte in die Datenbank importieren ★ 🤖
- `feat: Standard DON!! Karte von optcgapi importieren` Die Standard DON!! Karte wird von optcgapi
  (`/api/allDonCards/`) in die Tabelle `Cards` importiert. Neue Endpoints: `GET /cards/don` und
  `POST /cards/import/don` (mit `?all=true` für alle Varianten). Lokal getestet: Build grün,
  324 Tests bestanden (6 übersprungen, 0 fehlgeschlagen), der Import liefert `don_8` „DON!! Card“.
- **Überlegung:** Im Spiel werden DON bisher nur gezählt. Später sollen alle DON-Varianten als
  Belohnungen und Schätze für Spieler vorhanden sein. Vorerst reicht die Standard-Karte, deshalb
  importiert der Endpoint ohne Parameter nur diese eine.
- **Probleme/Lessons Learned:**
  - DON-Einträge haben kein `card_set_id`, daher dient `card_image_id` (`don_8`) als `CardId`.
  - DON-Karten haben `set_id` „P“. Sie werden deshalb aus `/cards/sets` und `/cards/set/{id}`
    herausgefiltert, damit sie im Deckbuilder nicht bei den Promos landen.
  - Migrationen laufen beim Serverstart nicht automatisch, sondern über `dotnet ef database update`.

<!-- Neue Einträge unten anfügen. Vorlage:

### JJJJ-MM-TT HH:MM – Titel ★/👥/🤖
- `commit` Was wurde gemacht?
- **Überlegung:** Warum so? Welche Alternativen gab es?
- **Probleme/Lessons Learned:** (optional)
- Screenshot: `screenshots/JJJJ-MM-TT_nr-name.png` (bei sichtbaren Änderungen)
-->
