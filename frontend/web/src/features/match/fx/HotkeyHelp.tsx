const KEYS: [string, string][] = [
  ['Leertaste', 'Zug beenden (halten oder zweimal drücken)'],
  ['A', 'Angriff mit der ausgewählten Karte'],
  ['D', 'DON!! an die ausgewählte Karte geben'],
  ['E', 'Effekt der ausgewählten Karte aktivieren'],
  ['Tab', 'Nächste eigene Feldkarte auswählen'],
  ['1 – 9', 'Handkarte auswählen (Enter spielt sie)'],
  ['Enter', 'Im Dialog bestätigen'],
  ['Esc', 'Abbrechen / Auswahl aufheben'],
  ['?', 'Diese Übersicht ein- und ausblenden'],
];

// Overview of all keyboard shortcuts ("?").
export function HotkeyHelp({ onClose }: { onClose: () => void }) {
  return (
    <div className="fx-hotkeys" role="dialog" aria-label="Tastenkürzel" onClick={onClose}>
      <section className="fx-hotkeys__panel" onClick={(event) => event.stopPropagation()}>
        <header>
          <h2>Tastenkürzel</h2>
          <button aria-label="Schließen" onClick={onClose} type="button">
            ✕
          </button>
        </header>
        <dl>
          {KEYS.map(([key, text]) => (
            <div key={key}>
              <dt>
                <kbd>{key}</kbd>
              </dt>
              <dd>{text}</dd>
            </div>
          ))}
        </dl>
      </section>
    </div>
  );
}
