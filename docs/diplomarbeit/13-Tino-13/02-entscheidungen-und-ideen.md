# Ideen, Entscheidungen und Überlegungen – 13-Tino-13

Stand: 2026-11-23.

## 1. Ideenliste mit Status

### 1.1 Umgesetzt

| Idee | Seit | Bemerkung |
|------|------|-----------|
| Login mit Benutzername + Passwort | 23.11. | ersetzt die Gast-ID als Identität; Gäste können weiter spielen |
| Passwort vergessen mit Wiederherstellungscode | 23.11. | einmal gültig, danach neuer Code |
| Sperre nach 5 Fehlversuchen (15 Min.) + Rate Limit pro IP | 23.11. | gegen Passwort-Raten |
| „Auf allen Geräten abmelden“ (SecurityStamp) | 23.11. | auch nach Passwortänderung/Reset automatisch |
| Gast-Verlauf beim Registrieren übernehmen | 23.11. | nur der erste Account bekommt eine Gast-ID |

### 1.2 Offen

| Idee | Bemerkung |
|------|-----------|
| Decks, Berries, Cosmetics serverseitig am Account speichern | nächster Schritt (Abnahme Punkt 6 und 7) |
| Anzeigename im Match für eingeloggte Spieler fest an den Benutzernamen koppeln | heute kann jeder Gast jeden Namen wählen |
| Optionale E-Mail als zweiter Weg zur Wiederherstellung | nur falls Spieler ihren Code oft verlieren |
| Admin-Reset für Spieler, die Passwort und Code verloren haben | braucht einen Nachweis, wem der Account gehört |
| Bestehende SignalR-Verbindungen nach „Überall abmelden“ sofort trennen | heute gilt die neue Sitzung erst ab der nächsten Verbindung |

### 1.3 Verworfen

| Idee | Warum verworfen |
|------|-----------------|
| Passwort-Reset per E-Mail-Link | braucht Mailserver (SMTP) und speichert eine E-Mail-Adresse – widerspricht „nur notwendige Daten“ (Projektauftrag 4.4) |
| JWT im localStorage | per XSS auslesbar; Cookie mit HttpOnly ist sicherer und für SignalR einfacher |
| Volle ASP.NET Core Identity | zu viele Tabellen und Funktionen, die wir nicht brauchen |
| Sicherheitsfragen („Name des ersten Haustiers“) | Antworten sind oft erratbar oder öffentlich |
| Komplexitätsregeln (Sonderzeichen, Großbuchstaben Pflicht) | NIST rät davon ab; führt zu „Passwort1!“ statt langen Passwörtern |

## 2. Entscheidungen

| Entscheidung | Begründung | Alternative |
|--------------|-----------|-------------|
| `PasswordHasher` (PBKDF2, Salt) für Passwort und Code | Standard von ASP.NET Core, kein Zusatzpaket | bcrypt/Argon2 (Zusatzpaket) |
| Sitzung im HttpOnly-Cookie (30 Tage, gleitend) | nicht per JavaScript lesbar, SignalR schickt es automatisch | JWT |
| Spieler-ID kommt immer aus der Sitzung | ein Client kann keine fremde ID mehr mitschicken | ID als Parameter (alt) |
| Eine Fehlermeldung für falschen Namen/Passwort/Code | niemand soll herausfinden, welche Accounts es gibt | getrennte Meldungen |
| Fehlversuche beim Code zählen wie beim Passwort | sonst könnte man den Code unbegrenzt raten | |
| Account-Spieler-ID = alte Gast-ID | Matches und Kopfgeld bleiben ohne Datenumzug | Tabellen umschreiben |
| Ranked zählt nur für eingeloggte Spieler | Gast-IDs sind nicht geheim, Rating wäre fälschbar | Ranked auch für Gäste (alt) |
| Handgeschriebene Migration `AddAccounts` beibehalten | `has-pending-model-changes` meldet keine Abweichung zum Modell | Migration mit `dotnet ef migrations add` neu erzeugen |
| Build/Tests/`dotnet ef` lokal im SDK-Container | Smart App Control blockiert unsignierte DLLs unter Windows und soll an bleiben | Smart App Control ausschalten (nicht umkehrbar) |
| Nach Registrieren/Reset zurück auf „Anmelden“ | sonst zeigt das Profil nach dem Abmelden das falsche Formular | |

## 3. Abweichungen vom Projektauftrag

- Keine. Der Projektauftrag nennt „Auth (Guest/Account)“; beides gibt es jetzt.
- Bewusste Einschränkung: Wer Passwort **und** Wiederherstellungscode verliert, kommt nicht mehr in
  seinen Account (keine E-Mail). Das ist der Preis für die Datensparsamkeit.

## 4. Risiken

| Risiko | Gegenmaßnahme |
|--------|---------------|
| Angreifer sperrt fremde Accounts absichtlich durch Fehlversuche | Sperre nur 15 Minuten, Rate Limit pro IP |
| Cookie-Schlüssel gehen beim Neustart verloren (alle abgemeldet) | `Auth:KeysDirectory` auf dem Server setzen |
| Gast übernimmt beim Registrieren eine fremde Gast-ID | nur einmal pro Gast-ID möglich; Gast-IDs sind ohnehin nicht geheim |
| Gesperrter Account antwortet mit „Zu viele Fehlversuche“, ein unbekannter Name nicht – so lässt sich erkennen, dass es den Namen gibt | bewusst hingenommen: „Benutzername vergeben“ bei der Registrierung verrät das ohnehin; Rate Limit bremst das Durchprobieren |

## 5. Lessons Learned

- Sicherheit heißt vor allem: Der Server darf dem Client nichts glauben, was er selbst wissen kann.
- Ohne SDK geschriebener Code kompilierte auf Anhieb, aber erst der Browser-Test hat den Fehler im
  Formularzustand gezeigt. Build und Unit-Tests ersetzen das Durchklicken nicht.
- Windows Smart App Control kann `dotnet test` stillschweigend aushebeln („kein Test verfügbar“ statt
  rot). Ein Testlauf ohne Testanzahl ist kein grüner Testlauf.
