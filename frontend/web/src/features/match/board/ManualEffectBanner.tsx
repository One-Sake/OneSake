import { useEffect, useState } from 'react';

import type { ChoicePromptDto } from '../../../types/realtime';

interface ManualEffectBannerProps {
  prompt: ChoicePromptDto | null;
  disabled: boolean;
  onDone: () => void;
}

// An effect the server cannot automate. Not a modal: the board stays usable, so the player
// can resolve the effect with manual actions and then press "Done". It can be collapsed
// when it covers cards that are needed for the effect.
export function ManualEffectBanner({ disabled, onDone, prompt }: ManualEffectBannerProps) {
  const [collapsed, setCollapsed] = useState(false);

  // Every new effect starts open.
  useEffect(() => {
    setCollapsed(false);
  }, [prompt?.choiceId]);

  if (!prompt || prompt.kind !== 'MANUAL_EFFECT') {
    return null;
  }

  const doneButton = (
    <button className="is-primary" disabled={disabled} onClick={onDone} type="button">
      Erledigt
    </button>
  );

  if (collapsed) {
    return (
      <section aria-label="Effekt manuell ausführen" className="manual-effect-banner is-collapsed" role="status">
        <button className="manual-effect-banner__toggle" onClick={() => setCollapsed(false)} type="button">
          <span aria-hidden="true">✋</span> {prompt.sourceName || 'Effekt'} manuell ausführen
        </button>
        {doneButton}
      </section>
    );
  }

  return (
    <section aria-label="Effekt manuell ausführen" className="manual-effect-banner" role="status">
      <header className="manual-effect-banner__header">
        <p className="manual-effect-banner__kicker">
          <span aria-hidden="true">✋</span> Effekt bitte manuell ausführen
        </p>
        <button
          aria-label="Hinweis einklappen"
          className="manual-effect-banner__toggle"
          onClick={() => setCollapsed(true)}
          type="button"
        >
          −
        </button>
      </header>
      <h2>{prompt.sourceName || 'Karteneffekt'}</h2>
      {prompt.effectText && <p className="manual-effect-banner__text">{prompt.effectText}</p>}
      <p className="manual-effect-banner__hint">Rechtsklick oder ⋯ auf Karten, Deck, Life und DON!! zeigt die Aktionen.</p>
      {doneButton}
    </section>
  );
}
