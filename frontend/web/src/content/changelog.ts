// "Was ist neu" entries, newest first. Add a new entry at the top for every release.

export interface ChangelogEntry {
  version: string;
  // ISO date (YYYY-MM-DD)
  date: string;
  title: string;
  items: string[];
}

export const CHANGELOG: ChangelogEntry[] = [
  {
    version: '0.5.0',
    date: '2026-11-21',
    title: 'Quick Match, Ranked, Meta und neues Spielfeld',
    items: [
      'Quick Match: ein Klick, und du wirst automatisch einem Gegner zugeteilt.',
      'Profil mit echter Statistik: Matches, Siege, Winrate, Lieblings-Leader und letzte Matches.',
      'Decks als Text exportieren und importieren (Format 4xOP01-016).',
      'Dein Browser bekommt eine feste Gast-ID, damit Ergebnisse dir zugeordnet werden.',
      'Neue Seite „Was ist neu“.',
      'Einheitliches Design für Lobby, Deckbuilder, Tools, Profil und Startbildschirm.',
      'Würfelwurf mit 3D-Animation entscheidet, wer anfangen darf.',
      'Ranked: Quick Matches zählen für deine Wertung (ELO), angezeigt als Kopfgeld.',
      'Steckbrief im Profil mit Kopfgeld-Verlauf und Kopfgeld-Rangliste.',
      'Neuer Meta-Tab: Leader-Statistiken und Matchup-Tabelle (Daten: Card Kaizoku).',
      'Karteneffekte werden automatisch erkannt und im Match ausgeführt (z. B. On Play, Trigger).',
      'Spielfeld passt sich der Fenstergröße an und ist besser lesbar.',
      'Handkarten und DON!! per Drag & Drop spielen.',
      'Log/Chat-Leiste lässt sich in der Breite ziehen und einklappen.',
      'Eigene Kartenrückseiten für Deck, Leben und DON!!.',
      'Zurück ins Match nach Verbindungsabbruch: dein Platz bleibt 90 Sekunden frei, auch nach dem Neuladen der Seite.',
    ],
  },
  {
    version: '0.4.0',
    date: '2026-11-18',
    title: 'Neue Lobby',
    items: [
      'Dein Leader steht groß im Mittelpunkt, die Lobby färbt sich nach seiner Farbe.',
      'Ein klarer Haupt-Button statt vieler gleicher Buttons.',
      'Offene Tische als Kacheln, Navigation mit Icons.',
    ],
  },
  {
    version: '0.3.0',
    date: '2026-11-12',
    title: 'Board-Redesign',
    items: [
      'Spielfeld als Raster mit Hand im Hearthstone-Stil.',
      'Größeres Board, kompakte Auswahlleiste und Log/Chat an der Seite.',
    ],
  },
  {
    version: '0.2.0',
    date: '2026-11-07',
    title: 'Spielbare Match-Regeln',
    items: [
      'Mulligan, Leben, DON!!, Angriffe, Blocker und Counter nach den OPTCG-Regeln.',
      'Echte Kartenbilder auf dem Board.',
    ],
  },
  {
    version: '0.1.0',
    date: '2026-11-03',
    title: 'Server-Liste',
    items: [
      'Räume erstellen, beitreten und verlassen.',
      'Live-Liste aller offenen Server, leere Räume werden automatisch gelöscht.',
    ],
  },
];

export const LATEST_VERSION = CHANGELOG[0].version;
